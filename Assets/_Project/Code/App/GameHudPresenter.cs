using Game.Core;
using Game.Networking;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Presenter that binds the <see cref="GameHudView"/> to an <see cref="IGameActions"/>/
    /// <see cref="IGameStateView"/> pair. By default it finds the networked
    /// <see cref="NetworkGameController"/> in the scene, but <see cref="Bind"/> accepts any
    /// implementation — pass a <see cref="LocalGameSession"/> to drive the same HUD fully offline.
    /// </summary>
    public sealed class GameHudPresenter : MonoBehaviour
    {
        [SerializeField] private GameHudView view;

        private IGameActions _actions;
        private IGameStateView _stateView;

        private void Start()
        {
            if (_stateView == null)
            {
                var controller = FindFirstObjectByType<NetworkGameController>();
                if (controller != null) Bind(controller, controller);
            }
        }

        /// <summary>Wire the HUD to a session (networked or local).</summary>
        public void Bind(IGameActions actions, IGameStateView stateView)
        {
            Unbind();

            _actions = actions;
            _stateView = stateView;

            view.RollClicked += OnRoll;
            view.EndTurnClicked += OnEndTurn;
            view.ClaimClicked += OnClaim;

            _stateView.Changed += OnStateChanged;
            _stateView.MoveRejected += OnMoveRejected;

            view.SetLocalPlayer(_stateView.LocalPlayer);
            view.Render(_stateView.Current);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (view != null)
            {
                view.RollClicked -= OnRoll;
                view.EndTurnClicked -= OnEndTurn;
                view.ClaimClicked -= OnClaim;
            }
            if (_stateView != null)
            {
                _stateView.Changed -= OnStateChanged;
                _stateView.MoveRejected -= OnMoveRejected;
            }
        }

        private void OnRoll() => _actions?.RequestRoll();
        private void OnEndTurn() => _actions?.RequestEndTurn();
        private void OnClaim(int cardId) => _actions?.RequestClaim(new CardId(cardId));

        private void OnStateChanged(GameStateSnapshot snapshot)
        {
            view.SetLocalPlayer(_stateView.LocalPlayer);
            view.Render(snapshot);
        }

        private void OnMoveRejected(MoveFailure failure) => view.ShowMessage(FriendlyReason(failure));

        private static string FriendlyReason(MoveFailure failure)
        {
            switch (failure)
            {
                case MoveFailure.NotYourTurn: return "It's not your turn.";
                case MoveFailure.NoRollsRemaining: return "No rolls left this turn.";
                case MoveFailure.RequirementNotMet: return "Your dice don't match that card.";
                case MoveFailure.WrongPhase: return "Roll your dice first.";
                case MoveFailure.CardNotInMarket: return "That card is no longer available.";
                default: return string.Empty;
            }
        }
    }
}
