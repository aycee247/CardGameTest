namespace Game.Networking
{
    /// <summary>
    /// What two peers must agree on to play together.
    ///
    /// A TestFlight group does not update in lockstep: someone always keeps an old build, joins a
    /// friend's match, connects fine at the transport level, and then sits on a board that never
    /// fills in. That happened on build 2 against a build 5 host — the scenes are regenerated per
    /// build, so the scene-placed controller no longer hashes to the same object on both ends and
    /// the messages go nowhere. Nothing in the game noticed or said so.
    ///
    /// <see cref="Version"/> is announced with the client's identity and checked by the server, so
    /// the mismatch is named instead of being left to look like a hang.
    ///
    /// <b>Bump it whenever the wire changes</b>: an RPC signature, the snapshot shape, or a scene
    /// regeneration that moves the networked objects. Bumping it when nothing changed only costs a
    /// forced update; failing to bump it costs someone a silent dead board.
    /// </summary>
    public static class NetProtocol
    {
        public const int Version = 1;
    }

    /// <summary>Why a peer cannot take part in the match it just connected to.</summary>
    public enum MatchUnavailableReason
    {
        /// <summary>The match is already under way and this client holds no seat in it.</summary>
        NoSeat = 0,

        /// <summary>The two builds do not share a <see cref="NetProtocol.Version"/>.</summary>
        VersionMismatch = 1,

        /// <summary>
        /// Nothing arrived at all. Not reported by the server — the client concludes it locally
        /// when no snapshot has turned up, which is the only way to catch a peer whose messages
        /// never reach us in the first place.
        /// </summary>
        NoResponse = 2
    }
}
