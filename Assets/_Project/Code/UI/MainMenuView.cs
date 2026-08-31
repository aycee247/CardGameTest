using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Main menu / matchmaking entry. Passive view: raises Host/Join intents and shows status and
    /// the shareable join code. A presenter in Game.App connects it to the SessionManager.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button passPlayButton;
        [SerializeField] private Button soloButton;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text joinCodeLabel;

        public event Action HostClicked;
        public event Action<string> JoinClicked;
        public event Action PassPlayClicked;
        public event Action SoloClicked;

        /// <summary>
        /// The player finished editing their display name (STORY-4.3). Raised on end-edit rather
        /// than per keystroke: the presenter persists it, and a save per character typed is waste.
        /// </summary>
        public event Action<string> NameChanged;

        private void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(() => HostClicked?.Invoke());
            if (joinButton != null) joinButton.onClick.AddListener(() =>
                JoinClicked?.Invoke(joinCodeInput != null ? joinCodeInput.text?.Trim() : string.Empty));
            if (passPlayButton != null) passPlayButton.onClick.AddListener(() => PassPlayClicked?.Invoke());
            if (soloButton != null) soloButton.onClick.AddListener(() => SoloClicked?.Invoke());
            if (nameInput != null)
                nameInput.onEndEdit.AddListener(value => NameChanged?.Invoke(value ?? string.Empty));
        }

        /// <summary>
        /// Shows the stored name. Leaves the field empty when there is none, so the placeholder
        /// does the asking rather than a pre-filled value the player might mistake for a choice.
        /// </summary>
        public void SetName(string name)
        {
            if (nameInput == null) return;

            // SetTextWithoutNotify: seeding the field must not read back as the player editing it.
            nameInput.SetTextWithoutNotify(name ?? string.Empty);
        }

        public void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }

        public void SetJoinCode(string code)
        {
            if (joinCodeLabel != null) joinCodeLabel.text = string.IsNullOrEmpty(code) ? "" : $"Code: {code}";
        }

        public void SetInteractable(bool interactable)
        {
            if (hostButton != null) hostButton.interactable = interactable;
            if (joinButton != null) joinButton.interactable = interactable;
            if (passPlayButton != null) passPlayButton.interactable = interactable;
            if (soloButton != null) soloButton.interactable = interactable;
        }
    }
}
