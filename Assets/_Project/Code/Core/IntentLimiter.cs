using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Per-player token bucket over incoming intents.
    ///
    /// A burst has to be allowed, because one tap legitimately produces several intents: selecting
    /// eight dice and pressing re-roll sends eight in a single frame. What must not be allowed is a
    /// sustained flood, since every accepted intent makes the server re-broadcast — and a broadcast
    /// costs one snapshot encode per recipient, so the amplification runs with the table size.
    ///
    /// Pure and clock-agnostic: the caller passes the current time, which keeps it testable and
    /// keeps the rules layer free of Unity.
    /// </summary>
    public sealed class IntentLimiter
    {
        private sealed class Bucket
        {
            public float Tokens;
            public float LastRefillAt;
            public int Dropped;
        }

        private readonly Dictionary<PlayerId, Bucket> _buckets = new Dictionary<PlayerId, Bucket>();

        public float Burst { get; }
        public float PerSecond { get; }

        /// <param name="burst">Intents accepted back to back before throttling begins.</param>
        /// <param name="perSecond">Sustained rate the bucket refills at.</param>
        public IntentLimiter(float burst = 24f, float perSecond = 12f)
        {
            if (burst <= 0f) throw new ArgumentOutOfRangeException(nameof(burst));
            if (perSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(perSecond));

            Burst = burst;
            PerSecond = perSecond;
        }

        /// <summary>
        /// Takes one token for <paramref name="player"/>, or returns false if they have outrun their
        /// budget. <paramref name="now"/> is a monotonically increasing time in seconds.
        /// </summary>
        public bool TryConsume(PlayerId player, float now)
        {
            if (!_buckets.TryGetValue(player, out var bucket))
            {
                bucket = new Bucket { Tokens = Burst, LastRefillAt = now };
                _buckets[player] = bucket;
            }

            // Refill for the elapsed time, never past the burst ceiling. Guarding against a
            // backwards clock keeps a bad timestamp from minting tokens.
            float elapsed = now - bucket.LastRefillAt;
            if (elapsed > 0f)
            {
                bucket.Tokens = Math.Min(Burst, bucket.Tokens + elapsed * PerSecond);
                bucket.LastRefillAt = now;
            }

            if (bucket.Tokens < 1f)
            {
                bucket.Dropped++;
                return false;
            }

            bucket.Tokens -= 1f;
            return true;
        }

        /// <summary>How many intents have been dropped for a player, for logging and diagnostics.</summary>
        public int DroppedFor(PlayerId player) =>
            _buckets.TryGetValue(player, out var bucket) ? bucket.Dropped : 0;

        /// <summary>Tokens currently available, for tests and diagnostics.</summary>
        public float TokensFor(PlayerId player) =>
            _buckets.TryGetValue(player, out var bucket) ? bucket.Tokens : Burst;

        public void Reset() => _buckets.Clear();
    }
}
