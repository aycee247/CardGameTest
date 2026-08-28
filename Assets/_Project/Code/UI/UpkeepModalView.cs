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

        private UiAnimationService _anims;

        private void Awake()
        {
            _anims = GetComponentInParent<UiAnimationService>(true);
        }

        public void Show(string body)
        {
            if (bodyText != null) bodyText.text = body ?? string.Empty;
            if (root != null)
            {
                bool wasOpen = root.activeSelf;
                root.SetActive(true);
                if (!wasOpen) UiEntrance.StampIn(_anims, root.transform);
            }
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
