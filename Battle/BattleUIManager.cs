using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
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
        public BattleCombatant? StatInfoTarget { get; private set; }

        public int ActiveTargetingSlot { get; private set; } = -1;
        public MoveData? MoveForTargeting { get; private set; }
        public MoveEntry? SpellForTargeting { get; private set; }
        public TargetType? TargetTypeForSelection => UIState == BattleUIState.Targeting ? MoveForTargeting?.Target : null;

        public MoveData? HoveredMove => _actionMenu.HoveredMove;

        // Always null now, as UI buttons for targeting are removed. 
        // Kept for compatibility with BattleRenderer/InputHandler which check this property.
        public BattleCombatant? HoveredCombatantFromUI { get; private set; } = null;

        public BattleCombatant? CombatantHoveredViaSprite { get; set; }
        public readonly HoverHighlightState HoverHighlightState = new HoverHighlightState();

        public float SharedPulseTimer { get; private set; } = 0f;
        private float _targetingTextAnimTimer = 0f;
        public Vector2 IntroOffset { get; set; } = Vector2.Zero;

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
            _switchMenu.OnBack += HandleSwitchMenuBack;

            _combatSwitchDialog.OnMemberSelected += (member) => OnForcedSwitchSelected?.Invoke(member);

            EventBus.Subscribe<GameEvents.ForcedSwitchRequested>(OnForcedSwitchRequested);

            _previousMouseState = Mouse.GetState();
        }

        public void ForceClearNarration() { }

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
            StatInfoTarget = null;
        }

        public void ShowActionMenu(BattleCombatant player, List<BattleCombatant> allCombatants)
        {
            _actionMenu.Show(player, allCombatants);
            _switchMenu.Hide();
            UIState = BattleUIState.Default;
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
            else if (UIState == BattleUIState.Switch && !_switchMenu.IsForced)
            {
                HandleSwitchMenuBack();
            }
            else if (_switchMenu.IsForced == false && _switchMenu.IsVisible)
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
            bool isSwitching = UIState == BattleUIState.Switch;

            if (isTargeting)
            {
                _targetingTextAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            // Block menu input if we are targeting.
            // If switching, we don't block globally, but we pass the switching slot index to block that specific panel.
            bool isMenuBlocked = isTargeting;
            int? switchingSlot = isSwitching ? ActiveTargetingSlot : null;

            _actionMenu.Update(currentMouseState, gameTime, isMenuBlocked, switchingSlot);

            // Only update switch menu if we are in Switch state
            if (isSwitching)
            {
                _switchMenu.Update(currentMouseState);
            }

            UpdateHoverHighlights(gameTime, currentActor);

            if (StatInfoTarget != null)
            {
                if (CombatantHoveredViaSprite != StatInfoTarget)
                {
                    StatInfoTarget = null;
                }
            }

            _previousMouseState = currentMouseState;
        }

        private void HandleActionMenuSelection(int slotIndex, QueuedAction action)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();

            if (action.ChosenMove != null && action.ChosenMove.Target != TargetType.None)
            {
                MoveForTargeting = action.ChosenMove;
                SpellForTargeting = action.SpellbookEntry;
                ActiveTargetingSlot = slotIndex;
                UIState = BattleUIState.Targeting;

                // Ensure switch menu is closed if we start targeting
                _switchMenu.Hide();
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
                // Validate that the clicked target is actually valid for the current move
                var battleManager = ServiceLocator.Get<BattleManager>();
                var actor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == ActiveTargetingSlot);

                if (actor != null && MoveForTargeting != null)
                {
                    var validTargets = TargetingHelper.GetValidTargets(actor, MoveForTargeting.Target, battleManager.AllCombatants);

                    if (validTargets.Contains(target))
                    {
                        SubmitTarget(target);
                    }
                    else
                    {
                        // Invalid target clicked (e.g. clicking self for an attack)
                        ServiceLocator.Get<HapticsManager>().TriggerShake(2f, 0.1f);
                    }
                }
            }
            else if (UIState == BattleUIState.Default)
            {
                if (StatInfoTarget == target)
                {
                    StatInfoTarget = null;
                }
                else
                {
                    StatInfoTarget = target;
                }
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
            _switchMenu.Show(slotIndex, battleManager.AllCombatants.ToList(), reserved);
            UIState = BattleUIState.Switch;
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
            UIState = BattleUIState.Default;
            ActiveTargetingSlot = -1;
        }

        private void HandleSwitchMenuBack()
        {
            _switchMenu.Hide();
            UIState = BattleUIState.Default;
            ActiveTargetingSlot = -1;
        }

        private void OnForcedSwitchRequested(GameEvents.ForcedSwitchRequested e)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            _combatSwitchDialog.IsMandatory = (e.Actor == null);
            _combatSwitchDialog.Show(battleManager.AllCombatants.ToList());
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
            var battleManager = ServiceLocator.Get<BattleManager>();

            // Determine which border to draw based on phase
            Texture2D border = (battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection)
                ? _spriteManager.BattleBorderAction
                : _spriteManager.BattleBorderCombat;

            if (border != null)
            {
                spriteBatch.DrawSnapped(border, IntroOffset, Color.White);
            }

            // Draw ActionMenu unless we are Targeting (where we want a clean screen for selection)
            if (UIState != BattleUIState.Targeting)
            {
                // If Switching, hide the active slot (it's covered by SwitchMenu)
                // If Default, show all
                int? hiddenSlot = (UIState == BattleUIState.Switch) ? ActiveTargetingSlot : null;
                _actionMenu.Draw(spriteBatch, font, gameTime, transform, IntroOffset, hiddenSlot);
            }

            if (UIState == BattleUIState.Targeting)
            {
                DrawTargetingText(spriteBatch, font, gameTime);
            }

            if (UIState == BattleUIState.Switch && (_switchMenu.IsForced || _switchMenu.IsVisible))
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

        private void DrawTargetingText(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            string text = "CHOOSE A TARGET";
            Vector2 size = secondaryFont.MeasureString(text);

            // Use the new layout system to get the exact area for the active slot
            var area = BattleLayout.GetActionMenuArea(ActiveTargetingSlot);

            Vector2 textPos = new Vector2(
                area.Center.X - (size.X / 2f),
                area.Center.Y - (size.Y / 2f)
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