using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public enum BattleUIState { Default, Targeting, Switch }

    public class HoverHighlightState
    {
        public MoveData? CurrentMove;
        public List<BattleCombatant> Targets = new List<BattleCombatant>();
        public float Timer = 0f;
    }

    public class BattleUIManager
    {
        public event Action<BattleCombatant>? OnForcedSwitchSelected;
        public event Action? OnFleeRequested;

        private readonly ActionMenu _actionMenu;
        private readonly SwitchMenu _switchMenu;
        private readonly CombatSwitchDialog _combatSwitchDialog;
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;

        public BattleUIState UIState { get; private set; } = BattleUIState.Default;

        public int ActiveTargetingSlot { get; private set; } = -1;
        public MoveData? MoveForTargeting { get; private set; }
        public MoveEntry? SpellForTargeting { get; private set; }
        public TargetType? TargetTypeForSelection => UIState == BattleUIState.Targeting ? MoveForTargeting?.Target : null;

        public MoveData? HoveredMove => _actionMenu.HoveredMove;
        public BattleCombatant? HoveredCombatantFromUI { get; private set; }
        public BattleCombatant? CombatantHoveredViaSprite { get; set; }
        public readonly HoverHighlightState HoverHighlightState = new HoverHighlightState();

        public float SharedPulseTimer { get; private set; } = 0f;
        private float _targetingTextAnimTimer = 0f;
        public Vector2 IntroOffset { get; set; } = Vector2.Zero;

        private readonly List<Button> _targetingButtons = new List<Button>();
        private MouseState _previousMouseState;

        public bool IsBusy => false;
        public bool IsWaitingForInput => false;

        public BattleUIManager()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _actionMenu = new ActionMenu();
            _switchMenu = new SwitchMenu();
            _combatSwitchDialog = new CombatSwitchDialog(null);

            _actionMenu.OnActionSelected += HandleActionMenuSelection;
            _actionMenu.OnSwitchMenuRequested += HandleSwitchMenuRequest;
            _actionMenu.OnCancelRequested += HandleCancelRequest;
            _actionMenu.OnFleeRequested += () => OnFleeRequested?.Invoke();

            _switchMenu.OnMemberSelected += HandleSwitchMemberSelected;
            _switchMenu.OnBack += () => { _switchMenu.Hide(); };

            _combatSwitchDialog.OnMemberSelected += (member) => OnForcedSwitchSelected?.Invoke(member);

            EventBus.Subscribe<GameEvents.ForcedSwitchRequested>(OnForcedSwitchRequested);

            _previousMouseState = Mouse.GetState();
            EnsureTargetingButtonsInitialized();
        }

        public void ForceClearNarration() { }

        private void EnsureTargetingButtonsInitialized()
        {
            if (_targetingButtons.Count > 0) return;
            for (int i = 0; i < 4; i++)
            {
                var btn = new Button(Rectangle.Empty, "", enableHoverSway: false);
                _targetingButtons.Add(btn);
            }
        }

        public void Reset()
        {
            _actionMenu.Reset();
            _switchMenu.Hide();
            _combatSwitchDialog.Hide();
            UIState = BattleUIState.Default;
            ActiveTargetingSlot = -1;
            MoveForTargeting = null;
            SpellForTargeting = null;
            HoveredCombatantFromUI = null;
            CombatantHoveredViaSprite = null;
            IntroOffset = Vector2.Zero;

            foreach (var btn in _targetingButtons) btn.ResetAnimationState();
        }

        public void ShowActionMenu(BattleCombatant player, List<BattleCombatant> allCombatants)
        {
            _actionMenu.Show(player, allCombatants);
            _switchMenu.Hide();
        }

        public void HideAllMenus()
        {
            UIState = BattleUIState.Default;
            _actionMenu.Hide();
            _switchMenu.Hide();
        }

        public void GoBack()
        {
            if (UIState == BattleUIState.Targeting)
            {
                UIState = BattleUIState.Default;
                MoveForTargeting = null;
                SpellForTargeting = null;
                ActiveTargetingSlot = -1;
            }
            else if (_switchMenu.IsForced == false)
            {
                _switchMenu.Hide();
            }
        }

        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState, BattleCombatant currentActor)
        {
            SharedPulseTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_combatSwitchDialog.IsActive)
            {
                _combatSwitchDialog.Update(gameTime);
                _previousMouseState = currentMouseState;
                return;
            }

            bool isTargeting = UIState == BattleUIState.Targeting;
            bool targetingHovered = false;

            if (isTargeting)
            {
                _targetingTextAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                var battleManager = ServiceLocator.Get<BattleManager>();
                var activeActor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == ActiveTargetingSlot);
                UpdateTargetingButtons(currentMouseState, _previousMouseState, activeActor ?? currentActor);
                targetingHovered = (HoveredCombatantFromUI != null);
            }
            else
            {
                HoveredCombatantFromUI = null;
            }

            bool isMenuBlocked = isTargeting || targetingHovered;
            _actionMenu.Update(currentMouseState, gameTime, isMenuBlocked);
            _switchMenu.Update(currentMouseState);
            UpdateHoverHighlights(gameTime, currentActor);

            _previousMouseState = currentMouseState;
        }

        private void HandleActionMenuSelection(int slotIndex, QueuedAction action)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();

            if (action.ChosenMove != null && action.ChosenMove.Target != TargetType.None && action.ChosenMove.Target != TargetType.Self && action.ChosenMove.Target != TargetType.All && action.ChosenMove.Target != TargetType.RandomAll)
            {
                var validTargets = TargetingHelper.GetValidTargets(action.Actor, action.ChosenMove.Target, battleManager.AllCombatants);
                bool autoSelect = action.ChosenMove.Target == TargetType.Self ||
                                  action.ChosenMove.Target == TargetType.All ||
                                  action.ChosenMove.Target == TargetType.RandomAll ||
                                  action.ChosenMove.Target == TargetType.Team ||
                                  action.ChosenMove.Target == TargetType.RandomBoth ||
                                  action.ChosenMove.Target == TargetType.RandomEvery;

                if (autoSelect)
                {
                    action.Target = validTargets.FirstOrDefault();
                    battleManager.SubmitAction(slotIndex, action);
                }
                else
                {
                    MoveForTargeting = action.ChosenMove;
                    SpellForTargeting = action.SpellbookEntry;
                    ActiveTargetingSlot = slotIndex;
                    UIState = BattleUIState.Targeting;
                }
            }
            else
            {
                battleManager.SubmitAction(slotIndex, action);
            }
        }

        private void HandleCancelRequest(int slotIndex)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            battleManager.CancelAction(slotIndex);
        }

        public void HandleSpriteClick(BattleCombatant target)
        {
            if (UIState == BattleUIState.Targeting)
            {
                SubmitTarget(target);
            }
        }

        private void SubmitTarget(BattleCombatant target)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            var actor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == ActiveTargetingSlot);

            if (actor != null && MoveForTargeting != null)
            {
                var action = new QueuedAction
                {
                    Actor = actor,
                    ChosenMove = MoveForTargeting,
                    SpellbookEntry = SpellForTargeting,
                    Target = target,
                    Type = QueuedActionType.Move,
                    Priority = MoveForTargeting.Priority,
                    ActorAgility = actor.GetEffectiveAgility()
                };

                battleManager.SubmitAction(ActiveTargetingSlot, action);

                UIState = BattleUIState.Default;
                MoveForTargeting = null;
                SpellForTargeting = null;
                ActiveTargetingSlot = -1;
            }
        }

        private void HandleSwitchMenuRequest(int slotIndex)
        {
            ActiveTargetingSlot = slotIndex;
            var battleManager = ServiceLocator.Get<BattleManager>();
            var reserved = battleManager.GetReservedBenchMembers();
            _switchMenu.Show(battleManager.AllCombatants.ToList(), reserved);
        }

        private void HandleSwitchMemberSelected(BattleCombatant targetMember)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            var actor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == ActiveTargetingSlot);

            if (actor != null)
            {
                var action = new QueuedAction
                {
                    Actor = actor,
                    Target = targetMember,
                    Type = QueuedActionType.Switch,
                    Priority = 6,
                    ActorAgility = actor.Stats.Agility
                };
                battleManager.SubmitAction(ActiveTargetingSlot, action);
            }

            _switchMenu.Hide();
            ActiveTargetingSlot = -1;
        }

        private void OnForcedSwitchRequested(GameEvents.ForcedSwitchRequested e)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            _combatSwitchDialog.IsMandatory = (e.Actor == null);
            _combatSwitchDialog.Show(battleManager.AllCombatants.ToList());
        }

        private void UpdateTargetingButtons(MouseState currentMouseState, MouseState previousMouseState, BattleCombatant currentActor)
        {
            EnsureTargetingButtonsInitialized();
            HoveredCombatantFromUI = null;
            var battleManager = ServiceLocator.Get<BattleManager>();
            var allCombatants = battleManager.AllCombatants.ToList();

            const int btnWidth = 150;
            const int btnHeight = 13;
            const int gridX = 10;
            const int gridY = 144;

            for (int i = 0; i < 4; i++)
            {
                var btn = _targetingButtons[i];
                int col = i % 2;
                int row = i / 2;
                btn.Bounds = new Rectangle(gridX + col * btnWidth, gridY + row * btnHeight, btnWidth, btnHeight);

                bool isPlayerSlot = row == 1;
                int slotIndex = col;
                var combatant = allCombatants.FirstOrDefault(c => c.IsPlayerControlled == isPlayerSlot && c.BattleSlot == slotIndex && c.IsActiveOnField);

                if (combatant != null)
                {
                    btn.Text = combatant.Name.ToUpper();
                    var validTargets = TargetingHelper.GetValidTargets(currentActor, TargetTypeForSelection ?? TargetType.None, allCombatants);
                    btn.IsEnabled = validTargets.Contains(combatant);
                    btn.CustomDisabledTextColor = _global.Palette_DarkShadow;
                    btn.OnClick = null;
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
                    ServiceLocator.Get<CursorManager>().SetState(CursorState.HoverClickable);

                    if (UIInputManager.CanProcessMouseClick() && currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
                    {
                        SubmitTarget(combatant);
                        UIInputManager.ConsumeMouseClick();
                    }
                }
            }
        }

        private void UpdateHoverHighlights(GameTime gameTime, BattleCombatant currentActor)
        {
            HoverHighlightState.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            MoveData? move = null;
            BattleCombatant? actor = null;

            if (UIState == BattleUIState.Targeting)
            {
                move = MoveForTargeting;
                var battleManager = ServiceLocator.Get<BattleManager>();
                actor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == ActiveTargetingSlot);
            }
            else
            {
                move = _actionMenu.HoveredMove;
                int slot = _actionMenu.HoveredSlotIndex;
                if (slot != -1)
                {
                    var battleManager = ServiceLocator.Get<BattleManager>();
                    actor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == slot);
                }
            }

            if (move != HoverHighlightState.CurrentMove)
            {
                HoverHighlightState.CurrentMove = move;
                HoverHighlightState.Timer = 0f;
                HoverHighlightState.Targets.Clear();

                if (move != null && actor != null)
                {
                    var battleManager = ServiceLocator.Get<BattleManager>();
                    var validTargets = TargetingHelper.GetValidTargets(actor, move.Target, battleManager.AllCombatants);
                    HoverHighlightState.Targets.AddRange(validTargets);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // Draw the Action Border
            if (_spriteManager.BattleBorderAction != null)
            {
                spriteBatch.DrawSnapped(_spriteManager.BattleBorderAction, IntroOffset, Color.White);
            }

            // Pass the active targeting slot to hide that specific panel's buttons
            int? hiddenSlot = (UIState == BattleUIState.Targeting) ? ActiveTargetingSlot : null;
            _actionMenu.Draw(spriteBatch, font, gameTime, transform, IntroOffset, hiddenSlot);

            if (UIState == BattleUIState.Targeting)
            {
                DrawTargetingButtons(spriteBatch, font, gameTime);
                DrawTargetingText(spriteBatch, font, gameTime);
            }

            if (_switchMenu.IsForced || _switchMenu.IsVisible)
            {
                _switchMenu.Draw(spriteBatch, font, gameTime);
            }
        }

        public void DrawFullscreenDialogs(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_combatSwitchDialog.IsActive)
            {
                _combatSwitchDialog.DrawOverlay(spriteBatch);
                spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);
                _combatSwitchDialog.DrawContent(spriteBatch, font, gameTime, Matrix.Identity);
                spriteBatch.End();
            }
        }

        private void DrawTargetingButtons(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            foreach (var btn in _targetingButtons)
            {
                if (btn.IsEnabled)
                {
                    bool isHovered = btn.IsHovered || (HoveredCombatantFromUI != null && btn.Text == HoveredCombatantFromUI.Name.ToUpper());
                    Color color = isHovered ? _global.ButtonHoverColor : _global.GameTextColor;
                    spriteBatch.DrawStringSnapped(secondaryFont, btn.Text, btn.Bounds.Location.ToVector2() + new Vector2(10, 2), color);
                }
            }
        }

        private void DrawTargetingText(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            string text = "CHOOSE A TARGET";
            Vector2 size = secondaryFont.MeasureString(text);

            // Calculate position based on the active targeting slot
            // Constants from ActionMenu: PANEL_WIDTH = 80, PANEL_Y = 139
            // Left Slot (0): X = 10
            // Right Slot (1): X = ScreenWidth - 80 - 10

            float panelX;
            if (ActiveTargetingSlot == 0)
            {
                panelX = 10;
            }
            else
            {
                panelX = Global.VIRTUAL_WIDTH - 80 - 10;
            }

            // Center text in the panel area
            // Panel Width is 80.
            float centerX = panelX + (80 / 2f);
            float centerY = 139 + (40 / 2f); // Approx center of button area

            Vector2 textPos = new Vector2(
                centerX - (size.X / 2f),
                centerY - (size.Y / 2f)
            );

            // Use DriftWave effect
            TextAnimator.DrawTextWithEffect(
                spriteBatch,
                secondaryFont,
                text,
                textPos,
                _global.Palette_Rust,
                TextEffectType.DriftWave,
                _targetingTextAnimTimer
            );
        }
    }
}