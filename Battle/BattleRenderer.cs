using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Systems;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI; // Added for TextAnimator
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static ProjectVagabond.Battle.Abilities.InflictStatusStunAbility;

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
        private readonly Core _core; // Added Core dependency for overlays

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
        private bool _floorOutroTriggered = false;
        private bool _waitingForFloorOutro = false;
        private bool _hasInitializedPositions = false; // Tracks if we've processed at least one frame with enemies

        // --- NEW: Forced Center Floor State ---
        public bool ForceDrawCenterFloor { get; set; } = false;

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

        // --- SQUASH AND STRETCH STATE (Enemies) ---
        private readonly Dictionary<string, Vector2> _enemySquashScales = new Dictionary<string, Vector2>();

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

        // --- Static Centers for Tooltips (Unaffected by bob/shake) ---
        private Dictionary<string, Vector2> _combatantStaticCenters = new Dictionary<string, Vector2>();

        // --- Cache for Bar Bottom Positions ---
        private Dictionary<string, float> _combatantBarBottomYs = new Dictionary<string, float>();

        // --- Cache for Bar Top-Left Positions (For HUD Layering) ---
        private readonly Dictionary<string, Vector2> _combatantBarPositions = new Dictionary<string, Vector2>();

        // --- TARGETING RETICLE CONTROLLER ---
        private class ReticleController
        {
            public Rectangle CurrentRect;
            public bool IsActive;

            public void Update(Rectangle targetRect)
            {
                // Instant snap logic - no tweening
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
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _hitstopManager = ServiceLocator.Get<HitstopManager>();
            _core = ServiceLocator.Get<Core>();

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
            ForceDrawCenterFloor = false; // Reset forced state
            _reticleController.Reset(); // Reset reticle
        }

        /// <summary>
        /// Manually sets the centering state. Used by BattleScene to prevent 1-frame glitches
        /// where the renderer defaults to 2-slot layout before detecting a single enemy.
        /// </summary>
        public void SetCenteringState(bool isCentered)
        {
            _centeringSequenceStarted = isCentered;
            // If we are forcing centered, we assume positions are initialized to avoid re-triggering logic
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

        public void TriggerAttackAnimation(string combatantId)
        {
            if (!_attackAnimControllers.ContainsKey(combatantId))
                _attackAnimControllers[combatantId] = new SpriteHopAnimationController();
            _attackAnimControllers[combatantId].Trigger();

            // --- JUICE: Stretch on Jump ---
            // If it's a player, trigger the stretch via the sprite class
            if (_playerSprites.TryGetValue(combatantId, out var sprite))
            {
                sprite.TriggerSquash(0.6f, 1.4f); // Noodle stretch
            }
            else
            {
                // If it's an enemy, set the scale directly
                _enemySquashScales[combatantId] = new Vector2(0.6f, 1.4f);
            }
        }

        public void TriggerRecoil(string combatantId, Vector2 direction, float magnitude)
        {
            if (!_recoilStates.ContainsKey(combatantId)) _recoilStates[combatantId] = new RecoilState();
            _recoilStates[combatantId].Velocity = direction * magnitude * 10f;

            // --- JUICE: Squash on Impact ---
            // Flatten the sprite (Wide X, Short Y)
            if (_playerSprites.TryGetValue(combatantId, out var sprite))
            {
                sprite.TriggerSquash(1.5f, 0.5f); // Pancake
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

            // Update Visibility Timers
            foreach (var c in combatants)
            {
                if (c.HealthBarVisibleTimer > 0) c.HealthBarVisibleTimer = Math.Max(0, c.HealthBarVisibleTimer - dt);
                if (c.ManaBarVisibleTimer > 0) c.ManaBarVisibleTimer = Math.Max(0, c.ManaBarVisibleTimer - dt);

                // --- LOW HEALTH FLASH LOGIC ---
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

            // Update Animations
            UpdateEnemyPositions(dt, combatants, animationManager);
            UpdateEnemyAnimations(dt, combatants);
            UpdateShadowAnimations(dt, combatants);
            UpdateRecoilAnimations(dt);
            UpdateStatusIconAnimations(dt);
            UpdateStatusIconTooltips(combatants);
            UpdateActiveTurnOffsets(dt, combatants, currentActor);
            UpdateEnemySquash(dt); // New

            foreach (var controller in _attackAnimControllers.Values) controller.Update(gameTime);

            foreach (var c in combatants)
            {
                if (c.IsPlayerControlled && _playerSprites.TryGetValue(c.CombatantID, out var sprite))
                {
                    sprite.Update(gameTime, currentActor == c);
                }
            }
        }

        private void UpdateEnemySquash(float dt)
        {
            // Recover enemy scales to (1,1)
            var keys = _enemySquashScales.Keys.ToList();
            foreach (var key in keys)
            {
                // FIX: Use Time-Corrected Damping for framerate independence
                float damping = 1.0f - MathF.Exp(-_global.SquashRecoverySpeed * dt);
                _enemySquashScales[key] = Vector2.Lerp(_enemySquashScales[key], Vector2.One, damping);
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
            _combatantBarPositions.Clear(); // Clear cached bar positions
            foreach (var list in _enemyStatusIcons.Values) list.Clear(); // Clear enemy icon trackers

            var enemies = allCombatants.Where(c => !c.IsPlayerControlled && c.IsActiveOnField).ToList();
            var players = allCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();

            // --- Stat Tooltip Logic ---
            UpdateStatTooltipState(hoveredCombatant, uiManager);

            // --- Targeting Logic ---
            var (selectableTargets, activeTargetType) = ResolveSelectableTargets(allCombatants, currentActor, uiManager);
            var silhouetteColors = ResolveSilhouetteColors(allCombatants, currentActor, selectableTargets, activeTargetType, uiManager, hoveredCombatant);
            bool shouldGrayOut = uiManager.UIState == BattleUIState.Targeting || (uiManager.HoveredMove != null && uiManager.HoveredMove.Target != TargetType.None);

            // --- IMPACT FLASH LOGIC ---
            var flashState = animationManager.GetImpactFlashState();
            if (flashState != null)
            {
                // 1. Draw Floors AND Shadows for EVERYONE (Bottom Layer)
                DrawEnemies(spriteBatch, enemies, allCombatants, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, transform, gameTime, uiManager, hoveredCombatant, drawFloor: true, drawShadow: true, drawSprite: false);
                DrawPlayers(spriteBatch, font, players, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, gameTime, uiManager, hoveredCombatant, drawFloor: true, drawShadow: true, drawSprite: false);

                // 2. Draw Non-Target Sprites (Middle Layer - Obscured)
                var nonTargetEnemies = enemies.Where(e => !flashState.TargetCombatantIDs.Contains(e.CombatantID)).ToList();
                var nonTargetPlayers = players.Where(p => !flashState.TargetCombatantIDs.Contains(p.CombatantID)).ToList();

                DrawEnemies(spriteBatch, nonTargetEnemies, allCombatants, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, transform, gameTime, uiManager, hoveredCombatant, drawFloor: false, drawShadow: false, drawSprite: true, includeDying: true);
                DrawPlayers(spriteBatch, font, nonTargetPlayers, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, gameTime, uiManager, hoveredCombatant, drawFloor: false, drawShadow: false, drawSprite: true);

                // --- 3. REQUEST FULLSCREEN OVERLAY FOR FLASH & TARGETS ---
                _core.RequestFullscreenOverlay((overlayBatch, uiMatrix) =>
                {
                    var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
                    int screenW = graphicsDevice.PresentationParameters.BackBufferWidth;
                    int screenH = graphicsDevice.PresentationParameters.BackBufferHeight;
                    var pixel = ServiceLocator.Get<Texture2D>();

                    // A. Draw Flash Rect
                    float alpha = Math.Clamp(flashState.Timer / flashState.Duration, 0f, 1f);
                    alpha = Easing.EaseOutQuad(alpha);

                    overlayBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.Identity);
                    overlayBatch.Draw(pixel, new Rectangle(0, 0, screenW, screenH), flashState.Color * alpha);
                    overlayBatch.End();

                    // B. Draw Target Sprites
                    var targetEnemies = enemies.Where(e => flashState.TargetCombatantIDs.Contains(e.CombatantID)).ToList();
                    var targetPlayers = players.Where(p => flashState.TargetCombatantIDs.Contains(p.CombatantID)).ToList();

                    overlayBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, uiMatrix);
                    DrawEnemies(overlayBatch, targetEnemies, allCombatants, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, uiMatrix, gameTime, uiManager, hoveredCombatant, drawFloor: false, drawShadow: false, drawSprite: true, includeDying: false);
                    DrawPlayers(overlayBatch, font, targetPlayers, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, gameTime, uiManager, hoveredCombatant, drawFloor: false, drawShadow: false, drawSprite: true);
                    overlayBatch.End();
                });
            }
            else
            {
                // Standard Draw
                DrawEnemies(spriteBatch, enemies, allCombatants, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, transform, gameTime, uiManager, hoveredCombatant);
                DrawPlayers(spriteBatch, font, players, currentActor, shouldGrayOut, selectableTargets, animationManager, silhouetteColors, gameTime, uiManager, hoveredCombatant);
            }

            // --- Draw Coins ---
            animationManager.DrawCoins(spriteBatch);

            // --- Draw Targeting Highlights (Dotted Rectangles) ---
            var effectiveFocus = hoveredCombatant ?? uiManager.HoveredCombatantFromUI;
            DrawTargetingHighlights(spriteBatch, uiManager, gameTime, silhouetteColors, effectiveFocus);

            // --- Draw HUD (Bars & Icons) - TOP LAYER ---
            DrawHUD(spriteBatch, animationManager, gameTime, uiManager, currentActor);

            // --- Draw UI Title ---
            DrawUITitle(spriteBatch, gameTime, uiManager.SubMenuState);

            // --- Draw Stat Tooltip ---
            if (_statTooltipAlpha > 0.01f && _statTooltipCombatantID != null)
            {
                var target = allCombatants.FirstOrDefault(c => c.CombatantID == _statTooltipCombatantID);
                if (target != null)
                {
                    bool hasInsight = allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.Abilities.Any(a => a is InsightAbility));

                    Vector2 center = Vector2.Zero;
                    if (_combatantStaticCenters.TryGetValue(target.CombatantID, out var staticPos))
                    {
                        center = staticPos;
                    }
                    else
                    {
                        center = GetCombatantVisualCenterPosition(target, allCombatants);
                    }

                    // Retrieve the cached bar bottom Y
                    float barBottomY = 0f;
                    if (_combatantBarBottomYs.TryGetValue(target.CombatantID, out float cachedY))
                    {
                        barBottomY = cachedY;
                    }
                    else
                    {
                        // Fallback if not cached (shouldn't happen if drawn)
                        barBottomY = center.Y;
                    }

                    _vfxRenderer.DrawStatChangeTooltip(spriteBatch, target, _statTooltipAlpha, hasInsight, center, barBottomY, gameTime);
                }
            }

            // NOTE: Ability Indicators are no longer drawn here.
            // They are drawn in BattleScene.cs to ensure they appear on top of the UI.
        }

        private void DrawTargetingHighlights(SpriteBatch spriteBatch, BattleUIManager uiManager, GameTime gameTime, Dictionary<string, Color> silhouetteColors, BattleCombatant focusedCombatant)
        {
            if (uiManager.HoveredMove == null)
            {
                _reticleController.Reset();
                return;
            }

            var targets = uiManager.HoverHighlightState.Targets;
            if (targets == null || !targets.Any())
            {
                _reticleController.Reset();
                return;
            }

            IEnumerable<BattleCombatant> targetsToDraw = targets;
            bool isTargetingMode = uiManager.UIState == BattleUIState.Targeting;
            bool isMulti = false;

            if (isTargetingMode)
            {
                var type = uiManager.TargetTypeForSelection ?? TargetType.None;
                isMulti = type == TargetType.All || type == TargetType.Both || type == TargetType.Every ||
                          type == TargetType.Team || type == TargetType.RandomAll || type == TargetType.RandomBoth || type == TargetType.RandomEvery;

                if (!isMulti)
                {
                    // Single Target Mode: Only draw if we have a focused combatant
                    if (focusedCombatant != null && targets.Contains(focusedCombatant))
                    {
                        targetsToDraw = new List<BattleCombatant> { focusedCombatant };
                    }
                    else
                    {
                        targetsToDraw = Enumerable.Empty<BattleCombatant>();
                    }
                }
            }
            else
            {
                // Preview Mode (Hovering Move Button)
                var type = uiManager.HoveredMove.Target;
                isMulti = type == TargetType.All || type == TargetType.Both || type == TargetType.Every ||
                          type == TargetType.Team || type == TargetType.RandomAll || type == TargetType.RandomBoth || type == TargetType.RandomEvery;

                if (!isMulti)
                {
                    // Single Target Preview: Cycle through targets
                    var activeTarget = targets.FirstOrDefault(t => silhouetteColors.ContainsKey(t.CombatantID));
                    if (activeTarget != null)
                    {
                        targetsToDraw = new List<BattleCombatant> { activeTarget };
                    }
                    else
                    {
                        targetsToDraw = Enumerable.Empty<BattleCombatant>();
                    }
                }
            }

            var pixel = ServiceLocator.Get<Texture2D>();
            float offset = (float)gameTime.TotalGameTime.TotalSeconds * 20f;
            const float dashLength = 4f;
            const float gapLength = 4f;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (isMulti)
            {
                // Multi-target: Draw all instantly (no sliding reticle)
                _reticleController.Reset();

                foreach (var target in targetsToDraw)
                {
                    if (!silhouetteColors.ContainsKey(target.CombatantID)) continue;

                    var targetInfo = _currentTargets.FirstOrDefault(t => t.Combatant == target);
                    if (targetInfo.Combatant != null)
                    {
                        DrawDottedBox(spriteBatch, pixel, targetInfo.Bounds, _global.Palette_Sun, offset, dashLength, gapLength);
                    }
                }
            }
            else
            {
                // Single-target: Use sliding reticle via Controller
                var target = targetsToDraw.FirstOrDefault();

                if (target != null)
                {
                    var targetInfo = _currentTargets.FirstOrDefault(t => t.Combatant == target);
                    if (targetInfo.Combatant != null)
                    {
                        // Update Controller
                        _reticleController.Update(targetInfo.Bounds);

                        // Draw using bounds
                        DrawDottedBox(spriteBatch, pixel, _reticleController.CurrentRect, _global.Palette_Sun, offset, dashLength, gapLength);
                    }
                }
                else
                {
                    _reticleController.Reset();
                }
            }
        }

        private void DrawDottedBox(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, float offset, float dashLength, float gapLength)
        {
            // --- SQUARED OUTLINE LOGIC ---
            // Draw the exact same dotted rectangle shifted 1 pixel in all 8 directions (cardinals + diagonals)
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;

                    var outlineRect = new Rectangle(rect.X + x, rect.Y + y, rect.Width, rect.Height);
                    spriteBatch.DrawAnimatedDottedRectangle(pixel, outlineRect, _global.Palette_Black, 1f, dashLength, gapLength, offset);
                }
            }

            // Draw Main Colored Rectangle
            spriteBatch.DrawAnimatedDottedRectangle(pixel, rect, color, 1f, dashLength, gapLength, offset);
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

        private void UpdateStatTooltipState(BattleCombatant hoveredCombatant, BattleUIManager uiManager)
        {
            bool isDefaultUI = uiManager.UIState == BattleUIState.Default;

            if (isDefaultUI && hoveredCombatant != null)
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

            if (uiManager.UIState == BattleUIState.Targeting)
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
            bool isTargeting = uiManager.UIState == BattleUIState.Targeting;

            BattleCombatant effectiveHover = hoveredCombatant ?? uiManager.HoveredCombatantFromUI;

            if (isTargeting)
            {
                // Only assign colors to actively highlighted targets.
                // Selectable but non-highlighted targets get NO entry in the dictionary (so they draw normally).

                if (effectiveHover != null && selectable.Contains(effectiveHover))
                {
                    bool isMulti = targetType == TargetType.Every || targetType == TargetType.Both || targetType == TargetType.All || targetType == TargetType.Team;
                    if (isMulti)
                    {
                        // Multi-target: Highlight everyone in the selection set
                        foreach (var c in selectable) colors[c.CombatantID] = _global.Palette_Sun;
                    }
                    else
                    {
                        // Single-target: Highlight only the hovered one
                        colors[effectiveHover.CombatantID] = _global.Palette_Sun;
                    }
                }
            }
            else if (selectable.Any() && targetType.HasValue)
            {
                // Hovering a move button (Preview Mode)
                float timer = uiManager.HoverHighlightState.Timer;
                var sorted = allCombatants.Where(c => selectable.Contains(c))
                    .OrderBy(c => c.IsPlayerControlled ? 1 : 0)
                    .ThenBy(c => c.IsPlayerControlled ? (c == currentActor ? 1 : 0) : 0)
                    .ThenBy(c => c.BattleSlot).ToList();

                bool isMulti = targetType == TargetType.Every || targetType == TargetType.Both || targetType == TargetType.All || targetType == TargetType.Team;

                if (isMulti)
                {
                    // Multi-target preview: Constant highlight
                    foreach (var t in sorted) colors[t.CombatantID] = _global.Palette_Sun;
                }
                else if (sorted.Count > 0)
                {
                    // Single-target cycling preview
                    float cycle = sorted.Count * _global.TargetingSingleCycleSpeed;
                    int idx = (int)((timer % cycle) / _global.TargetingSingleCycleSpeed);
                    idx = Math.Clamp(idx, 0, sorted.Count - 1);

                    // Only the currently cycled target gets the Sun highlight.
                    // Others get nothing (normal sprite).
                    colors[sorted[idx].CombatantID] = _global.Palette_Sun;
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
            bool isActionSelection = battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot1;

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
                        // FIX: Use Time-Corrected Damping for framerate independence
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

        private void DrawEnemies(SpriteBatch spriteBatch, List<BattleCombatant> activeEnemies, IEnumerable<BattleCombatant> allCombatants, BattleCombatant currentActor, bool shouldGrayOut, HashSet<BattleCombatant> selectable, BattleAnimationManager animManager, Dictionary<string, Color> silhouetteColors, Matrix transform, GameTime gameTime, BattleUIManager uiManager, BattleCombatant hoveredCombatant, bool drawFloor = true, bool drawShadow = true, bool drawSprite = true, bool includeDying = true)
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

            // --- MODIFIED: Check ForceDrawCenterFloor ---
            bool hideEmptyFloors = _centeringSequenceStarted || ForceDrawCenterFloor;

            if (drawFloor)
            {
                if (hideEmptyFloors)
                {
                    // If forced or no entities, draw center floor
                    if (floorEntities.Count == 0 || ForceDrawCenterFloor)
                    {
                        var center = BattleLayout.GetEnemyCenter();
                        int size = BattleLayout.ENEMY_SPRITE_SIZE_NORMAL;

                        float floorScale = 1.0f;

                        // Check for center intro animation
                        var centerIntro = animManager.GetFloorIntroAnimationState("floor_center");
                        if (centerIntro != null)
                        {
                            float progress = Math.Clamp(centerIntro.Timer / BattleAnimationManager.FloorIntroAnimationState.DURATION, 0f, 1f);
                            floorScale = Easing.EaseOutBack(progress);
                        }

                        _vfxRenderer.DrawFloor(spriteBatch, center, center.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 12, floorScale);
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

                            // CHECK OUTRO FIRST
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

                            _vfxRenderer.DrawFloor(spriteBatch, center, center.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 12, floorScale);
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

                        _vfxRenderer.DrawFloor(spriteBatch, center, center.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 12, floorScale);
                    }

                    // --- NEW: Check for Center Floor Animation even in multi-slot mode ---
                    // This allows the center floor to animate IN while the side floors animate OUT during the loot transition.
                    var centerIntro = animManager.GetFloorIntroAnimationState("floor_center");
                    if (centerIntro != null)
                    {
                        var centerPos = BattleLayout.GetEnemyCenter();
                        // Use a default size since we don't have a specific enemy to reference
                        int size = BattleLayout.ENEMY_SPRITE_SIZE_NORMAL;

                        float progress = Math.Clamp(centerIntro.Timer / BattleAnimationManager.FloorIntroAnimationState.DURATION, 0f, 1f);
                        float floorScale = Easing.EaseOutBack(progress);

                        _vfxRenderer.DrawFloor(spriteBatch, centerPos, centerPos.Y + size + BattleLayout.ENEMY_SLOT_Y_OFFSET - 12, floorScale);
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

                    Color? highlight = silhouetteColors.ContainsKey(enemy.CombatantID) ? silhouetteColors[enemy.CombatantID] : null;
                    bool isSelectable = selectable.Contains(enemy);
                    bool isSilhouetted = shouldGrayOut && !isSelectable;

                    if (enemy.CombatantID == _statTooltipCombatantID && _statTooltipAlpha > 0) isSilhouetted = true;

                    Color silhouetteColor = isSilhouetted ? _global.Palette_DarkShadow : _global.Palette_DarkShadow;

                    // --- MODIFIED: Outline Color Logic ---
                    // Only the active actor gets the Sun outline. Everyone else gets Transparent (no outer outline).
                    Color outlineColor = (enemy == currentActor) ? _global.Palette_Sun : Color.Transparent;

                    // Hide outline if showing stat tooltip to improve readability
                    if (enemy.CombatantID == _statTooltipCombatantID && _statTooltipAlpha > 0)
                    {
                        outlineColor = Color.Transparent;
                    }

                    outlineColor = outlineColor * enemy.VisualAlpha;

                    var spawnAnim = animManager.GetSpawnAnimationState(enemy.CombatantID);
                    var switchOut = animManager.GetSwitchOutAnimationState(enemy.CombatantID);
                    var switchIn = animManager.GetSwitchInAnimationState(enemy.CombatantID);
                    var healFlash = animManager.GetHealFlashAnimationState(enemy.CombatantID);
                    var hitFlash = animManager.GetHitFlashState(enemy.CombatantID);
                    var healBounce = animManager.GetHealBounceAnimationState(enemy.CombatantID);
                    var introSlide = animManager.GetIntroSlideAnimationState(enemy.CombatantID);

                    float spawnY = 0f;
                    float alpha = enemy.VisualAlpha;
                    float silhouetteAmt = enemy.VisualSilhouetteAmount;
                    Vector2 slideOffset = Vector2.Zero;

                    if (isSilhouetted && spawnAnim == null && switchOut == null && switchIn == null && introSlide == null)
                    {
                        silhouetteAmt = 1.0f;
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

                    // Apply Squash and Stretch
                    if (_enemySquashScales.TryGetValue(enemy.CombatantID, out var squashScale))
                    {
                        scale = squashScale;
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
                        (int)(center.X - spriteSize / 2f + recoil.X + slideOffset.X),
                        (int)(center.Y + bob + spawnY + recoil.Y + slideOffset.Y),
                        spriteSize, spriteSize
                    );

                    if (drawShadow && silhouetteAmt < 1.0f)
                    {
                        // --- UPDATED: Shift shadow up by 4 pixels ---
                        float groundY = center.Y + spriteSize - 4;
                        float heightFactor = 1.0f - Math.Clamp(Math.Abs(spawnY) / 50f, 0f, 1f);
                        Vector2 shadowAnim = _shadowOffsets.TryGetValue(enemy.CombatantID, out var s) ? s : Vector2.Zero;
                        _vfxRenderer.DrawShadow(spriteBatch, new Vector2(spriteRect.Center.X, groundY), heightFactor * alpha, shadowAnim);
                    }

                    if (drawSprite)
                    {
                        Vector2[] offsets = _enemySpritePartOffsets.TryGetValue(enemy.CombatantID, out var o) ? o : null;

                        // --- FIX: Only highlight if explicitly set in the dictionary ---
                        bool isHighlighted = highlight.HasValue;

                        Color? lowHealthOverlay = null;
                        if (enemy.LowHealthFlashTimer > 0f && !enemy.IsDefeated)
                        {
                            // Double Flash Pattern: Flash -> Flash -> Pause
                            // Cycle length is tunable via Global
                            float patternLen = _global.LowHealthFlashPatternLength;
                            float cycleT = enemy.LowHealthFlashTimer % patternLen;
                            float rawAlpha = 0f;

                            if (cycleT < 1.0f)
                                rawAlpha = 1.0f - cycleT; // First Flash (Fade Out)
                            else if (cycleT >= 1.0f && cycleT < 2.0f)
                                rawAlpha = 1.0f - (cycleT - 1.0f); // Second Flash (Fade Out)
                            // Else: Pause (0 alpha)

                            // Apply max intensity (0.6)
                            float flashAlpha = rawAlpha * 0.6f;

                            if (flashAlpha > 0)
                                lowHealthOverlay = _global.LowHealthFlashColor * flashAlpha;
                        }

                        _entityRenderer.DrawEnemy(spriteBatch, enemy, spriteRect, offsets, shake, alpha, silhouetteAmt, silhouetteColor, isHighlighted, highlight, outlineColor, flashWhite, tint * alpha, scale, transform, lowHealthOverlay);

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
                            _combatantStaticCenters[enemy.CombatantID] = new Vector2(center.X, center.Y + spriteSize / 2f);

                            // --- NEW: Smart Bar Visibility Logic ---
                            bool showHP = false;
                            bool showMana = false;

                            // 1. Check if hovering a move
                            if (uiManager.HoveredMove != null)
                            {
                                AnalyzeMoveImpact(uiManager.HoveredMove, currentActor, enemy, out bool affectsHP, out bool affectsMana);
                                showHP = affectsHP;
                                showMana = affectsMana;
                            }
                            // 2. Fallback: Check if hovering/selecting the unit itself
                            else if ((hoveredCombatant == enemy) || (uiManager.HoveredCombatantFromUI == enemy) || selectable.Contains(enemy))
                            {
                                showHP = true;
                                showMana = true;
                            }

                            UpdateBarAlpha(enemy, (float)gameTime.ElapsedGameTime.TotalSeconds, showHP, showMana);

                            float visualCenterY = center.Y + spriteSize / 2f;
                            float tooltipTopY = visualCenterY - 3;
                            if (tooltipTopY < 5) tooltipTopY = 5;
                            float spriteTopY = GetEnemyStaticVisualTop(enemy, center.Y);
                            float anchorY = Math.Min(tooltipTopY - 2, spriteTopY);
                            float barY = anchorY - 4;
                            float barX = center.X - BattleLayout.ENEMY_BAR_WIDTH / 2f;

                            // Calculate and cache the bottom Y of the mana bar for stat panel positioning
                            // HP Bar Height (2) + Gap (1) + Mana Bar Height (1) = 4 pixels tall total
                            // barY is the top. Bottom is barY + 4.
                            float barBottomY = barY + 4;
                            _combatantBarBottomYs[enemy.CombatantID] = barBottomY;

                            if (enemy.VisualHealthBarAlpha > 0.01f || enemy.VisualManaBarAlpha > 0.01f)
                            {
                                _hudRenderer.DrawStatusIcons(spriteBatch, enemy, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, false, _enemyStatusIcons.ContainsKey(enemy.CombatantID) ? _enemyStatusIcons[enemy.CombatantID] : null, GetStatusIconOffset, IsStatusIconAnimating);
                                _hudRenderer.DrawEnemyBars(spriteBatch, enemy, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, enemy.VisualHealthBarAlpha, enemy.VisualManaBarAlpha, gameTime);
                            }
                        }
                    }
                }
            }
        }

        private void DrawPlayers(SpriteBatch spriteBatch, BitmapFont font, List<BattleCombatant> players, BattleCombatant currentActor, bool shouldGrayOut, HashSet<BattleCombatant> selectable, BattleAnimationManager animManager, Dictionary<string, Color> silhouetteColors, GameTime gameTime, BattleUIManager uiManager, BattleCombatant hoveredCombatant, bool drawFloor = true, bool drawShadow = true, bool drawSprite = true)
        {
            foreach (var player in players)
            {
                var center = BattleLayout.GetPlayerSpriteCenter(player.BattleSlot);
                // FIX: Use Time-Corrected Damping for turn offset
                float targetOffset = (player == currentActor) ? 0f : INACTIVE_Y_OFFSET;
                if (!_turnActiveOffsets.ContainsKey(player.CombatantID)) _turnActiveOffsets[player.CombatantID] = INACTIVE_Y_OFFSET;
                float current = _turnActiveOffsets[player.CombatantID];
                float damping = 1.0f - MathF.Exp(-ACTIVE_TWEEN_SPEED * (float)gameTime.ElapsedGameTime.TotalSeconds);
                _turnActiveOffsets[player.CombatantID] = MathHelper.Lerp(current, targetOffset, damping);

                float yOffset = _turnActiveOffsets[player.CombatantID];
                center.Y += yOffset;

                if (player.BattleSlot == 0) PlayerSpritePosition = center;

                Color? highlight = silhouetteColors.ContainsKey(player.CombatantID) ? silhouetteColors[player.CombatantID] : null;
                bool isSelectable = selectable.Contains(player);
                bool isSilhouetted = shouldGrayOut && !isSelectable;
                if (player.CombatantID == _statTooltipCombatantID && _statTooltipAlpha > 0) isSilhouetted = true;

                Color silhouetteColor = isSilhouetted ? _global.Palette_DarkShadow : _global.Palette_DarkShadow;

                // --- MODIFIED: Outline Color Logic ---
                // Only the active actor gets the Sun outline. Everyone else gets Transparent (no outer outline).
                Color outlineColor = (player == currentActor) ? _global.Palette_Sun : Color.Transparent;

                // Hide outline if showing stat tooltip to improve readability
                if (player.CombatantID == _statTooltipCombatantID && _statTooltipAlpha > 0)
                {
                    outlineColor = Color.Transparent;
                }

                outlineColor = outlineColor * player.VisualAlpha;

                var spawnAnim = animManager.GetSpawnAnimationState(player.CombatantID);
                var switchOut = animManager.GetSwitchOutAnimationState(player.CombatantID);
                var switchIn = animManager.GetSwitchInAnimationState(player.CombatantID);
                var healFlash = animManager.GetHealFlashAnimationState(player.CombatantID);
                var hitFlash = animManager.GetHitFlashState(player.CombatantID);
                var healBounce = animManager.GetHealBounceAnimationState(player.CombatantID);
                var coinCatch = animManager.GetCoinCatchAnimationState(player.CombatantID);
                var introSlide = animManager.GetIntroSlideAnimationState(player.CombatantID);

                float spawnY = 0f;
                float alpha = player.VisualAlpha;
                float scale = 1.0f;
                float rotation = 0f; // New rotation variable
                Vector2 slideOffset = Vector2.Zero;

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

                if (coinCatch != null)
                {
                    float p = coinCatch.Timer / BattleAnimationManager.CoinCatchAnimationState.DURATION;
                    float s = MathF.Sin(p * MathHelper.Pi);
                    scale += s * 0.15f;
                    rotation = coinCatch.CurrentRotation; // Apply rotation from animation state
                }

                Vector2 recoil = _recoilStates.TryGetValue(player.CombatantID, out var r) ? r.Offset : Vector2.Zero;
                Vector2 shake = hitFlash != null ? hitFlash.ShakeOffset : Vector2.Zero;

                if (!_playerSprites.TryGetValue(player.CombatantID, out var sprite))
                {
                    sprite = new PlayerCombatSprite(player.ArchetypeId);
                    _playerSprites[player.CombatantID] = sprite;
                }
                sprite.SetPosition(new Vector2(center.X, center.Y + bob + spawnY) + recoil + slideOffset);

                bool isHighlighted = selectable.Contains(player) && shouldGrayOut;
                float pulse = 0f;

                if (drawFloor)
                {
                    // --- NEW: Check for Player Floor Outro ---
                    float floorScale = 1.0f;
                    var floorOutro = animManager.GetFloorOutroAnimationState($"player_floor_{player.BattleSlot}");
                    if (floorOutro != null)
                    {
                        float progress = Math.Clamp(floorOutro.Timer / BattleAnimationManager.FloorOutroAnimationState.DURATION, 0f, 1f);
                        floorScale = 1.0f - Easing.EaseInBack(progress);
                    }

                    var baseCenter = BattleLayout.GetPlayerSpriteCenter(player.BattleSlot);
                    _vfxRenderer.DrawPlayerFloor(spriteBatch, baseCenter + slideOffset, player.VisualAlpha, floorScale);
                }

                if (drawSprite)
                {
                    Color? lowHealthOverlay = null;
                    if (player.LowHealthFlashTimer > 0f && !player.IsDefeated)
                    {
                        // Double Flash Pattern: Flash -> Flash -> Pause
                        // Cycle length is tunable via Global
                        float patternLen = _global.LowHealthFlashPatternLength;
                        float cycleT = player.LowHealthFlashTimer % patternLen;
                        float rawAlpha = 0f;

                        if (cycleT < 1.0f)
                            rawAlpha = 1.0f - cycleT; // First Flash (Fade Out)
                        else if (cycleT >= 1.0f && cycleT < 2.0f)
                            rawAlpha = 1.0f - (cycleT - 1.0f); // Second Flash (Fade Out)
                        // Else: Pause (0 alpha)

                        // Apply max intensity (0.6)
                        float flashAlpha = rawAlpha * 0.6f;

                        if (flashAlpha > 0)
                            lowHealthOverlay = _global.LowHealthFlashColor * flashAlpha;
                    }

                    // --- FIX: Only highlight if explicitly set in the dictionary ---
                    bool isHighlightedSprite = highlight.HasValue;

                    // Pass rotation to sprite draw
                    sprite.Draw(spriteBatch, animManager, player, tint, isHighlightedSprite, pulse, isSilhouetted, silhouetteColor, gameTime, highlight, outlineColor, scale, lowHealthOverlay, rotation);

                    // Use 'center' (animated) instead of 'baseCenter' (static) for bounds calculation
                    Rectangle bounds = new Rectangle((int)(center.X - 16), (int)(center.Y - 16), 32, 32);
                    Rectangle hitBox = GetPatternAlignedRect(bounds);

                    _currentTargets.Add(new TargetInfo { Combatant = player, Bounds = hitBox });
                    _combatantVisualCenters[player.CombatantID] = hitBox.Center.ToVector2();
                    _combatantStaticCenters[player.CombatantID] = center; // Use center here too

                    // --- NEW: Smart Bar Visibility Logic ---
                    bool showHP = false;
                    bool showMana = false;

                    // 1. Check if hovering a move
                    if (uiManager.HoveredMove != null)
                    {
                        AnalyzeMoveImpact(uiManager.HoveredMove, currentActor, player, out bool affectsHP, out bool affectsMana);
                        showHP = affectsHP;
                        showMana = affectsMana;
                    }
                    // 2. Fallback: Check if hovering/selecting the unit itself
                    else if ((hoveredCombatant == player) || (uiManager.HoveredCombatantFromUI == player) || selectable.Contains(player))
                    {
                        showHP = true;
                        showMana = true;
                    }

                    UpdateBarAlpha(player, (float)gameTime.ElapsedGameTime.TotalSeconds, showHP, showMana);

                    if (player == currentActor && (!isSilhouetted || player.CombatantID == _statTooltipCombatantID))
                    {
                        Vector2 nameSize = font.MeasureString(player.Name);
                        Vector2 namePos = new Vector2(center.X - nameSize.X / 2f, BattleLayout.PLAYER_NAME_TOP_Y);
                        Color nameColor = (highlight == Color.Yellow) ? _global.Palette_DarkSun : _global.Palette_Sun;

                        // --- NAME DIMMING LOGIC ---
                        bool isHovered = (hoveredCombatant == player) || (uiManager.HoveredCombatantFromUI == player);
                        if (isHovered) nameColor = _global.Palette_DarkShadow;

                        spriteBatch.DrawStringSnapped(font, player.Name, namePos, nameColor);
                    }

                    Vector2 barPos = GetCombatantBarPosition(player);
                    float barX = barPos.X - BattleLayout.PLAYER_BAR_WIDTH / 2f;
                    float barY = barPos.Y + 4; // Shift down 4 pixels

                    // Calculate and cache the bottom Y of the mana bar for stat panel positioning
                    // HP Bar Height (2) + Gap (1) + Mana Bar Height (1) = 4 pixels tall total
                    // barY is the top. Bottom is barY + 4.
                    float barBottomY = barY + 4;
                    _combatantBarBottomYs[player.CombatantID] = barBottomY;

                    // --- HUD LAYERING FIX ---
                    // Store position for later drawing in DrawHUDPass
                    _combatantBarPositions[player.CombatantID] = new Vector2(barX, barY);
                }
            }
        }

        /// <summary>
        /// Analyzes a move to determine if it affects the HP or Mana of a specific combatant.
        /// </summary>
        private void AnalyzeMoveImpact(MoveData move, BattleCombatant actor, BattleCombatant candidate, out bool affectsHP, out bool affectsMana)
        {
            affectsHP = false;
            affectsMana = false;

            if (move == null || actor == null || candidate == null) return;

            bool isActor = actor == candidate;

            // Check if candidate is a valid target for this move
            // We use TargetingHelper to get the list, then check containment.
            // This handles complex logic like "Random" or "Team" correctly.
            var validTargets = TargetingHelper.GetValidTargets(actor, move.Target, ServiceLocator.Get<BattleManager>().AllCombatants);
            bool isTarget = validTargets.Contains(candidate);

            // --- ACTOR ANALYSIS (Self-Cost / Self-Harm) ---
            if (isActor)
            {
                // Mana Cost
                if (move.ManaCost > 0) affectsMana = true;
                if (move.Abilities.Any(a => a is ManaDumpAbility)) affectsMana = true;

                // HP Cost (Recoil / Bloodletter)
                if (move.Abilities.Any(a => a is RecoilAbility || a is BloodletterAbility)) affectsHP = true;
            }

            // --- TARGET ANALYSIS (Damage / Healing / Drain) ---
            if (isTarget)
            {
                // HP Impact
                if (move.Power > 0) affectsHP = true;
                if (move.Effects.ContainsKey("Heal")) affectsHP = true;
                if (move.Abilities.Any(a => a is PercentageDamageAbility)) affectsHP = true;
                if (move.Abilities.Any(a => a is IFixedDamageModifier)) affectsHP = true;
                if (move.Abilities.Any(a => a is ManaDumpAbility)) affectsHP = true; // FIX: ManaDump implies damage

                // Mana Impact
                if (move.Abilities.Any(a => a is ManaBurnOnHitAbility || a is ManaDamageAbility)) affectsMana = true;
                if (move.Abilities.Any(a => a is RestoreManaAbility)) affectsMana = true;
            }
        }

        private void UpdateBarAlpha(BattleCombatant c, float dt, bool showHP, bool showMana)
        {
            var battleManager = ServiceLocator.Get<BattleManager>();
            // Check if we are in a phase where bars should persist (Combat execution)
            bool inCombatPhase = battleManager.CurrentPhase != BattleManager.BattlePhase.ActionSelection_Slot1 &&
                                 battleManager.CurrentPhase != BattleManager.BattlePhase.ActionSelection_Slot2 &&
                                 battleManager.CurrentPhase != BattleManager.BattlePhase.StartOfTurn &&
                                 battleManager.CurrentPhase != BattleManager.BattlePhase.BattleStartIntro;

            // --- HP BAR LOGIC ---
            // Force visibility if animating damage/healing
            bool isHealthVisuallyActive = c.VisualHealthBarAlpha > 0f || c.HealthBarVisibleTimer > 0f || c.HealthBarDelayTimer > 0f || c.HealthBarDisappearTimer > 0f;
            bool hpForceVisible = inCombatPhase && isHealthVisuallyActive;

            // Determine desired visibility
            bool hpVisible = showHP || c.HealthBarVisibleTimer > 0 || hpForceVisible;

            if (hpVisible)
            {
                c.VisualHealthBarAlpha = 1.0f;
                c.HealthBarDelayTimer = 0f;
                c.HealthBarDisappearTimer = 0f;
                c.CurrentBarVariance = (float)(_random.NextDouble() * BattleCombatant.BAR_VARIANCE_MAX);
            }
            else
            {
                // If we are in menu mode (not combat), hide immediately for snappiness
                if (!inCombatPhase)
                {
                    c.VisualHealthBarAlpha = 0f;
                    c.HealthBarDelayTimer = 0f;
                    c.HealthBarDisappearTimer = 0f;
                }
                else if (c.VisualHealthBarAlpha > 0f)
                {
                    // Standard fade logic for combat events
                    if (c.HealthBarDelayTimer < BattleCombatant.BAR_DELAY_DURATION + c.CurrentBarVariance)
                    {
                        c.HealthBarDelayTimer += dt;
                    }
                    else
                    {
                        c.HealthBarDisappearTimer += dt;
                        float progress = c.HealthBarDisappearTimer / BattleCombatant.BAR_DISAPPEAR_DURATION;
                        c.VisualHealthBarAlpha = 1.0f - Math.Clamp(progress, 0f, 1f);

                        if (c.HealthBarDisappearTimer >= BattleCombatant.BAR_DISAPPEAR_DURATION)
                        {
                            c.VisualHealthBarAlpha = 0f;
                            c.HealthBarDelayTimer = 0f;
                            c.HealthBarDisappearTimer = 0f;
                        }
                    }
                }
            }

            // --- MANA BAR LOGIC ---
            bool isManaVisuallyActive = c.VisualManaBarAlpha > 0f || c.ManaBarVisibleTimer > 0f || c.ManaBarDelayTimer > 0f || c.ManaBarDisappearTimer > 0f;
            bool manaForceVisible = inCombatPhase && isManaVisuallyActive;
            bool manaVisible = showMana || c.ManaBarVisibleTimer > 0 || manaForceVisible;

            if (manaVisible)
            {
                c.VisualManaBarAlpha = 1.0f;
                c.ManaBarDelayTimer = 0f;
                c.ManaBarDisappearTimer = 0f;
            }
            else
            {
                // If we are in menu mode (not combat), hide immediately
                if (!inCombatPhase)
                {
                    c.VisualManaBarAlpha = 0f;
                    c.ManaBarDelayTimer = 0f;
                    c.ManaBarDisappearTimer = 0f;
                }
                else if (c.VisualManaBarAlpha > 0f)
                {
                    if (c.ManaBarDelayTimer < BattleCombatant.BAR_DELAY_DURATION + c.CurrentBarVariance)
                    {
                        c.ManaBarDelayTimer += dt;
                    }
                    else
                    {
                        c.ManaBarDisappearTimer += dt;
                        float progress = c.ManaBarDisappearTimer / BattleCombatant.BAR_DISAPPEAR_DURATION;
                        c.VisualManaBarAlpha = 1.0f - Math.Clamp(progress, 0f, 1f);

                        if (c.ManaBarDisappearTimer >= BattleCombatant.BAR_DISAPPEAR_DURATION)
                        {
                            c.VisualManaBarAlpha = 0f;
                            c.ManaBarDelayTimer = 0f;
                            c.ManaBarDisappearTimer = 0f;
                        }
                    }
                }
            }
        }

        private void DrawUITitle(SpriteBatch spriteBatch, GameTime gameTime, BattleSubMenuState subMenuState)
        {
            string title = "";
            if (!string.IsNullOrEmpty(title))
            {
                var font = ServiceLocator.Get<Core>().SecondaryFont;
                // Use TextAnimator for the title effect (Bounce)
                var size = font.MeasureString(title);
                var pos = new Vector2((Global.VIRTUAL_WIDTH - size.Width) / 2, BattleLayout.DIVIDER_Y + 3);

                TextAnimator.DrawTextWithEffect(spriteBatch, font, title, pos, _global.Palette_Shadow, TextEffectType.Bounce, (float)gameTime.TotalGameTime.TotalSeconds);
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
                    // FIX: Use Time-Corrected Damping for framerate independence
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
                if (!_combatantBarPositions.TryGetValue(combatant.CombatantID, out var pos)) continue;

                float barX = pos.X;
                float barY = pos.Y;

                if (combatant.VisualHealthBarAlpha <= 0.01f && combatant.VisualManaBarAlpha <= 0.01f) continue;

                if (combatant.IsPlayerControlled)
                {
                    _hudRenderer.DrawStatusIcons(spriteBatch, combatant, barX, barY, BattleLayout.PLAYER_BAR_WIDTH, true, _playerStatusIcons, GetStatusIconOffset, IsStatusIconAnimating);
                    _hudRenderer.DrawPlayerBars(spriteBatch, combatant, barX, barY, BattleLayout.PLAYER_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, combatant.VisualHealthBarAlpha, combatant.VisualManaBarAlpha, gameTime, uiManager, combatant == currentActor);
                }
                else
                {
                    // Ensure list exists
                    if (!_enemyStatusIcons.ContainsKey(combatant.CombatantID))
                        _enemyStatusIcons[combatant.CombatantID] = new List<StatusIconInfo>();

                    _hudRenderer.DrawStatusIcons(spriteBatch, combatant, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, false, _enemyStatusIcons[combatant.CombatantID], GetStatusIconOffset, IsStatusIconAnimating);
                    _hudRenderer.DrawEnemyBars(spriteBatch, combatant, barX, barY, BattleLayout.ENEMY_BAR_WIDTH, BattleLayout.ENEMY_BAR_HEIGHT, animManager, combatant.VisualHealthBarAlpha, combatant.VisualManaBarAlpha, gameTime);
                }
            }
        }
    }
}