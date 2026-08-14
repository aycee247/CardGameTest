using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The limiter has to pass real play untouched and still bound a flood. The two tests that
    /// matter most are the ones pinning each end of that: a legitimate burst is accepted whole,
    /// and a sustained flood converges to the configured rate.
    /// </summary>
    public class IntentLimiterTests
    {
        private static readonly PlayerId A = new PlayerId(0);
        private static readonly PlayerId B = new PlayerId(1);

        [Test]
        public void ABurstIsAcceptedWhole()
        {
            var limiter = new IntentLimiter(burst: 24f, perSecond: 12f);

            // Everything at the same instant, as a single frame's worth of taps would arrive.
            for (int i = 0; i < 24; i++)
                Assert.IsTrue(limiter.TryConsume(A, 0f), $"intent {i} of a legitimate burst was dropped");
        }

        [Test]
        public void ShapingEveryDieTwiceInOneFrameIsNotThrottled()
        {
            // The worst legitimate case: select all eight dice, re-roll, then set them all.
            var limiter = new IntentLimiter();

            for (int i = 0; i < 16; i++)
                Assert.IsTrue(limiter.TryConsume(A, 0f));
        }

        [Test]
        public void PastTheBurstIntentsAreDropped()
        {
            var limiter = new IntentLimiter(burst: 4f, perSecond: 1f);

            for (int i = 0; i < 4; i++) Assert.IsTrue(limiter.TryConsume(A, 0f));

            Assert.IsFalse(limiter.TryConsume(A, 0f));
            Assert.IsFalse(limiter.TryConsume(A, 0f));
            Assert.AreEqual(2, limiter.DroppedFor(A));
        }

        [Test]
        public void TokensRefillOverTime()
        {
            var limiter = new IntentLimiter(burst: 4f, perSecond: 2f);

            for (int i = 0; i < 4; i++) limiter.TryConsume(A, 0f);
            Assert.IsFalse(limiter.TryConsume(A, 0f));

            // Half a second at 2/s is one token.
            Assert.IsTrue(limiter.TryConsume(A, 0.5f));
            Assert.IsFalse(limiter.TryConsume(A, 0.5f));
        }

        [Test]
        public void RefillNeverExceedsTheBurstCeiling()
        {
            var limiter = new IntentLimiter(burst: 4f, perSecond: 10f);

            limiter.TryConsume(A, 0f);
            Assert.AreEqual(3f, limiter.TokensFor(A), 0.001f, "the bucket starts full and spends one");

            // A hundred idle seconds at 10/s is a thousand tokens of refill, but the ceiling is 4:
            // idling must not bank an allowance to dump all at once.
            for (int i = 0; i < 4; i++)
                Assert.IsTrue(limiter.TryConsume(A, 100f), $"token {i} should have been available");

            Assert.IsFalse(limiter.TryConsume(A, 100f), "idle time banked more than the burst ceiling");
        }

        [Test]
        public void ASustainedFloodConvergesToTheConfiguredRate()
        {
            var limiter = new IntentLimiter(burst: 24f, perSecond: 12f);

            int accepted = 0;
            // Ten seconds of a client hammering 1000 intents a second.
            for (int step = 0; step < 10_000; step++)
                if (limiter.TryConsume(A, step * 0.001f)) accepted++;

            // The burst plus ten seconds of refill, and nothing more.
            Assert.LessOrEqual(accepted, 24 + 12 * 10 + 1);
            Assert.Greater(accepted, 12 * 10, "the limiter must not throttle below its configured rate");
        }

        [Test]
        public void OnePlayerFloodingDoesNotStarveAnother()
        {
            var limiter = new IntentLimiter(burst: 4f, perSecond: 1f);

            for (int i = 0; i < 20; i++) limiter.TryConsume(A, 0f);
            Assert.IsFalse(limiter.TryConsume(A, 0f));

            // B has touched nothing and must still have their whole budget.
            for (int i = 0; i < 4; i++)
                Assert.IsTrue(limiter.TryConsume(B, 0f), "a flooding client starved a quiet one");
        }

        [Test]
        public void ABackwardsClockCannotMintTokens()
        {
            var limiter = new IntentLimiter(burst: 4f, perSecond: 10f);

            for (int i = 0; i < 4; i++) limiter.TryConsume(A, 10f);
            Assert.IsFalse(limiter.TryConsume(A, 10f));

            Assert.IsFalse(limiter.TryConsume(A, 0f), "time going backwards refilled the bucket");
        }
    }
}
