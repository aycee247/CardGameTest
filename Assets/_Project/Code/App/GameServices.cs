namespace Game.App
{
    /// <summary>
    /// Static access point to the app-wide <see cref="ServiceLocator"/>, assigned once by
    /// <see cref="GameBootstrap"/>. Kept static so scene-scoped UI can resolve services without
    /// wiring references through the inspector across scene loads.
    /// </summary>
    public static class GameServices
    {
        public static ServiceLocator Locator { get; internal set; }

        public static bool IsReady => Locator != null;
    }
}
