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
        public event Action OnItemMenuRequested;
        public event Action OnMovesMenuOpened;
        public event Action OnMainMenuOpened;
        public event Action OnFleeRequested;

        private bool _isVisible;
        private BattleCombatant _player;
        private List<BattleCombatant> _allCombatants;
        private List<BattleCombatant> _allTargets;
        private List<Button> _actionButtons = new List<Button>();
        private List<MoveButton> _moveButtons = new List<MoveButton>();
        private Button _backButton;
        private readonly Global _global;

        public enum MenuState { Main, Moves, Targeting, Tooltip }
        private MenuState _currentState;
        public MenuState CurrentMenuState => _currentState;
        private MoveData _selectedMove;
        public MoveData SelectedMove => _selectedMove;
        private MoveData _tooltipMove;
        public MoveData HoveredMove { get; private set; }

        private float _targetingTextAnimTimer = 0f;
        private bool _buttonsInitialized = false;
        private MouseState _previousMouseState;

        // State for animation
        private MoveData[] _previousHandState = new MoveData[6];
        private Queue<MoveButton> _buttonsToAnimate = new Queue<MoveButton>();
        private float _animationDelayTimer = 0f;
        private const float SEQUENTIAL_ANIMATION_DELAY = 0.05f;

        private Queue<ImageButton> _actionButtonsToAnimate = new Queue<ImageButton>();
        private float _actionButtonAnimationDelayTimer = 0f;
        private const float ACTION_BUTTON_SEQUENTIAL_ANIMATION_DELAY = 0.05f;

        public ActionMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _backButton = new Button(Rectangle.Empty, "BACK");
            _backButton.OnClick += () => {
                if (_currentState == MenuState.Targeting || _currentState == MenuState.Tooltip)
                {
                    SetState(MenuState.Moves);
                }
                else if (_currentState == MenuState.Moves)
                {
                    SetState(MenuState.Main);
                }
            };
            _previousMouseState = Mouse.GetState();
        }

        private void InitializeButtons()
        {
            if (_buttonsInitialized) return;

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var actionSheet = spriteManager.ActionButtonsSpriteSheet;
            var rects = spriteManager.ActionButtonSourceRects;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[0], rects[1], rects[2], function: "Act", startVisible: false, debugColor: new Color(100, 0, 0, 150)));
            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[3], rects[4], rects[5], function: "Item", startVisible: false, debugColor: new Color(0, 100, 0, 150)));
            _actionButtons.Add(new ImageButton(Rectangle.Empty, actionSheet, rects[6], rects[7], rects[8], function: "Flee", startVisible: false, debugColor: new Color(0, 0, 100, 150)));

            _actionButtons[0].OnClick += () => SetState(MenuState.Moves);
            _actionButtons[1].OnClick += () => OnItemMenuRequested?.Invoke();
            _actionButtons[2].OnClick += () => OnFleeRequested?.Invoke();

            _backButton.Font = secondaryFont;

            _buttonsInitialized = true;
        }

        public void GoBack()
        {
            if (!_isVisible) return;

            if (_currentState == MenuState.Tooltip || _currentState == MenuState.Moves || _currentState == MenuState.Targeting)
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
            Array.Clear(_previousHandState, 0, _previousHandState.Length);
        }

        public void Show(BattleCombatant player, List<BattleCombatant> allCombatants)
        {
            _isVisible = true;
            _player = player;
            _allCombatants = allCombatants;
            _allTargets = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();
            SetState(MenuState.Main);
        }

        public void Hide()
        {
            _isVisible = false;
        }

        public void SetState(MenuState newState)
        {
            _currentState = newState;
            HoveredMove = null;

            if (newState == MenuState.Main)
            {
                OnMainMenuOpened?.Invoke();
                _actionButtonsToAnimate.Clear();
                _actionButtonAnimationDelayTimer = 0f;
                foreach (var button in _actionButtons)
                {
                    if (button is ImageButton imageButton)
                    {
                        imageButton.HideForAnimation();
                        _actionButtonsToAnimate.Enqueue(imageButton);
                    }
                }
            }
            else if (newState == MenuState.Moves)
            {
                OnMovesMenuOpened?.Invoke();
                PopulateAndAnimateMoveButtons();
            }
            else if (newState == MenuState.Targeting)
            {
                _targetingTextAnimTimer = 0f;
            }
        }

        private void PopulateAndAnimateMoveButtons()
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            _moveButtons.Clear();
            _buttonsToAnimate.Clear();
            _animationDelayTimer = 0f;

            var currentHand = _player.AvailableMoves.Take(6).ToList();
            if (!currentHand.Any())
            {
                if (BattleDataCache.Moves.TryGetValue("Stall", out var stallMove))
                {
                    var target = _allTargets.FirstOrDefault();
                    if (target != null) OnMoveSelected?.Invoke(stallMove, target);
                }
                return;
            }

            var newHand = new MoveData[6];
            for (int i = 0; i < Math.Min(currentHand.Count, 6); i++)
            {
                newHand[i] = currentHand[i];
            }

            for (int i = 0; i < 6; i++)
            {
                var move = newHand[i];
                if (move == null) continue;

                bool isNew = _previousHandState[i] == null || _previousHandState[i].MoveID != move.MoveID;
                var moveButton = CreateMoveButton(move, secondaryFont, spriteManager.ActionButtonTemplateSprite, !isNew);
                _moveButtons.Add(moveButton);

                if (isNew)
                {
                    _buttonsToAnimate.Enqueue(moveButton);
                }
            }

            Array.Copy(newHand, _previousHandState, newHand.Length);
        }

        private MoveButton CreateMoveButton(MoveData move, BitmapFont font, Texture2D background, bool startVisible)
        {
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            int elementId = move.OffensiveElementIDs.FirstOrDefault();
            Rectangle? sourceRect = null;
            if (spriteManager.ElementIconSourceRects.TryGetValue(elementId, out var rect))
            {
                sourceRect = rect;
            }

            var moveButton = new MoveButton(move, font, background, spriteManager.ElementIconsSpriteSheet, sourceRect, startVisible);
            moveButton.OnClick += () => {
                _selectedMove = move;

                // Update the internal hand state immediately upon selection to mark the slot as empty.
                for (int i = 0; i < _previousHandState.Length; i++)
                {
                    if (_previousHandState[i] == move)
                    {
                        _previousHandState[i] = null;
                        break;
                    }
                }

                switch (move.Target)
                {
                    case TargetType.Self:
                        OnMoveSelected?.Invoke(move, _player);
                        break;

                    case TargetType.None:
                    case TargetType.Every:
                    case TargetType.EveryAll:
                        OnMoveSelected?.Invoke(move, null);
                        break;

                    case TargetType.Single:
                        var enemies = _allTargets.Where(c => !c.IsDefeated).ToList();
                        if (enemies.Count == 1)
                        {
                            OnMoveSelected?.Invoke(move, enemies[0]);
                        }
                        else
                        {
                            SetState(MenuState.Targeting);
                        }
                        break;

                    case TargetType.SingleAll:
                        var allValidTargets = _allCombatants.Where(c => !c.IsDefeated).ToList();
                        if (allValidTargets.Count == 1)
                        {
                            OnMoveSelected?.Invoke(move, allValidTargets[0]);
                        }
                        else
                        {
                            SetState(MenuState.Targeting);
                        }
                        break;
                }
            };
            moveButton.OnRightClick += () => {
                _tooltipMove = move;
                SetState(MenuState.Tooltip);
            };
            return moveButton;
        }

        public void Update(MouseState currentMouseState, GameTime gameTime)
        {
            InitializeButtons();
            if (!_isVisible) return;

            if (_actionButtonsToAnimate.Any())
            {
                _actionButtonAnimationDelayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_actionButtonAnimationDelayTimer >= ACTION_BUTTON_SEQUENTIAL_ANIMATION_DELAY)
                {
                    _actionButtonAnimationDelayTimer = 0f;
                    var buttonToAnimate = _actionButtonsToAnimate.Dequeue();
                    buttonToAnimate.TriggerAppearAnimation();
                }
            }

            if (_buttonsToAnimate.Any())
            {
                _animationDelayTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_animationDelayTimer >= SEQUENTIAL_ANIMATION_DELAY)
                {
                    _animationDelayTimer = 0f;
                    var buttonToAnimate = _buttonsToAnimate.Dequeue();
                    buttonToAnimate.TriggerAppearAnimation();
                }
            }

            switch (_currentState)
            {
                case MenuState.Main:
                    foreach (var button in _actionButtons) button.Update(currentMouseState);
                    break;
                case MenuState.Moves:
                    HoveredMove = null;
                    foreach (var button in _moveButtons)
                    {
                        button.Update(currentMouseState);
                        if (button.IsHovered)
                        {
                            HoveredMove = button.Move;
                        }
                    }
                    _backButton.Update(currentMouseState);
                    break;
                case MenuState.Targeting:
                    _targetingTextAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    _backButton.Update(currentMouseState);
                    break;
                case MenuState.Tooltip:
                    bool leftClick = currentMouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
                    bool rightClick = currentMouseState.RightButton == ButtonState.Released && _previousMouseState.RightButton == ButtonState.Pressed;
                    if (leftClick || rightClick)
                    {
                        if (UIInputManager.CanProcessMouseClick())
                        {
                            SetState(MenuState.Moves);
                            UIInputManager.ConsumeMouseClick();
                        }
                    }
                    _backButton.Update(currentMouseState);
                    break;
            }

            _previousMouseState = currentMouseState;
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

                        int startY = dividerY - 9;
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
                        DrawMovesMenu(spriteBatch, font, gameTime, transform);
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
                case MenuState.Tooltip:
                    {
                        var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                        // Define the area for the tooltip content, matching the moves menu area.
                        const int dividerY = 114;
                        const int moveButtonWidth = 157;
                        const int moveButtonHeight = 17;
                        const int columns = 2;
                        const int rows = 3;
                        const int columnSpacing = 0;
                        const int rowSpacing = 0;

                        int totalGridWidth = (moveButtonWidth * columns) + columnSpacing;
                        int gridHeight = (moveButtonHeight * rows) + (rowSpacing * (rows - 1));
                        int gridStartX = (Global.VIRTUAL_WIDTH - totalGridWidth) / 2;
                        int gridStartY = dividerY - 4;

                        // Draw the background sprite for the tooltip area
                        var spriteManager = ServiceLocator.Get<SpriteManager>();
                        var tooltipBg = spriteManager.ActionTooltipBackgroundSprite;
                        var tooltipBgRect = new Rectangle(gridStartX, gridStartY, totalGridWidth, gridHeight);
                        spriteBatch.DrawSnapped(tooltipBg, tooltipBgRect, Color.White);

                        // Draw the move name and stats
                        if (_tooltipMove != null)
                        {
                            var moveName = _tooltipMove.MoveName.ToUpper();
                            var nameSize = font.MeasureString(moveName);
                            var namePos = new Vector2(
                                tooltipBgRect.Center.X - nameSize.Width / 2,
                                tooltipBgRect.Y + 10
                            );
                            spriteBatch.DrawStringSnapped(font, moveName, namePos, _global.Palette_BrightWhite);

                            string powerText = _tooltipMove.Power > 0 ? $"POWER: {_tooltipMove.Power}" : "POWER: ---";
                            string accuracyText = _tooltipMove.Accuracy >= 0 ? $"ACCURACY: {_tooltipMove.Accuracy}%" : "ACCURACY: ---";

                            var powerSize = secondaryFont.MeasureString(powerText);
                            var accuracySize = secondaryFont.MeasureString(accuracyText);
                            const int statSpacing = 20;
                            float totalStatsWidth = powerSize.Width + accuracySize.Width + statSpacing;
                            float statsStartX = tooltipBgRect.Center.X - totalStatsWidth / 2;

                            var powerPos = new Vector2(statsStartX, namePos.Y + nameSize.Height + 15);
                            var accuracyPos = new Vector2(statsStartX + powerSize.Width + statSpacing, powerPos.Y);

                            spriteBatch.DrawStringSnapped(secondaryFont, powerText, powerPos, _global.Palette_White);
                            spriteBatch.DrawStringSnapped(secondaryFont, accuracyText, accuracyPos, _global.Palette_White);
                        }

                        // Draw the back button
                        const int backButtonTopMargin = 7;
                        int backButtonY = gridStartY + gridHeight + backButtonTopMargin;
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
            }
        }

        private void DrawMovesMenu(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
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
            int gridStartY = dividerY - 4;

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
            int backButtonY = gridStartY + gridHeight + 7;
            var backSize = (_backButton.Font ?? font).MeasureString(_backButton.Text);
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2,
                backButtonY,
                backWidth,
                13
            );
            _backButton.Draw(spriteBatch, font, gameTime, transform);
        }
    }
}