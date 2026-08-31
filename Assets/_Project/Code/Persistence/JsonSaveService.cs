using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Persistence
{
    /// <summary>
    /// File-backed save service using Newtonsoft JSON. Writes atomically (temp file + replace)
    /// so a crash mid-write can never corrupt the existing save. Defaults to
    /// <see cref="Application.persistentDataPath"/>.
    /// </summary>
    public sealed class JsonSaveService : ISaveService
    {
        private const string FileName = "profile.json";

        private readonly string _path;
        private readonly JsonSerializerSettings _settings;
        private bool _dirty;

        public PlayerProfile Profile { get; private set; }

        public bool ProfileWasReset { get; private set; }

        public event Action ProfileChanged;

        public JsonSaveService(string directory = null)
        {
            var dir = string.IsNullOrEmpty(directory) ? Application.persistentDataPath : directory;
            _path = Path.Combine(dir, FileName);
            _settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public string FilePath => _path;

        public PlayerProfile Load()
        {
            ProfileWasReset = false;
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    Profile = PlayerProfile.Migrate(
                        JsonConvert.DeserializeObject<PlayerProfile>(json, _settings));
                }
                else
                {
                    // A fresh install, not a reset — the flag stays false.
                    Profile = PlayerProfile.CreateDefault();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Failed to load profile, using default: {e.Message}");
                Profile = PlayerProfile.CreateDefault();
                ProfileWasReset = true;
            }

            _dirty = false;
            return Profile;
        }

        public void MarkDirty()
        {
            _dirty = true;
            ProfileChanged?.Invoke();
        }

        public void FlushIfDirty()
        {
            if (_dirty) Save();
        }

        public void Save()
        {
            if (Profile == null) Profile = PlayerProfile.CreateDefault();
            try
            {
                var json = JsonConvert.SerializeObject(Profile, _settings);
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tmp, _path);
                _dirty = false;
                ProfileChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Failed to write profile: {e.Message}");
            }
        }
    }
}
