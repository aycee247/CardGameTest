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

        public void ReportOnlineFailure(string message) =>
            OnlineError = string.IsNullOrEmpty(message) ? "unknown error" : message;
    }
}
