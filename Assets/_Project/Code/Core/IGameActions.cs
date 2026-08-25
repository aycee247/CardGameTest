using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Player intents the UI can issue. Implemented by the networking layer (sends server RPCs) or
    /// by <see cref="LocalMatchSession"/> (applies through <see cref="RulesEngine"/> immediately),
    /// so every screen runs identically online and offline.
    ///
    /// There is no RequestRoll: rolling is automatic and simultaneous at the start of every round,
    /// and no RequestEndTurn, because there are no turns.
    /// </summary>
    public interface IGameActions
    {
        /// <summary>Manipulate one die during Shape.</summary>
        void RequestShape(ShapeAction action);

        /// <summary>Secretly claim a market card, paying with the named dice.</summary>
        void RequestCommit(CardId cardId, IReadOnlyList<int> diceIndices);

        /// <summary>Decline to claim anything this pass.</summary>
        void RequestPass();

        /// <summary>
        /// Finished shaping (Shape phase only). Lighter than a pass: the claim stays open for the
        /// Commit window, but the server may close Shape early once everyone is done.
        /// </summary>
        void RequestDone();

        /// <summary>Take back a commit or pass, freeing the dice to be shaped again.</summary>
        void RequestWithdraw();
    }

    /// <summary>
    /// Read-only, observable match state for the UI. The networking layer raises
    /// <see cref="Changed"/> whenever the server replicates a new snapshot.
    ///
    /// <see cref="Current"/> is always the view filtered for <see cref="LocalPlayer"/> — a client is
    /// never handed anyone else's hidden information (NET-2).
    /// </summary>
    public interface IMatchView
    {
        /// <summary>The player this client controls.</summary>
        PlayerId LocalPlayer { get; }

        /// <summary>Latest known snapshot, filtered for <see cref="LocalPlayer"/>.</summary>
        MatchSnapshot Current { get; }

        /// <summary>
        /// Seconds left in the current phase (UI-2), or a negative value when the match has no
        /// clock — hot-seat advances when the player says so, not when time runs out.
        /// </summary>
        float SecondsLeft { get; }

        /// <summary>Raised on the main thread whenever <see cref="Current"/> changes.</summary>
        event Action<MatchSnapshot> Changed;

        /// <summary>Raised when the server rejects one of this client's requests.</summary>
        event Action<MoveFailure> MoveRejected;
    }
}
