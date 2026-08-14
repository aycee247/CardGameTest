using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>How present a player is. Public information — the rail shows it.</summary>
    public enum SeatStatus
    {
        Connected,

        /// <summary>Dropped, still inside the reconnect window. The seat is being held.</summary>
        Reconnecting,

        /// <summary>Dropped and the window has closed. Still scored, but playing itself.</summary>
        Abandoned
    }

    /// <summary>
    /// Maps a stable player key to a seat, and tracks how present each seat is.
    ///
    /// A reconnecting client arrives with a brand new transport id, so transport identity cannot be
    /// what owns a seat. The key is whatever survives a reconnect — the UGS authentication id in a
    /// real session — and this is the only thing that decides which seat someone gets back.
    ///
    /// The reconnect window does not change whether a player is auto-passed: they are auto-passed
    /// either way, and stay in the match so the scoring stays intact (NET-3). What it changes is
    /// what everyone is told — a seat that might still come back reads differently from one that
    /// has gone — and it gives the UI something honest to show.
    ///
    /// Pure and clock-agnostic: callers pass the time, which keeps it testable and keeps Core free
    /// of Unity.
    /// </summary>
    public sealed class SeatRegistry
    {
        private sealed class Seat
        {
            public PlayerId Player;
            public bool Connected = true;
            public float DisconnectedAt;
        }

        private readonly Dictionary<string, Seat> _byKey = new Dictionary<string, Seat>(StringComparer.Ordinal);
        private readonly Dictionary<PlayerId, Seat> _bySeat = new Dictionary<PlayerId, Seat>();

        public float ReconnectWindowSeconds { get; }

        public SeatRegistry(float reconnectWindowSeconds = 45f)
        {
            if (reconnectWindowSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(reconnectWindowSeconds));
            ReconnectWindowSeconds = reconnectWindowSeconds;
        }

        public int Count => _bySeat.Count;

        /// <summary>Claims a seat for a key at match start. Re-binding the same key is idempotent.</summary>
        public void Bind(string key, PlayerId player)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A seat key is required.", nameof(key));

            if (_byKey.TryGetValue(key, out var existing))
            {
                existing.Player = player;
                _bySeat[player] = existing;
                return;
            }

            var seat = new Seat { Player = player };
            _byKey[key] = seat;
            _bySeat[player] = seat;
        }

        /// <summary>Which seat this key owns, if any. This is what a reconnecting client is given back.</summary>
        public bool TryResolve(string key, out PlayerId player)
        {
            player = default;
            if (string.IsNullOrEmpty(key) || !_byKey.TryGetValue(key, out var seat)) return false;

            player = seat.Player;
            return true;
        }

        public void MarkDisconnected(PlayerId player, float now)
        {
            if (!_bySeat.TryGetValue(player, out var seat)) return;
            if (!seat.Connected) return;   // keep the original drop time; a second report is noise

            seat.Connected = false;
            seat.DisconnectedAt = now;
        }

        /// <summary>
        /// Takes a seat back. Deliberately allowed even after the window has closed: refusing would
        /// only punish someone whose train went into a tunnel, and the seat is still theirs.
        /// </summary>
        public void MarkConnected(PlayerId player)
        {
            if (!_bySeat.TryGetValue(player, out var seat)) return;

            seat.Connected = true;
            seat.DisconnectedAt = 0f;
        }

        public bool IsConnected(PlayerId player) =>
            !_bySeat.TryGetValue(player, out var seat) || seat.Connected;

        public SeatStatus StatusOf(PlayerId player, float now)
        {
            if (!_bySeat.TryGetValue(player, out var seat) || seat.Connected) return SeatStatus.Connected;

            return now - seat.DisconnectedAt <= ReconnectWindowSeconds
                ? SeatStatus.Reconnecting
                : SeatStatus.Abandoned;
        }

        /// <summary>Seconds left to reconnect, or zero once the window has closed.</summary>
        public float ReconnectSecondsLeft(PlayerId player, float now)
        {
            if (!_bySeat.TryGetValue(player, out var seat) || seat.Connected) return 0f;

            float left = ReconnectWindowSeconds - (now - seat.DisconnectedAt);
            return left > 0f ? left : 0f;
        }

        public IEnumerable<PlayerId> Seats => _bySeat.Keys;
    }
}
