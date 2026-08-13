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
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text joinCodeLabel;

        public event Action HostClicked;
        public event Action<string> JoinClicked;

        private void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(() => HostClicked?.Invoke());
            if (joinButton != null) joinButton.onClick.AddListener(() =>
                JoinClicked?.Invoke(joinCodeInput != null ? joinCodeInput.text?.Trim() : string.Empty));
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
        }
    }
}
