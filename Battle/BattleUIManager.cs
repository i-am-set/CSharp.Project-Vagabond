using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI; // Added for TextAnimator and TextEffectType
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle.UI
{
    public enum BattleUIState { Default, Targeting, Switch }
    public enum BattleSubMenuState { None, ActionRoot, ActionMoves, Switch }
    public class HoverHighlightState { public MoveData? CurrentMove; public List<BattleCombatant> Targets = new List<BattleCombatant>(); public float Timer = 0f; public const float SingleTargetFlashOnDuration = 0.4f; public const float SingleTargetFlashOffDuration = 0.2f; public const float MultiTargetFlashOnDuration = 0.4f; public const float MultiTargetFlashOffDuration = 0.4f; }
    public class BattleUIManager
    {
        public event Action<MoveData, MoveEntry, BattleCombatant>? OnMoveSelected;
        public event Action<BattleCombatant>? OnSwitchActionSelected;
        public event Action<BattleCombatant>? OnForcedSwitchSelected;
        public event Action? OnFleeRequested;
        public event Action<BattleCombatant>? OnTargetSelectedFromUI;
        private readonly BattleNarrator _battleNarrator;
        private readonly ActionMenu _actionMenu;
        private readonly SwitchMenu _switchMenu;
        private readonly CombatSwitchDialog _combatSwitchDialog;
        private readonly Global _global;

        public BattleUIState UIState { get; private set; } = BattleUIState.Default;
        public BattleSubMenuState SubMenuState { get; private set; } = BattleSubMenuState.None;
        public MoveData? MoveForTargeting { get; private set; }
        public MoveEntry? SpellForTargeting => _actionMenu.SelectedSpellbookEntry;
        public TargetType? TargetTypeForSelection =>
            UIState == BattleUIState.Targeting ? MoveForTargeting?.Target :
            null;
        public MoveData? HoveredMove => _actionMenu.HoveredMove;


        private float _targetingTextAnimTimer = 0f;
        private readonly Queue<Action> _narrationQueue = new Queue<Action>();
        public readonly HoverHighlightState HoverHighlightState = new HoverHighlightState();
        public float SharedPulseTimer { get; private set; } = 0f;

        public bool IsBusy => _battleNarrator.IsBusy || _narrationQueue.Any();
        public bool IsWaitingForInput => _battleNarrator.IsWaitingForInput;

        private bool _isPromptVisible;
        private readonly List<(Texture2D Texture, Texture2D Silhouette)> _promptTextures = new List<(Texture2D, Texture2D)>();
        private int _promptTextureIndex;
        private float _promptCycleTimer;
        private const float PROMPT_CYCLE_INTERVAL = 0.5f;
        private Button? _lastHoveredButton;

        // Debug Bounds
        private Rectangle _narratorBounds;
        private Rectangle _controlPromptBounds;

        // Targeting Buttons
        private readonly List<ImageButton> _targetingButtons = new List<ImageButton>();
        public BattleCombatant? HoveredCombatantFromUI { get; private set; }
        public BattleCombatant? CombatantHoveredViaSprite { get; set; }

        // Input State
        private MouseState _previousMouseState;

        // --- INTRO ANIMATION SUPPORT ---
        /// <summary>
        /// Offset applied to the main battle border (and attached UI) for intro animations.
        /// </summary>
        public Vector2 IntroOffset { get; set; } = Vector2.Zero;

        public BattleUIManager()
        {
            _global = ServiceLocator.Get<Global>();

            const int narratorWidth = 307;
            const int narratorHeight = 51;
            int narratorX = 7;
            const int narratorY = 123;
            _narratorBounds = new Rectangle(narratorX, narratorY, narratorWidth, narratorHeight);

            _battleNarrator = new BattleNarrator(_narratorBounds);
            _actionMenu = new ActionMenu();
            _switchMenu = new SwitchMenu();

            // Initialize the new dialog
            // We pass null for the scene because we handle drawing manually in Draw()
            _combatSwitchDialog = new CombatSwitchDialog(null);
            _combatSwitchDialog.OnMemberSelected += (member) => OnForcedSwitchSelected?.Invoke(member);

            _actionMenu.OnMoveSelected += HandlePlayerMoveSelected;
            _actionMenu.OnSwitchMenuRequested += OnSwitchMenuRequested;
            _actionMenu.OnMovesMenuOpened += () => SubMenuState = BattleSubMenuState.ActionMoves;
            _actionMenu.OnMainMenuOpened += () => SubMenuState = BattleSubMenuState.ActionRoot;
            _actionMenu.OnFleeRequested += () => OnFleeRequested?.Invoke();
            _actionMenu.OnSlot2BackRequested += () => ServiceLocator.Get<BattleManager>().CancelSlot2Selection();

            // --- UPDATED STRIKE HANDLER ---
            _actionMenu.OnStrikeRequested += (player) =>
            {
                if (player != null)
                {
                    // Use the smart property that resolves weapon vs default
                    var strikeMove = player.StrikeMove;
                    if (strikeMove != null)
                    {
                        // Manually trigger move selection flow
                        // We pass null for MoveEntry because this is a basic action, not a spell slot
                        _actionMenu.SelectMoveExternal(strikeMove, null);
                    }
                }
            };

            _switchMenu.OnMemberSelected += (target) => OnSwitchActionSelected?.Invoke(target);
            _switchMenu.OnBack += () =>
            {
                SubMenuState = BattleSubMenuState.ActionRoot;
                _switchMenu.Hide();

                var battleManager = ServiceLocator.Get<BattleManager>();
                var player = battleManager.CurrentActingCombatant ?? battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && !c.IsDefeated);

                if (player != null)
                {
                    _actionMenu.Show(player, battleManager.AllCombatants.ToList());
                }
            };

            EventBus.Subscribe<GameEvents.ForcedSwitchRequested>(OnForcedSwitchRequested);

            _previousMouseState = Mouse.GetState();
        }

        private void OnForcedSwitchRequested(GameEvents.ForcedSwitchRequested e)
        {
            // Open the dedicated forced switch dialog
            var battleManager = ServiceLocator.Get<BattleManager>();
            _combatSwitchDialog.Show(battleManager.AllCombatants.ToList());
        }

        private void EnsureTargetingButtonsInitialized()
        {
            if (_targetingButtons.Count > 0) return;

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var sheet = spriteManager.TargetingButtonSpriteSheet;
            var rects = spriteManager.TargetingButtonSourceRects;

            // 2x2 Grid
            // TL, TR, BL, BR
            for (int i = 0; i < 4; i++)
            {
                var btn = new ImageButton(Rectangle.Empty, sheet, rects[0], rects[1], disabledSourceRect: rects[2], enableHoverSway: false);
                _targetingButtons.Add(btn);
            }
        }

        public void Reset()
        {
            EnsureTargetingButtonsInitialized();
            _actionMenu.Reset();
            _switchMenu.Hide();
            _combatSwitchDialog.Hide(); // Ensure dialog is closed
            UIState = BattleUIState.Default;
            SubMenuState = BattleSubMenuState.None;
            _narrationQueue.Clear();
            foreach (var btn in _targetingButtons) btn.ResetAnimationState();

            MoveForTargeting = null;
            CombatantHoveredViaSprite = null;
            HoveredCombatantFromUI = null; // Ensure this is cleared on reset
            IntroOffset = Vector2.Zero; // Reset offset
        }

        public void ForceClearNarration()
        {
            _narrationQueue.Clear();
            _battleNarrator.ForceClear();
        }

        public void ShowActionMenu(BattleCombatant player, List<BattleCombatant> allCombatants)
        {
            ForceClearNarration();
            SubMenuState = BattleSubMenuState.ActionRoot;
            _actionMenu.Show(player, allCombatants);
            _switchMenu.Hide();
        }

        public void HideAllMenus()
        {
            UIState = BattleUIState.Default;
            SubMenuState = BattleSubMenuState.None;
            _actionMenu.Hide();
            _actionMenu.SetState(ActionMenu.MenuState.Main);
            _switchMenu.Hide();
        }

        public void ShowNarration(string message, Action? onShow = null)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            _narrationQueue.Enqueue(() =>
            {
                onShow?.Invoke();
                _battleNarrator.Show(message, secondaryFont);
            });
        }

        public void GoBack()
        {
            if (SubMenuState == BattleSubMenuState.Switch)
            {
                SubMenuState = BattleSubMenuState.ActionRoot;
                _switchMenu.Hide();

                var battleManager = ServiceLocator.Get<BattleManager>();
                var player = battleManager.CurrentActingCombatant ?? battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && !c.IsDefeated);

                if (player != null)
                {
                    _actionMenu.Show(player, battleManager.AllCombatants.ToList());
                }
            }
            else
            {
                _actionMenu.GoBack();
            }
        }

        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState, BattleCombatant currentActor)
        {
            SharedPulseTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            _battleNarrator.Update(gameTime);
            UpdateHoverHighlights(gameTime, currentActor);
            UpdateControlPrompt(gameTime);

            // If the forced switch dialog is active, it takes priority and blocks other UI
            if (_combatSwitchDialog.IsActive)
            {
                _combatSwitchDialog.Update(gameTime);
                _previousMouseState = currentMouseState;
                return;
            }

            // --- REORDERED UPDATE LOGIC FOR OVERLAP FIX ---

            // 1. Update Targeting Buttons First (if active)
            bool checkTargeting = (UIState == BattleUIState.Targeting);
            bool targetingHovered = false;

            if (checkTargeting)
            {
                UpdateTargetingButtons(currentMouseState, _previousMouseState, currentActor);
                targetingHovered = (HoveredCombatantFromUI != null);
            }
            else
            {
                HoveredCombatantFromUI = null;
            }

            // 2. Update Action Menu (Pass blocking flag)
            bool isPhaseBlocked = currentActor == null;
            _actionMenu.Update(currentMouseState, gameTime, isInputBlocked: targetingHovered || isPhaseBlocked);

            if (SubMenuState == BattleSubMenuState.Switch)
            {
                _switchMenu.Update(currentMouseState);
            }

            if (UIState == BattleUIState.Targeting)
            {
                _targetingTextAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            if (_actionMenu.CurrentMenuState == ActionMenu.MenuState.Targeting)
            {
                UIState = BattleUIState.Targeting;
                MoveForTargeting = _actionMenu.SelectedMove;
            }
            else
            {
                UIState = BattleUIState.Default;
            }

            // Note: UpdateTargetingButtons was already called at step 1.

            if (!_battleNarrator.IsBusy && _narrationQueue.Any())
            {
                var nextStep = _narrationQueue.Dequeue();
                nextStep.Invoke();
            }

            _previousMouseState = currentMouseState;
        }

        private void UpdateTargetingButtons(MouseState currentMouseState, MouseState previousMouseState, BattleCombatant currentActor)
        {
            EnsureTargetingButtonsInitialized();

            HoveredCombatantFromUI = null;
            var battleManager = ServiceLocator.Get<BattleManager>();
            var allCombatants = battleManager.AllCombatants.ToList();

            // Layout Constants
            const int btnWidth = 150;
            const int btnHeight = 22;
            const int gridX = 10; // (320 - 300) / 2
            const int gridY = 126; // Moved up 2 pixels from 128

            // 0: TL (Enemy 0), 1: TR (Enemy 1), 2: BL (Player 0), 3: BR (Player 1)
            for (int i = 0; i < 4; i++)
            {
                var btn = _targetingButtons[i];
                int col = i % 2;
                int row = i / 2;

                btn.Bounds = new Rectangle(gridX + col * btnWidth, gridY + row * btnHeight, btnWidth, btnHeight);

                // Find Combatant
                bool isPlayerSlot = row == 1;
                int slotIndex = col; // 0 or 1

                var combatant = allCombatants.FirstOrDefault(c => c.IsPlayerControlled == isPlayerSlot && c.BattleSlot == slotIndex && c.IsActiveOnField);

                if (combatant != null)
                {
                    btn.Text = combatant.Name.ToUpper();

                    // Check if valid target
                    var validTargets = TargetingHelper.GetValidTargets(currentActor, TargetTypeForSelection ?? TargetType.None, allCombatants);
                    bool isValid = validTargets.Contains(combatant);

                    btn.IsEnabled = isValid;
                    btn.CustomDisabledTextColor = _global.Palette_DarkShadow;

                    // Clear standard OnClick to prevent double firing or release-based firing
                    btn.OnClick = null;

                    // Manual "On Press" Logic
                    if (btn.IsHovered && btn.IsEnabled &&
                        UIInputManager.CanProcessMouseClick() &&
                        currentMouseState.LeftButton == ButtonState.Pressed &&
                        previousMouseState.LeftButton == ButtonState.Released)
                    {
                        OnTargetSelectedFromUI?.Invoke(combatant);
                        UIInputManager.ConsumeMouseClick();
                    }
                }
                else
                {
                    btn.Text = "EMPTY";
                    btn.IsEnabled = false;
                    btn.CustomDisabledTextColor = _global.Palette_DarkShadow;
                    btn.OnClick = null;
                }

                btn.Update(currentMouseState);

                if (btn.IsHovered && btn.IsEnabled && combatant != null)
                {
                    HoveredCombatantFromUI = combatant;
                    // --- CURSOR LOGIC ---
                    ServiceLocator.Get<CursorManager>().SetState(CursorState.HoverClickable);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (SubMenuState == BattleSubMenuState.Switch)
            {
                _switchMenu.Draw(spriteBatch, font, gameTime);
            }
            else
            {
                // Pass IntroOffset to ActionMenu.Draw
                _actionMenu.Draw(spriteBatch, font, gameTime, transform, IntroOffset);
            }

            if (UIState == BattleUIState.Targeting)
            {
                DrawTargetingButtons(spriteBatch, font, gameTime, transform);
                // Draw text AFTER buttons to ensure it's on top
                DrawTargetingText(spriteBatch, font, gameTime);
            }

            // Pass IntroOffset to Narrator Draw
            _battleNarrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime, IntroOffset);

            DrawControlPrompt(spriteBatch);

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                var pixel = ServiceLocator.Get<Texture2D>();

                // Narrator
                spriteBatch.DrawSnapped(pixel, _narratorBounds, Color.Purple * 0.5f);

                // Control Prompt
                if (_isPromptVisible && _promptTextures.Any())
                {
                    spriteBatch.DrawSnapped(pixel, _controlPromptBounds, Color.Purple * 0.5f);
                }
            }
        }

        public void DrawFullscreenDialogs(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_combatSwitchDialog.IsActive)
            {
                // DrawOverlay handles its own Begin/End with Identity transform (Screen Space)
                _combatSwitchDialog.DrawOverlay(spriteBatch);

                // Draw Content needs the Virtual->Screen transform
                spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
                _combatSwitchDialog.DrawContent(spriteBatch, font, gameTime, Matrix.Identity);
                spriteBatch.End();
            }
        }

        private void DrawTargetingButtons(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            EnsureTargetingButtonsInitialized();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            var battleManager = ServiceLocator.Get<BattleManager>();
            var currentActor = battleManager.CurrentActingCombatant;
            var allCombatants = battleManager.AllCombatants.ToList();

            // Determine the currently focused combatant (either via UI hover or Sprite hover)
            var focusCombatant = HoveredCombatantFromUI ?? CombatantHoveredViaSprite;

            // Determine if the current targeting mode is multi-target
            var targetType = TargetTypeForSelection ?? TargetType.None;
            bool isMultiTarget = targetType == TargetType.Every || targetType == TargetType.Both || targetType == TargetType.All || targetType == TargetType.Team || targetType == TargetType.RandomAll || targetType == TargetType.RandomBoth || targetType == TargetType.RandomEvery;

            // Get valid targets for the current actor and move
            var validTargets = TargetingHelper.GetValidTargets(currentActor, targetType, allCombatants);

            for (int i = 0; i < _targetingButtons.Count; i++)
            {
                var btn = _targetingButtons[i];

                // Find the combatant associated with this button
                int col = i % 2;
                int row = i / 2;
                bool isPlayerSlot = row == 1;
                int slotIndex = col;
                var buttonCombatant = allCombatants.FirstOrDefault(c => c.IsPlayerControlled == isPlayerSlot && c.BattleSlot == slotIndex && c.IsActiveOnField);

                bool shouldHighlight = false;

                // Highlight logic
                if (btn.IsEnabled && focusCombatant != null && buttonCombatant != null)
                {
                    if (isMultiTarget)
                    {
                        // If multi-target, highlight if BOTH the focused combatant AND this button's combatant are valid targets.
                        // This means hovering ANY valid target highlights ALL valid targets.
                        if (validTargets.Contains(focusCombatant) && validTargets.Contains(buttonCombatant))
                        {
                            shouldHighlight = true;
                        }
                    }
                    else
                    {
                        // Single target: Highlight only if this button matches the focused combatant
                        if (buttonCombatant == focusCombatant)
                        {
                            shouldHighlight = true;
                        }
                    }
                }

                // Draw the button sprite with forceHover
                btn.Draw(spriteBatch, secondaryFont, gameTime, transform, forceHover: shouldHighlight);

                // Draw the text
                if (!string.IsNullOrEmpty(btn.Text))
                {
                    Vector2 textSize = secondaryFont.MeasureString(btn.Text);
                    // Pixel-perfect centering
                    Vector2 textPos = new Vector2(
                        (int)(btn.Bounds.X + (btn.Bounds.Width - textSize.X) / 2f),
                        (int)(btn.Bounds.Y + (btn.Bounds.Height - textSize.Y) / 2f)
                    );

                    // Determine color based on state
                    Color textColor;

                    if (btn.Text == "EMPTY")
                    {
                        textColor = _global.Palette_DarkShadow;
                    }
                    else if (shouldHighlight)
                    {
                        textColor = Color.Yellow;
                    }
                    else if (buttonCombatant == currentActor)
                    {
                        // Override for current actor
                        textColor = _global.Palette_DarkShadow;
                    }
                    else if (!btn.IsEnabled)
                    {
                        textColor = btn.CustomDisabledTextColor ?? _global.Palette_DarkShadow;
                    }
                    else
                    {
                        textColor = _global.Palette_Red;
                    }

                    if (shouldHighlight)
                    {
                        // Use modulo timer to create a looping wave effect
                        float cycleDuration = 2.0f; // 2 seconds per wave cycle
                        float waveTimer = _targetingTextAnimTimer % cycleDuration;

                        // Use the new Square Outlined Wave Text with SmallWave effect
                        // FIX: Use TextAnimator instead of AnimationUtils/TextUtils
                        TextAnimator.DrawTextWithEffectSquareOutlined(
                            spriteBatch,
                            secondaryFont,
                            btn.Text,
                            textPos,
                            textColor,
                            _global.Palette_Black,
                            TextEffectType.SmallWave,
                            waveTimer
                        );
                    }
                    else
                    {
                        // Use standard Square Outlined Text
                        spriteBatch.DrawStringSquareOutlinedSnapped(secondaryFont, btn.Text, textPos, textColor, _global.Palette_Black);
                    }

                    // Strikethrough for unselectable targets (INCLUDING EMPTY if disabled)
                    if (!btn.IsEnabled)
                    {
                        float lineY = textPos.Y + (secondaryFont.LineHeight / 2f) + 1; // Moved down 1 pixel
                        float startX = textPos.X - 2;
                        float endX = textPos.X + textSize.X + 2;
                        spriteBatch.DrawLineSnapped(new Vector2(startX, lineY), new Vector2(endX, lineY), textColor);
                    }
                }
            }
        }

        private void DrawTargetingText(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // Layout Constants matching DrawItemTargetingOverlay
            const int dividerY = 123;
            const int backButtonHeight = 15;
            const int backButtonTopMargin = 1;
            const int horizontalPadding = 10;
            const int verticalPadding = 2;
            int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
            int availableHeight = Global.VIRTUAL_HEIGHT - dividerY - (verticalPadding * 2);
            int gridAreaHeight = availableHeight - backButtonHeight - backButtonTopMargin;
            int gridStartY = dividerY + verticalPadding + 4;

            string text = "CHOOSE A TARGET";
            Vector2 textSize = font.MeasureString(text);

            Vector2 textPos = new Vector2(
                horizontalPadding + (availableWidth - textSize.X) / 2,
                gridStartY + (gridAreaHeight - textSize.Y) / 2
            );

            // Use DriftWave effect via TextAnimator
            TextAnimator.DrawTextWithEffectSquareOutlined(
                spriteBatch,
                font,
                text,
                textPos,
                _global.Palette_Red,
                _global.Palette_Black,
                TextEffectType.DriftWave,
                _targetingTextAnimTimer
            );
        }

        private void UpdateControlPrompt(GameTime gameTime)
        {
            if (IsBusy)
            {
                _isPromptVisible = false;
                _lastHoveredButton = null;
                return;
            }

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            Button? currentHoveredButton = null;

            if (SubMenuState == BattleSubMenuState.ActionRoot || SubMenuState == BattleSubMenuState.ActionMoves)
            {
                currentHoveredButton = _actionMenu.HoveredButton;
            }

            // Check targeting buttons
            if (UIState == BattleUIState.Targeting)
            {
                foreach (var btn in _targetingButtons)
                {
                    if (btn.IsHovered)
                    {
                        currentHoveredButton = btn;
                        break;
                    }
                }
            }

            _isPromptVisible = true;

            if (currentHoveredButton == null)
            {
                if (_lastHoveredButton != null)
                {
                    _promptTextures.Clear();
                    _promptTextures.Add((spriteManager.MousePromptBlank, spriteManager.MousePromptBlankSilhouette));
                    _promptTextureIndex = 0;
                    _lastHoveredButton = null;
                }
                else if (!_promptTextures.Any())
                {
                    _promptTextures.Add((spriteManager.MousePromptBlank, spriteManager.MousePromptBlankSilhouette));
                }
                return;
            }

            if (currentHoveredButton != _lastHoveredButton)
            {
                _lastHoveredButton = currentHoveredButton;
                _promptTextures.Clear();
                _promptCycleTimer = 0f;
                _promptTextureIndex = 0;

                if (currentHoveredButton.HasLeftClickAction)
                {
                    _promptTextures.Add((spriteManager.MousePromptLeftClick, spriteManager.MousePromptLeftClickSilhouette));
                }
                if (currentHoveredButton.HasRightClickAction)
                {
                    _promptTextures.Add((spriteManager.MousePromptRightClick, spriteManager.MousePromptRightClickSilhouette));
                }
                // Added Middle Click Prompt
                if (currentHoveredButton.HasMiddleClickAction)
                {
                    _promptTextures.Add((spriteManager.MousePromptMiddleClick, spriteManager.MousePromptMiddleClickSilhouette));
                }
            }

            if (_promptTextures.Count > 1)
            {
                _promptCycleTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_promptCycleTimer >= PROMPT_CYCLE_INTERVAL)
                {
                    _promptCycleTimer -= PROMPT_CYCLE_INTERVAL;
                    _promptTextureIndex = (_promptTextureIndex + 1) % _promptTextures.Count;
                }
            }
            else
            {
                _promptTextureIndex = 0;
            }
        }

        private void DrawControlPrompt(SpriteBatch spriteBatch)
        {
            if (!_isPromptVisible || !_promptTextures.Any())
            {
                return;
            }

            var (textureToDraw, silhouetteToDraw) = _promptTextures[_promptTextureIndex];
            const int padding = 2;
            var position = new Vector2(
                Global.VIRTUAL_WIDTH - textureToDraw.Width - padding,
                Global.VIRTUAL_HEIGHT - textureToDraw.Height - padding
            );

            // Update debug bounds
            _controlPromptBounds = new Rectangle((int)position.X, (int)position.Y, textureToDraw.Width, textureToDraw.Height);

            var global = ServiceLocator.Get<Global>();
            var outlineColor = global.Palette_Black;

            // Draw Outline using Silhouette if available, otherwise fallback to texture
            var outlineTexture = silhouetteToDraw ?? textureToDraw;

            spriteBatch.DrawSnapped(outlineTexture, position + new Vector2(-1, 0), outlineColor);
            spriteBatch.DrawSnapped(outlineTexture, position + new Vector2(1, 0), outlineColor);
            spriteBatch.DrawSnapped(outlineTexture, position + new Vector2(0, -1), outlineColor);
            spriteBatch.DrawSnapped(outlineTexture, position + new Vector2(0, 1), outlineColor);

            spriteBatch.DrawSnapped(textureToDraw, position, Color.White);
        }

        private void HandlePlayerMoveSelected(MoveData move, MoveEntry entry, BattleCombatant target)
        {
            OnMoveSelected?.Invoke(move, entry, target);
        }

        private void OnSwitchMenuRequested()
        {
            SubMenuState = BattleSubMenuState.Switch;
            _actionMenu.Hide();
            var battleManager = ServiceLocator.Get<BattleManager>();
            var reserved = battleManager.GetReservedBenchMembers();
            _switchMenu.Show(battleManager.AllCombatants.ToList(), reserved);
        }

        private void UpdateHoverHighlights(GameTime gameTime, BattleCombatant currentActor)
        {
            HoverHighlightState.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            var hoveredMove = _actionMenu.HoveredMove;
            var allCombatants = ServiceLocator.Get<BattleManager>().AllCombatants;

            if (hoveredMove != HoverHighlightState.CurrentMove)
            {
                HoverHighlightState.CurrentMove = hoveredMove;
                HoverHighlightState.Timer = 0f; // Reset timer on move change
                HoverHighlightState.Targets.Clear();

                if (hoveredMove != null && currentActor != null)
                {
                    var enemies = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
                    var allActive = allCombatants.Where(c => !c.IsDefeated && c.IsActiveOnField).ToList();
                    var allies = allCombatants.Where(c => c.IsPlayerControlled && !c.IsDefeated && c.IsActiveOnField).ToList();
                    var ally = allies.FirstOrDefault(c => c != currentActor);

                    switch (hoveredMove.Target)
                    {
                        case TargetType.Single:
                            HoverHighlightState.Targets.AddRange(enemies);
                            if (ally != null) HoverHighlightState.Targets.Add(ally);
                            break;
                        case TargetType.Both:
                            HoverHighlightState.Targets.AddRange(enemies);
                            break;
                        case TargetType.Every:
                            HoverHighlightState.Targets.AddRange(enemies);
                            if (ally != null) HoverHighlightState.Targets.Add(ally);
                            break;
                        case TargetType.All:
                            HoverHighlightState.Targets.AddRange(allActive);
                            break;
                        case TargetType.Self:
                            HoverHighlightState.Targets.Add(currentActor);
                            break;
                        case TargetType.Team:
                            HoverHighlightState.Targets.Add(currentActor);
                            if (ally != null) HoverHighlightState.Targets.Add(ally);
                            break;
                        case TargetType.Ally:
                            if (ally != null) HoverHighlightState.Targets.Add(ally);
                            break;
                        case TargetType.SingleTeam:
                            HoverHighlightState.Targets.AddRange(allies);
                            break;
                        case TargetType.SingleAll:
                            HoverHighlightState.Targets.AddRange(allActive);
                            break;
                        case TargetType.RandomBoth:
                            HoverHighlightState.Targets.AddRange(enemies);
                            break;
                        case TargetType.RandomEvery:
                            HoverHighlightState.Targets.AddRange(enemies);
                            if (ally != null) HoverHighlightState.Targets.Add(ally);
                            break;
                        case TargetType.RandomAll:
                            HoverHighlightState.Targets.AddRange(allActive);
                            break;
                    }

                    // --- SORTING LOGIC FOR SINGLE TARGET CYCLING ---
                    // Priority: Enemy 1 -> Enemy 2 -> Ally -> Self
                    if (hoveredMove.Target == TargetType.Single || hoveredMove.Target == TargetType.SingleTeam || hoveredMove.Target == TargetType.SingleAll)
                    {
                        HoverHighlightState.Targets.Sort((a, b) =>
                        {
                            // 1. Enemies first
                            bool aIsEnemy = !a.IsPlayerControlled;
                            bool bIsEnemy = !b.IsPlayerControlled;
                            if (aIsEnemy && !bIsEnemy) return -1;
                            if (!aIsEnemy && bIsEnemy) return 1;

                            // 2. If both enemies, sort by slot (0 then 1)
                            if (aIsEnemy) return a.BattleSlot.CompareTo(b.BattleSlot);

                            // 3. If both players, Ally before Self
                            if (a == currentActor) return 1; // Self goes last
                            if (b == currentActor) return -1;

                            // Fallback to slot for allies
                            return a.BattleSlot.CompareTo(b.BattleSlot);
                        });
                    }
                }
            }
        }
    }
}