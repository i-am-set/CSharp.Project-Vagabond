using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public class ActionMenu
    {
        public event Action<MoveData, BattleCombatant> OnMoveSelected;
        public event Action<MoveData> OnTargetingInitiated;
        public event Action OnTargetingCancelled;
        public event Action OnItemMenuRequested;
        public event Action OnMovesMenuOpened;
        public event Action OnMainMenuOpened;

        private bool _isVisible;
        private BattleCombatant _player;
        private List<BattleCombatant> _allTargets;
        private List<Button> _actionButtons = new List<Button>();
        private List<MoveButton> _moveButtons = new List<MoveButton>();
        private Button _backButton;
        private readonly Global _global;

        private enum MenuState { Main, Moves, Targeting }
        private MenuState _currentState;
        private MoveData _selectedMove;
        private float _targetingTextAnimTimer = 0f;
        private bool _buttonsInitialized = false;

        public ActionMenu()
        {
            _global = ServiceLocator.Get<Global>();
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

        private void InitializeButtons()
        {
            if (_buttonsInitialized) return;

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var actionSheet = spriteManager.ActionButtonsSpriteSheet;
            var rects = spriteManager.ActionButtonSourceRects;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[0], rects[1], rects[2], function: "Act", debugColor: new Color(100, 0, 0, 150)));
            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[3], rects[4], rects[5], function: "Item", debugColor: new Color(0, 100, 0, 150)));
            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[6], rects[7], rects[8], function: "Flee", debugColor: new Color(0, 0, 100, 150)) { IsEnabled = false });

            _actionButtons[0].OnClick += () => SetState(MenuState.Moves);
            _actionButtons[1].OnClick += () => OnItemMenuRequested?.Invoke();

            _backButton.Font = secondaryFont;

            _buttonsInitialized = true;
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
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            if (newState == MenuState.Main)
            {
                OnMainMenuOpened?.Invoke();
            }
            else if (newState == MenuState.Moves)
            {
                OnMovesMenuOpened?.Invoke();
            }

            if (_currentState == MenuState.Moves)
            {
                _moveButtons.Clear();
                var movesToDisplay = _player.AvailableMoves.Take(6).ToList();

                if (!movesToDisplay.Any())
                {
                    if (BattleDataCache.Moves.TryGetValue("Stall", out var stallMove))
                    {
                        var target = _allTargets.FirstOrDefault();
                        if (target != null)
                        {
                            OnMoveSelected?.Invoke(stallMove, target);
                            Hide();
                        }
                    }
                    return;
                }

                foreach (var move in movesToDisplay)
                {
                    var moveButton = new MoveButton(move, secondaryFont, spriteManager.ActionButtonTemplateSprite);
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
            else if (newState == MenuState.Targeting)
            {
                _targetingTextAnimTimer = 0f;
            }
        }

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            InitializeButtons();
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
                    _targetingTextAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    _backButton.Update(currentMouseState);
                    break;
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            InitializeButtons();
            if (!_isVisible) return;

            switch (_currentState)
            {
                case MenuState.Main:
                    {
                        const int horizontalPadding = 10;
                        const int buttonSpacing = 5;
                        const int dividerY = 120;

                        int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY;

                        int buttonWidth = (availableWidth - (buttonSpacing * (_actionButtons.Count - 1))) / _actionButtons.Count;
                        int buttonHeight = availableHeight;

                        int startY = dividerY - 5;
                        int startX = horizontalPadding;

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
                        const int dividerY = 114;
                        const int moveButtonWidth = 157;
                        const int moveButtonHeight = 17;
                        const int columnSpacing = 0;
                        const int rowSpacing = 0;
                        const int columns = 2;
                        const int rows = 3;

                        int totalGridWidth = (moveButtonWidth * columns) + columnSpacing;
                        int gridStartX = (Global.VIRTUAL_WIDTH - totalGridWidth) / 2;
                        int gridStartY = dividerY + 2;

                        for (int i = 0; i < _moveButtons.Count; i++)
                        {
                            var button = _moveButtons[i];
                            int row = i / columns;
                            int col = i % columns;

                            button.Bounds = new Rectangle(
                                gridStartX + col * (moveButtonWidth + columnSpacing),
                                gridStartY + row * (moveButtonHeight + rowSpacing),
                                moveButtonWidth,
                                moveButtonHeight
                            );
                            button.Draw(spriteBatch, font, gameTime, transform);
                        }

                        int gridHeight = (moveButtonHeight * rows) + (rowSpacing * (rows - 1));
                        int backButtonY = gridStartY + gridHeight - 1;
                        var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
                        int backWidth = (int)backSize.Width + 16;
                        _backButton.Bounds = new Rectangle(
                            (Global.VIRTUAL_WIDTH - backWidth) / 2,
                            backButtonY,
                            backWidth,
                            13
                        );
                        _backButton.Draw(spriteBatch, font, gameTime, transform);
                        break;
                    }
                case MenuState.Targeting:
                    {
                        const int backButtonPadding = 8;
                        const int backButtonHeight = 13;
                        const int backButtonTopMargin = 1;
                        const int dividerY = 120;
                        const int horizontalPadding = 10;
                        const int verticalPadding = 2;
                        int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
                        int availableHeight = Global.VIRTUAL_HEIGHT - dividerY - (verticalPadding * 2);
                        int gridAreaHeight = availableHeight - backButtonHeight - backButtonTopMargin;
                        int gridStartY = dividerY + verticalPadding + 4;

                        string text = "CHOOSE A TARGET";
                        Vector2 textSize = font.MeasureString(text);

                        // Figure 8 animation
                        float animX = MathF.Sin(_targetingTextAnimTimer * 4f) * 1f;
                        float animY = MathF.Sin(_targetingTextAnimTimer * 8f) * 1f;
                        Vector2 animOffset = new Vector2(animX, animY);

                        Vector2 textPos = new Vector2(
                            horizontalPadding + (availableWidth - textSize.X) / 2,
                            gridStartY + (gridAreaHeight - textSize.Y) / 2
                        ) + animOffset;
                        spriteBatch.DrawStringSnapped(font, text, textPos, Color.Red);

                        int backButtonWidth = (int)(_backButton.Font ?? font).MeasureString(_backButton.Text).Width + backButtonPadding * 2;
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