namespace Game.App
{
    /// <summary>
    /// Records whether online services came up during boot. The main menu reads this to explain a
    /// degraded (offline) start. There is no explicit retry call: Host/Join re-run UGS init through
    /// <see cref="Game.Networking.SessionManager"/>, which only latches IsInitialized on success.
    /// </summary>
    public sealed class BootStatus
    {
        public string OnlineError { get; private set; }

        public bool OnlineFailed => !string.IsNullOrEmpty(OnlineError);

        private bool _profileWasReset;

        public void ReportOnlineFailure(string message) =>
            OnlineError = string.IsNullOrEmpty(message) ? "unknown error" : message;

        /// <summary>The saved profile was unreadable and defaults now stand (STORY-4.2 AC3).</summary>
        public void ReportProfileReset() => _profileWasReset = true;

        /// <summary>
        /// Returns the reset flag and clears it, so the menu says it exactly once — "says so
        /// once" is the AC, and a menu the player revisits must not repeat it.
        /// </summary>
        public bool ConsumeProfileReset()
        {
            bool was = _profileWasReset;
            _profileWasReset = false;
            return was;
        }
    }
}
