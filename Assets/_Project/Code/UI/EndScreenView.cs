using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Match-end standings (handoff screen 4): winner headline, a tie-break note when one applied,
    /// ranked blueprint rows rising in with a stagger, and REMATCH / MAIN MENU. Renders from the
    /// snapshot's Standings projection, so an online client shows exactly what the server scored
    /// (CARD-3) — hot-seat and online share every line of it.
    /// </summary>
    public sealed class EndScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text eyebrow;
        [SerializeField] private TMP_Text headline;
        [SerializeField] private TMP_Text note;
        [SerializeField] private Transform rowsRoot;
        [SerializeField] private RectTransform rowTemplate;
        [SerializeField] private Button rematchButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private UiAnimationService anims;
        [SerializeField] private ThemeAsset theme;

        private sealed class Row
        {
            public RectTransform Root;
            public Image Background;
            public BlueprintFrame Frame;
            public TMP_Text Rank;
            public TMP_Text Name;
            public TMP_Text Detail;
            public TMP_Text Score;
        }

        private readonly List<Row> _rows = new List<Row>();

        public event Action RematchClicked;
        public event Action MenuClicked;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (rematchButton != null) rematchButton.onClick.AddListener(() => RematchClicked?.Invoke());
            if (menuButton != null) menuButton.onClick.AddListener(() => MenuClicked?.Invoke());
        }

        public void SetRematchVisible(bool visible)
        {
            if (rematchButton != null) rematchButton.gameObject.SetActive(visible);
        }

        public void Show(in MatchSnapshot snapshot)
        {
            var standings = snapshot.Standings ?? Array.Empty<FinalScoreSnapshot>();

            if (eyebrow != null) eyebrow.text = $"MATCH OVER — {snapshot.TotalRounds} ROUNDS";

            if (standings.Length > 0)
            {
                var winner = standings[0];
                if (headline != null) headline.text = $"{winner.DisplayName.ToUpperInvariant()} WINS";
                if (note != null) note.text = DescribeOutcome(snapshot, standings);
            }
            else
            {
                if (headline != null) headline.text = "MATCH OVER";
                if (note != null) note.text = string.Empty;
            }

            RenderRows(snapshot, standings);
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void RenderRows(in MatchSnapshot snapshot, FinalScoreSnapshot[] standings)
        {
            if (rowsRoot == null || rowTemplate == null) return;

            while (_rows.Count < standings.Length)
            {
                var rt = Instantiate(rowTemplate, rowsRoot);
                _rows.Add(new Row
                {
                    Root = rt,
                    Background = rt.GetComponent<Image>(),
                    Frame = rt.GetComponentInChildren<BlueprintFrame>(true),
                    Rank = Find(rt, "Rank"),
                    Name = Find(rt, "Name"),
                    Detail = Find(rt, "Detail"),
                    Score = Find(rt, "Score"),
                });
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool active = i < standings.Length;
                row.Root.gameObject.SetActive(active);
                if (!active) continue;

                var standing = standings[i];
                bool isWinner = standing.Rank == 0;
                bool isObserver = standing.PlayerId == snapshot.ObserverId;

                if (row.Rank != null)
                {
                    row.Rank.text = (standing.Rank + 1).ToString();
                    if (theme != null) row.Rank.color = isWinner ? theme.Accent(700) : theme.textMuted;
                }

                if (row.Name != null)
                    row.Name.text = standing.DisplayName + (isObserver ? " — YOU" : string.Empty);

                if (row.Detail != null)
                {
                    string detail = $"{standing.CardCount} cards · {standing.Sparks} sparks";
                    if (standing.PowerPoints > 0) detail += $" · +{standing.PowerPoints} end-game VP";
                    row.Detail.text = detail;
                }

                if (row.Score != null) row.Score.text = $"{standing.Total} VP";

                if (theme != null)
                {
                    if (row.Background != null)
                        row.Background.color = isWinner ? theme.Accent(100) : theme.surfaceRaised;
                    if (row.Frame != null)
                        row.Frame.SetBorderColor(isWinner ? theme.accentPriority : theme.divider);
                }

                // Staggered rise-in (~0.35s, 0.08s apart), snapping to rest under reduced motion.
                if (anims != null)
                {
                    var target = row.Root;
                    int index = i;
                    float total = 0.35f + index * 0.08f;
                    anims.Play(total, UiEase.OutCubic, t =>
                    {
                        float local = Mathf.Clamp01((t * total - index * 0.08f) / 0.35f);
                        target.localScale = new Vector3(1f, Mathf.Max(0.01f, local), 1f);
                    });
                }
            }
        }

        private string DescribeOutcome(in MatchSnapshot snapshot, FinalScoreSnapshot[] standings)
        {
            if (standings.Length > 1 && standings[0].Total == standings[1].Total)
            {
                if (standings[0].Sparks != standings[1].Sparks)
                    return "Tied on VP — Sparks broke the tie.";
                if (standings[0].CardCount != standings[1].CardCount)
                    return "Tied on VP and Sparks — card count broke the tie.";
                return "Dead even — seat order broke the tie.";
            }

            return standings[0].PlayerId == snapshot.ObserverId
                ? "Your engine paid out."
                : "Final scores, end-game powers included.";
        }

        private static TMP_Text Find(RectTransform row, string childName)
        {
            var child = row.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }
    }
}
