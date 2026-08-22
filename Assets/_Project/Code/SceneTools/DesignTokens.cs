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

        // The Industry system's --space-1..8 steps (3.4 / 6.8 / 10.2 / 13.6 / 20.4 / 27.2 px).
        public static readonly float Space1 = Px(3.4f);
        public static readonly float Space2 = Px(6.8f);
        public static readonly float Space3 = Px(10.2f);
        public static readonly float Space4 = Px(13.6f);
        public static readonly float Space6 = Px(20.4f);
        public static readonly float Space8 = Px(27.2f);
    }
}
