using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// A simulated opponent for solo play (STORY-7.1). Decides exclusively from a
    /// <see cref="MatchSnapshot"/> built for its own seat — the same redacted view a remote human
    /// would receive — so a bot cannot read an opponent's secret commit even by accident (NET-2
    /// discipline; enforced, not promised: a snapshot for any other seat throws). Card *costs*
    /// come from a caller-supplied resolver, which is public information — all 48 definitions are
    /// printed on the cards.
    ///
    /// The strategy is the balance harness's greedy caricature grown up a little: re-roll the
    /// worst dice with free re-rolls, then claim the highest-value affordable card, with a small
    /// seeded jitter so a table of bots doesn't chase one card in lockstep. Deterministic for a
    /// fixed seed (xorshift, no <c>System.Random</c>).
    /// </summary>
    public sealed class BotPlayer
    {
        private readonly Func<CardId, Card> _resolveCard;
        private XorShift64Star _rng;

        public PlayerId Seat { get; }

        public BotPlayer(PlayerId seat, Func<CardId, Card> resolveCard, ulong seed)
        {
            Seat = seat;
            _resolveCard = resolveCard ?? throw new ArgumentNullException(nameof(resolveCard));
            _rng = new XorShift64Star(seed);
        }

        /// <summary>
        /// Takes the bot's one action point for the current pass: shape (in Shape only), then
        /// commit or pass. <paramref name="view"/> must produce snapshots for this bot's seat; it
        /// is re-pulled after every action, because acting invalidates the previous snapshot.
        /// </summary>
        public void TakeTurn(Func<MatchSnapshot> view, LocalMatchSession session)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (session == null) throw new ArgumentNullException(nameof(session));

            var snap = Pull(view);
            if (snap.Observer.HasDecided) return;

            if (snap.Phase == RoundPhase.Shape)
            {
                ShapeDice(view, session);
                snap = Pull(view);
            }

            CommitOrPass(snap, session);
        }

        private void ShapeDice(Func<MatchSnapshot> view, LocalMatchSession session)
        {
            // Free re-rolls only, on the lowest face: low pips are the least useful for the sums
            // and sets most costs ask for. Sparks are hoarded — losing a contest pays 3 anyway.
            var me = Pull(view).Observer;
            while (me.RerollsLeft > 0)
            {
                int worst = WorstUnspentDie(me);
                if (worst < 0 || !session.Shape(Seat, ShapeAction.Reroll(worst)).Success) return;
                me = Pull(view).Observer;
            }
        }

        private void CommitOrPass(in MatchSnapshot snap, LocalMatchSession session)
        {
            var me = snap.Observer;
            var wilds = new HashSet<int>(me.WildFaces ?? Array.Empty<int>());

            int bestCard = -1;
            int[] bestPay = null;
            float bestValue = float.MinValue;

            foreach (var offer in snap.Market ?? Array.Empty<CardSnapshot>())
            {
                var card = _resolveCard(new CardId(offer.CardId));
                if (card == null) continue;

                var pay = PaymentSuggester.Suggest(card.Cost, me.DiceFaces, me.DiceSpent,
                    wilds, me.WildDice);
                if (pay.Length == 0) continue;

                // Points first; the jitter (< 1 point) only breaks ties, so bots stay sensible
                // while not all diving at the same card.
                float value = card.Points + NextJitter();
                if (value <= bestValue) continue;
                bestValue = value;
                bestCard = offer.CardId;
                bestPay = pay;
            }

            if (bestCard < 0 || !session.Commit(Seat, new CardId(bestCard), bestPay).Success)
                session.Pass(Seat);
        }

        /// <summary>Pulls a fresh view and verifies it is this bot's own (NET-2, mechanically).</summary>
        private MatchSnapshot Pull(Func<MatchSnapshot> view)
        {
            var snap = view();
            if (snap.ObserverId != Seat.Value)
                throw new InvalidOperationException(
                    $"Bot for seat {Seat.Value} was handed a snapshot for seat {snap.ObserverId} — " +
                    "bots may only ever see their own view (NET-2).");
            return snap;
        }

        private static int WorstUnspentDie(PlayerSnapshot me)
        {
            int worst = -1, worstFace = int.MaxValue;
            for (int i = 0; i < me.DiceFaces.Length; i++)
            {
                if (me.DiceSpent != null && i < me.DiceSpent.Length && me.DiceSpent[i]) continue;
                if (me.DiceFaces[i] < worstFace) { worstFace = me.DiceFaces[i]; worst = i; }
            }
            return worst;
        }

        private float NextJitter() => _rng.NextBelow(1000) / 1000f * 0.9f;
    }
}
