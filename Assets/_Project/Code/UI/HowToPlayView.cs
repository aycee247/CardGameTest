using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The first-run explainer (STORY-3.5): four pages covering the round, what dice are for,
    /// Sparks, and how a contested card resolves.
    ///
    /// These four were chosen because they are the things the board never says out loud. A new
    /// player can read the market, the dice and the timer from the screen; they cannot work out
    /// that unspent dice become currency, or that two players wanting the same card is a normal
    /// event with a rule behind it rather than a mistake.
    ///
    /// Passive, like every other view here: it owns its copy and its paging, raises events, and
    /// decides nothing about when it appears or what is written down afterwards.
    ///
    /// The numbers below are transcribed from docs/game-design.md §2–3. If the economy changes,
    /// they change with it, and <see cref="Version"/> goes up so everyone sees the flow again.
    /// </summary>
    public sealed class HowToPlayView : MonoBehaviour
    {
        /// <summary>
        /// The revision of this explainer. Compared against the profile's
        /// <c>OnboardingSeenVersion</c>: raise it when the rules it describes change, and every
        /// player is shown the flow once more.
        /// </summary>
        public const int Version = 1;

        private readonly struct Page
        {
            public readonly string Title;
            public readonly string Body;

            public Page(string title, string body)
            {
                Title = title;
                Body = body;
            }
        }

        private static readonly Page[] Pages =
        {
            new Page("Everyone plays at once",
                "There are no turns. Ten rounds, and in each one the whole table acts " +
                "together.\n\n" +
                "ROLL  ·  SHAPE  ·  COMMIT\n" +
                "REVEAL  ·  RE-PICK  ·  UPKEEP\n\n" +
                "You roll, you improve what you rolled, then you secretly pick a card to " +
                "claim."),

            new Page("Dice are how you pay",
                "Every card in the market costs a pattern of dice — three of a kind, a run, a " +
                "total. Not a price you can haggle: a shape you either have or don't.\n\n" +
                "Dice you spend claiming a card are exhausted for the rest of the round.\n\n" +
                "Tap any card to see exactly which of your dice would pay for it."),

            new Page("Nothing is wasted",
                "Dice you don't spend aren't lost. Each becomes 1 Spark at Upkeep, and you can " +
                "hold up to 10.\n\n" +
                "2 Sparks   re-roll one die\n" +
                "4 Sparks   set one die to any face\n" +
                "3 Sparks   given for a round that leaves you empty-handed\n\n" +
                "A bad roll is a slower round, never a lost one."),

            new Page("Two people, one card",
                "Commits are secret until Reveal, so you pick without knowing who else wants " +
                "it. Claims nobody contested simply land.\n\n" +
                "When two players want the same card, priority decides — and priority sits " +
                "with whoever is furthest behind. Everyone can see who holds it.\n\n" +
                "Lose a contest and you get your dice back for one quick re-pick.")
        };

        [Header("Panel")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text progressText;

        [Header("Controls")]
        [SerializeField] private Button nextButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button playSoloButton;

        /// <summary>Read every page, or pressed PLAY SOLO from the last one.</summary>
        public event Action Finished;

        /// <summary>Left early. Deliberately distinct from <see cref="Finished"/> — the caller
        /// may want to know, even though both mean "do not show this again".</summary>
        public event Action Skipped;

        /// <summary>Finished and wants to play right now, rather than return to the menu.</summary>
        public event Action PlaySoloRequested;

        private int _page;

        public bool IsOpen => root != null && root.activeSelf;

        public static int PageCount => Pages.Length;

        private void Awake()
        {
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (backButton != null) backButton.onClick.AddListener(Back);
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
            if (playSoloButton != null) playSoloButton.onClick.AddListener(PlaySolo);

            // Deliberately no Close() here. This component lives on the panel it shows, which the
            // generator leaves inactive, so Awake does not run until Open() activates it —
            // closing here would slam the panel shut on the way in. Same arrangement as
            // HintToastView.
        }

        public void Open()
        {
            _page = 0;
            if (root != null) root.SetActive(true);
            Render();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        public void Next()
        {
            if (_page >= Pages.Length - 1)
            {
                Close();
                Finished?.Invoke();
                return;
            }

            _page++;
            Render();
        }

        public void Back()
        {
            if (_page == 0) return;

            _page--;
            Render();
        }

        public void Skip()
        {
            Close();
            Skipped?.Invoke();
        }

        private void PlaySolo()
        {
            Close();
            Finished?.Invoke();
            PlaySoloRequested?.Invoke();
        }

        private void Render()
        {
            var page = Pages[_page];

            if (titleText != null) titleText.text = page.Title;
            if (bodyText != null) bodyText.text = page.Body;
            if (progressText != null) progressText.text = $"{_page + 1} / {Pages.Length}";

            // Back is hidden rather than disabled on the first page: a dead control invites a tap
            // and then explains nothing about why it did not work.
            if (backButton != null) backButton.gameObject.SetActive(_page > 0);

            // The last page swaps NEXT for the thing worth doing next. Reading about a dice game
            // is not the point; the solo table is where the rest of it lands.
            bool last = _page == Pages.Length - 1;
            if (nextButton != null) nextButton.gameObject.SetActive(!last);
            if (playSoloButton != null) playSoloButton.gameObject.SetActive(last);
            if (skipButton != null) skipButton.gameObject.SetActive(!last);
        }
    }
}
