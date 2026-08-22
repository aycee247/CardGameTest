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

        /// <summary>Collapses every UI animation to its end state (STORY-4.5 AC2).</summary>
        public bool ReducedMotion;

        /// <summary>Scales every UI animation; 1 is authored speed, 2 twice as fast.</summary>
        public float AnimationSpeed = 1f;
    }

    /// <summary>
    /// The persisted player record: identity, owned collection, and settings.
    /// Plain serializable POCO written as JSON — no Unity or netcode types so it round-trips
    /// safely under IL2CPP. Bump <see cref="Version"/> when the shape changes and migrate on load.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public int Version = 2;   // v2: first-time hint flags (absent members default false on load)
        public string DisplayName = "Player";

        /// <summary>First-time onboarding hints already dismissed (handoff 6i).</summary>
        public bool ShapeHintSeen;
        public bool CommitHintSeen;

        /// <summary>Card ids the player has unlocked into their collection.</summary>
        public List<int> OwnedCardIds = new List<int>();

        /// <summary>Dice skin ids the player owns.</summary>
        public List<string> OwnedDiceSkinIds = new List<string>();

        public string SelectedDiceSkinId = string.Empty;

        public GameSettings Settings = new GameSettings();

        public static PlayerProfile CreateDefault() => new PlayerProfile();
    }
}
