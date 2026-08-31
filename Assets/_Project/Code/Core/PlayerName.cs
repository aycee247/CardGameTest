using System.Globalization;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// The one place a player-supplied name is made safe to render (STORY-4.3).
    ///
    /// Pure and Unity-free so it can run on the server, on the hot-seat host and in the headless
    /// suite alike — which matters, because online a name is <b>untrusted client input</b> (AC4).
    /// It arrives over an RPC from a peer that can send whatever it likes, and it is then drawn
    /// into the standings rail on every other player's device.
    ///
    /// What is stripped, and why:
    ///  - <b>Control and separator</b> characters — a newline in a name reflows the rail.
    ///  - <b>Format</b> characters — zero-width joiners and the bidi overrides, which can hide
    ///    text entirely or reverse the reading order of everything after them.
    ///  - <b>Lone surrogates, private use and unassigned</b> code points — these render as tofu
    ///    at best and are a font-dependent crash surface at worst.
    ///  - <b>Runs of whitespace</b>, collapsed to a single space, so a name padded out with
    ///    spaces cannot push its way across the rail.
    ///  - <b>Stacked combining marks</b>, capped at <see cref="MaxMarksPerCharacter"/> per base
    ///    character. Marks are drawn on top of what precedes them and do not count against a
    ///    width budget, so fifteen of them on one letter smear vertically across the rows above
    ///    and below. Accented names are unaffected: "José" needs one mark, not fifteen.
    ///
    /// Length is capped at <see cref="MaxLength"/>. The cap is applied last, and never inside a
    /// surrogate pair — splitting one would emit exactly the lone surrogate this is here to
    /// remove.
    /// </summary>
    public static class PlayerName
    {
        /// <summary>
        /// UTF-16 units, not glyphs. Chosen to fit the standings rail at six seats on the
        /// narrowest supported device, where the row also carries score, cards and a state chip.
        /// </summary>
        public const int MaxLength = 16;

        /// <summary>
        /// Combining marks allowed per base character. Two covers real orthography — a vowel with
        /// both an accent and a diaeresis is as deep as living scripts stack for a name — while
        /// stopping the decorative pile-up that renders outside its own row.
        /// </summary>
        public const int MaxMarksPerCharacter = 2;

        /// <summary>The name a seat carries when its player has not chosen one (AC3).</summary>
        public static string SeatDefault(int seatIndex) => "Player " + (seatIndex + 1);

        /// <summary>
        /// Cleans <paramref name="raw"/>, falling back to <paramref name="fallback"/> when nothing
        /// renderable survives — which covers empty, whitespace-only, and a name made entirely of
        /// characters this strips.
        /// </summary>
        public static string Sanitize(string raw, string fallback)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;

            var sb = new StringBuilder(MaxLength);
            bool spacePending = false;
            bool hasBase = false;       // is there a character for a mark to attach to?
            int marks = 0;              // marks already stacked on that character

            for (int i = 0; i < raw.Length && sb.Length < MaxLength; i++)
            {
                char c = raw[i];

                if (char.IsWhiteSpace(c))
                {
                    // Leading whitespace is dropped outright; inner runs become one space, and a
                    // trailing run is simply never flushed.
                    spacePending |= sb.Length > 0;
                    continue;
                }

                // A surrogate pair is one character to the reader, so it is taken or left whole.
                bool isPair = char.IsHighSurrogate(c) && i + 1 < raw.Length && char.IsLowSurrogate(raw[i + 1]);
                var category = isPair
                    ? char.GetUnicodeCategory(raw, i)
                    : CharUnicodeInfo.GetUnicodeCategory(c);

                if (!Allowed(category))
                {
                    if (isPair) i++;
                    continue;
                }

                if (IsMark(category))
                {
                    // A mark with nothing under it — one opening the name, or following a space —
                    // has no base to combine with, and past the cap they only stack higher.
                    if (spacePending || !hasBase || marks >= MaxMarksPerCharacter)
                    {
                        if (isPair) i++;
                        continue;
                    }

                    marks++;
                }

                int width = isPair ? 2 : 1;
                int spaceWidth = spacePending ? 1 : 0;
                if (sb.Length + spaceWidth + width > MaxLength) break;

                if (spacePending)
                {
                    sb.Append(' ');
                    spacePending = false;
                }

                sb.Append(c);
                if (isPair) sb.Append(raw[++i]);

                if (!IsMark(category))
                {
                    hasBase = true;
                    marks = 0;
                }
            }

            return sb.Length == 0 ? fallback : sb.ToString();
        }

        /// <summary>Cleans a name, falling back to that seat's default (AC3).</summary>
        public static string Sanitize(string raw, int seatIndex) => Sanitize(raw, SeatDefault(seatIndex));

        private static bool IsMark(UnicodeCategory category) =>
            category == UnicodeCategory.NonSpacingMark ||
            category == UnicodeCategory.SpacingCombiningMark ||
            category == UnicodeCategory.EnclosingMark;

        private static bool Allowed(UnicodeCategory category)
        {
            switch (category)
            {
                case UnicodeCategory.Control:
                case UnicodeCategory.Format:
                case UnicodeCategory.Surrogate:
                case UnicodeCategory.PrivateUse:
                case UnicodeCategory.OtherNotAssigned:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                    return false;
                default:
                    return true;
            }
        }
    }
}
