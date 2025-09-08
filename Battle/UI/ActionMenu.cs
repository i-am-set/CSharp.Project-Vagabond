using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public class ActionMenu
    {
        public event Action<MoveData, BattleCombatant> OnMoveSelected;
        public event Action<MoveData> OnTargetingInitiated;
        public event Action OnTargetingCancelled;

        private bool _isVisible;
        private BattleCombatant _player;
        private List<BattleCombatant> _allTargets;
        private List<Button> _actionButtons = new List<Button>();
        private List<Button> _moveButtons = new List<Button>();
        private Button _backButton;

        private enum MenuState { Main, Moves, Targeting }
        private MenuState _currentState;
        private MoveData _selectedMove;

        public ActionMenu()
        {
            // Initialize main action buttons as ImageButtons without textures
            _actionButtons.Add(new ImageButton(Rectangle.Empty, function: "Act", debugColor: new Color(100, 0, 0, 150)));
            _actionButtons.Add(new ImageButton(Rectangle.Empty, function: "Item", debugColor: new Color(0, 100, 0, 150)) { IsEnabled = false }); // Disabled for now
            _actionButtons.Add(new ImageButton(Rectangle.Empty, function: "Flee", debugColor: new Color(0, 0, 100, 150)) { IsEnabled = false }); // Disabled for now

            _actionButtons[0].OnClick += () => SetState(MenuState.Moves);

            _backButton = new Button(Rectangle.Empty, "BACK");
            _backButton.OnClick += () => {
                if (_currentState == MenuState.Targeting)
                {
                    OnTargetingCancelled?.Invoke();
                    SetState(MenuState.Moves);
                }
                else if (_currentState == MenuState.Moves)
                {
                    SetState(MenuState.Main);
                }
            };
        }

        public void GoBack()
        {
            if (_isVisible && (_currentState == MenuState.Moves || _currentState == MenuState.Targeting))
            {
                _backButton.TriggerClick();
            }
        }

        public void ResetAnimationState()
        {
            foreach (var button in _actionButtons)
            {
                button.ResetAnimationState();
            }
            foreach (var button in _moveButtons)
            {
                button.ResetAnimationState();
            }
            _backButton.ResetAnimationState();
        }

        public void Show(BattleCombatant player, List<BattleCombatant> allCombatants)
        {
            _isVisible = true;
            _player = player;
            _allTargets = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();
            SetState(MenuState.Main);
        }

        public void Hide()
        {
            _isVisible = false;
        }

        private void SetState(MenuState newState)
        {
            _currentState = newState;

            if (_currentState == MenuState.Moves)
            {
                _moveButtons.Clear();
                foreach (var move in _player.AvailableMoves)
                {
                    var moveButton = new Button(Rectangle.Empty, move.MoveName.ToUpper());
                    moveButton.OnClick += () => {
                        _selectedMove = move;
                        if (_allTargets.Count == 1)
                        {
                            OnMoveSelected?.Invoke(_selectedMove, _allTargets[0]);
                            Hide();
                        }
                        else
                        {
                            OnTargetingInitiated?.Invoke(_selectedMove);
                            SetState(MenuState.Targeting);
                        }
                    };
                    _moveButtons.Add(moveButton);
                }
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
                    _backButton.Update(currentMouseState);
                    break;
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (!_isVisible) return;

            switch (_currentState)
            {
                case MenuState.Main:
                    {
                        const int horizontalPadding = 10;
                        const int verticalPadding = 5;
                        const int buttonSpacing = 5;
                        const int dividerY = 120;

                        int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY - (verticalPadding * 2);

                        int buttonWidth = (availableWidth - (buttonSpacing * (_actionButtons.Count - 1))) / _actionButtons.Count;
                        int buttonHeight = availableHeight;

                        int startX = horizontalPadding;
                        int startY = dividerY + verticalPadding;

                        int currentX = startX;
                        foreach (var button in _actionButtons)
                        {
                            button.Bounds = new Rectangle(currentX, startY, buttonWidth, buttonHeight);
                            button.Draw(spriteBatch, font, gameTime, transform);
                            currentX += buttonWidth + buttonSpacing;
                        }
                        break;
                    }
                case MenuState.Moves:
                    {
                        const int horizontalPadding = 10;
                        const int verticalPadding = 5;
                        const int gridSpacing = 5;
                        const int backButtonTopMargin = 5;
                        const int backButtonHeight = 15;
                        const int backButtonPadding = 8;
                        const int dividerY = 120;

                        int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY - (verticalPadding * 2);

                        int gridAreaHeight = availableHeight - backButtonHeight - backButtonTopMargin;

                        int slotWidth = (availableWidth - gridSpacing) / 2;
                        int slotHeight = (gridAreaHeight - gridSpacing) / 2;

                        int gridStartX = horizontalPadding;
                        int gridStartY = dividerY + verticalPadding;

                        var pixel = ServiceLocator.Get<Texture2D>();

                        for (int i = 0; i < 4; i++)
                        {
                            int row = i / 2;
                            int col = i % 2;
                            var slotRect = new Rectangle(
                                gridStartX + col * (slotWidth + gridSpacing),
                                gridStartY + row * (slotHeight + gridSpacing),
                                slotWidth,
                                slotHeight
                            );

                            if (i < _moveButtons.Count)
                            {
                                var button = _moveButtons[i];
                                button.Bounds = slotRect;
                                button.Draw(spriteBatch, font, gameTime, transform);
                            }
                            else
                            {
                                spriteBatch.DrawSnapped(pixel, slotRect, new Color(40, 40, 40, 150));
                            }
                        }

                        int backButtonWidth = (int)font.MeasureString(_backButton.Text).Width + backButtonPadding * 2;
                        _backButton.Bounds = new Rectangle(
                            gridStartX + (availableWidth - backButtonWidth) / 2,
                            gridStartY + gridAreaHeight + backButtonTopMargin,
                            backButtonWidth,
                            backButtonHeight
                        );
                        _backButton.Draw(spriteBatch, font, gameTime, transform);
                        break;
                    }
                case MenuState.Targeting:
                    {
                        const int backButtonPadding = 8;
                        const int backButtonHeight = 15;
                        const int backButtonTopMargin = 5;
                        const int dividerY = 120;
                        const int horizontalPadding = 10;
                        const int verticalPadding = 5;
                        int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY - (verticalPadding * 2);
                        int gridAreaHeight = availableHeight - backButtonHeight - backButtonTopMargin;
                        int gridStartY = dividerY + verticalPadding;

                        string text = "CHOOSE A TARGET";
                        Vector2 textSize = font.MeasureString(text);
                        Vector2 textPos = new Vector2(
                            horizontalPadding + (availableWidth - textSize.X) / 2,
                            gridStartY + (gridAreaHeight - textSize.Y) / 2
                        );
                        spriteBatch.DrawStringSnapped(font, text, textPos, Color.Red);

                        int backButtonWidth = (int)font.MeasureString(_backButton.Text).Width + backButtonPadding * 2;
                        _backButton.Bounds = new Rectangle(
                            horizontalPadding + (availableWidth - backButtonWidth) / 2,
                            gridStartY + gridAreaHeight + backButtonTopMargin,
                            backButtonWidth,
                            backButtonHeight
                        );
                        _backButton.Draw(spriteBatch, font, gameTime, transform);
                        break;
                    }
            }
        }
    }
}