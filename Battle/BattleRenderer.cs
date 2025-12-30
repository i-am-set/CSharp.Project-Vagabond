using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// The main orchestrator for battle visuals. It manages state and delegates drawing to helper renderers.
    /// </summary>
    public class BattleRenderer
    {
        // --- Dependencies ---
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly BattleEntityRenderer _entityRenderer;
        private readonly BattleHudRenderer _hudRenderer;
        private readonly BattleVfxRenderer _vfxRenderer;
        private readonly TooltipManager _tooltipManager;
        private readonly HitstopManager _hitstopManager;
        // --- State Management ---
        private readonly List<TargetInfo> _currentTargets = new List<TargetInfo>();
        private readonly List<StatusIconInfo> _playerStatusIcons = new List<StatusIconInfo>();
        private readonly Dictionary<string, List<StatusIconInfo>> _enemyStatusIcons = new Dictionary<string, List<StatusIconInfo>>();

        // Animation States
        private readonly Dictionary<string, PlayerCombatSprite> _playerSprites = new Dictionary<string, PlayerCombatSprite>();
        private readonly Dictionary<string, SpriteHopAnimationController> _attackAnimControllers = new Dictionary<string, SpriteHopAnimationController>();

        // Active Turn Offset State (Tweening players forward/backward)
        private readonly Dictionary<string, float> _turnActiveOffsets = new Dictionary<string, float>();
        private const float INACTIVE_Y_OFFSET = 8f;
        private const float ACTIVE_TWEEN_SPEED = 5f;

        // Enemy Positioning State (Dynamic Centering)
        private readonly Dictionary<string, float> _enemyVisualXPositions = new Dictionary<string, float>();
        private const float ENEMY_POSITION_TWEEN_SPEED = 4.0f;

        // Centering Transition State
        private bool _centeringSequenceStarted = false;
        private float _centeringDelayTimer = 0f;
        private const float CENTERING_DELAY_DURATION = 0.5f;

        // Enemy Animation Data
        private Dictionary<string, Vector2[]> _enemySpritePartOffsets = new Dictionary<string, Vector2[]>();
        private Dictionary<string, float[]> _enemyAnimationTimers = new Dictionary<string, float[]>();
        private Dictionary<string, float[]> _enemyAnimationIntervals = new Dictionary<string, float[]>();

        // TUNING: Slowed down enemy part animation
        private const float ENEMY_ANIM_MIN_INTERVAL = 0.8f;
        private const float ENEMY_ANIM_MAX_INTERVAL = 1.2f;

        // Shadow Animation Data
        private Dictionary<string, Vector2> _shadowOffsets = new Dictionary<string, Vector2>();
        private Dictionary<string, float> _shadowTimers = new Dictionary<string, float>();
        private Dictionary<string, float> _shadowIntervals = new Dictionary<string, float>();

        // TUNING: Slowed down shadow animation
        private const float SHADOW_ANIM_MIN_INTERVAL = 1.5f;
        private const float SHADOW_ANIM_MAX_INTERVAL = 2.5f;

        // Recoil Data
        private class RecoilState { public Vector2 Offset; public Vector2 Velocity; public const float STIFFNESS = 600f; public const float DAMPING = 15f; }
        private readonly Dictionary<string, RecoilState> _recoilStates = new Dictionary<string, RecoilState>();

        // Status Icon Hop Data
        private class StatusIconAnim { public string CombatantID; public StatusEffectType Type; public float Timer; public const float DURATION = 0.3f; public const float HEIGHT = 5f; }
        private readonly List<StatusIconAnim> _activeStatusIconAnims = new List<StatusIconAnim>();

        // Tooltip State
        private float _statTooltipAlpha = 0f;
        private string _statTooltipCombatantID = null;
        private const float STAT_TOOLTIP_FADE_SPEED = 5.0f;

        private readonly Random _random = new Random();

        // Public Accessors
        public Vector2 PlayerSpritePosition { get; private set; }
        private Dictionary<string, Vector2> _combatantVisualCenters = new Dictionary<string, Vector2>();

        public BattleRenderer()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _hitstopManager = ServiceLocator.Get<HitstopManager>();

            // Initialize Helpers
            _entityRenderer = new BattleEntityRenderer();
            _hudRenderer = new BattleHudRenderer();
            _vfxRenderer = new BattleVfxRenderer();
        }

        public void Reset()
        {
            _currentTargets.Clear();
            _playerStatusIcons.Clear();
            _enemyStatusIcons.Clear();
            _playerSprites.Clear();
            _attackAnimControllers.Clear();
            _turnActiveOffsets.Clear();
            _enemyVisualXPositions.Clear();
            _enemySpritePartOffsets.Clear();
            _enemyAnimationTimers.Clear();
            _enemyAnimationIntervals.Clear();
            _shadowOffsets.Clear();
            _shadowTimers.Clear();
            _shadowIntervals.Clear();
            _recoilStates.Clear();
            _activeStatusIconAnims.Clear();
            _combatantVisualCenters.Clear();
            _statTooltipAlpha = 0f;
            _statTooltipCombatantID = null;
            _centeringSequenceStarted = false;
            _centeringDelayTimer = 0f;
        }

        public List<TargetInfo> GetCurrentTargets() => _currentTargets;

        public Vector2 GetCombatantVisualCenterPosition(BattleCombatant combatant, IEnumerable<BattleCombatant> allCombatants)
        {
            if (_combatantVisualCenters.TryGetValue(combatant.CombatantID, out var pos)) return pos;
            return Vector2.Zero;
        }

        public Vector2 GetCombatantHudCenterPosition(BattleCombatant combatant, IEnumerable<BattleCombatant> allCombatants)
        {
            return GetCombatantVisualCenterPosition(combatant, allCombatants);
        }

        public void TriggerAttackAnimation(string combatantId)
        {
            if (!_attackAnimControllers.ContainsKey(combatantId))
                _attackAnimControllers[combatantId] = new SpriteHopAnimationController();
            _attackAnimControllers[combatantId].Trigger();
        }

        public void TriggerRecoil(string combatantId, Vector2 direction, float magnitude)
        {
            if (!_recoilStates.ContainsKey(combatantId)) _recoilStates[combatantId] = new RecoilState();
            _recoilStates[combatantId].Velocity = direction * magnitude * 10f;
        }

        public void TriggerStatusIconHop(string combatantId, StatusEffectType type)
        {
            _activeStatusIconAnims.RemoveAll(a => a.CombatantID == combatantId && a.Type == type);
            _activeStatusIconAnims.Add(new StatusIconAnim { CombatantID = combatantId, Type = type, Timer = 0f });
        }

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants, BattleAnimationManager animationManager, BattleCombatant currentActor)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update Visibility Timers
            foreach (var c in combatants)
            {
                if (c.HealthBarVisibleTimer > 0) c.HealthBarVisibleTimer = Math.Max(0, c.HealthBarVisibleTimer - dt);
                if (c.ManaBarVisibleTimer > 0) c.ManaBarVisibleTimer = Math.Max(0, c.ManaBarVisibleTimer - dt);
            }

            // Update Animations
            UpdateEnemyPositions(dt, combatants, animationManager);
            UpdateEnemyAnimations(dt, combatants);
            UpdateShadowAnimations(dt, combatants);
            UpdateRecoilAnimations(dt);
            UpdateStatusIconAnimations(dt);
            UpdateStatusIconTooltips(combatants);
            UpdateActiveTurnOffsets(dt, combatants, currentActor);

            foreach (var controller in _attackAnimControllers.Values) controller.Update(gameTime);

            foreach (var c in combatants)
            {
                if (c.IsPlayerControlled && _playerSprites.TryGetValue(c.CombatantID, out var sprite))
                {
                    sprite.Update(gameTime, currentActor == c);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, IEnumerable<BattleCombatant> allCombatants, BattleCombatant currentActor, BattleUIManager uiManager, BattleInputHandler inputHandler, BattleAnimationManager animationManager, float sharedBobbingTimer, Matrix transform)
        {
            // Capture Hovered Combatant BEFORE Clearing Targets
            BattleCombatant hoveredCombatant = null;
            if (inputHandler.HoveredTargetIndex >= 0 && inputHandler.HoveredTargetIndex < _currentTargets.Count)
            {
                hoveredCombatant = _currentTargets[inputHandler.HoveredTargetIndex].Combatant;
            }

            _currentTargets.Clear();
            _playerStatusIcons.Clear();

            var enemies = allCombatants.Where(c => !c.IsPlayerControlled && c.IsActiveOnField).ToList();
            var players = allCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();

            // --- Stat Tooltip Logic ---
            UpdateStatTooltipState(hoveredCombatant, uiManager);

            // --- Targeting Logic ---
            var (selectableTargets, activeTargetType) = ResolveSelectableTargets(allCombatants, currentActor, uiManager);
            var silhouetteColors = ResolveSilhouetteColors(allCombatants, currentActor, selectableTargets, activeTargetType, uiManager, hoveredCombatant);
            bool shouldGrayOut = uiManager.UIState == BattleUIState.Targeting || uiManager.UIState == BattleUIState.ItemTargeting || (uiManager.HoveredMove != null && uiManager.HoveredMove.Target != TargetType.None);

            // --- Draw Enemies ---
            DrawEnemies(spriteBatch, enemies, allCombatants, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, transform, gameTime, uiManager, hoveredCombatant);

            // --- Draw Coins ---
            animationManager.DrawCoins(spriteBatch);

            // --- Draw Players ---
            DrawPlayers(spriteBatch, font, players, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, gameTime, uiManager, hoveredCombatant);

            // --- Draw UI Title ---
            DrawUITitle(spriteBatch, gameTime, uiManager.SubMenuState);

            // --- Draw Stat Tooltip ---
            if (_statTooltipAlpha > 0.01f && _statTooltipCombatantID != null)
            {
                var target = allCombatants.FirstOrDefault(c => c.CombatantID == _statTooltipCombatantID);
                if (target != null)
                {
                    bool hasInsight = allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.Abilities.Any(a => a is InsightAbility));
                    Vector2 center = GetCombatantVisualCenterPosition(target, allCombatants);
                    _vfxRenderer.DrawStatChangeTooltip(spriteBatch, target, _statTooltipAlpha, hasInsight, center);
                }
            }
        }

        public void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font)
        {
            // Tooltips for status icons are handled by TooltipManager.
        }

        // --- Private Helpers ---

        private void UpdateStatTooltipState(BattleCombatant hoveredCombatant, BattleUIManager uiManager)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            bool isSelectionPhase = battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot1 || battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot2;
            bool isDefaultUI = uiManager.UIState == BattleUIState.Default;

            if (isSelectionPhase && isDefaultUI && hoveredCombatant != null)
            {
                _statTooltipCombatantID = hoveredCombatant.CombatantID;
                _statTooltipAlpha = 1.0f;
            }
            else
            {
                _statTooltipAlpha = 0.0f;
                _statTooltipCombatantID = null;
            }
        }

        private (HashSet<BattleCombatant>, TargetType?) ResolveSelectableTargets(IEnumerable<BattleCombatant> allCombatants, BattleCombatant currentActor, BattleUIManager uiManager)
        {
            var set = new HashSet<BattleCombatant>();
            TargetType? type = null;

            if (uiManager.UIState == BattleUIState.Targeting || uiManager.UIState == BattleUIState.ItemTargeting)
            {
                type = uiManager.TargetTypeForSelection;
                if (type.HasValue && currentActor != null)
                {
                    var valid = TargetingHelper.GetValidTargets(currentActor, type.Value, allCombatants);
                    foreach (var t in valid) set.Add(t);
                }
            }
            else if (uiManager.HoverHighlightState.CurrentMove != null && uiManager.HoverHighlightState.Targets.Any())
            {
                foreach (var t in uiManager.HoverHighlightState.Targets) set.Add(t);
                type = uiManager.HoverHighlightState.CurrentMove.Target;
            }

            return (set, type);
        }

        private Dictionary<string, Color> ResolveSilhouetteColors(IEnumerable<BattleCombatant> allCombatants, BattleCombatant currentActor, HashSet<BattleCombatant> selectable, TargetType? targetType, BattleUIManager uiManager, BattleCombatant hoveredCombatant)
        {
            var colors = new Dictionary<string, Color>();
            bool isTargeting = uiManager.UIState == BattleUIState.Targeting || uiManager.UIState == BattleUIState.ItemTargeting;

            BattleCombatant effectiveHover = hoveredCombatant ?? uiManager.HoveredCombatantFromUI;

            if (isTargeting)
            {
                foreach (var c in selectable) colors[c.CombatantID] = _global.Palette_Red;

                if (effectiveHover != null && selectable.Contains(effectiveHover))
                {
                    bool isMulti = targetType == TargetType.Every || targetType == TargetType.Both || targetType == TargetType.All || targetType == TargetType.Team;
                    if (isMulti)
                    {
                        foreach (var c in selectable) colors[c.CombatantID] = Color.Yellow;
                    }
                    else
                    {
                        colors[effectiveHover.CombatantID] = Color.Yellow;
                    }
                }
            }
            else if (selectable.Any() && targetType.HasValue)
            {
                float timer = uiManager.HoverHighlightState.Timer;
                var sorted = allCombatants.Where(c => selectable.Contains(c))
                    .OrderBy(c => c.IsPlayerControlled ? 1 : 0)
                    .ThenBy(c => c.IsPlayerControlled ? (c == currentActor ? 1 : 0) : 0)
                    .ThenBy(c => c.BattleSlot).ToList();

                bool isMulti = targetType == TargetType.Every || targetType == TargetType.Both || targetType == TargetType.All || targetType == TargetType.Team;

                if (isMulti)
                {
                    bool flash = (timer % _global.TargetingMultiBlinkSpeed) < (_global.TargetingMultiBlinkSpeed / 2f);
                    Color c = flash ? Color.Yellow : _global.Palette_Red;
                    foreach (var t in sorted) colors[t.CombatantID] = c;
                }
                else if (sorted.Count > 0)
                {
                    float cycle = sorted.Count * _global.TargetingSingleCycleSpeed;
                    int idx = (int)((timer % cycle) / _global.TargetingSingleCycleSpeed);
                    idx = Math.Clamp(idx, 0, sorted.Count - 1);
                    for (int i = 0; i < sorted.Count; i++)
                        colors[sorted[i].CombatantID] = (i == idx) ? Color.Yellow : _global.Palette_Red;
                }
            }
            return colors;
        }

        private void UpdateEnemyPositions(float dt, IEnumerable<BattleCombatant> combatants, BattleAnimationManager animManager)
        {
            var enemies = combatants.Where(c => !c.IsPlayerControlled).ToList();
            var activeEnemies = enemies.Where(c => !c.IsDefeated && c.IsActiveOnField).ToList();
            var benchedEnemies = enemies.Where(c => !c.IsDefeated && c.BattleSlot >= 2).ToList();
            var dyingEnemies = enemies.Where(c => animManager.IsDeathAnimating(c.CombatantID)).ToList();

            // Visual Enemies = Active + Dying
            var visualEnemies = activeEnemies.Concat(dyingEnemies).Distinct().ToList();

            // Eligibility: 
            // 1. Exactly 1 visual enemy (Active or Dying)
            // OR
            // 2. 0 visual enemies (Victory state), BUT we were already centering. 
            //    This prevents snapping back to 2 slots when the last enemy finishes dying.
            bool isVictoryState = visualEnemies.Count == 0 && _centeringSequenceStarted;

            bool eligibleForCentering = (visualEnemies.Count == 1 || isVictoryState) && !benchedEnemies.Any();

            // Check Battle Phase
            var battleManager = ServiceLocator.Get<BattleManager>();
            bool isActionSelection = battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot1;

            // State Machine for Centering Sequence
            if (!eligibleForCentering)
            {
                _centeringSequenceStarted = false;
                _centeringDelayTimer = 0f;
            }
            else if (isActionSelection && !_centeringSequenceStarted)
            {
                // Only start the sequence (remove other floor -> wait -> move) when turn starts
                _centeringSequenceStarted = true;
                _centeringDelayTimer = 0f;
            }

            // Update Timer
            bool moveNow = false;
            if (_centeringSequenceStarted)
            {
                _centeringDelayTimer += dt;
                if (_centeringDelayTimer >= CENTERING_DELAY_DURATION)
                {
                    moveNow = true;
                }
            }

            float centerX = BattleLayout.GetEnemyCenter().X;

            foreach (var enemy in activeEnemies)
            {
                float targetX;
                if (moveNow)
                {
                    targetX = centerX;
                }
                else
                {
                    targetX = BattleLayout.GetEnemySlotCenter(enemy.BattleSlot).X;
                }

                if (!_enemyVisualXPositions.ContainsKey(enemy.CombatantID))
                {
                    // Initialize immediately if not present
                    _enemyVisualXPositions[enemy.CombatantID] = targetX;
                }
                else
                {
                    float currentX = _enemyVisualXPositions[enemy.CombatantID];

                    // SAFETY FIX: If the position is 0 (uninitialized) or wildly off-screen, snap it.
                    // This prevents enemies from "flying in" from (0,0) when they switch in.
                    if (Math.Abs(currentX) < 1.0f || Math.Abs(currentX - targetX) > Global.VIRTUAL_WIDTH)
                    {
                        _enemyVisualXPositions[enemy.CombatantID] = targetX;
                    }
                    else
                    {
                        // Tween towards target
                        _enemyVisualXPositions[enemy.CombatantID] = MathHelper.Lerp(currentX, targetX, dt * ENEMY_POSITION_TWEEN_SPEED);
                    }
                }
            }

            // Cleanup stale entries from the position cache.
            // We keep entries for Active enemies AND Dying enemies (so they don't snap while fading out).
            // Benched enemies are removed so they snap correctly when they return.
            var keepIds = activeEnemies.Select(e => e.CombatantID).ToHashSet();
            foreach (var d in dyingEnemies) keepIds.Add(d.CombatantID);

            var keysToRemove = _enemyVisualXPositions.Keys.Where(k => !keepIds.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _enemyVisualXPositions.Remove(key);
            }
        }

        private void DrawEnemies(SpriteBatch spriteBatch, List<BattleCombatant> activeEnemies, IEnumerable<BattleCombatant> allCombatants, BattleCombatant currentActor, bool shouldGrayOut, HashSet<BattleCombatant> selectable, BattleAnimationManager animManager, Dictionary<string, Color> silhouetteColors, Matrix transform, GameTime gameTime, BattleUIManager uiManager, BattleCombatant hoveredCombatant)
        {
            // --- FLOOR DRAWING LOGIC ---
            // Identify enemies that are dying (playing death animation) to draw their floors
            var dyingEnemies = allCombatants.Where(c => !c.IsPlayerControlled && animManager.IsDeathAnimating(c.CombatantID)).ToList();

            // Combine active and dying enemies for floor rendering
            var floorEntities = new List<BattleCombatant>(activeEnemies);
            foreach (var dying in dyingEnemies)
            {
                if (!floorEntities.Contains(dying))
                {
                    floorEntities.Add(dying);
                }
            }

            // Determine if we should hide empty floors (Sequence Started)
            bool hideEmptyFloors = _centeringSequenceStarted;

            if (hideEmptyFloors)
            {
                if (floorEntities.Count == 0)
                {
                    // Victory State: No enemies left, but we want to keep the centered floor visible
                    // so it doesn't just pop out of existence or revert to 2 slots.
                    var center = BattleLayout.GetEnemyCenter();
                    // Use Normal size as default for the lingering floor
                    int size = BattleLayout.ENEMY_SPRITE_SIZE_NORMAL;
                    _vfxRenderer.DrawFloor(spriteBatch, center, center.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 12);
                }
                else
                {
                    // Draw floor ONLY for floorEntities (Active + Dying)
                    foreach (var enemy in floorEntities)
                    {
                        // Use dynamic visual X position if available, else slot center
                        float visualX;
                        if (_enemyVisualXPositions.TryGetValue(enemy.CombatantID, out float pos))
                        {
                            visualX = pos;
                        }
                        else
                        {
                            visualX = BattleLayout.GetEnemySlotCenter(Math.Max(0, enemy.BattleSlot)).X;
                        }

                        var center = new Vector2(visualX, BattleLayout.ENEMY_SLOT_Y_OFFSET);
                        int size = _spriteManager.IsMajorEnemySprite(enemy.ArchetypeId) ? BattleLayout.ENEMY_SPRITE_SIZE_MAJOR : BattleLayout.ENEMY_SPRITE_SIZE_NORMAL;
                        _vfxRenderer.DrawFloor(spriteBatch, center, center.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 12);
                    }
                }
            }
            else
            {
                // Draw floors for BOTH slots (0 and 1) to define the arena, regardless of occupancy
                for (int i = 0; i < 2; i++)
                {
                    var center = BattleLayout.GetEnemySlotCenter(i);

                    // Determine size based on occupant (Active or Dying)
                    // If empty, default to Normal size
                    var occupant = floorEntities.FirstOrDefault(e => e.BattleSlot == i);
                    int size = (occupant != null && _spriteManager.IsMajorEnemySprite(occupant.ArchetypeId)) ? BattleLayout.ENEMY_SPRITE_SIZE_MAJOR : BattleLayout.ENEMY_SPRITE_SIZE_NORMAL;

                    _vfxRenderer.DrawFloor(spriteBatch, center, center.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 12);
                }
            }

            // --- SPRITE DRAWING LOGIC ---
            // We must draw sprites for BOTH active and dying enemies.
            var spritesToDraw = new List<BattleCombatant>(activeEnemies);
            spritesToDraw.AddRange(dyingEnemies);

            foreach (var enemy in spritesToDraw)
            {
                // Use dynamic visual X position
                float visualX = _enemyVisualXPositions.ContainsKey(enemy.CombatantID) ? _enemyVisualXPositions[enemy.CombatantID] : BattleLayout.GetEnemySlotCenter(enemy.BattleSlot).X;
                var center = new Vector2(visualX, BattleLayout.ENEMY_SLOT_Y_OFFSET);

                Color? highlight = silhouetteColors.ContainsKey(enemy.CombatantID) ? silhouetteColors[enemy.CombatantID] : null;
                bool isSelectable = selectable.Contains(enemy);
                bool isSilhouetted = shouldGrayOut && !isSelectable;

                if (enemy.CombatantID == _statTooltipCombatantID && _statTooltipAlpha > 0) isSilhouetted = true;

                Color silhouetteColor = isSilhouetted ? _global.Palette_DarkerGray : _global.Palette_DarkGray;
                Color outlineColor = (enemy == currentActor) ? _global.Palette_BrightWhite : _global.Palette_DarkGray;
                if (isSilhouetted) outlineColor = outlineColor * 0.5f;

                // Animations
                var spawnAnim = animManager.GetSpawnAnimationState(enemy.CombatantID);
                var switchOut = animManager.GetSwitchOutAnimationState(enemy.CombatantID);
                var switchIn = animManager.GetSwitchInAnimationState(enemy.CombatantID);
                var healFlash = animManager.GetHealFlashAnimationState(enemy.CombatantID);
                var hitFlash = animManager.GetHitFlashState(enemy.CombatantID);
                var healBounce = animManager.GetHealBounceAnimationState(enemy.CombatantID);

                float spawnY = 0f;
                float alpha = enemy.VisualAlpha;
                float silhouetteAmt = enemy.VisualSilhouetteAmount;

                if (isSilhouetted && spawnAnim == null && switchOut == null && switchIn == null)
                {
                    silhouetteAmt = 1.0f;
                }

                if (spawnAnim != null)
                {
                    if (spawnAnim.CurrentPhase == BattleAnimationManager.SpawnAnimationState.Phase.Flash)
                    {
                        bool visible = ((int)(spawnAnim.Timer / BattleAnimationManager.SpawnAnimationState.FLASH_INTERVAL) % 2) == 0;
                        alpha = 0f; silhouetteAmt = 1f; silhouetteColor = visible ? Color.White : Color.Transparent;
                        spawnY = -BattleAnimationManager.SpawnAnimationState.DROP_HEIGHT;
                    }
                    else
                    {
                        float p = Math.Clamp(spawnAnim.Timer / BattleAnimationManager.SpawnAnimationState.FADE_DURATION, 0f, 1f);
                        alpha = Easing.EaseOutQuad(p);
                        spawnY = MathHelper.Lerp(-BattleAnimationManager.SpawnAnimationState.DROP_HEIGHT, 0f, Easing.EaseOutCubic(p));
                        silhouetteAmt = 0f;
                    }
                }
                else if (switchOut != null)
                {
                    float p = Math.Clamp(switchOut.Timer / BattleAnimationManager.SwitchOutAnimationState.DURATION, 0f, 1f);
                    spawnY = MathHelper.Lerp(0f, -BattleAnimationManager.SwitchOutAnimationState.LIFT_HEIGHT, Easing.EaseOutCubic(p));
                    alpha = 1.0f - Easing.EaseOutCubic(p);
                }
                else if (switchIn != null)
                {
                    float p = Math.Clamp(switchIn.Timer / BattleAnimationManager.SwitchInAnimationState.DURATION, 0f, 1f);
                    spawnY = MathHelper.Lerp(-BattleAnimationManager.SwitchInAnimationState.DROP_HEIGHT, 0f, Easing.EaseOutCubic(p));
                    alpha = Easing.EaseOutCubic(p);
                }

                Color tint = Color.White;
                if (healFlash != null)
                {
                    float p = healFlash.Timer / BattleAnimationManager.HealFlashAnimationState.Duration;
                    tint = Color.Lerp(Color.White, _global.Palette_LightGreen, (1f - Easing.EaseOutQuad(p)) * 0.8f);
                }

                var hitstop = animManager.GetHitstopVisualState(enemy.CombatantID);
                float scale = 1.0f;
                if (_hitstopManager.IsActive && hitstop != null)
                {
                    scale = 1.0f; // Keep scale at 1.0f
                    tint = hitstop.IsCrit ? Color.Red : Color.White;
                    alpha = 1.0f;
                    silhouetteAmt = 0f;
                }

                float bob = CalculateAttackBobOffset(enemy.CombatantID, false);
                if (healBounce != null)
                {
                    float p = healBounce.Timer / BattleAnimationManager.HealBounceAnimationState.Duration;
                    bob += MathF.Sin(p * MathHelper.Pi) * -BattleAnimationManager.HealBounceAnimationState.Height;
                }
                Vector2 recoil = _recoilStates.TryGetValue(enemy.CombatantID, out var r) ? r.Offset : Vector2.Zero;
                Vector2 shake = hitFlash != null ? hitFlash.ShakeOffset : Vector2.Zero;
                bool flashWhite = hitFlash != null && hitFlash.IsCurrentlyWhite;

                int spriteSize = _spriteManager.IsMajorEnemySprite(enemy.ArchetypeId) ? 96 : 64;
                var spriteRect = new Rectangle(
                    (int)(center.X - spriteSize / 2f + recoil.X),
                    (int)(center.Y + bob + spawnY + recoil.Y),
                    spriteSize, spriteSize
                );

                if (silhouetteAmt < 1.0f)
                {
                    float groundY = center.Y + spriteSize;
                    float heightFactor = 1.0f - Math.Clamp(Math.Abs(spawnY) / 50f, 0f, 1f);
                    Vector2 shadowAnim = _shadowOffsets.TryGetValue(enemy.CombatantID, out var s) ? s : Vector2.Zero;
                    _vfxRenderer.DrawShadow(spriteBatch, new Vector2(spriteRect.Center.X, groundY), heightFactor * alpha, shadowAnim);
                }

                Vector2[] offsets = _enemySpritePartOffsets.TryGetValue(enemy.CombatantID, out var o) ? o : null;
                bool isHighlighted = isSelectable && shouldGrayOut;

                _entityRenderer.DrawEnemy(spriteBatch, enemy, spriteRect, offsets, shake, alpha, silhouetteAmt, silhouetteColor, isHighlighted, highlight, outlineColor, flashWhite, tint, scale, transform);

                // Only add hitboxes for ACTIVE enemies (not dying ones)
                if (!enemy.IsDefeated)
                {
                    Rectangle hitBox = new Rectangle((int)(center.X - spriteSize / 2f), (int)(center.Y + bob + spawnY), spriteSize, spriteSize);
                    _currentTargets.Add(new TargetInfo { Combatant = enemy, Bounds = hitBox });
                    _combatantVisualCenters[enemy.CombatantID] = hitBox.Center.ToVector2();

                    // --- VISIBILITY LOGIC ---
                    bool showBars = (hoveredCombatant == enemy) || (uiManager.HoveredCombatantFromUI == enemy) || selectable.Contains(enemy);
                    UpdateBarAlpha(enemy, (float)gameTime.ElapsedGameTime.TotalSeconds, showBars);

                    // --- POSITIONING LOGIC ---
                    // 1. Calculate Visual Center Y (Middle of sprite)
                    float visualCenterY = center.Y + spriteSize / 2f;

                    // 2. Calculate Tooltip Top Y (relative to visual center, matching BattleVfxRenderer)
                    float tooltipTopY = visualCenterY - 3;
                    if (tooltipTopY < 5) tooltipTopY = 5;

                    // 3. Calculate Sprite Top Pixel Y (Highest non-animated pixel)
                    float spriteTopY = GetEnemyStaticVisualTop(enemy, center.Y);

                    // 4. Determine Anchor (Highest point between tooltip top and sprite top)
                    // We want the bar to be above whichever is higher up on the screen (smaller Y value).
                    float anchorY = Math.Min(tooltipTopY - 2, spriteTopY);

                    // 5. Place Bars above Anchor (4px height)
                    float barY = anchorY - 4;
                    float barX = center.X - BattleLayout.ENEMY_BAR_WIDTH / 2f;

                    if (enemy.VisualHealthBarAlpha > 0.01f || enemy.VisualManaBarAlpha > 0.01f)
                    {
                        _hudRenderer.DrawStatusIcons(spriteBatch, enemy, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, false, _enemyStatusIcons.ContainsKey(enemy.CombatantID) ? _enemyStatusIcons[enemy.CombatantID] : null, GetStatusIconOffset, IsStatusIconAnimating);
                        _hudRenderer.DrawEnemyBars(spriteBatch, enemy, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, enemy.VisualHealthBarAlpha, enemy.VisualManaBarAlpha, gameTime);
                    }
                }
            }
        }

        private void DrawPlayers(SpriteBatch spriteBatch, BitmapFont font, List<BattleCombatant> players, BattleCombatant currentActor, bool shouldGrayOut, HashSet<BattleCombatant> selectable, BattleAnimationManager animManager, Dictionary<string, Color> silhouetteColors, GameTime gameTime, BattleUIManager uiManager, BattleCombatant hoveredCombatant)
        {
            foreach (var player in players)
            {
                var center = BattleLayout.GetPlayerSpriteCenter(player.BattleSlot);

                // --- Apply Active Turn Offset ---
                float yOffset = _turnActiveOffsets.TryGetValue(player.CombatantID, out var off) ? off : INACTIVE_Y_OFFSET;
                center.Y += yOffset;

                if (player.BattleSlot == 0) PlayerSpritePosition = center;

                Color? highlight = silhouetteColors.ContainsKey(player.CombatantID) ? silhouetteColors[player.CombatantID] : null;
                bool isSelectable = selectable.Contains(player);
                bool isSilhouetted = shouldGrayOut && !isSelectable;
                if (player.CombatantID == _statTooltipCombatantID && _statTooltipAlpha > 0) isSilhouetted = true;

                Color silhouetteColor = isSilhouetted ? _global.Palette_DarkerGray : _global.Palette_DarkerGray;
                Color outlineColor = (player == currentActor) ? _global.Palette_BrightWhite : _global.Palette_DarkGray;

                var spawnAnim = animManager.GetSpawnAnimationState(player.CombatantID);
                var switchOut = animManager.GetSwitchOutAnimationState(player.CombatantID);
                var switchIn = animManager.GetSwitchInAnimationState(player.CombatantID);
                var healFlash = animManager.GetHealFlashAnimationState(player.CombatantID);
                var hitFlash = animManager.GetHitFlashState(player.CombatantID);
                var healBounce = animManager.GetHealBounceAnimationState(player.CombatantID);

                float spawnY = 0f;
                float alpha = player.VisualAlpha;

                if (switchOut != null)
                {
                    float p = Math.Clamp(switchOut.Timer / BattleConstants.SWITCH_ANIMATION_DURATION, 0f, 1f);
                    spawnY = MathHelper.Lerp(0f, -BattleConstants.SWITCH_VERTICAL_OFFSET, Easing.EaseOutCubic(p));
                    alpha = 1.0f - Easing.EaseOutCubic(p);
                }
                else if (switchIn != null)
                {
                    float p = Math.Clamp(switchIn.Timer / BattleConstants.SWITCH_ANIMATION_DURATION, 0f, 1f);
                    spawnY = MathHelper.Lerp(-BattleConstants.SWITCH_VERTICAL_OFFSET, 0f, Easing.EaseOutCubic(p));
                    alpha = Easing.EaseOutCubic(p);
                }

                Color tint = Color.White * alpha;
                if (healFlash != null)
                {
                    float p = healFlash.Timer / BattleAnimationManager.HealFlashAnimationState.Duration;
                    tint = Color.Lerp(tint, _global.Palette_LightGreen * alpha, (1f - Easing.EaseOutQuad(p)) * 0.8f);
                }

                float bob = CalculateAttackBobOffset(player.CombatantID, true);
                if (healBounce != null)
                {
                    float p = healBounce.Timer / BattleAnimationManager.HealBounceAnimationState.Duration;
                    bob += MathF.Sin(p * MathHelper.Pi) * -BattleAnimationManager.HealBounceAnimationState.Height;
                }
                Vector2 recoil = _recoilStates.TryGetValue(player.CombatantID, out var r) ? r.Offset : Vector2.Zero;
                Vector2 shake = hitFlash != null ? hitFlash.ShakeOffset : Vector2.Zero;

                if (!_playerSprites.TryGetValue(player.CombatantID, out var sprite))
                {
                    sprite = new PlayerCombatSprite(player.ArchetypeId);
                    _playerSprites[player.CombatantID] = sprite;
                }
                sprite.SetPosition(new Vector2(center.X, center.Y + bob + spawnY) + recoil);

                bool isHighlighted = selectable.Contains(player) && shouldGrayOut;
                float pulse = 0f;

                sprite.Draw(spriteBatch, animManager, player, tint, isHighlighted, pulse, isSilhouetted, silhouetteColor, gameTime, highlight, outlineColor);

                Rectangle bounds = sprite.GetStaticBounds(animManager, player);
                _currentTargets.Add(new TargetInfo { Combatant = player, Bounds = bounds });
                _combatantVisualCenters[player.CombatantID] = bounds.Center.ToVector2();

                // --- VISIBILITY LOGIC ---
                // Check if this player is the current actor AND hovering a mana move
                bool isActingAndHoveringManaMove = false;
                if (player == currentActor && uiManager.HoveredMove != null)
                {
                    bool usesMana = uiManager.HoveredMove.ManaCost > 0 || uiManager.HoveredMove.Abilities.Any(a => a is ManaDumpAbility);
                    if (usesMana)
                    {
                        isActingAndHoveringManaMove = true;
                    }
                }

                bool showBars = (hoveredCombatant == player) ||
                                (uiManager.HoveredCombatantFromUI == player) ||
                                selectable.Contains(player) ||
                                isActingAndHoveringManaMove;

                UpdateBarAlpha(player, (float)gameTime.ElapsedGameTime.TotalSeconds, showBars);

                // Only draw name if it is the current actor
                if (player == currentActor && (!isSilhouetted || player.CombatantID == _statTooltipCombatantID))
                {
                    Vector2 nameSize = font.MeasureString(player.Name);
                    Vector2 namePos = new Vector2(center.X - nameSize.X / 2f, BattleLayout.PLAYER_NAME_TOP_Y);
                    Color nameColor = (highlight == Color.Yellow) ? _global.Palette_Yellow : _global.Palette_BrightWhite;
                    spriteBatch.DrawStringSnapped(font, player.Name, namePos, nameColor);
                }

                // --- POSITIONING LOGIC ---
                Vector2 barPos = GetCombatantBarPosition(player);
                float barX = barPos.X - BattleLayout.PLAYER_BAR_WIDTH / 2f;
                float barY = barPos.Y;

                if (player.VisualHealthBarAlpha > 0.01f || player.VisualManaBarAlpha > 0.01f)
                {
                    _hudRenderer.DrawStatusIcons(spriteBatch, player, barX, barY, BattleLayout.PLAYER_BAR_WIDTH, true, _playerStatusIcons, GetStatusIconOffset, IsStatusIconAnimating);

                    // Pass uiManager and isActiveActor to DrawPlayerBars
                    _hudRenderer.DrawPlayerBars(spriteBatch, player, barX, barY, BattleLayout.PLAYER_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, player.VisualHealthBarAlpha, player.VisualManaBarAlpha, gameTime, uiManager, player == currentActor);
                }
            }
        }

        private void DrawUITitle(SpriteBatch spriteBatch, GameTime gameTime, BattleSubMenuState subMenuState)
        {
            string title = "";
            if (!string.IsNullOrEmpty(title))
            {
                var font = ServiceLocator.Get<Core>().SecondaryFont;
                float bob = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 0.375f * MathF.PI) > 0) ? -1f : 0f;
                var size = font.MeasureString(title);
                var pos = new Vector2((Global.VIRTUAL_WIDTH - size.Width) / 2, BattleLayout.DIVIDER_Y + 3 + bob);
                spriteBatch.DrawStringSnapped(font, title, pos, _global.Palette_LightGray);
            }
        }

        private void UpdateEnemyAnimations(float dt, IEnumerable<BattleCombatant> combatants)
        {
            foreach (var c in combatants)
            {
                if (c.IsPlayerControlled || c.IsDefeated) continue;
                string id = c.CombatantID;

                Texture2D sprite = _spriteManager.GetEnemySprite(c.ArchetypeId);
                if (sprite == null) continue;

                bool isMajor = _spriteManager.IsMajorEnemySprite(c.ArchetypeId);
                int partSize = isMajor ? 96 : 64;
                int numParts = sprite.Width / partSize;

                if (!_enemyAnimationTimers.ContainsKey(id) || _enemySpritePartOffsets[id].Length != numParts)
                {
                    _enemySpritePartOffsets[id] = new Vector2[numParts];
                    _enemyAnimationTimers[id] = new float[numParts];
                    _enemyAnimationIntervals[id] = new float[numParts];

                    for (int i = 0; i < numParts; i++)
                    {
                        _enemyAnimationIntervals[id][i] = (float)(_random.NextDouble() * (ENEMY_ANIM_MAX_INTERVAL - ENEMY_ANIM_MIN_INTERVAL) + ENEMY_ANIM_MIN_INTERVAL);
                        _enemyAnimationTimers[id][i] = (float)_random.NextDouble();
                    }
                }

                var timers = _enemyAnimationTimers[id];
                var intervals = _enemyAnimationIntervals[id];
                var offsets = _enemySpritePartOffsets[id];

                offsets[0] = Vector2.Zero;

                for (int i = 1; i < numParts; i++)
                {
                    timers[i] += dt;
                    if (timers[i] >= intervals[i])
                    {
                        timers[i] = 0f;
                        intervals[i] = (float)(_random.NextDouble() * (ENEMY_ANIM_MAX_INTERVAL - ENEMY_ANIM_MIN_INTERVAL) + ENEMY_ANIM_MIN_INTERVAL);

                        if (offsets[i] != Vector2.Zero)
                        {
                            offsets[i] = Vector2.Zero;
                        }
                        else
                        {
                            // TUNING: Restricted to Up/Down only
                            int dir = _random.Next(2);
                            offsets[i] = dir == 0 ? new Vector2(0, -1) : new Vector2(0, 1);
                        }
                    }
                }
            }
        }

        private void UpdateShadowAnimations(float dt, IEnumerable<BattleCombatant> combatants)
        {
            foreach (var c in combatants)
            {
                if (c.IsPlayerControlled || c.IsDefeated) continue;
                string id = c.CombatantID;
                if (!_shadowTimers.ContainsKey(id))
                {
                    _shadowTimers[id] = (float)_random.NextDouble();
                    _shadowIntervals[id] = (float)(_random.NextDouble() * (SHADOW_ANIM_MAX_INTERVAL - SHADOW_ANIM_MIN_INTERVAL) + SHADOW_ANIM_MIN_INTERVAL);
                    _shadowOffsets[id] = Vector2.Zero;
                }

                _shadowTimers[id] += dt;
                if (_shadowTimers[id] >= _shadowIntervals[id])
                {
                    _shadowTimers[id] = 0f;
                    _shadowIntervals[id] = (float)(_random.NextDouble() * (SHADOW_ANIM_MAX_INTERVAL - SHADOW_ANIM_MIN_INTERVAL) + SHADOW_ANIM_MIN_INTERVAL);
                    if (_shadowOffsets[id] != Vector2.Zero) _shadowOffsets[id] = Vector2.Zero;
                    else
                    {
                        int dir = _random.Next(4);
                        _shadowOffsets[id] = dir switch { 0 => new Vector2(0, -1), 1 => new Vector2(0, 1), 2 => new Vector2(-1, 0), _ => new Vector2(1, 0) };
                    }
                }
            }
        }

        private void UpdateRecoilAnimations(float dt)
        {
            foreach (var state in _recoilStates.Values)
            {
                Vector2 force = (-state.Offset * RecoilState.STIFFNESS) - (state.Velocity * RecoilState.DAMPING);
                state.Velocity += force * dt;
                state.Offset += state.Velocity * dt;
                if (state.Offset.LengthSquared() < 0.1f && state.Velocity.LengthSquared() < 0.1f)
                {
                    state.Offset = Vector2.Zero;
                    state.Velocity = Vector2.Zero;
                }
            }
        }

        private void UpdateStatusIconAnimations(float dt)
        {
            for (int i = _activeStatusIconAnims.Count - 1; i >= 0; i--)
            {
                var anim = _activeStatusIconAnims[i];
                anim.Timer += dt;
                if (anim.Timer >= StatusIconAnim.DURATION) _activeStatusIconAnims.RemoveAt(i);
            }
        }

        private void UpdateStatusIconTooltips(IEnumerable<BattleCombatant> allCombatants)
        {
            var virtualMousePos = Core.TransformMouse(Mouse.GetState().Position);

            foreach (var iconInfo in _playerStatusIcons)
            {
                if (iconInfo.Bounds.Contains(virtualMousePos))
                {
                    _tooltipManager.RequestTooltip(
                        iconInfo.Effect,
                        iconInfo.Effect.GetTooltipText(),
                        new Vector2(iconInfo.Bounds.Center.X, iconInfo.Bounds.Top),
                        0.1f,
                        iconInfo.Effect.GetDescription()
                    );
                    return;
                }
            }

            foreach (var combatantEntry in _enemyStatusIcons)
            {
                foreach (var iconInfo in combatantEntry.Value)
                {
                    if (iconInfo.Bounds.Contains(virtualMousePos))
                    {
                        _tooltipManager.RequestTooltip(
                            iconInfo.Effect,
                            iconInfo.Effect.GetTooltipText(),
                            new Vector2(iconInfo.Bounds.Center.X, iconInfo.Bounds.Top),
                            0.1f,
                            iconInfo.Effect.GetDescription()
                        );
                        return;
                    }
                }
            }
        }

        private void UpdateActiveTurnOffsets(float dt, IEnumerable<BattleCombatant> combatants, BattleCombatant currentActor)
        {
            foreach (var c in combatants)
            {
                if (c.IsPlayerControlled)
                {
                    float targetOffset = (c == currentActor) ? 0f : INACTIVE_Y_OFFSET;
                    if (!_turnActiveOffsets.ContainsKey(c.CombatantID)) _turnActiveOffsets[c.CombatantID] = INACTIVE_Y_OFFSET;

                    float current = _turnActiveOffsets[c.CombatantID];
                    _turnActiveOffsets[c.CombatantID] = MathHelper.Lerp(current, targetOffset, dt * ACTIVE_TWEEN_SPEED);
                }
            }
        }

        private void UpdateBarAlpha(BattleCombatant c, float dt, bool shouldBeVisible)
        {
            float target = (shouldBeVisible || c.HealthBarVisibleTimer > 0 || c.ManaBarVisibleTimer > 0) ? 1.0f : 0.0f;
            c.VisualHealthBarAlpha = target;
            c.VisualManaBarAlpha = target;
        }

        private float CalculateAttackBobOffset(string id, bool isPlayer)
        {
            if (_attackAnimControllers.TryGetValue(id, out var c)) return c.GetOffset(isPlayer);
            return 0f;
        }

        private float GetStatusIconOffset(string id, StatusEffectType type)
        {
            var anim = _activeStatusIconAnims.FirstOrDefault(a => a.CombatantID == id && a.Type == type);
            if (anim != null)
            {
                float p = anim.Timer / StatusIconAnim.DURATION;
                return MathF.Sin(p * MathHelper.Pi) * -StatusIconAnim.HEIGHT;
            }
            return 0f;
        }

        private bool IsStatusIconAnimating(string id, StatusEffectType type)
        {
            return _activeStatusIconAnims.Any(a => a.CombatantID == id && a.Type == type);
        }

        private float GetEnemyStaticVisualTop(BattleCombatant enemy, float baseTopY)
        {
            var offsets = _spriteManager.GetEnemySpriteTopPixelOffsets(enemy.ArchetypeId);
            if (offsets == null || offsets.Length == 0) return baseTopY;

            int minOffset = int.MaxValue;
            for (int i = 0; i < offsets.Length; i++)
            {
                if (offsets[i] < minOffset) minOffset = offsets[i];
            }

            if (minOffset == int.MaxValue) return baseTopY;
            return baseTopY + minOffset;
        }

        private Vector2 GetCombatantBarPosition(BattleCombatant c)
        {
            // 1. Get Visual Center
            if (!_combatantVisualCenters.TryGetValue(c.CombatantID, out var center)) return Vector2.Zero;

            // 2. Calculate Tooltip Top
            float tooltipTopY;
            if (c.IsPlayerControlled)
            {
                tooltipTopY = center.Y - 16;
            }
            else
            {
                float baseTooltipY = center.Y - 3;
                float spriteTopY = GetEnemyStaticVisualTop(c, center.Y);
                tooltipTopY = Math.Min(baseTooltipY, spriteTopY + 2);
            }

            if (tooltipTopY < 5) tooltipTopY = 5;

            // 3. Calculate Bar Position
            // AnchorY = tooltipTopY - 2
            // BarY = AnchorY - 4
            float barY = tooltipTopY - 6;

            return new Vector2(center.X, barY);
        }
    }
}