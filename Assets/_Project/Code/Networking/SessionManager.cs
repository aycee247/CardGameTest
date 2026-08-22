using System;
using System.Threading.Tasks;
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
        public async Task<string> CreateSessionAsync(int maxPlayers = 2)
        {
            await InitializeAsync();

            var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
            CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

            Debug.Log($"[Session] Created. Code={CurrentSession.Code}");
            HookRoster(CurrentSession);
            SessionStarted?.Invoke(CurrentSession);
            return CurrentSession.Code;
        }

        /// <summary>Joins an existing match by its share code.</summary>
        public async Task JoinSessionByCodeAsync(string code)
        {
            await InitializeAsync();

            CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

            Debug.Log($"[Session] Joined. Code={CurrentSession.Code}");
            HookRoster(CurrentSession);
            SessionStarted?.Invoke(CurrentSession);
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
