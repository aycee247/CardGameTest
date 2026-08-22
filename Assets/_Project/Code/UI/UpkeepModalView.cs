using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// The upkeep recap (handoff 6j): a small centred blueprint dialog, purely informational, no
    /// interaction. It shows while the phase lasts and disappears with it — the auto-dismiss is
    /// the phase clock itself.
    /// </summary>
    public sealed class UpkeepModalView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text bodyText;

        public void Show(string body)
        {
            if (bodyText != null) bodyText.text = body ?? string.Empty;
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
