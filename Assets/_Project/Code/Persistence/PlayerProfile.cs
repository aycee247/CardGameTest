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
    /// safely under IL2CPP. Bump <see cref="CurrentVersion"/> when the shape changes and add the
    /// step to <see cref="Migrate"/>, which runs on every load.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        /// <summary>Bumped when the shape changes; <see cref="Migrate"/> brings older saves forward.</summary>
        public const int CurrentVersion = 3;

        // v2: first-time hint flags (absent members default false on load)
        // v3: DisplayName became player-chosen, and empty means "use the seat default"
        public int Version = CurrentVersion;

        /// <summary>
        /// The name this player chose (STORY-4.3). Empty is the normal state for someone who has
        /// never set one — the seat default is then used, so it is deliberately not pre-filled
        /// with a placeholder that would be mistaken for a choice.
        /// </summary>
        public string DisplayName = string.Empty;

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

        /// <summary>
        /// Brings a loaded profile forward to <see cref="CurrentVersion"/>. Kept here rather than
        /// in the save service so the rules for each version live next to the fields they concern.
        /// </summary>
        public static PlayerProfile Migrate(PlayerProfile profile)
        {
            if (profile == null) return CreateDefault();

            // v2 wrote a literal "Player" for everyone, since the name could not be edited. Read
            // as a real choice it would put "Player" on every seat, which is worse than the seat
            // defaults it would replace — so it is cleared rather than carried forward.
            if (profile.Version < 3 && profile.DisplayName == "Player")
                profile.DisplayName = string.Empty;

            profile.Version = CurrentVersion;
            return profile;
        }
    }
}
