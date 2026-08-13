using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Pre-match lobby. Shows the shareable join code and a Host-only "Start Match" button.
    /// Passive view; a presenter in Game.App wires it to the session + scene flow.
    /// </summary>
    public sealed class LobbyView : MonoBehaviour
    {
        [SerializeField] private TMP_Text codeLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button startButton;

        public event Action StartClicked;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(() => StartClicked?.Invoke());
        }

        public void SetCode(string code)
        {
            if (codeLabel != null) codeLabel.text = string.IsNullOrEmpty(code) ? "" : $"Join code: {code}";
        }

        public void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }

        public void SetStartVisible(bool visible)
        {
            if (startButton != null) startButton.gameObject.SetActive(visible);
        }
    }
}
