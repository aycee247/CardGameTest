using System;

namespace Game.Persistence
{
    /// <summary>
    /// Loads/saves the player's <see cref="PlayerProfile"/>. Consumers mutate the in-memory
    /// <see cref="Profile"/> and call <see cref="Save"/> (or <see cref="MarkDirty"/> to defer a
    /// flush to app pause/quit — important on iOS where the process is suspended, not closed).
    /// </summary>
    public interface ISaveService
    {
        PlayerProfile Profile { get; }

        /// <summary>
        /// True when <see cref="Load"/> found a profile on disk but could not read it and fell
        /// back to defaults (STORY-4.2 AC3). A missing file — a fresh install — does not count.
        /// </summary>
        bool ProfileWasReset { get; }

        /// <summary>
        /// Raised whenever the profile is mutated (<see cref="MarkDirty"/>) or written
        /// (<see cref="Save"/>). Appliers subscribe and re-read the settings they care about,
        /// which is what makes a future settings screen take effect live rather than on the
        /// next scene load (STORY-4.2).
        /// </summary>
        event Action ProfileChanged;

        PlayerProfile Load();
        void Save();
        void MarkDirty();
        void FlushIfDirty();
    }
}
