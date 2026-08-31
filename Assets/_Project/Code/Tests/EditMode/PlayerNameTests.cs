using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// A display name is the one string a peer can put on everyone else's screen (STORY-4.3 AC4),
    /// so these are as much a hostile-input suite as a formatting one.
    /// </summary>
    [TestFixture]
    public class PlayerNameTests
    {
        [Test]
        public void SeatDefault_IsOneBased()
        {
            Assert.AreEqual("Player 1", PlayerName.SeatDefault(0));
            Assert.AreEqual("Player 6", PlayerName.SeatDefault(5));
        }

        [Test]
        public void EmptyOrWhitespace_FallsBackToTheSeatDefault()
        {
            Assert.AreEqual("Player 3", PlayerName.Sanitize(null, 2));
            Assert.AreEqual("Player 3", PlayerName.Sanitize("", 2));
            Assert.AreEqual("Player 3", PlayerName.Sanitize("   ", 2));
            Assert.AreEqual("Player 3", PlayerName.Sanitize("\t\n ", 2));
        }

        [Test]
        public void AnOrdinaryNameSurvivesUntouched()
        {
            Assert.AreEqual("Aaron", PlayerName.Sanitize("Aaron", 0));
            Assert.AreEqual("Ada L", PlayerName.Sanitize("Ada L", 0));
        }

        [Test]
        public void SurroundingWhitespaceIsTrimmedAndInnerRunsCollapse()
        {
            Assert.AreEqual("Ada L", PlayerName.Sanitize("  Ada \t\t L  ", 0));
        }

        [Test]
        public void WhitespacePaddingCannotSpendTheLengthBudget()
        {
            // Sixteen spaces then a name: the spaces must not eat the cap and leave nothing.
            Assert.AreEqual("Ada", PlayerName.Sanitize("                Ada", 0));
        }

        [Test]
        public void ControlCharactersAreStripped()
        {
            Assert.AreEqual("AdaLovelace", PlayerName.Sanitize("Ada\0Lovelace", 0));
        }

        [Test]
        public void NewlinesCannotReflowTheRail()
        {
            Assert.AreEqual("Ada Lovelace", PlayerName.Sanitize("Ada\nLovelace", 0));
            Assert.AreEqual("Ada Lovelace", PlayerName.Sanitize("Ada\u2028Lovelace", 0));
        }

        [Test]
        public void ZeroWidthAndBidiOverridesAreStripped()
        {
            // U+200B zero-width space, U+200D joiner, U+202E right-to-left override.
            Assert.AreEqual("Ada", PlayerName.Sanitize("\u200BA\u200Dd\u202Ea", 0));
        }

        [Test]
        public void ANameOfNothingButStrippedCharactersFallsBack()
        {
            Assert.AreEqual("Player 2", PlayerName.Sanitize("\u200B\u200B\u202E", 1));
            Assert.AreEqual("Player 2", PlayerName.Sanitize("\0\0", 1));
        }

        [Test]
        public void LongNamesAreCappedWithoutATrailingSpace()
        {
            var result = PlayerName.Sanitize("Bartholomew Fitzgerald III", 0);

            Assert.AreEqual(PlayerName.MaxLength, result.Length);
            Assert.AreEqual("Bartholomew Fitz", result);

            // A cap landing exactly on a space must not leave one dangling, whether the space is
            // the first character past the cap or the last one inside it.
            Assert.AreEqual("Bartholomew Fitz", PlayerName.Sanitize("Bartholomew Fitz gerald", 0));
            Assert.AreEqual("Bartholomew Fit", PlayerName.Sanitize("Bartholomew Fit gerald", 0));
        }

        [Test]
        public void TheCapNeverSplitsASurrogatePair()
        {
            // Fifteen chars, then an emoji that needs two units: it does not fit, so it is
            // dropped whole rather than cut in half into a lone surrogate.
            var result = PlayerName.Sanitize("Ada Lovelace123\U0001F600", 0);

            Assert.AreEqual("Ada Lovelace123", result);
            foreach (char c in result) Assert.IsFalse(char.IsSurrogate(c));
        }

        [Test]
        public void AnEmojiThatFitsIsKeptWhole()
        {
            Assert.AreEqual("Ada \U0001F600", PlayerName.Sanitize("Ada \U0001F600", 0));
        }

        [Test]
        public void ALoneSurrogateIsStripped()
        {
            Assert.AreEqual("Ada", PlayerName.Sanitize("A\ud83dda", 0));
        }

        [Test]
        public void SanitizingIsIdempotent()
        {
            string[] inputs =
            {
                "  Ada \t Lovelace  ", "Bartholomew Fitzgerald III", "\u200BA\u202Eda",
                "", "   ", "\U0001F600\U0001F600\U0001F600\U0001F600\U0001F600\U0001F600\U0001F600\U0001F600\U0001F600"
            };

            foreach (var input in inputs)
            {
                var once = PlayerName.Sanitize(input, 0);
                Assert.AreEqual(once, PlayerName.Sanitize(once, 0), $"not idempotent for '{input}'");
            }
        }

        [Test]
        public void AnExplicitFallbackIsUsedWhenNothingSurvives()
        {
            Assert.AreEqual("Bot 2", PlayerName.Sanitize("  ", "Bot 2"));
            Assert.AreEqual("Ada", PlayerName.Sanitize(" Ada ", "Bot 2"));
        }
    }
}
