#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using Game.Persistence;

namespace Game.App
{
    /// <summary>
    /// The game's haptic beats (STORY-3.4): light on a die tap, medium on a commit, success on
    /// a claim won, warning on a contest lost. Honours <c>GameSettings.Haptics</c> — the flag
    /// that existed unread since E4 was specced — and compiles to a no-op everywhere except an
    /// iOS device, where it calls the own-code bridge in Assets/Plugins/iOS/FoundryHaptics.mm.
    /// </summary>
    public static class Haptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _foundryHapticImpact(int strength);
        [DllImport("__Internal")] private static extern void _foundryHapticNotify(int type);
#endif

        public static void Light() => Impact(0);
        public static void Medium() => Impact(1);
        public static void Success() => Notify(0);
        public static void Warning() => Notify(1);

        private static bool Enabled =>
            GameServices.IsReady &&
            GameServices.Locator.TryGet<ISaveService>(out var save) &&
            save.Profile.Settings.Haptics;

        private static void Impact(int strength)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Enabled) _foundryHapticImpact(strength);
#else
            _ = strength;
#endif
        }

        private static void Notify(int type)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Enabled) _foundryHapticNotify(type);
#else
            _ = type;
#endif
        }
    }
}
