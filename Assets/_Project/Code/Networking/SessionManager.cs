using System;
using System.Threading.Tasks;
using Game.Core;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Thin wrapper over Unity Gaming Services + the Multiplayer Services (MPS) Sessions API.
    /// A Session provisions Relay + Lobby and starts NGO for us, so the rest of the game never
    /// touches raw Relay/transport wiring.
    ///
    /// NOTE (version-sensitive): the MPS Sessions surface (SessionOptions, WithRelayNetwork,
    /// CreateSessionAsync/JoinSessionByCodeAsync, ISession.Code) is the current 2.x pattern.
    /// After the packages resolve in-editor, confirm the exact member names against the installed
    /// com.unity.services.multiplayer version — Unity has renamed a few of these across minors.
    /// </summary>
    public sealed class SessionManager
    {
        /// <summary>
        /// Session player property carrying the chosen display name (STORY-4.3), so the lobby can
        /// name its seats before NGO exists. A custom property rather than the built-in
        /// <c>WithPlayerName</c> module: that one routes through the Authentication player name,
        /// which forbids spaces and appends a numeric discriminator — neither of which belongs on
        /// a name the player typed for their friends to read.
        /// </summary>
        public const string DisplayNameProperty = "foundry_name";

        public ISession CurrentSession { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool IsInSession => CurrentSession != null;

        /// <summary>Shareable join code for the active session (host shows this to invite).</summary>
        public string JoinCode => CurrentSession?.Code;

        public int PlayerCount => CurrentSession?.PlayerCount ?? 0;
        public int MaxPlayers => CurrentSession?.MaxPlayers ?? 0;
        public bool IsHost => CurrentSession?.IsHost ?? false;

        public event Action<ISession> SessionStarted;
        public event Action SessionEnded;

        /// <summary>Raised whenever who is in the session changes — join, leave, host migration.</summary>
        public event Action RosterChanged;

        /// <summary>Initializes UGS and signs the player in anonymously (idempotent).</summary>
        public async Task InitializeAsync()
        {
            if (IsInitialized) return;

            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            IsInitialized = true;
            Debug.Log($"[Session] UGS ready. PlayerId={AuthenticationService.Instance.PlayerId}");
        }

        /// <summary>Hosts a new match. Returns the join code others use to connect.</summary>
        public async Task<string> CreateSessionAsync(int maxPlayers = 2, string displayName = null)
        {
            await InitializeAsync();

            var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
            ApplyDisplayName(options, displayName);
            CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

            Debug.Log($"[Session] Created. Code={CurrentSession.Code}");
            HookRoster(CurrentSession);
            SessionStarted?.Invoke(CurrentSession);
            return CurrentSession.Code;
        }

        /// <summary>Joins an existing match by its share code.</summary>
        public async Task JoinSessionByCodeAsync(string code, string displayName = null)
        {
            await InitializeAsync();

            var options = new JoinSessionOptions();
            ApplyDisplayName(options, displayName);

            CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);

            Debug.Log($"[Session] Joined. Code={CurrentSession.Code}");
            HookRoster(CurrentSession);
            SessionStarted?.Invoke(CurrentSession);
        }

        /// <summary>
        /// Publishes the name to the other members of the session, if there is one. Visible to
        /// members rather than the world: it is a name for the people already in your match.
        /// </summary>
        private static void ApplyDisplayName(BaseSessionOptions options, string displayName)
        {
            var cleaned = PlayerName.Sanitize(displayName, string.Empty);
            if (string.IsNullOrEmpty(cleaned)) return;

            options.PlayerProperties[DisplayNameProperty] =
                new PlayerProperty(cleaned, VisibilityPropertyOptions.Member);
        }

        /// <summary>
        /// The display name a session member published, cleaned again on the way in — it crossed
        /// the network from another player's device, so it is no more trusted here than a name
        /// arriving over an RPC (STORY-4.3 AC4).
        /// </summary>
        public static string DisplayNameOf(IReadOnlyPlayer player, int seatIndex)
        {
            string raw = null;
            if (player?.Properties != null &&
                player.Properties.TryGetValue(DisplayNameProperty, out var property))
                raw = property?.Value;

            return PlayerName.Sanitize(raw, seatIndex);
        }

        private void HookRoster(ISession session)
        {
            session.PlayerJoined += OnRosterPlayerEvent;
            session.PlayerLeaving += OnRosterPlayerEvent;
            session.PlayerHasLeft += OnRosterPlayerEvent;
            session.SessionHostChanged += OnRosterPlayerEvent;
            session.Changed += OnRosterChanged;
        }

        private void UnhookRoster(ISession session)
        {
            session.PlayerJoined -= OnRosterPlayerEvent;
            session.PlayerLeaving -= OnRosterPlayerEvent;
            session.PlayerHasLeft -= OnRosterPlayerEvent;
            session.SessionHostChanged -= OnRosterPlayerEvent;
            session.Changed -= OnRosterChanged;
        }

        private void OnRosterPlayerEvent(string _) => RosterChanged?.Invoke();
        private void OnRosterChanged() => RosterChanged?.Invoke();

        /// <summary>
        /// Closes the session to new arrivals. Called when the host starts the match: seats are
        /// fixed at that moment, so anyone who joins by code afterwards connects to a table that
        /// has no room for them and sits on a board that never fills in.
        ///
        /// Best effort — a lock that fails to save is logged and swallowed, because a host who
        /// cannot reach the lobby service should still get to play the match they just started.
        /// The client-side refusal covers the gap.
        /// </summary>
        public async Task LockAsync()
        {
            if (CurrentSession == null || !CurrentSession.IsHost) return;

            try
            {
                var host = CurrentSession.AsHost();
                host.IsLocked = true;
                await host.SavePropertiesAsync();
                Debug.Log("[Session] Locked — the match is under way.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Session] Could not lock the session: {e.Message}");
            }
        }

        /// <summary>Leaves/ends the current session and tears down networking.</summary>
        public async Task LeaveSessionAsync()
        {
            if (CurrentSession == null) return;
            UnhookRoster(CurrentSession);
            try
            {
                await CurrentSession.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Session] LeaveAsync failed: {e.Message}");
            }
            finally
            {
                CurrentSession = null;
                SessionEnded?.Invoke();
            }
        }
    }
}
