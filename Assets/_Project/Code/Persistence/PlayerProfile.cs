using System;
using System.Collections.Generic;

namespace Game.Persistence
{
    /// <summary>Player-tunable options; serialized as part of the profile.</summary>
    [Serializable]
    public class GameSettings
    {
        public float MasterVolume = 1f;
        public float MusicVolume = 0.8f;
        public float SfxVolume = 1f;
        public bool Haptics = true;
    }

    /// <summary>
    /// The persisted player record: identity, owned collection, and settings.
    /// Plain serializable POCO written as JSON — no Unity or netcode types so it round-trips
    /// safely under IL2CPP. Bump <see cref="Version"/> when the shape changes and migrate on load.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public int Version = 1;
        public string DisplayName = "Player";

        /// <summary>Card ids the player has unlocked into their collection.</summary>
        public List<int> OwnedCardIds = new List<int>();

        /// <summary>Dice skin ids the player owns.</summary>
        public List<string> OwnedDiceSkinIds = new List<string>();

        public string SelectedDiceSkinId = string.Empty;

        public GameSettings Settings = new GameSettings();

        public static PlayerProfile CreateDefault() => new PlayerProfile();
    }
}
