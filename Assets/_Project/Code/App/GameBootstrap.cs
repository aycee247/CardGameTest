using System;
using Game.Audio;
using Game.Networking;
using Game.Persistence;
using Unity.Netcode;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Composition root. Lives in the Boot scene on a persistent object, initializes UGS, builds
    /// the service graph, then hands off to the Main Menu. This is the ONE place that news-up
    /// concrete services — everything downstream resolves interfaces from <see cref="GameServices"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("AudioManager in the Boot scene (or leave null to auto-add).")]
        [SerializeField] private AudioManager audioManager;

        [Tooltip("NetworkManager for the whole app (persists across scenes).")]
        [SerializeField] private NetworkManager networkManager;

        private ISaveService _saveService;

        private void Awake()
        {
            if (GameServices.IsReady)
            {
                // A Boot scene re-entry; the singleton graph already exists.
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            if (networkManager != null) DontDestroyOnLoad(networkManager.gameObject);

            // Crash/exception context rides on this persistent object (STORY-6.6).
            gameObject.AddComponent<CrashContextReporter>();

            BuildServiceGraph();
        }

        private async void Start()
        {
            try
            {
                var session = GameServices.Locator.Get<SessionManager>();
                await session.InitializeAsync();
            }
            catch (Exception e)
            {
                // Online is optional at boot: hot-seat shares no UGS code (NET-5), and Host/Join
                // re-attempt init through SessionManager. Record why it failed for the menu to
                // show, and keep going — stalling here bricked first runs without UGS.
                Debug.LogError($"[Bootstrap] Online services unavailable: {e}");
                GameServices.Locator.Get<BootStatus>().ReportOnlineFailure(e.Message);
            }

            GameServices.Locator.Get<SceneFlowService>().LoadMainMenu();
        }

        private void BuildServiceGraph()
        {
            var locator = new ServiceLocator();

            // Audio
            if (audioManager == null) audioManager = gameObject.AddComponent<AudioManager>();
            locator.Register<IAudioService>(audioManager);

            // Persistence — load the profile immediately so menus have collection/settings data.
            _saveService = new JsonSaveService();
            var profile = _saveService.Load();
            locator.Register<ISaveService>(_saveService);

            // Apply persisted audio settings.
            var s = profile.Settings;
            audioManager.SetVolumes(s.MasterVolume, s.MusicVolume, s.SfxVolume);

            // Re-apply whenever the profile changes, so volume edits land live (STORY-4.2).
            _saveService.ProfileChanged += ApplyAudioSettings;

            // Networking / online services
            locator.Register(new SessionManager());

            // Scene flow
            locator.Register(new SceneFlowService());

            // Boot outcome, for the menu to explain a degraded (offline) start.
            var bootStatus = new BootStatus();
            if (_saveService.ProfileWasReset) bootStatus.ReportProfileReset();
            locator.Register(bootStatus);

            GameServices.Locator = locator;
        }

        private void ApplyAudioSettings()
        {
            var s = _saveService.Profile.Settings;
            audioManager.SetVolumes(s.MasterVolume, s.MusicVolume, s.SfxVolume);
        }

        private void OnDestroy()
        {
            if (_saveService != null) _saveService.ProfileChanged -= ApplyAudioSettings;
        }

        // iOS suspends (not closes) apps — persist on pause as well as quit.
        private void OnApplicationPause(bool paused)
        {
            if (paused) _saveService?.FlushIfDirty();
        }

        private void OnApplicationQuit()
        {
            _saveService?.FlushIfDirty();
        }
    }
}
