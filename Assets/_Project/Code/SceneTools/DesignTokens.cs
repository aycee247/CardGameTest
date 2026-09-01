namespace Game.SceneTools
{
    /// <summary>
    /// Converts the design handoff's dimensions into canvas units. The handoff is drawn for a
    /// ~390px-wide phone viewport; the canvas reference is 1080×1920, so everything scales by
    /// 1080/390. Every scaffolder dimension goes through <see cref="Px"/> so the conversion —
    /// and the Industry spacing scale — lives in exactly one place.
    /// </summary>
    internal static class DesignTokens
    {
        public const float Scale = 1080f / 390f;   // ≈ 2.769

        public static float Px(float handoffPx) => handoffPx * Scale;

        /// <summary>
        /// Layout units per point on the <b>narrowest</b> supported phone. The canvas matches
        /// width, so a fixed unit size renders smaller the narrower the device — sizing against
        /// 375pt (SE / mini) is what makes a minimum a real minimum rather than one that only
        /// holds on a big screen.
        /// </summary>
        public const float UnitsPerPoint = 1080f / 375f;   // = 2.88

        /// <summary>Point sizes, in layout units. iOS body text is 17pt; nothing here goes under 13.</summary>
        public static float Pt(float points) => points * UnitsPerPoint;

        /// <summary>
        /// The floor for anything a player has to read. Type was carried over from a 390px web
        /// handoff and rendered at 8.6-12pt on device — most of the game's text was below iOS's
        /// smallest system style, and one label was 6pt.
        /// </summary>
        public static readonly float MinReadable = Pt(13f);

        // The Industry system's --space-1..8 steps (3.4 / 6.8 / 10.2 / 13.6 / 20.4 / 27.2 px).
        public static readonly float Space1 = Px(3.4f);
        public static readonly float Space2 = Px(6.8f);
        public static readonly float Space3 = Px(10.2f);
        public static readonly float Space4 = Px(13.6f);
        public static readonly float Space6 = Px(20.4f);
        public static readonly float Space8 = Px(27.2f);
    }
}
