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
        PlayerProfile Load();
        void Save();
        void MarkDirty();
        void FlushIfDirty();
    }
}
