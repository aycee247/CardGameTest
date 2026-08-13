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

            BuildServiceGraph();
        }

        private async void Start()
        {
            try
            {
                var session = GameServices.Locator.Get<SessionManager>();
                await session.InitializeAsync();

                GameServices.Locator.Get<SceneFlowService>().LoadMainMenu();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bootstrap] Initialization failed: {e}");
                // TODO: surface a retry/offline UI instead of hanging on the Boot scene.
            }
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

            // Networking / online services
            locator.Register(new SessionManager());

            // Scene flow
            locator.Register(new SceneFlowService());

            GameServices.Locator = locator;
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
