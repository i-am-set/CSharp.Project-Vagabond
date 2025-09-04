using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public class ActionMenu
    {
        public event Action<MoveData, BattleCombatant> OnMoveSelected;

        private bool _isVisible;
        private BattleCombatant _player;
        private List<BattleCombatant> _allTargets;
        private List<Button> _actionButtons = new List<Button>();
        private List<Button> _moveButtons = new List<Button>();
        private List<Button> _targetButtons = new List<Button>();
        private Button _backButton;

        private enum MenuState { Main, Moves, Targeting }
        private MenuState _currentState;
        private MoveData _selectedMove;

        public ActionMenu()
        {
            // Initialize main action buttons
            _actionButtons.Add(new Button(Rectangle.Empty, "Act"));
            _actionButtons.Add(new Button(Rectangle.Empty, "Item") { IsEnabled = false }); // Disabled for now
            _actionButtons.Add(new Button(Rectangle.Empty, "Flee") { IsEnabled = false }); // Disabled for now

            _actionButtons[0].OnClick += () => SetState(MenuState.Moves);

            _backButton = new Button(Rectangle.Empty, "Back");
            _backButton.OnClick += () => {
                if (_currentState == MenuState.Targeting) SetState(MenuState.Moves);
                else if (_currentState == MenuState.Moves) SetState(MenuState.Main);
            };
        }

        public void Show(BattleCombatant player, List<BattleCombatant> allCombatants)
        {
            _isVisible = true;
            _player = player;
            _allTargets = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList(); // Simple targeting for now
            SetState(MenuState.Main);
        }

        public void Hide()
        {
            _isVisible = false;
        }

        private void SetState(MenuState newState)
        {
            _currentState = newState;

            // Populate button lists based on the new state. This is now the only place buttons are created.
            switch (_currentState)
            {
                case MenuState.Moves:
                    _moveButtons.Clear();
                    foreach (var move in _player.AvailableMoves)
                    {
                        var moveButton = new Button(Rectangle.Empty, move.MoveName);
                        moveButton.OnClick += () => {
                            _selectedMove = move;
                            SetState(MenuState.Targeting);
                        };
                        _moveButtons.Add(moveButton);
                    }
                    break;

                case MenuState.Targeting:
                    _targetButtons.Clear();
                    foreach (var target in _allTargets)
                    {
                        var targetButton = new Button(Rectangle.Empty, target.Name);
                        targetButton.OnClick += () => {
                            OnMoveSelected?.Invoke(_selectedMove, target);
                            Hide();
                        };
                        _targetButtons.Add(targetButton);
                    }
                    break;
            }
        }

        public void Update(MouseState currentMouseState)
        {
            if (!_isVisible) return;

            switch (_currentState)
            {
                case MenuState.Main:
                    foreach (var button in _actionButtons) button.Update(currentMouseState);
                    break;
                case MenuState.Moves:
                    foreach (var button in _moveButtons) button.Update(currentMouseState);
                    _backButton.Update(currentMouseState);
                    break;
                case MenuState.Targeting:
                    foreach (var button in _targetButtons) button.Update(currentMouseState);
                    _backButton.Update(currentMouseState);
                    break;
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!_isVisible) return;

            // --- New Horizontal Layout ---
            const int buttonWidth = 40;
            const int buttonHeight = 15;
            const int buttonSpacing = 5;
            const int bottomMargin = 5;
            const int rightMargin = 10;

            int totalWidth = (_actionButtons.Count * buttonWidth) + ((_actionButtons.Count - 1) * buttonSpacing);
            int startX = Global.VIRTUAL_WIDTH - rightMargin - totalWidth;
            int startY = Global.VIRTUAL_HEIGHT - bottomMargin - buttonHeight;

            switch (_currentState)
            {
                case MenuState.Main:
                    int currentX = startX;
                    foreach (var button in _actionButtons)
                    {
                        button.Bounds = new Rectangle(currentX, startY, buttonWidth, buttonHeight);
                        button.Draw(spriteBatch, font, gameTime);
                        currentX += buttonWidth + buttonSpacing;
                    }
                    break;
                case MenuState.Moves:
                    currentX = startX;
                    foreach (var button in _moveButtons)
                    {
                        button.Bounds = new Rectangle(currentX, startY, buttonWidth, buttonHeight);
                        button.Draw(spriteBatch, font, gameTime);
                        currentX += buttonWidth + buttonSpacing;
                    }
                    _backButton.Bounds = new Rectangle(currentX, startY, buttonWidth, buttonHeight);
                    _backButton.Draw(spriteBatch, font, gameTime);
                    break;
                case MenuState.Targeting:
                    currentX = startX;
                    foreach (var button in _targetButtons)
                    {
                        button.Bounds = new Rectangle(currentX, startY, buttonWidth, buttonHeight);
                        button.Draw(spriteBatch, font, gameTime);
                        currentX += buttonWidth + buttonSpacing;
                    }
                    _backButton.Bounds = new Rectangle(currentX, startY, buttonWidth, buttonHeight);
                    _backButton.Draw(spriteBatch, font, gameTime);
                    break;
            }
        }
    }
}