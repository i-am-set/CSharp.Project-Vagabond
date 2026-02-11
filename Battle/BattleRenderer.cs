using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
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
    public class BattleRenderer
    {
        private Global _global => ServiceLocator.Get<Global>();
        private SpriteManager _spriteManager => ServiceLocator.Get<SpriteManager>();
        private TooltipManager _tooltipManager => ServiceLocator.Get<TooltipManager>();
        private HitstopManager _hitstopManager => ServiceLocator.Get<HitstopManager>();
        private Core _core => ServiceLocator.Get<Core>();
        private GraphicsDevice _graphicsDevice => ServiceLocator.Get<GraphicsDevice>();
        private Texture2D _pixel => ServiceLocator.Get<Texture2D>();

        private readonly BattleEntityRenderer _entityRenderer;
        private readonly BattleHudRenderer _hudRenderer;
        private readonly BattleVfxRenderer _vfxRenderer;

        private readonly List<TargetInfo> _currentTargets = new List<TargetInfo>();
        private readonly List<StatusIconInfo> _playerStatusIcons = new List<StatusIconInfo>();
        private readonly Dictionary<string, List<StatusIconInfo>> _enemyStatusIcons = new Dictionary<string, List<StatusIconInfo>>();

        private readonly Dictionary<string, PlayerCombatSprite> _playerSprites = new Dictionary<string, PlayerCombatSprite>();
        private readonly Dictionary<string, SpriteHopAnimationController> _attackAnimControllers = new Dictionary<string, SpriteHopAnimationController>();

        private readonly Dictionary<string, float> _turnActiveOffsets = new Dictionary<string, float>();
        private const float INACTIVE_Y_OFFSET = 8f;
        private const float ACTIVE_TWEEN_SPEED = 5f;

        private readonly Dictionary<string, float> _enemyVisualXPositions = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _playerVisualXPositions = new Dictionary<string, float>();
        private const float ENEMY_POSITION_TWEEN_SPEED = 4.0f;

        private bool _centeringSequenceStarted = false;
        private float _centeringDelayTimer = 0f;
        private const float CENTERING_DELAY_DURATION = 0.5f;
        private float _bobSpeed = 3f;
        private bool _floorOutroTriggered = false;
        private bool _waitingForFloorOutro = false;
        private bool _hasInitializedPositions = false;

        public bool ForceDrawCenterFloor { get; set; } = false;

        private Dictionary<string, Vector2[]> _enemySpritePartOffsets = new Dictionary<string, Vector2[]>();
        private Dictionary<string, float[]> _enemyAnimationTimers = new Dictionary<string, float[]>();
        private Dictionary<string, float[]> _enemyAnimationIntervals = new Dictionary<string, float[]>();

        private const float ENEMY_ANIM_MIN_INTERVAL = 0.8f;
        private const float ENEMY_ANIM_MAX_INTERVAL = 1.2f;

        private class RecoilState { public Vector2 Offset; public Vector2 Velocity; public const float STIFFNESS = 600f; public const float DAMPING = 15f; }
        private readonly Dictionary<string, RecoilState> _recoilStates = new Dictionary<string, RecoilState>();

        private readonly Dictionary<string, Vector2> _enemySquashScales = new Dictionary<string, Vector2>();

        private class StatusIconAnim { public string CombatantID; public StatusEffectType Type; public float Timer; public const float DURATION = 0.3f; public const float HEIGHT = 5f; }
        private readonly List<StatusIconAnim> _activeStatusIconAnims = new List<StatusIconAnim>();

        private float _statTooltipAlpha = 0f;
        private string _statTooltipCombatantID = null;

        private readonly Random _random = new Random();

        public Vector2 PlayerSpritePosition { get; private set; }
        private Dictionary<string, Vector2> _combatantVisualCenters = new Dictionary<string, Vector2>();
        private Dictionary<string, Vector2> _combatantStaticCenters = new Dictionary<string, Vector2>();
        private Dictionary<string, float> _combatantBarBottomYs = new Dictionary<string, float>();
        private readonly Dictionary<string, Vector2> _combatantBarPositions = new Dictionary<string, Vector2>();

        private class ReticleController
        {
            public Rectangle CurrentRect;
            public bool IsActive;

            public void Update(Rectangle targetRect)
            {
                CurrentRect = targetRect;
                IsActive = true;
            }

            public void Reset()
            {
                IsActive = false;
            }
        }
        private readonly ReticleController _reticleController = new ReticleController();

        public BattleRenderer()
        {
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
            _playerVisualXPositions.Clear();
            _enemySpritePartOffsets.Clear();
            _enemyAnimationTimers.Clear();
            _enemyAnimationIntervals.Clear();
            _recoilStates.Clear();
            _activeStatusIconAnims.Clear();
            _combatantVisualCenters.Clear();
            _combatantStaticCenters.Clear();
            _combatantBarBottomYs.Clear();
            _combatantBarPositions.Clear();
            _statTooltipAlpha = 0f;
            _statTooltipCombatantID = null;
            _centeringSequenceStarted = false;
            _centeringDelayTimer = 0f;
            _floorOutroTriggered = false;
            _waitingForFloorOutro = false;
            _hasInitializedPositions = false;
            _enemySquashScales.Clear();
            ForceDrawCenterFloor = false;
            _reticleController.Reset();
        }

        public void SetCenteringState(bool isCentered)
        {
            _centeringSequenceStarted = isCentered;
            if (isCentered) _hasInitializedPositions = true;
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

        public float GetCombatantVisualX(BattleCombatant c)
        {
            if (c.IsPlayerControlled)
            {
                if (_playerVisualXPositions.TryGetValue(c.CombatantID, out float x)) return x;
                return BattleLayout.GetPlayerSpriteCenter(c.BattleSlot).X;
            }
            else
            {
                if (_enemyVisualXPositions.TryGetValue(c.CombatantID, out float x)) return x;
                return BattleLayout.GetEnemySlotCenter(c.BattleSlot).X;
            }
        }

        public void TriggerAttackAnimation(string combatantId)
        {
            if (!_attackAnimControllers.ContainsKey(combatantId))
                _attackAnimControllers[combatantId] = new SpriteHopAnimationController();
            _attackAnimControllers[combatantId].Trigger();

            if (_playerSprites.TryGetValue(combatantId, out var sprite))
            {
                sprite.TriggerSquash(0.6f, 1.4f);
            }
            else
            {
                _enemySquashScales[combatantId] = new Vector2(0.6f, 1.4f);
            }
        }

        public void TriggerRecoil(string combatantId, Vector2 direction, float magnitude)
        {
            if (!_recoilStates.ContainsKey(combatantId)) _recoilStates[combatantId] = new RecoilState();
            _recoilStates[combatantId].Velocity = direction * magnitude * 10f;

            if (_playerSprites.TryGetValue(combatantId, out var sprite))
            {
                sprite.TriggerSquash(1.5f, 0.5f);
            }
            else
            {
                _enemySquashScales[combatantId] = new Vector2(1.5f, 0.5f);
            }
        }

        public void TriggerStatusIconHop(string combatantId, StatusEffectType type)
        {
            _activeStatusIconAnims.RemoveAll(a => a.CombatantID == combatantId && a.Type == type);
            _activeStatusIconAnims.Add(new StatusIconAnim { CombatantID = combatantId, Type = type, Timer = 0f });
        }

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants, BattleAnimationManager animationManager, BattleCombatant currentActor)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var battleManager = ServiceLocator.Get<BattleManager>();

            foreach (var c in combatants)
            {
                if (c.HealthBarVisibleTimer > 0) c.HealthBarVisibleTimer = Math.Max(0, c.HealthBarVisibleTimer - dt);

                float hpPercent = (float)c.Stats.CurrentHP / c.Stats.MaxHP;
                if (hpPercent <= _global.LowHealthThreshold && c.Stats.CurrentHP > 0)
                {
                    float ratio = hpPercent / _global.LowHealthThreshold;
                    float intensity = 1.0f - ratio;
                    float speed = MathHelper.Lerp(_global.LowHealthFlashSpeedMin, _global.LowHealthFlashSpeedMax, intensity);
                    c.LowHealthFlashTimer += dt * speed;
                }
                else
                {
                    c.LowHealthFlashTimer = 0f;
                }
            }

            UpdateEnemyPositions(dt, combatants, animationManager);
            UpdatePlayerPositions(dt, combatants, animationManager);
            UpdateEnemyAnimations(dt, combatants);
            UpdateRecoilAnimations(dt);
            UpdateStatusIconAnimations(dt);
            UpdateStatusIconTooltips(combatants);
            UpdateActiveTurnOffsets(dt, combatants, currentActor);
            UpdateEnemySquash(dt);

            foreach (var controller in _attackAnimControllers.Values) controller.Update(gameTime);

            foreach (var c in combatants)
            {
                if (c.IsPlayerControlled && _playerSprites.TryGetValue(c.CombatantID, out var sprite))
                {
                    float? manualBob = null;
                    bool? manualAlt = null;

                    if (battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection && !battleManager.IsActionPending(c.BattleSlot))
                    {
                        float t = (float)gameTime.TotalGameTime.TotalSeconds;
                        float phase = (c.BattleSlot == 1) ? MathHelper.Pi : 0f;
                        float rawSin = MathF.Sin(t * _bobSpeed + phase);

                        manualBob = -rawSin * 0.5f;

                        manualAlt = manualBob < 0;
                    }

                    sprite.Update(gameTime, currentActor == c, manualBob, manualAlt);
                }
            }
        }

        private void UpdateEnemySquash(float dt)
        {
            var keys = _enemySquashScales.Keys.ToList();
            foreach (var key in keys)
            {
                float damping = 1.0f - MathF.Exp(-_global.SquashRecoverySpeed * dt);
                _enemySquashScales[key] = Vector2.Lerp(_enemySquashScales[key], Vector2.One, damping);
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, IEnumerable<BattleCombatant> allCombatants, BattleCombatant currentActor, BattleUIManager uiManager, BattleInputHandler inputHandler, BattleAnimationManager animationManager, float sharedBobbingTimer, Matrix transform)
        {
            BattleCombatant hoveredCombatant = null;
            if (inputHandler.HoveredTargetIndex >= 0 && inputHandler.HoveredTargetIndex < _currentTargets.Count)
            {
                hoveredCombatant = _currentTargets[inputHandler.HoveredTargetIndex].Combatant;
            }

            _currentTargets.Clear();
            _playerStatusIcons.Clear();
            _combatantBarPositions.Clear();
            foreach (var list in _enemyStatusIcons.Values) list.Clear();

            var enemies = allCombatants.Where(c => !c.IsPlayerControlled && c.IsActiveOnField).ToList();
            var players = allCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();

            var (selectableTargets, activeTargetType) = ResolveSelectableTargets(allCombatants, currentActor, uiManager);
            var silhouetteColors = ResolveSilhouetteColors(allCombatants, currentActor, selectableTargets, activeTargetType, uiManager, hoveredCombatant);
            bool shouldGrayOut = uiManager.UIState == BattleUIState.Targeting || (uiManager.HoveredMove != null && uiManager.HoveredMove.Target != TargetType.None);

            // --- NEW: Determine Targeting Mode and Hovered Group Color ---
            bool isTargetingMode = uiManager.UIState == BattleUIState.Targeting;
            Color? hoveredGroupColor = null;
            if (hoveredCombatant != null && silhouetteColors.TryGetValue(hoveredCombatant.CombatantID, out var color))
            {
                hoveredGroupColor = color;
            }
            // -------------------------------------------------------------

            var flashState = animationManager.GetImpactFlashState();
            if (flashState != null)
            {
                DrawEnemies(spriteBatch, enemies, allCombatants, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, transform, gameTime, uiManager, hoveredCombatant, isTargetingMode, hoveredGroupColor, drawFloor: true, drawShadow: true, drawSprite: false);
                DrawPlayers(spriteBatch, font, players, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, gameTime, uiManager, hoveredCombatant, isTargetingMode, hoveredGroupColor, drawFloor: true, drawShadow: true, drawSprite: false);

                var nonTargetEnemies = enemies.Where(e => !flashState.TargetCombatantIDs.Contains(e.CombatantID)).ToList();
                var nonTargetPlayers = players.Where(p => !flashState.TargetCombatantIDs.Contains(p.CombatantID)).ToList();

                DrawEnemies(spriteBatch, nonTargetEnemies, allCombatants, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, transform, gameTime, uiManager, hoveredCombatant, isTargetingMode, hoveredGroupColor, drawFloor: false, drawShadow: false, drawSprite: true, includeDying: true);
                DrawPlayers(spriteBatch, font, nonTargetPlayers, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, gameTime, uiManager, hoveredCombatant, isTargetingMode, hoveredGroupColor, drawFloor: false, drawShadow: false, drawSprite: true);

                _core.RequestFullscreenOverlay((overlayBatch, uiMatrix) =>
                {
                    int screenW = _graphicsDevice.PresentationParameters.BackBufferWidth;
                    int screenH = _graphicsDevice.PresentationParameters.BackBufferHeight;

                    float alpha = Math.Clamp(flashState.Timer / flashState.Duration, 0f, 1f);
                    alpha = Easing.EaseOutQuad(alpha);

                    overlayBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.Identity);
                    overlayBatch.Draw(_pixel, new Rectangle(0, 0, screenW, screenH), flashState.Color * alpha);
                    overlayBatch.End();

                    var targetEnemies = enemies.Where(e => flashState.TargetCombatantIDs.Contains(e.CombatantID)).ToList();
                    var targetPlayers = players.Where(p => flashState.TargetCombatantIDs.Contains(p.CombatantID)).ToList();

                    overlayBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, uiMatrix);
                    DrawEnemies(overlayBatch, targetEnemies, allCombatants, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, uiMatrix, gameTime, uiManager, hoveredCombatant, isTargetingMode, hoveredGroupColor, drawFloor: false, drawShadow: false, drawSprite: true, includeDying: false);
                    DrawPlayers(overlayBatch, font, targetPlayers, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, gameTime, uiManager, hoveredCombatant, isTargetingMode, hoveredGroupColor, drawFloor: false, drawShadow: false, drawSprite: true);
                    overlayBatch.End();
                });
            }
            else
            {
                DrawEnemies(spriteBatch, enemies, allCombatants, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, transform, gameTime, uiManager, hoveredCombatant, isTargetingMode, hoveredGroupColor);
                DrawPlayers(spriteBatch, font, players, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, gameTime, uiManager, hoveredCombatant, isTargetingMode, hoveredGroupColor);
            }

            var effectiveFocus = hoveredCombatant ?? uiManager.HoveredCombatantFromUI;
            DrawTargetingHighlights(spriteBatch, uiManager, gameTime, silhouetteColors, effectiveFocus);

            DrawHUD(spriteBatch, animationManager, gameTime, uiManager, currentActor, silhouetteColors, hoveredCombatant);

            if (_statTooltipAlpha > 0.01f && _statTooltipCombatantID != null)
            {
                var target = allCombatants.FirstOrDefault(c => c.CombatantID == _statTooltipCombatantID);
                if (target != null)
                {
                    bool hasInsight = false;

                    Vector2 center = Vector2.Zero;
                    if (_combatantStaticCenters.TryGetValue(target.CombatantID, out var staticPos))
                    {
                        center = staticPos;
                    }
                    else
                    {
                        center = GetCombatantVisualCenterPosition(target, allCombatants);
                    }

                    float barBottomY = 0f;
                    if (_combatantBarBottomYs.TryGetValue(target.CombatantID, out float cachedY))
                    {
                        barBottomY = cachedY;
                    }
                    else
                    {
                        barBottomY = center.Y;
                    }

                    _vfxRenderer.DrawStatChangeTooltip(spriteBatch, target, _statTooltipAlpha, hasInsight, center, barBottomY, gameTime);
                }
            }
        }

        public void DrawHUD(SpriteBatch spriteBatch, BattleAnimationManager animManager, GameTime gameTime, BattleUIManager uiManager, BattleCombatant currentActor, Dictionary<string, Color> silhouetteColors, BattleCombatant hoveredCombatant)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();

            foreach (var combatant in battleManager.AllCombatants)
            {
                // Instantly hide HUD if defeated to prevent visual snapping during death animation
                if (combatant.IsDefeated) continue;

                if (!_combatantBarPositions.TryGetValue(combatant.CombatantID, out var pos)) continue;

                float barX = pos.X;
                float barY = pos.Y;

                if (combatant.VisualHealthBarAlpha <= 0.01f) continue;

                float hudAlpha = combatant.HudVisualAlpha;

                // Determine alignment: Slot 1 and 3 (Odd) are Right Aligned
                bool isRightAligned = (combatant.BattleSlot % 2 != 0);

                (int Min, int Max)? projectedDamage = null;
                if (silhouetteColors.ContainsKey(combatant.CombatantID))
                {
                    bool showDamage = true;
                    if (uiManager.UIState == BattleUIState.Targeting)
                    {
                        showDamage = false;
                        if (hoveredCombatant != null)
                        {
                            if (combatant == hoveredCombatant)
                            {
                                showDamage = true;
                            }
                            else if (silhouetteColors.TryGetValue(hoveredCombatant.CombatantID, out var hCol) &&
                                     silhouetteColors.TryGetValue(combatant.CombatantID, out var cCol) &&
                                     hCol == cCol)
                            {
                                showDamage = true;
                            }
                        }
                    }

                    if (showDamage)
                    {
                        var move = uiManager.HoveredMove ?? uiManager.MoveForTargeting;
                        BattleCombatant actor = currentActor;

                        if (uiManager.UIState == BattleUIState.Targeting && uiManager.ActiveTargetingSlot != -1)
                        {
                            actor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == uiManager.ActiveTargetingSlot);
                        }
                        else if (uiManager.HoveredMove != null && uiManager.HoveredSlotIndex != -1)
                        {
                            actor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == uiManager.HoveredSlotIndex);
                        }

                        if (move != null && actor != null)
                        {
                            projectedDamage = battleManager.GetProjectedDamageRange(actor, combatant, move);
                        }
                    }
                }

                if (combatant.IsPlayerControlled)
                {
                    // --- NEW LOGIC: Idle Bob for Pending Action ---
                    float yOffset = 0f;
                    if (battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection)
                    {
                        // If action is NOT pending (not locked in), bob up and down
                        if (!battleManager.IsActionPending(combatant.BattleSlot))
                        {
                            float t = (float)gameTime.TotalGameTime.TotalSeconds;
                            // Invert phase for slot 1 (the second player) so they bob opposite to slot 0
                            float phase = (combatant.BattleSlot == 1) ? MathHelper.Pi : 0f;
                            yOffset = MathF.Sin(t * _bobSpeed + phase) * 0.5f;
                        }
                    }
                    // ----------------------------------------------

                    _hudRenderer.DrawStatusIcons(spriteBatch, combatant, barX, barY + yOffset, BattleLayout.PLAYER_BAR_WIDTH, true, _playerStatusIcons, GetStatusIconOffset, IsStatusIconAnimating, isRightAligned);
                    _hudRenderer.DrawPlayerBars(spriteBatch, combatant, barX, barY + yOffset, BattleLayout.PLAYER_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, combatant.VisualHealthBarAlpha * hudAlpha, gameTime, uiManager, combatant == currentActor, isRightAligned, projectedDamage);
                }
                else
                {
                    if (!_enemyStatusIcons.ContainsKey(combatant.CombatantID))
                        _enemyStatusIcons[combatant.CombatantID] = new List<StatusIconInfo>();

                    _hudRenderer.DrawStatusIcons(spriteBatch, combatant, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, false, _enemyStatusIcons[combatant.CombatantID], GetStatusIconOffset, IsStatusIconAnimating, isRightAligned);
                    _hudRenderer.DrawEnemyBars(spriteBatch, combatant, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, combatant.VisualHealthBarAlpha * hudAlpha, gameTime, isRightAligned, projectedDamage);
                }
            }
        }

        public void DrawHUD(SpriteBatch spriteBatch, BattleAnimationManager animManager, GameTime gameTime, BattleUIManager uiManager, BattleCombatant currentActor, Dictionary<string, Color> silhouetteColors)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();

            foreach (var combatant in battleManager.AllCombatants)
            {
                // Instantly hide HUD if defeated to prevent visual snapping during death animation
                if (combatant.IsDefeated) continue;

                if (!_combatantBarPositions.TryGetValue(combatant.CombatantID, out var pos)) continue;

                float barX = pos.X;
                float barY = pos.Y;

                if (combatant.VisualHealthBarAlpha <= 0.01f) continue;

                float hudAlpha = combatant.HudVisualAlpha;

                // Determine alignment: Slot 1 and 3 (Odd) are Right Aligned
                bool isRightAligned = (combatant.BattleSlot % 2 != 0);

                (int Min, int Max)? projectedDamage = null;
                if (silhouetteColors.ContainsKey(combatant.CombatantID))
                {
                    var move = uiManager.HoveredMove ?? uiManager.MoveForTargeting;
                    BattleCombatant actor = currentActor;

                    if (uiManager.UIState == BattleUIState.Targeting && uiManager.ActiveTargetingSlot != -1)
                    {
                        actor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == uiManager.ActiveTargetingSlot);
                    }
                    else if (uiManager.HoveredMove != null && uiManager.HoveredSlotIndex != -1)
                    {
                        actor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == uiManager.HoveredSlotIndex);
                    }

                    if (move != null && actor != null)
                    {
                        projectedDamage = battleManager.GetProjectedDamageRange(actor, combatant, move);
                    }
                }

                if (combatant.IsPlayerControlled)
                {
                    // --- NEW LOGIC: Idle Bob for Pending Action ---
                    float yOffset = 0f;
                    if (battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection)
                    {
                        // If action is NOT pending (not locked in), bob up and down
                        if (!battleManager.IsActionPending(combatant.BattleSlot))
                        {
                            float t = (float)gameTime.TotalGameTime.TotalSeconds;
                            // Invert phase for slot 1 (the second player) so they bob opposite to slot 0
                            float phase = (combatant.BattleSlot == 1) ? MathHelper.Pi : 0f;
                            yOffset = MathF.Sin(t * _bobSpeed + phase) * 0.5f;
                        }
                    }
                    // ----------------------------------------------

                    _hudRenderer.DrawStatusIcons(spriteBatch, combatant, barX, barY + yOffset, BattleLayout.PLAYER_BAR_WIDTH, true, _playerStatusIcons, GetStatusIconOffset, IsStatusIconAnimating, isRightAligned);
                    _hudRenderer.DrawPlayerBars(spriteBatch, combatant, barX, barY + yOffset, BattleLayout.PLAYER_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, combatant.VisualHealthBarAlpha * hudAlpha, gameTime, uiManager, combatant == currentActor, isRightAligned, projectedDamage);
                }
                else
                {
                    if (!_enemyStatusIcons.ContainsKey(combatant.CombatantID))
                        _enemyStatusIcons[combatant.CombatantID] = new List<StatusIconInfo>();

                    _hudRenderer.DrawStatusIcons(spriteBatch, combatant, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, false, _enemyStatusIcons[combatant.CombatantID], GetStatusIconOffset, IsStatusIconAnimating, isRightAligned);
                    _hudRenderer.DrawEnemyBars(spriteBatch, combatant, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, combatant.VisualHealthBarAlpha * hudAlpha, gameTime, isRightAligned, projectedDamage);
                }
            }
        }

        private void DrawTargetingHighlights(SpriteBatch spriteBatch, BattleUIManager uiManager, GameTime gameTime, Dictionary<string, Color> silhouetteColors, BattleCombatant focusedCombatant)
        {
            _reticleController.Reset();
        }

        private List<BattleCombatant> SortTargetsByHierarchy(IEnumerable<BattleCombatant> targets, BattleCombatant user)
        {
            return targets
                .OrderBy(c => c.IsPlayerControlled)
                .ThenBy(c => c == user)
                .ThenBy(c => c.BattleSlot)
                .ToList();
        }

        private Rectangle GetPatternAlignedRect(Rectangle baseRect)
        {
            const int patternLength = 8;
            float minW = baseRect.Width + 4;
            float minH = baseRect.Height + 4;

            float targetW = MathF.Ceiling(minW / patternLength) * patternLength;
            float targetH = MathF.Ceiling(minH / patternLength) * patternLength;

            int newW = (int)targetW;
            int newH = (int)targetH;

            int newX = baseRect.Center.X - (newW / 2);
            int newY = baseRect.Center.Y - (newH / 2);

            return new Rectangle(newX, newY, newW, newH);
        }

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }

        public void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font)
        {
        }

        private (HashSet<BattleCombatant>, TargetType?) ResolveSelectableTargets(IEnumerable<BattleCombatant> allCombatants, BattleCombatant currentActor, BattleUIManager uiManager)
        {
            var set = new HashSet<BattleCombatant>();
            TargetType? type = null;

            if (uiManager.UIState == BattleUIState.Targeting)
            {
                type = uiManager.TargetTypeForSelection;
                var battleManager = ServiceLocator.Get<BattleManager>();
                var activeActor = battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.BattleSlot == uiManager.ActiveTargetingSlot);
                var actor = activeActor ?? currentActor;

                if (type.HasValue && actor != null)
                {
                    var valid = TargetingHelper.GetValidTargets(actor, type.Value, allCombatants);
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

            if (selectable.Count == 0 || !targetType.HasValue)
                return colors;

            bool isMulti = targetType == TargetType.Every || targetType == TargetType.Both || targetType == TargetType.All || targetType == TargetType.Team;

            var palette = new Color[] { _global.Palette_Sun, _global.Palette_Sky, _global.Palette_Leaf, _global.Palette_LightPale };

            if (isMulti)
            {
                foreach (var c in selectable)
                {
                    colors[c.CombatantID] = palette[0];
                }
            }
            else
            {
                var sorted = SortTargetsByHierarchy(selectable, currentActor);
                for (int i = 0; i < sorted.Count; i++)
                {
                    colors[sorted[i].CombatantID] = palette[i % palette.Length];
                }
            }

            return colors;
        }

        private void UpdateEnemyPositions(float dt, IEnumerable<BattleCombatant> combatants, BattleAnimationManager animationManager)
        {
            var enemies = combatants.Where(c => !c.IsPlayerControlled).ToList();
            var activeEnemies = enemies.Where(c => !c.IsDefeated && c.IsActiveOnField).ToList();
            var benchedEnemies = enemies.Where(c => !c.IsDefeated && c.BattleSlot >= 2).ToList();
            var dyingEnemies = enemies.Where(c => animationManager.IsDeathAnimating(c.CombatantID)).ToList();

            var visualEnemies = activeEnemies.Concat(dyingEnemies).Distinct().ToList();

            if (!_hasInitializedPositions && visualEnemies.Count == 1)
            {
                _centeringSequenceStarted = true;
                _floorOutroTriggered = true;
                _waitingForFloorOutro = false;
            }

            bool isVictoryState = visualEnemies.Count == 0 && _centeringSequenceStarted;
            bool eligibleForCentering = (visualEnemies.Count == 1 || isVictoryState) && !benchedEnemies.Any();

            var battleManager = ServiceLocator.Get<BattleManager>();
            bool isActionSelection = battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection;

            if (!eligibleForCentering)
            {
                _centeringSequenceStarted = false;
                _centeringDelayTimer = 0f;
                _floorOutroTriggered = false;
                _waitingForFloorOutro = false;
            }
            else if (isActionSelection && !_centeringSequenceStarted)
            {
                if (!_floorOutroTriggered)
                {
                    int emptySlot = -1;
                    if (visualEnemies.Count == 1)
                    {
                        int occupiedSlot = visualEnemies[0].BattleSlot;
                        emptySlot = (occupiedSlot == 0) ? 1 : 0;
                    }

                    if (emptySlot != -1)
                    {
                        animationManager.StartFloorOutroAnimation("floor_" + emptySlot);
                        _waitingForFloorOutro = true;
                        _floorOutroTriggered = true;
                    }
                    else
                    {
                        _centeringSequenceStarted = true;
                    }
                }

                if (_waitingForFloorOutro)
                {
                    bool isAnimating = animationManager.IsFloorAnimatingOut("floor_0") || animationManager.IsFloorAnimatingOut("floor_1");
                    if (!isAnimating)
                    {
                        _waitingForFloorOutro = false;
                        _centeringSequenceStarted = true;
                        _centeringDelayTimer = 0f;
                    }
                }
            }

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
                    _enemyVisualXPositions[enemy.CombatantID] = targetX;
                }
                else
                {
                    float currentX = _enemyVisualXPositions[enemy.CombatantID];
                    if (Math.Abs(currentX) < 1.0f || Math.Abs(currentX - targetX) > Global.VIRTUAL_WIDTH)
                    {
                        _enemyVisualXPositions[enemy.CombatantID] = targetX;
                    }
                    else
                    {
                        float damping = 1.0f - MathF.Exp(-ENEMY_POSITION_TWEEN_SPEED * dt);
                        _enemyVisualXPositions[enemy.CombatantID] = MathHelper.Lerp(currentX, targetX, damping);
                    }
                }
            }

            var allOnFieldEnemies = enemies.Where(c => c.IsActiveOnField).ToList();
            var keepIds = allOnFieldEnemies.Select(e => e.CombatantID).ToHashSet();

            var keysToRemove = _enemyVisualXPositions.Keys.Where(k => !keepIds.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _enemyVisualXPositions.Remove(key);
            }

            if (visualEnemies.Count > 0)
            {
                _hasInitializedPositions = true;
            }
        }

        private void UpdatePlayerPositions(float dt, IEnumerable<BattleCombatant> combatants, BattleAnimationManager animationManager)
        {
            var players = combatants.Where(c => c.IsPlayerControlled).ToList();

            var activePlayers = players.Where(c => !c.IsDefeated && c.IsActiveOnField).ToList();
            var dyingPlayers = players.Where(c => animationManager.IsDeathAnimating(c.CombatantID)).ToList();
            var visualPlayers = activePlayers.Concat(dyingPlayers).Distinct().ToList();

            float centerX = Global.VIRTUAL_WIDTH / 2f;
            bool shouldCenter = visualPlayers.Count == 1;

            foreach (var player in visualPlayers)
            {
                float targetX;
                if (shouldCenter)
                {
                    targetX = centerX;
                }
                else
                {
                    targetX = BattleLayout.GetPlayerSpriteCenter(player.BattleSlot).X;
                }

                if (!_playerVisualXPositions.ContainsKey(player.CombatantID))
                {
                    _playerVisualXPositions[player.CombatantID] = targetX;
                }
                else
                {
                    float currentX = _playerVisualXPositions[player.CombatantID];
                    if (Math.Abs(currentX - targetX) < 1.0f)
                    {
                        _playerVisualXPositions[player.CombatantID] = targetX;
                    }
                    else
                    {
                        float damping = 1.0f - MathF.Exp(-ENEMY_POSITION_TWEEN_SPEED * dt);
                        _playerVisualXPositions[player.CombatantID] = MathHelper.Lerp(currentX, targetX, damping);
                    }
                }
            }

            var keepIds = visualPlayers.Select(p => p.CombatantID).ToHashSet();
            var keysToRemove = _playerVisualXPositions.Keys.Where(k => !keepIds.Contains(k)).ToList();
            foreach (var k in keysToRemove) _playerVisualXPositions.Remove(k);
        }

        private void DrawEnemies(SpriteBatch spriteBatch, List<BattleCombatant> activeEnemies, IEnumerable<BattleCombatant> allCombatants, BattleCombatant currentActor, bool shouldGrayOut, HashSet<BattleCombatant> selectable, BattleAnimationManager animManager, Dictionary<string, Color> silhouetteColors, Matrix transform, GameTime gameTime, BattleUIManager uiManager, BattleCombatant hoveredCombatant, bool isTargetingMode, Color? hoveredGroupColor, bool drawFloor = true, bool drawShadow = true, bool drawSprite = true, bool includeDying = true)
        {
            var dyingEnemies = allCombatants.Where(c => !c.IsPlayerControlled && animManager.IsDeathAnimating(c.CombatantID)).ToList();
            var floorEntities = new List<BattleCombatant>(activeEnemies);
            foreach (var dying in dyingEnemies)
            {
                if (!floorEntities.Contains(dying))
                {
                    floorEntities.Add(dying);
                }
            }

            bool hideEmptyFloors = _centeringSequenceStarted || ForceDrawCenterFloor;

            if (drawFloor)
            {
                if (hideEmptyFloors)
                {
                    if (floorEntities.Count == 0 || ForceDrawCenterFloor)
                    {
                        var center = BattleLayout.GetEnemyCenter();
                        int size = BattleLayout.ENEMY_SPRITE_SIZE_NORMAL;

                        float floorScale = 1.0f;

                        var centerIntro = animManager.GetFloorIntroAnimationState("floor_center");
                        if (centerIntro != null)
                        {
                            float progress = Math.Clamp(centerIntro.Timer / BattleAnimationManager.FloorIntroAnimationState.DURATION, 0f, 1f);
                            floorScale = Easing.EaseOutBack(progress);
                        }

                        _vfxRenderer.DrawFloor(spriteBatch, center, center.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 4, floorScale);
                    }
                    else
                    {
                        foreach (var enemy in floorEntities)
                        {
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

                            float floorScale = 1.0f;

                            var outroAnim = animManager.GetFloorOutroAnimationState("floor_center");
                            if (outroAnim != null)
                            {
                                float progress = Math.Clamp(outroAnim.Timer / BattleAnimationManager.FloorOutroAnimationState.DURATION, 0f, 1f);
                                floorScale = 1.0f - Easing.EaseInBack(progress);
                            }
                            else
                            {
                                var floorAnim = animManager.GetFloorIntroAnimationState("floor_center");
                                if (floorAnim != null)
                                {
                                    float progress = Math.Clamp(floorAnim.Timer / BattleAnimationManager.FloorIntroAnimationState.DURATION, 0f, 1f);
                                    floorScale = Easing.EaseOutBack(progress);
                                }
                            }

                            _vfxRenderer.DrawFloor(spriteBatch, center, center.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 4, floorScale);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var center = BattleLayout.GetEnemySlotCenter(i);
                        var occupant = floorEntities.FirstOrDefault(e => e.BattleSlot == i);
                        int size = (occupant != null && _spriteManager.IsMajorEnemySprite(occupant.ArchetypeId)) ? BattleLayout.ENEMY_SPRITE_SIZE_MAJOR : BattleLayout.ENEMY_SPRITE_SIZE_NORMAL;

                        float floorScale = 1.0f;
                        var outroAnim = animManager.GetFloorOutroAnimationState("floor_" + i);
                        if (outroAnim != null)
                        {
                            float progress = Math.Clamp(outroAnim.Timer / BattleAnimationManager.FloorOutroAnimationState.DURATION, 0f, 1f);
                            floorScale = 1.0f - Easing.EaseInBack(progress);
                        }
                        else
                        {
                            var floorAnim = animManager.GetFloorIntroAnimationState("floor_" + i);
                            if (floorAnim != null)
                            {
                                float progress = Math.Clamp(floorAnim.Timer / BattleAnimationManager.FloorIntroAnimationState.DURATION, 0f, 1f);
                                floorScale = Easing.EaseOutBack(progress);
                            }
                        }

                        _vfxRenderer.DrawFloor(spriteBatch, center, center.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 4, floorScale);
                    }

                    var centerIntro = animManager.GetFloorIntroAnimationState("floor_center");
                    if (centerIntro != null)
                    {
                        var centerPos = BattleLayout.GetEnemyCenter();
                        int size = BattleLayout.ENEMY_SPRITE_SIZE_NORMAL;

                        float progress = Math.Clamp(centerIntro.Timer / BattleAnimationManager.FloorIntroAnimationState.DURATION, 0f, 1f);
                        float floorScale = Easing.EaseOutBack(progress);

                        _vfxRenderer.DrawFloor(spriteBatch, centerPos, centerPos.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 4, floorScale);
                    }
                }
            }

            if (drawShadow || drawSprite)
            {
                var spritesToDraw = new List<BattleCombatant>(activeEnemies);
                if (includeDying)
                {
                    spritesToDraw.AddRange(dyingEnemies);
                }

                foreach (var enemy in spritesToDraw)
                {
                    float visualX = _enemyVisualXPositions.ContainsKey(enemy.CombatantID) ? _enemyVisualXPositions[enemy.CombatantID] : BattleLayout.GetEnemySlotCenter(enemy.BattleSlot).X;
                    var center = new Vector2(visualX, BattleLayout.ENEMY_SLOT_Y_OFFSET);

                    Color? assignedColor = silhouetteColors.ContainsKey(enemy.CombatantID) ? silhouetteColors[enemy.CombatantID] : null;
                    bool isSelectable = selectable.Contains(enemy);

                    bool isHovered = (hoveredCombatant == enemy);
                    bool isGroupHovered = assignedColor.HasValue && hoveredGroupColor.HasValue && assignedColor.Value == hoveredGroupColor.Value;

                    bool isSilhouetted = shouldGrayOut && !isSelectable;
                    Color silhouetteColor = _global.Palette_DarkShadow;
                    Color outlineColor = (enemy == currentActor) ? _global.Palette_Sun : Color.Transparent;

                    if (assignedColor.HasValue)
                    {
                        if (isTargetingMode)
                        {
                            outlineColor = assignedColor.Value;
                            if (isGroupHovered)
                            {
                                isSilhouetted = true;
                                silhouetteColor = assignedColor.Value;
                            }
                            else
                            {
                                isSilhouetted = false;
                            }
                        }
                        else
                        {
                            isSilhouetted = true;
                            silhouetteColor = assignedColor.Value;
                            outlineColor = Color.Transparent;
                        }
                    }
                    else if (isSelectable)
                    {
                        outlineColor = _global.Palette_Sun;
                    }

                    if (isHovered && !shouldGrayOut && !assignedColor.HasValue)
                    {
                        isSilhouetted = false;
                        outlineColor = _global.HoveredCombatantOutline;
                    }

                    outlineColor = outlineColor * enemy.VisualAlpha;

                    var spawnAnim = animManager.GetSpawnAnimationState(enemy.CombatantID);
                    var switchOut = animManager.GetSwitchOutAnimationState(enemy.CombatantID);
                    var switchIn = animManager.GetSwitchInAnimationState(enemy.CombatantID);
                    var healFlash = animManager.GetHealFlashAnimationState(enemy.CombatantID);
                    var hitFlash = animManager.GetHitFlashState(enemy.CombatantID);
                    var healBounce = animManager.GetHealBounceAnimationState(enemy.CombatantID);
                    var introSlide = animManager.GetIntroSlideAnimationState(enemy.CombatantID);
                    var attackCharge = animManager.GetAttackChargeState(enemy.CombatantID);

                    float spawnY = 0f;
                    float alpha = enemy.VisualAlpha;
                    float silhouetteAmt = enemy.VisualSilhouetteAmount;
                    Vector2 slideOffset = Vector2.Zero;
                    Vector2 chargeOffset = Vector2.Zero;
                    Vector2 chargeScale = Vector2.One;

                    if (isSilhouetted && spawnAnim == null && switchOut == null && switchIn == null && introSlide == null)
                    {
                        silhouetteAmt = 1.0f;
                    }

                    if (attackCharge != null)
                    {
                        chargeOffset = attackCharge.Offset;
                        chargeScale = attackCharge.Scale;
                    }

                    if (introSlide != null)
                    {
                        slideOffset = introSlide.CurrentOffset;

                        if (introSlide.IsEnemy)
                        {
                            if (introSlide.CurrentPhase == BattleAnimationManager.IntroSlideAnimationState.Phase.Sliding)
                            {
                                silhouetteAmt = 1.0f;
                                silhouetteColor = _global.Palette_DarkShadow;
                            }
                            else if (introSlide.CurrentPhase == BattleAnimationManager.IntroSlideAnimationState.Phase.Waiting)
                            {
                                silhouetteAmt = 1.0f;
                                silhouetteColor = _global.Palette_DarkShadow;
                            }
                            else if (introSlide.CurrentPhase == BattleAnimationManager.IntroSlideAnimationState.Phase.Revealing)
                            {
                                float revealProgress = Math.Clamp(introSlide.RevealTimer / BattleAnimationManager.IntroSlideAnimationState.REVEAL_DURATION, 0f, 1f);
                                silhouetteAmt = 1.0f - Easing.EaseInQuad(revealProgress);
                                silhouetteColor = _global.Palette_DarkShadow;
                            }
                        }
                    }
                    else if (spawnAnim != null)
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
                        if (switchOut.IsEnemy)
                        {
                            if (switchOut.CurrentPhase == BattleAnimationManager.SwitchOutAnimationState.Phase.Silhouetting)
                            {
                                float p = Math.Clamp(switchOut.SilhouetteTimer / BattleAnimationManager.SwitchOutAnimationState.SILHOUETTE_DURATION, 0f, 1f);
                                silhouetteAmt = Easing.EaseOutQuad(p);
                                silhouetteColor = _global.Palette_DarkShadow;
                            }
                            else if (switchOut.CurrentPhase == BattleAnimationManager.SwitchOutAnimationState.Phase.Lifting)
                            {
                                float p = Math.Clamp(switchOut.LiftTimer / BattleAnimationManager.SwitchOutAnimationState.LIFT_DURATION, 0f, 1f);
                                float eased = Easing.EaseInCubic(p);
                                spawnY = MathHelper.Lerp(0f, -BattleAnimationManager.SwitchOutAnimationState.LIFT_HEIGHT, eased);
                                alpha = 1.0f - eased;
                                silhouetteAmt = 1.0f;
                                silhouetteColor = _global.Palette_DarkShadow;
                            }
                        }
                        else
                        {
                            float p = Math.Clamp(switchOut.Timer / BattleAnimationManager.SwitchOutAnimationState.DURATION, 0f, 1f);
                            spawnY = MathHelper.Lerp(0f, -BattleAnimationManager.SwitchOutAnimationState.SIMPLE_LIFT_HEIGHT, Easing.EaseOutCubic(p));
                            alpha = 1.0f - Easing.EaseOutCubic(p);
                        }
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
                        tint = Color.Lerp(Color.White, _global.Palette_Leaf, (1f - Easing.EaseOutQuad(p)) * 0.8f);
                    }

                    var hitstop = animManager.GetHitstopVisualState(enemy.CombatantID);
                    Vector2 scale = Vector2.One;
                    if (_hitstopManager.IsActive && hitstop != null)
                    {
                        scale = Vector2.One;
                        tint = hitstop.IsCrit ? Color.Red : Color.White;
                        alpha = 1.0f;
                        silhouetteAmt = 0f;
                    }

                    if (_enemySquashScales.TryGetValue(enemy.CombatantID, out var squashScale))
                    {
                        scale = squashScale;
                    }

                    scale *= chargeScale;

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
                        (int)(center.X - spriteSize / 2f + recoil.X + slideOffset.X + chargeOffset.X),
                        (int)(center.Y + bob + spawnY + recoil.Y + slideOffset.Y + chargeOffset.Y),
                        spriteSize, spriteSize
                    );

                    if (drawSprite)
                    {
                        Vector2[] offsets = _enemySpritePartOffsets.TryGetValue(enemy.CombatantID, out var o) ? o : null;

                        bool isHighlighted = assignedColor.HasValue && !isTargetingMode;

                        Color? lowHealthOverlay = null;
                        if (enemy.LowHealthFlashTimer > 0f && !enemy.IsDefeated)
                        {
                            float patternLen = _global.LowHealthFlashPatternLength;
                            float cycleT = enemy.LowHealthFlashTimer % patternLen;
                            float rawAlpha = 0f;

                            if (cycleT < 1.0f)
                                rawAlpha = 1.0f - cycleT;
                            else if (cycleT >= 1.0f && cycleT < 2.0f)
                                rawAlpha = 1.0f - (cycleT - 1.0f);

                            float flashAlpha = rawAlpha;

                            if (flashAlpha > 0)
                                lowHealthOverlay = _global.LowHealthFlashColor * flashAlpha;
                        }

                        _entityRenderer.DrawEnemy(spriteBatch, enemy, spriteRect, offsets, shake, alpha, silhouetteAmt, silhouetteColor, isHighlighted, assignedColor, outlineColor, flashWhite, tint * alpha, scale, transform, lowHealthOverlay);

                        if (!enemy.IsDefeated)
                        {
                            var topOffsets = _spriteManager.GetEnemySpriteTopPixelOffsets(enemy.ArchetypeId);
                            var leftOffsets = _spriteManager.GetEnemySpriteLeftPixelOffsets(enemy.ArchetypeId);
                            var rightOffsets = _spriteManager.GetEnemySpriteRightPixelOffsets(enemy.ArchetypeId);
                            var bottomOffsets = _spriteManager.GetEnemySpriteBottomPixelOffsets(enemy.ArchetypeId);

                            Rectangle hitBox;

                            if (topOffsets != null && topOffsets.Length > 0)
                            {
                                int minX = int.MaxValue;
                                int minY = int.MaxValue;
                                int maxX = int.MinValue;
                                int maxY = int.MinValue;
                                bool foundAny = false;

                                for (int i = 0; i < topOffsets.Length; i++)
                                {
                                    int top = topOffsets[i];
                                    int left = leftOffsets[i];
                                    int right = rightOffsets[i];
                                    int bottom = bottomOffsets[i];

                                    if (top != int.MaxValue)
                                    {
                                        foundAny = true;
                                        if (left < minX) minX = left;
                                        if (top < minY) minY = top;
                                        if (right > maxX) maxX = right;
                                        if (bottom > maxY) maxY = bottom;
                                    }
                                }

                                if (foundAny)
                                {
                                    int frameX = (int)(center.X - spriteSize / 2f);
                                    int frameY = (int)center.Y;

                                    int x = frameX + minX;
                                    int y = frameY + minY;
                                    int w = (maxX - minX) + 1;
                                    int h = (maxY - minY) + 1;

                                    hitBox = GetPatternAlignedRect(new Rectangle(x, y, w, h));
                                }
                                else
                                {
                                    hitBox = GetPatternAlignedRect(new Rectangle((int)(center.X - spriteSize / 2f), (int)(center.Y), spriteSize, spriteSize));
                                }
                            }
                            else
                            {
                                hitBox = GetPatternAlignedRect(new Rectangle((int)(center.X - spriteSize / 2f), (int)(center.Y), spriteSize, spriteSize));
                            }

                            _currentTargets.Add(new TargetInfo { Combatant = enemy, Bounds = hitBox });
                            _combatantVisualCenters[enemy.CombatantID] = hitBox.Center.ToVector2();
                            _combatantStaticCenters[enemy.CombatantID] = center;

                            bool showHP = false;
                            bool showMana = false;

                            if (uiManager.HoveredMove != null)
                            {
                                AnalyzeMoveImpact(uiManager.HoveredMove, currentActor, enemy, out bool affectsHP);
                                showHP = affectsHP;
                            }
                            else if ((hoveredCombatant == enemy) || (uiManager.HoveredCombatantFromUI == enemy) || selectable.Contains(enemy))
                            {
                                showHP = true;
                                showMana = true;
                            }

                            UpdateBarAlpha(enemy, (float)gameTime.ElapsedGameTime.TotalSeconds, showHP);

                            float visualCenterY = center.Y + spriteSize / 2f;
                            float tooltipTopY = visualCenterY - 3;
                            if (tooltipTopY < 5) tooltipTopY = 5;
                            float spriteTopY = GetEnemyStaticVisualTop(enemy, center.Y);
                            float anchorY = Math.Min(tooltipTopY - 2, spriteTopY);
                            float barY = anchorY - 4;

                            // Mirroring Logic for Enemies
                            bool isRightAligned = (enemy.BattleSlot % 2 != 0);
                            float barX;
                            if (isRightAligned)
                                barX = center.X + 24f;
                            else
                                barX = center.X - 24f - BattleLayout.ENEMY_BAR_WIDTH;

                            float barBottomY = barY + 4;
                            _combatantBarBottomYs[enemy.CombatantID] = barBottomY;

                            _combatantBarPositions[enemy.CombatantID] = new Vector2(barX, barY);
                        }
                    }
                }
            }
        }

        private void DrawPlayers(SpriteBatch spriteBatch, BitmapFont font, List<BattleCombatant> players, BattleCombatant currentActor, bool shouldGrayOut, HashSet<BattleCombatant> selectable, BattleAnimationManager animManager, Dictionary<string, Color> silhouetteColors, GameTime gameTime, BattleUIManager uiManager, BattleCombatant hoveredCombatant, bool isTargetingMode, Color? hoveredGroupColor, bool drawFloor = true, bool drawShadow = true, bool drawSprite = true)
        {
            foreach (var player in players)
            {
                // Use dynamic visual position if available, else fallback to static layout
                float visualX = _playerVisualXPositions.ContainsKey(player.CombatantID) ? _playerVisualXPositions[player.CombatantID] : BattleLayout.GetPlayerSpriteCenter(player.BattleSlot).X;
                var spriteCenter = new Vector2(visualX, BattleLayout.PLAYER_HEART_CENTER_Y);

                float targetOffset = (player == currentActor) ? 0f : INACTIVE_Y_OFFSET;
                if (!_turnActiveOffsets.ContainsKey(player.CombatantID)) _turnActiveOffsets[player.CombatantID] = INACTIVE_Y_OFFSET;
                float current = _turnActiveOffsets[player.CombatantID];
                float damping = 1.0f - MathF.Exp(-ACTIVE_TWEEN_SPEED * (float)gameTime.ElapsedGameTime.TotalSeconds);
                _turnActiveOffsets[player.CombatantID] = MathHelper.Lerp(current, targetOffset, damping);

                float yOffset = _turnActiveOffsets[player.CombatantID];
                spriteCenter.Y += yOffset;

                if (player.BattleSlot == 0) PlayerSpritePosition = spriteCenter;

                Color? assignedColor = silhouetteColors.ContainsKey(player.CombatantID) ? silhouetteColors[player.CombatantID] : null;
                bool isSelectable = selectable.Contains(player);

                bool isHovered = (hoveredCombatant == player);
                bool isGroupHovered = assignedColor.HasValue && hoveredGroupColor.HasValue && assignedColor.Value == hoveredGroupColor.Value;

                bool isSilhouetted = shouldGrayOut && !isSelectable;
                Color silhouetteColor = _global.Palette_DarkShadow;
                Color outlineColor = (player == currentActor) ? _global.Palette_Sun : Color.Transparent;

                if (assignedColor.HasValue)
                {
                    if (isTargetingMode)
                    {
                        outlineColor = assignedColor.Value;
                        if (isGroupHovered)
                        {
                            isSilhouetted = true;
                            silhouetteColor = assignedColor.Value;
                        }
                        else
                        {
                            isSilhouetted = false;
                        }
                    }
                    else
                    {
                        isSilhouetted = true;
                        silhouetteColor = assignedColor.Value;
                        outlineColor = Color.Transparent;
                    }
                }
                else if (isSelectable)
                {
                    outlineColor = _global.Palette_Sun;
                }

                if (isHovered && !shouldGrayOut && !assignedColor.HasValue)
                {
                    isSilhouetted = false;
                    outlineColor = _global.HoveredCombatantOutline;
                }

                outlineColor = outlineColor * player.VisualAlpha;

                var spawnAnim = animManager.GetSpawnAnimationState(player.CombatantID);
                var switchOut = animManager.GetSwitchOutAnimationState(player.CombatantID);
                var switchIn = animManager.GetSwitchInAnimationState(player.CombatantID);
                var healFlash = animManager.GetHealFlashAnimationState(player.CombatantID);
                var hitFlash = animManager.GetHitFlashState(player.CombatantID);
                var healBounce = animManager.GetHealBounceAnimationState(player.CombatantID);
                var introSlide = animManager.GetIntroSlideAnimationState(player.CombatantID);
                var attackCharge = animManager.GetAttackChargeState(player.CombatantID);

                float spawnY = 0f;
                float alpha = player.VisualAlpha;
                float scale = 1.0f;
                float rotation = 0f;
                Vector2 slideOffset = Vector2.Zero;
                Vector2 chargeOffset = Vector2.Zero;
                Vector2 chargeScale = Vector2.One;

                if (attackCharge != null)
                {
                    chargeOffset = attackCharge.Offset;
                    chargeScale = attackCharge.Scale;
                }

                if (introSlide != null)
                {
                    slideOffset = introSlide.CurrentOffset;
                }
                else if (switchOut != null)
                {
                    float p = Math.Clamp(switchOut.Timer / BattleAnimationManager.SwitchOutAnimationState.DURATION, 0f, 1f);
                    spawnY = MathHelper.Lerp(0f, -BattleAnimationManager.SwitchOutAnimationState.SIMPLE_LIFT_HEIGHT, Easing.EaseInCubic(p));
                    alpha = 1.0f - Easing.EaseOutCubic(p);
                }
                else if (switchIn != null)
                {
                    float p = Math.Clamp(switchIn.Timer / BattleAnimationManager.SwitchInAnimationState.DURATION, 0f, 1f);
                    spawnY = MathHelper.Lerp(-BattleAnimationManager.SwitchInAnimationState.DROP_HEIGHT, 0f, Easing.EaseOutCubic(p));
                    alpha = Easing.EaseOutCubic(p);
                }

                Color tint = Color.White * alpha;
                if (healFlash != null)
                {
                    float p = healFlash.Timer / BattleAnimationManager.HealFlashAnimationState.Duration;
                    tint = Color.Lerp(tint, _global.Palette_Leaf * alpha, (1f - Easing.EaseOutQuad(p)) * 0.8f);
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
                sprite.SetPosition(new Vector2(spriteCenter.X, spriteCenter.Y + bob + spawnY) + recoil + slideOffset + chargeOffset);

                bool isHighlightedSprite = assignedColor.HasValue && !isTargetingMode;
                float pulse = 0f;

                if (drawFloor)
                {
                    float floorScale = 1.0f;
                    var floorOutro = animManager.GetFloorOutroAnimationState($"player_floor_{player.BattleSlot}");
                    if (floorOutro != null)
                    {
                        float progress = Math.Clamp(floorOutro.Timer / BattleAnimationManager.FloorOutroAnimationState.DURATION, 0f, 1f);
                        floorScale = 1.0f - Easing.EaseInBack(progress);
                    }

                    // Use the dynamic center for the floor too
                    var floorCenter = new Vector2(visualX, BattleLayout.PLAYER_HEART_CENTER_Y);
                    _vfxRenderer.DrawPlayerFloor(spriteBatch, floorCenter + slideOffset, player.VisualAlpha, floorScale);
                }

                if (drawSprite)
                {
                    Color? lowHealthOverlay = null;
                    if (player.LowHealthFlashTimer > 0f && !player.IsDefeated)
                    {
                        float patternLen = _global.LowHealthFlashPatternLength;
                        float cycleT = player.LowHealthFlashTimer % patternLen;
                        float rawAlpha = 0f;

                        if (cycleT < 1.0f)
                            rawAlpha = 1.0f - cycleT;
                        else if (cycleT >= 1.0f && cycleT < 2.0f)
                            rawAlpha = 1.0f - (cycleT - 1.0f);

                        float flashAlpha = rawAlpha;

                        if (flashAlpha > 0)
                            lowHealthOverlay = _global.LowHealthFlashColor * flashAlpha;
                    }

                    sprite.Draw(spriteBatch, animManager, player, tint, isHighlightedSprite, pulse, isSilhouetted, silhouetteColor, gameTime, assignedColor, outlineColor, scale * chargeScale.X, lowHealthOverlay, rotation);

                    Rectangle bounds = new Rectangle((int)(spriteCenter.X - 16), (int)(spriteCenter.Y - 16), 32, 32);
                    Rectangle hitBox = GetPatternAlignedRect(bounds);

                    _currentTargets.Add(new TargetInfo { Combatant = player, Bounds = hitBox });
                    _combatantVisualCenters[player.CombatantID] = hitBox.Center.ToVector2();
                    _combatantStaticCenters[player.CombatantID] = spriteCenter;

                    bool showHP = false;
                    bool showMana = false;

                    if (uiManager.HoveredMove != null)
                    {
                        AnalyzeMoveImpact(uiManager.HoveredMove, currentActor, player, out bool affectsHP);
                        showHP = affectsHP;
                    }
                    else if ((hoveredCombatant == player) || (uiManager.HoveredCombatantFromUI == player) || selectable.Contains(player))
                    {
                        showHP = true;
                        showMana = true;
                    }

                    UpdateBarAlpha(player, (float)gameTime.ElapsedGameTime.TotalSeconds, showHP);

                    // Mirroring Logic for Players
                    bool isRightAligned = (player.BattleSlot % 2 != 0);
                    float barX;
                    if (isRightAligned)
                    {
                        barX = spriteCenter.X + 16f;
                    }
                    else
                    {
                        barX = spriteCenter.X - 16f - BattleLayout.PLAYER_BAR_WIDTH;
                    }

                    float barY = BattleLayout.PLAYER_BARS_TOP_Y + 4;

                    float barBottomY = barY + 4;
                    _combatantBarBottomYs[player.CombatantID] = barBottomY;

                    _combatantBarPositions[player.CombatantID] = new Vector2(barX, barY);
                }
            }
        }

        private void AnalyzeMoveImpact(MoveData move, BattleCombatant actor, BattleCombatant candidate, out bool affectsHP)
        {
            affectsHP = false;

            if (move == null || actor == null || candidate == null) return;

            bool isActor = actor == candidate;
            var battleManager = ServiceLocator.Get<BattleManager>();
            var validTargets = TargetingHelper.GetValidTargets(actor, move.Target, battleManager.AllCombatants);
            bool isTarget = validTargets.Contains(candidate);

            if (isActor)
            {
                if (move.AffectsUserHP) affectsHP = true;
            }

            if (isTarget)
            {
                if (move.Power > 0 || move.Effects.ContainsKey("Heal")) affectsHP = true;
                if (move.AffectsTargetHP) affectsHP = true;
            }
        }

        private void UpdateBarAlpha(BattleCombatant c, float dt, bool showHP)
        {
            c.VisualHealthBarAlpha = 1.0f;
            c.HealthBarDelayTimer = 0f;
            c.HealthBarDisappearTimer = 0f;
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
                            int dir = _random.Next(2);
                            offsets[i] = dir == 0 ? new Vector2(0, -1) : new Vector2(0, 1);
                        }
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
                    float damping = 1.0f - MathF.Exp(-ACTIVE_TWEEN_SPEED * dt);
                    _turnActiveOffsets[c.CombatantID] = MathHelper.Lerp(current, targetOffset, damping);
                }
            }
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
            if (!_combatantVisualCenters.TryGetValue(c.CombatantID, out var center)) return Vector2.Zero;

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

            float barY = tooltipTopY - 6;

            return new Vector2(center.X, barY);
        }

        public void DrawHUD(SpriteBatch spriteBatch, BattleAnimationManager animManager, GameTime gameTime, BattleUIManager uiManager, BattleCombatant currentActor)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();

            foreach (var combatant in battleManager.AllCombatants)
            {
                // Instantly hide HUD if defeated to prevent visual snapping during death animation
                if (combatant.IsDefeated) continue;

                if (!_combatantBarPositions.TryGetValue(combatant.CombatantID, out var pos)) continue;

                float barX = pos.X;
                float barY = pos.Y;

                if (combatant.VisualHealthBarAlpha <= 0.01f) continue;

                float hudAlpha = combatant.HudVisualAlpha;

                // Determine alignment: Slot 1 and 3 (Odd) are Right Aligned
                bool isRightAligned = (combatant.BattleSlot % 2 != 0);

                if (combatant.IsPlayerControlled)
                {
                    // --- NEW LOGIC: Idle Bob for Pending Action ---
                    float yOffset = 0f;
                    if (battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection)
                    {
                        // If action is NOT pending (not locked in), bob up and down
                        if (!battleManager.IsActionPending(combatant.BattleSlot))
                        {
                            float t = (float)gameTime.TotalGameTime.TotalSeconds;
                            // Invert phase for slot 1 (the second player) so they bob opposite to slot 0
                            float phase = (combatant.BattleSlot == 1) ? MathHelper.Pi : 0f;
                            yOffset = MathF.Sin(t * _bobSpeed + phase) * 0.5f;
                        }
                    }
                    // ----------------------------------------------

                    _hudRenderer.DrawStatusIcons(spriteBatch, combatant, barX, barY + yOffset, BattleLayout.PLAYER_BAR_WIDTH, true, _playerStatusIcons, GetStatusIconOffset, IsStatusIconAnimating, isRightAligned);
                    _hudRenderer.DrawPlayerBars(spriteBatch, combatant, barX, barY + yOffset, BattleLayout.PLAYER_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, combatant.VisualHealthBarAlpha * hudAlpha, gameTime, uiManager, combatant == currentActor, isRightAligned);
                }
                else
                {
                    if (!_enemyStatusIcons.ContainsKey(combatant.CombatantID))
                        _enemyStatusIcons[combatant.CombatantID] = new List<StatusIconInfo>();

                    _hudRenderer.DrawStatusIcons(spriteBatch, combatant, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, false, _enemyStatusIcons[combatant.CombatantID], GetStatusIconOffset, IsStatusIconAnimating, isRightAligned);
                    _hudRenderer.DrawEnemyBars(spriteBatch, combatant, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, combatant.VisualHealthBarAlpha * hudAlpha, gameTime, isRightAligned);
                }
            }
        }
    }
}