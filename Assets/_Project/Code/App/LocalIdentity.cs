using Game.Core;
using Game.Persistence;

namespace Game.App
{
    /// <summary>
    /// Who this device's player says they are (STORY-4.3). The single read/write point for the
    /// profile's display name, so the hosts, the menu and the networked announcement cannot drift
    /// on how a missing name is handled.
    ///
    /// Holds no state of its own — it reads the profile through the service locator every call —
    /// which keeps it honest when the profile is edited mid-session and keeps it working in a
    /// scene opened directly in the editor, where there is no service graph at all.
    /// </summary>
    public static class LocalIdentity
    {
        /// <summary>
        /// The stored name, or empty when there is no profile or none was chosen.
        ///
        /// "Raw" in two senses. It carries <b>no seat fallback</b> — the seat that receives it
        /// decides that, via <see cref="PlayerName.Sanitize(string, int)"/>. And while anything
        /// written through <see cref="SetDisplayName"/> was sanitized on the way in, this reads
        /// back from a file on disk that may predate the sanitizer or have been edited by hand,
        /// so it is <b>not</b> to be trusted as clean. Every consumer sanitizes again.
        /// </summary>
        public static string RawDisplayName =>
            GameServices.IsReady &&
            GameServices.Locator.TryGet<ISaveService>(out var save) &&
            save.Profile != null
                ? save.Profile.DisplayName ?? string.Empty
                : string.Empty;

        /// <summary>The name to show for the local player's seat, falling back to that seat's default.</summary>
        public static string NameForSeat(int seatIndex) => PlayerName.Sanitize(RawDisplayName, seatIndex);

        /// <summary>
        /// Records a newly chosen name. Sanitized on the way in so what is persisted is what
        /// everyone will see, and marked dirty rather than written immediately — the bootstrap
        /// flushes on pause and quit, which is what survives an iOS suspend.
        /// </summary>
        public static void SetDisplayName(string raw)
        {
            if (!GameServices.IsReady || !GameServices.Locator.TryGet<ISaveService>(out var save)) return;
            if (save.Profile == null) return;

            // An empty box is a real choice: it clears the name and restores the seat defaults.
            var cleaned = PlayerName.Sanitize(raw, string.Empty);
            if (cleaned == save.Profile.DisplayName) return;

            save.Profile.DisplayName = cleaned;
            save.MarkDirty();
        }
    }
}
