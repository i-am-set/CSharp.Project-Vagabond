using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Particles;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
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
    public struct TargetInfo { public BattleCombatant Combatant; public Rectangle Bounds; }
    public struct StatusIconInfo { public StatusEffectInstance Effect; public Rectangle Bounds; }
    public class BattleRenderer
    {
        // --- TUNING ---
        public const float BAR_MIN_ALPHA = 0.0f;
        public const float BAR_FADE_SPEED = 5.0f;
        public const float BAR_VISIBILITY_DURATION = 6.0f;

        // Dependencies
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly Core _core;
        private readonly TooltipManager _tooltipManager;
        private readonly HitstopManager _hitstopManager;

        // Sprite Management
        private readonly Dictionary<string, PlayerCombatSprite> _playerSprites = new Dictionary<string, PlayerCombatSprite>();

        // Rendering Resources
        private RenderTarget2D _flattenTarget;
        private const int FLATTEN_TARGET_SIZE = 256;
        private const int FLATTEN_MARGIN = 16;

        // State
        private List<TargetInfo> _currentTargets = new List<TargetInfo>();
        private readonly List<StatusIconInfo> _playerStatusIcons = new List<StatusIconInfo>();
        private readonly Dictionary<string, List<StatusIconInfo>> _enemyStatusIcons = new Dictionary<string, List<StatusIconInfo>>();
        private StatusIconInfo? _hoveredStatusIcon;

        public Vector2 PlayerSpritePosition { get; private set; }
        private Dictionary<string, Vector2> _combatantVisualCenters = new Dictionary<string, Vector2>();

        // Enemy Sprite Animation
        private Dictionary<string, Vector2[]> _enemySpritePartOffsets = new Dictionary<string, Vector2[]>();
        private Dictionary<string, float[]> _enemyAnimationTimers = new Dictionary<string, float[]>();
        private Dictionary<string, float[]> _enemyAnimationIntervals = new Dictionary<string, float[]>();
        private readonly Random _random = new Random();
        private const float ENEMY_ANIM_MIN_INTERVAL = 0.4f;
        private const float ENEMY_ANIM_MAX_INTERVAL = 0.6f;

        // Shadow Animation State
        private Dictionary<string, Vector2> _shadowOffsets = new Dictionary<string, Vector2>();
        private Dictionary<string, float> _shadowTimers = new Dictionary<string, float>();
        private Dictionary<string, float> _shadowIntervals = new Dictionary<string, float>();
        private const float SHADOW_ANIM_MIN_INTERVAL = 0.8f;
        private const float SHADOW_ANIM_MAX_INTERVAL = 1.2f;

        // Attacker Animation
        private readonly Dictionary<string, SpriteHopAnimationController> _attackAnimControllers = new();
        private string? _lastAttackerId;

        // Recoil Animation State (Physical knockback on hit)
        private class RecoilState
        {
            public Vector2 Offset;
            public Vector2 Velocity;
            // TUNING: Stiffer spring for snappier impact
            public const float STIFFNESS = 600f;
            public const float DAMPING = 15f;
        }
        private readonly Dictionary<string, RecoilState> _recoilStates = new Dictionary<string, RecoilState>();

        // Status Icon Animation State
        private class StatusIconAnim
        {
            public string CombatantID;
            public StatusEffectType Type;
            public float Timer;
            public const float DURATION = 0.3f;
            public const float HEIGHT = 5f;
        }
        private readonly List<StatusIconAnim> _activeStatusIconAnims = new List<StatusIconAnim>();

        // Stat Change Tooltip State
        private float _statTooltipAlpha = 0f;
        private string? _statTooltipCombatantID = null;
        private const float STAT_TOOLTIP_FADE_SPEED = 5.0f;

        // Layout Constants
        private const int DIVIDER_Y = 123;
        private const int ENEMY_SLOT_Y_OFFSET = 12; // Moved up 16 pixels (was 16)
        private const float TITLE_INDICATOR_BOB_SPEED = 0.375f;
        private const float HITSTOP_SCALE_MULTIPLIER = 1.2f; // Scale up during hitstop

        // Noise generator for organic sway
        private static readonly SeededPerlin _swayNoise = new SeededPerlin(9999);

        public BattleRenderer()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _hitstopManager = ServiceLocator.Get<HitstopManager>();

            _flattenTarget = new RenderTarget2D(_core.GraphicsDevice, FLATTEN_TARGET_SIZE, FLATTEN_TARGET_SIZE, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }

        public void Reset()
        {
            _currentTargets.Clear();
            _playerStatusIcons.Clear();
            _enemyStatusIcons.Clear();
            _enemySpritePartOffsets.Clear();
            _enemyAnimationTimers.Clear();
            _enemyAnimationIntervals.Clear();
            _shadowOffsets.Clear();
            _shadowTimers.Clear();
            _shadowIntervals.Clear();
            _attackAnimControllers.Clear();
            _combatantVisualCenters.Clear();
            _playerSprites.Clear();
            _activeStatusIconAnims.Clear();
            _recoilStates.Clear();
            _lastAttackerId = null;
            _statTooltipAlpha = 0f;
            _statTooltipCombatantID = null;
        }

        public List<TargetInfo> GetCurrentTargets() => _currentTargets;

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants, BattleAnimationManager animationManager, BattleCombatant? currentActor)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update visibility timers
            foreach (var combatant in combatants)
            {
                if (combatant.HealthBarVisibleTimer > 0)
                {
                    combatant.HealthBarVisibleTimer -= dt;
                    if (combatant.HealthBarVisibleTimer < 0) combatant.HealthBarVisibleTimer = 0;
                }
                if (combatant.ManaBarVisibleTimer > 0)
                {
                    combatant.ManaBarVisibleTimer -= dt;
                    if (combatant.ManaBarVisibleTimer < 0) combatant.ManaBarVisibleTimer = 0;
                }
            }

            UpdateEnemyAnimations(gameTime, combatants);
            UpdateShadowAnimations(gameTime, combatants);
            UpdateStatusIconTooltips(combatants);
            UpdateStatusIconAnimations(gameTime);
            UpdateRecoilAnimations(gameTime);

            foreach (var combatant in combatants)
            {
                if (combatant.IsPlayerControlled && _playerSprites.TryGetValue(combatant.CombatantID, out var sprite))
                {
                    bool isActive = currentActor == combatant;
                    sprite.Update(gameTime, isActive);
                }
            }
        }

        public void TriggerAttackAnimation(string combatantId)
        {
            if (!_attackAnimControllers.ContainsKey(combatantId))
            {
                _attackAnimControllers[combatantId] = new SpriteHopAnimationController();
            }
            _attackAnimControllers[combatantId].Trigger();
        }

        public void TriggerRecoil(string combatantId, Vector2 direction, float magnitude)
        {
            if (!_recoilStates.ContainsKey(combatantId))
            {
                _recoilStates[combatantId] = new RecoilState();
            }
            // Apply an impulse velocity away from the hit
            _recoilStates[combatantId].Velocity = direction * magnitude * 10f; // Scale up for velocity
        }

        private void UpdateRecoilAnimations(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            foreach (var state in _recoilStates.Values)
            {
                // Spring physics: F = -kx - cv
                Vector2 force = (-state.Offset * RecoilState.STIFFNESS) - (state.Velocity * RecoilState.DAMPING);
                state.Velocity += force * dt;
                state.Offset += state.Velocity * dt;

                // Snap to zero if very small
                if (state.Offset.LengthSquared() < 0.1f && state.Velocity.LengthSquared() < 0.1f)
                {
                    state.Offset = Vector2.Zero;
                    state.Velocity = Vector2.Zero;
                }
            }
        }

        public void TriggerStatusIconHop(string combatantId, StatusEffectType type)
        {
            _activeStatusIconAnims.RemoveAll(a => a.CombatantID == combatantId && a.Type == type);
            _activeStatusIconAnims.Add(new StatusIconAnim
            {
                CombatantID = combatantId,
                Type = type,
                Timer = 0f
            });
        }

        private void UpdateStatusIconAnimations(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _activeStatusIconAnims.Count - 1; i >= 0; i--)
            {
                var anim = _activeStatusIconAnims[i];
                anim.Timer += dt;
                if (anim.Timer >= StatusIconAnim.DURATION)
                {
                    _activeStatusIconAnims.RemoveAt(i);
                }
            }
        }

        private float GetStatusIconOffset(string combatantId, StatusEffectType type)
        {
            var anim = _activeStatusIconAnims.FirstOrDefault(a => a.CombatantID == combatantId && a.Type == type);
            if (anim != null)
            {
                float progress = anim.Timer / StatusIconAnim.DURATION;
                return MathF.Sin(progress * MathHelper.Pi) * -StatusIconAnim.HEIGHT;
            }
            return 0f;
        }

        private bool IsStatusIconAnimating(string combatantId, StatusEffectType type)
        {
            return _activeStatusIconAnims.Any(a => a.CombatantID == combatantId && a.Type == type);
        }

        private void UpdateStatusIconTooltips(IEnumerable<BattleCombatant> allCombatants)
        {
            var virtualMousePos = Core.TransformMouse(Mouse.GetState().Position);
            _hoveredStatusIcon = null;

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
                    _hoveredStatusIcon = iconInfo;
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
                        _hoveredStatusIcon = iconInfo;
                        return;
                    }
                }
            }
        }

        public void Draw(
            SpriteBatch spriteBatch,
            BitmapFont font,
            GameTime gameTime,
            IEnumerable<BattleCombatant> allCombatants,
            BattleCombatant currentActor,
            BattleUIManager uiManager,
            BattleInputHandler inputHandler,
            BattleAnimationManager animationManager,
            float sharedBobbingTimer,
            Matrix transform)
        {
            var secondaryFont = _core.SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            // --- STAT CHANGE TOOLTIP LOGIC (Moved to top for sprite tinting) ---
            var battleManager = ServiceLocator.Get<BattleManager>();
            bool isSelectionPhase = battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot1 || battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot2;
            bool isDefaultUI = uiManager.UIState == BattleUIState.Default;

            BattleCombatant? hoveredCombatant = null;
            if (inputHandler.HoveredTargetIndex >= 0 && inputHandler.HoveredTargetIndex < _currentTargets.Count)
            {
                hoveredCombatant = _currentTargets[inputHandler.HoveredTargetIndex].Combatant;
            }

            if (isSelectionPhase && isDefaultUI && hoveredCombatant != null)
            {
                _statTooltipCombatantID = hoveredCombatant.CombatantID;
                _statTooltipAlpha = 1.0f; // Instant appear
            }
            else
            {
                _statTooltipAlpha = 0.0f; // Instant disappear
                _statTooltipCombatantID = null;
            }

            var currentAttackerId = (currentActor != null) ? currentActor.CombatantID : null;
            if (currentAttackerId != _lastAttackerId)
            {
                _lastAttackerId = currentAttackerId;
            }

            foreach (var controller in _attackAnimControllers.Values)
            {
                controller.Update(gameTime);
            }

            _currentTargets.Clear();
            _playerStatusIcons.Clear();

            var enemies = allCombatants.Where(c => !c.IsPlayerControlled && c.IsActiveOnField).ToList();
            var players = allCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();

            float pulseAlpha = 0.10f + ((MathF.Sin(sharedBobbingTimer * 6f) + 1f) / 2f) * 0.10f;

            var selectableTargets = new HashSet<BattleCombatant>();
            bool isTargetingPhase = uiManager.UIState == BattleUIState.Targeting || uiManager.UIState == BattleUIState.ItemTargeting;
            bool isHoveringMoveWithTargets = uiManager.HoverHighlightState.CurrentMove != null && uiManager.HoverHighlightState.Targets.Any();
            bool shouldGrayOutUnselectable = isTargetingPhase || isHoveringMoveWithTargets;

            var hoveredMove = uiManager.HoverHighlightState.CurrentMove;
            if (!isTargetingPhase && hoveredMove != null && hoveredMove.Target == TargetType.None)
            {
                shouldGrayOutUnselectable = true;
            }

            TargetType? activeTargetType = null;

            if (isTargetingPhase && currentActor != null)
            {
                activeTargetType = uiManager.TargetTypeForSelection;
                if (activeTargetType.HasValue)
                {
                    foreach (var c in allCombatants)
                    {
                        if (!c.IsDefeated && c.IsActiveOnField)
                        {
                            bool isValid = false;
                            bool isOpponent = c.IsPlayerControlled != currentActor.IsPlayerControlled;
                            bool isAlly = c.IsPlayerControlled == currentActor.IsPlayerControlled && c != currentActor;
                            bool isUser = c == currentActor;

                            switch (activeTargetType)
                            {
                                case TargetType.Single: if (!isUser) isValid = true; break;
                                case TargetType.SingleTeam: if (!isOpponent) isValid = true; break;
                                case TargetType.SingleAll: isValid = true; break;
                                case TargetType.Self: if (isUser) isValid = true; break;
                                case TargetType.Ally: if (isAlly) isValid = true; break;
                                case TargetType.Both:
                                case TargetType.RandomBoth: if (isOpponent) isValid = true; break;
                                case TargetType.Every:
                                case TargetType.RandomEvery: if (isOpponent || isAlly) isValid = true; break;
                                case TargetType.Team: if (!isOpponent) isValid = true; break;
                                case TargetType.All:
                                case TargetType.RandomAll: isValid = true; break;
                            }

                            if (isValid) selectableTargets.Add(c);
                        }
                    }
                }
            }
            else if (isHoveringMoveWithTargets)
            {
                foreach (var target in uiManager.HoverHighlightState.Targets) selectableTargets.Add(target);
                activeTargetType = hoveredMove?.Target;
            }

            var silhouetteColors = new Dictionary<string, Color>();

            if (isTargetingPhase)
            {
                foreach (var target in selectableTargets)
                {
                    silhouetteColors[target.CombatantID] = _global.Palette_Red;
                }

                if (hoveredCombatant != null && selectableTargets.Contains(hoveredCombatant))
                {
                    bool isMultiTarget = activeTargetType == TargetType.Every || activeTargetType == TargetType.Both || activeTargetType == TargetType.All || activeTargetType == TargetType.Team || activeTargetType == TargetType.RandomAll || activeTargetType == TargetType.RandomBoth || activeTargetType == TargetType.RandomEvery;
                    if (isMultiTarget)
                    {
                        foreach (var target in selectableTargets) silhouetteColors[target.CombatantID] = Color.Yellow;
                    }
                    else
                    {
                        silhouetteColors[hoveredCombatant.CombatantID] = Color.Yellow;
                    }
                }
            }
            else if (selectableTargets.Any() && activeTargetType.HasValue)
            {
                float targetingTimer = uiManager.HoverHighlightState.Timer;
                var sortedTargets = allCombatants
                    .Where(c => selectableTargets.Contains(c))
                    .OrderBy(c => c.IsPlayerControlled ? 1 : 0)
                    .ThenBy(c => c.IsPlayerControlled ? (c == currentActor ? 1 : 0) : 0)
                    .ThenBy(c => c.BattleSlot)
                    .ToList();

                bool isMultiTarget = activeTargetType == TargetType.Every || activeTargetType == TargetType.Both || activeTargetType == TargetType.All || activeTargetType == TargetType.Team || activeTargetType == TargetType.RandomAll || activeTargetType == TargetType.RandomBoth || activeTargetType == TargetType.RandomEvery;

                if (isMultiTarget)
                {
                    if (sortedTargets.Count == 1)
                    {
                        silhouetteColors[sortedTargets[0].CombatantID] = Color.Yellow;
                    }
                    else
                    {
                        bool isFlash = (targetingTimer % _global.TargetingMultiBlinkSpeed) < (_global.TargetingMultiBlinkSpeed / 2f);
                        Color color = isFlash ? Color.Yellow : _global.Palette_Red;
                        foreach (var target in sortedTargets)
                        {
                            silhouetteColors[target.CombatantID] = color;
                        }
                    }
                }
                else
                {
                    int count = sortedTargets.Count;
                    if (count > 0)
                    {
                        float cycleDuration = count * _global.TargetingSingleCycleSpeed;
                        float currentCycleTime = targetingTimer % cycleDuration;
                        int activeIndex = (int)(currentCycleTime / _global.TargetingSingleCycleSpeed);
                        activeIndex = Math.Clamp(activeIndex, 0, count - 1);

                        for (int i = 0; i < count; i++)
                        {
                            silhouetteColors[sortedTargets[i].CombatantID] = (i == activeIndex) ? Color.Yellow : _global.Palette_Red;
                        }
                    }
                }
            }

            DrawEnemyHuds(spriteBatch, font, secondaryFont, enemies, currentActor, isTargetingPhase, shouldGrayOutUnselectable, selectableTargets, animationManager, uiManager.HoverHighlightState, pulseAlpha, gameTime, silhouetteColors, transform, inputHandler, uiManager);
            animationManager.DrawCoins(spriteBatch);

            foreach (var playerCombatant in players)
            {
                DrawPlayerHud(spriteBatch, font, secondaryFont, playerCombatant, currentActor, gameTime, animationManager, uiManager, uiManager.HoverHighlightState, shouldGrayOutUnselectable, selectableTargets, isTargetingPhase, pulseAlpha, silhouetteColors, inputHandler);
            }

            DrawUITitle(spriteBatch, secondaryFont, gameTime, uiManager.SubMenuState);

            if (currentActor != null && !currentActor.IsDefeated)
            {
                DrawIndicatorArrow(spriteBatch, gameTime, currentActor);
            }

            foreach (var kvp in silhouetteColors)
            {
                if (kvp.Value == Color.Yellow)
                {
                    var combatant = allCombatants.FirstOrDefault(c => c.CombatantID == kvp.Key);
                    if (combatant != null && !combatant.IsDefeated)
                    {
                        if (combatant != currentActor)
                        {
                            DrawIndicatorArrow(spriteBatch, gameTime, combatant);
                        }
                    }
                }
            }

            DrawTargetingUI(spriteBatch, font, gameTime, uiManager, inputHandler);
            spriteBatch.DrawSnapped(pixel, new Rectangle(0, DIVIDER_Y, Global.VIRTUAL_WIDTH, 1), Color.White);

            if (_global.ShowSplitMapGrid)
            {
                foreach (var target in _currentTargets)
                {
                    spriteBatch.DrawSnapped(pixel, target.Bounds, Color.Cyan * 0.5f);
                }
            }

            if (_statTooltipAlpha > 0.01f && _statTooltipCombatantID != null)
            {
                var target = allCombatants.FirstOrDefault(c => c.CombatantID == _statTooltipCombatantID);
                if (target != null)
                {
                    // Check for Insight Ability
                    bool hasInsight = allCombatants.Any(c => c.IsPlayerControlled && !c.IsDefeated && c.Abilities.Any(a => a is InsightAbility));
                    DrawStatChangeTooltip(spriteBatch, target, _statTooltipAlpha, hasInsight);
                }
            }
        }

        private void DrawStatChangeTooltip(SpriteBatch spriteBatch, BattleCombatant combatant, float alpha, bool hasInsight)
        {
            var tertiaryFont = _core.TertiaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();
            var icons = _spriteManager.StatChangeIconsSpriteSheet;
            var iconSilhouette = _spriteManager.StatChangeIconsSpriteSheetSilhouette;
            var iconRects = _spriteManager.StatChangeIconSourceRects;

            if (icons == null || iconRects == null || iconSilhouette == null) return;

            const int width = 55; // Increased width to fit value
            const int height = 28;
            const int rowHeight = 7;
            const int iconSize = 3;
            const int iconGap = 1;

            // 1. Center UI in the middle of the hitbox
            Vector2 centerPos = GetCombatantVisualCenterPosition(combatant, null);

            // Apply Global Left Shift (-6)
            int xPos = (int)(centerPos.X - width / 2) - 6;

            // Adjust Y based on combatant type
            float yPos;
            if (combatant.IsPlayerControlled)
            {
                yPos = centerPos.Y - 16;
            }
            else
            {
                // Enemy: Base (-40) + Down Shift (+3) = -37
                yPos = centerPos.Y - 3;
            }

            // Clamp to screen
            if (yPos < 5) yPos = 5;

            var bounds = new Rectangle(xPos, (int)yPos, width, height);

            // Draw Background (Removed as requested, but keeping the bounds for layout)
            // spriteBatch.DrawSnapped(pixel, bounds, _global.Palette_DarkGray * alpha);

            // Draw Stats
            string[] statLabels = { "STR", "INT", "TEN", "AGI" };
            OffensiveStatType[] statTypes = { OffensiveStatType.Strength, OffensiveStatType.Intelligence, OffensiveStatType.Tenacity, OffensiveStatType.Agility };
            Color[] statColors = { _global.StatColor_Strength, _global.StatColor_Intelligence, _global.StatColor_Tenacity, _global.StatColor_Agility };

            for (int i = 0; i < 4; i++)
            {
                int rowY = bounds.Y + (i * rowHeight);

                // Calculate Effective Stat Value
                int effectiveValue = 0;
                switch (statTypes[i])
                {
                    case OffensiveStatType.Strength: effectiveValue = combatant.GetEffectiveStrength(); break;
                    case OffensiveStatType.Intelligence: effectiveValue = combatant.GetEffectiveIntelligence(); break;
                    case OffensiveStatType.Tenacity: effectiveValue = combatant.GetEffectiveTenacity(); break;
                    case OffensiveStatType.Agility: effectiveValue = combatant.GetEffectiveAgility(); break;
                }

                // Draw Value (Right Aligned to Label X)
                string valueText = (combatant.IsPlayerControlled || hasInsight) ? effectiveValue.ToString() : "??";
                Vector2 valueSize = tertiaryFont.MeasureString(valueText);

                // Label starts at bounds.X + 16. Value ends at bounds.X + 14.
                float valueX = bounds.X + 14 - valueSize.X;
                Vector2 valuePos = new Vector2(valueX, rowY + 1);
                spriteBatch.DrawStringSquareOutlinedSnapped(tertiaryFont, valueText, valuePos, _global.Palette_White * alpha, _global.Palette_Black * alpha);

                // Draw Label with Square Outline
                Vector2 labelPos = new Vector2(bounds.X + 16, rowY + 1);
                spriteBatch.DrawStringSquareOutlinedSnapped(tertiaryFont, statLabels[i], labelPos, statColors[i] * alpha, _global.Palette_Black * alpha);

                // Draw Icons
                int stage = combatant.StatStages[statTypes[i]];
                int absStage = Math.Abs(stage);
                bool isPositive = stage > 0;

                // 2. Move sprites Right 1, Up 1
                // Original X: bounds.X + 14
                // New X: bounds.X + 29 (Shifted right to accommodate value + label)
                // Original Y: rowY + 2
                // New Y: rowY + 1
                int startIconX = bounds.X + 29;
                int iconY = rowY + 1;

                for (int j = 0; j < 6; j++)
                {
                    int iconIndex = 0; // Neutral
                    if (j < absStage)
                    {
                        iconIndex = isPositive ? 1 : 2; // Up or Down
                    }

                    var destRect = new Rectangle(startIconX + (j * (iconSize + iconGap)), iconY, iconSize, iconSize);
                    var sourceRect = iconRects[iconIndex];

                    // 3. Draw Square Outline around sprites using Silhouette
                    // Draw 8 offsets in Black
                    Color outlineColor = _global.Palette_Black * alpha;

                    // Top-Left
                    spriteBatch.DrawSnapped(iconSilhouette, new Rectangle(destRect.X - 1, destRect.Y - 1, iconSize, iconSize), sourceRect, outlineColor);
                    // Top
                    spriteBatch.DrawSnapped(iconSilhouette, new Rectangle(destRect.X, destRect.Y - 1, iconSize, iconSize), sourceRect, outlineColor);
                    // Top-Right
                    spriteBatch.DrawSnapped(iconSilhouette, new Rectangle(destRect.X + 1, destRect.Y - 1, iconSize, iconSize), sourceRect, outlineColor);
                    // Left
                    spriteBatch.DrawSnapped(iconSilhouette, new Rectangle(destRect.X - 1, destRect.Y, iconSize, iconSize), sourceRect, outlineColor);
                    // Right
                    spriteBatch.DrawSnapped(iconSilhouette, new Rectangle(destRect.X + 1, destRect.Y, iconSize, iconSize), sourceRect, outlineColor);
                    // Bottom-Left
                    spriteBatch.DrawSnapped(iconSilhouette, new Rectangle(destRect.X - 1, destRect.Y + 1, iconSize, iconSize), sourceRect, outlineColor);
                    // Bottom
                    spriteBatch.DrawSnapped(iconSilhouette, new Rectangle(destRect.X, destRect.Y + 1, iconSize, iconSize), sourceRect, outlineColor);
                    // Bottom-Right
                    spriteBatch.DrawSnapped(iconSilhouette, new Rectangle(destRect.X + 1, destRect.Y + 1, iconSize, iconSize), sourceRect, outlineColor);

                    // Draw Main Sprite
                    spriteBatch.DrawSnapped(icons, destRect, sourceRect, Color.White * alpha);
                }
            }
        }

        public void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font)
        {
        }

        private void DrawEnemyHuds(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, List<BattleCombatant> enemies, BattleCombatant currentActor, bool isTargetingPhase, bool shouldGrayOutUnselectable, HashSet<BattleCombatant> selectableTargets, BattleAnimationManager animationManager, HoverHighlightState hoverHighlightState, float pulseAlpha, GameTime gameTime, Dictionary<string, Color> silhouetteColors, Matrix transform, BattleInputHandler inputHandler, BattleUIManager uiManager, Color? tintOverride = null)
        {
            const int enemyAreaPadding = 40;
            int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
            int slotWidth = availableWidth / 2;

            for (int slotIndex = 0; slotIndex < 2; slotIndex++)
            {
                var slotCenter = new Vector2(enemyAreaPadding + (slotIndex * slotWidth) + (slotWidth / 2), ENEMY_SLOT_Y_OFFSET);
                var enemyInSlot = enemies.FirstOrDefault(e => e.BattleSlot == slotIndex);

                int spriteSize = 64;
                if (enemyInSlot != null)
                {
                    bool isMajor = _spriteManager.IsMajorEnemySprite(enemyInSlot.ArchetypeId);
                    spriteSize = isMajor ? 96 : 64;
                }

                float groundY = spriteSize + ENEMY_SLOT_Y_OFFSET;

                if (_spriteManager.BattleEnemyFloorSprite != null)
                {
                    Vector2 floorOrigin = new Vector2(_spriteManager.BattleEnemyFloorSprite.Width / 2f, _spriteManager.BattleEnemyFloorSprite.Height / 2f);
                    spriteBatch.DrawSnapped(_spriteManager.BattleEnemyFloorSprite, new Vector2(slotCenter.X, groundY), null, Color.White, 0f, floorOrigin, 1f, SpriteEffects.None, 0f);
                }
            }

            if (!enemies.Any()) return;

            foreach (var enemy in enemies)
            {
                int slotIndex = enemy.BattleSlot;
                var slotCenter = new Vector2(enemyAreaPadding + (slotIndex * slotWidth) + (slotWidth / 2), ENEMY_SLOT_Y_OFFSET);

                Color? highlightColor = null;
                if (silhouetteColors.TryGetValue(enemy.CombatantID, out var color))
                {
                    highlightColor = color;
                }

                // --- TINT LOGIC ---
                Color? tintOverrideLocal = tintOverride;
                bool localShouldGrayOut = shouldGrayOutUnselectable;

                if (enemy.CombatantID == _statTooltipCombatantID && _statTooltipAlpha > 0f)
                {
                    // Force silhouette mode for this specific enemy
                    localShouldGrayOut = true;
                    // Override silhouette color to DarkerGray
                    // We handle this by modifying the parameters passed to DrawCombatantHud
                }

                // --- HEAL FLASH LOGIC ---
                var healFlash = animationManager.GetHealFlashAnimationState(enemy.CombatantID);
                if (healFlash != null)
                {
                    float progress = healFlash.Timer / BattleAnimationManager.HealFlashAnimationState.Duration;
                    float flashIntensity = 1.0f - Easing.EaseOutQuad(progress);
                    // Tint Greenish-White
                    tintOverrideLocal = Color.Lerp(Color.White, _global.Palette_LightGreen, flashIntensity * 0.8f);
                }

                DrawCombatantHud(spriteBatch, nameFont, statsFont, enemy, slotCenter, currentActor, isTargetingPhase, localShouldGrayOut, selectableTargets, animationManager, hoverHighlightState, pulseAlpha, gameTime, highlightColor, transform, inputHandler, uiManager, tintOverrideLocal);

                // --- ALWAYS REGISTER HITBOX ---
                // Calculate bounds for hover detection regardless of selection state
                bool isMajor = _spriteManager.IsMajorEnemySprite(enemy.ArchetypeId);
                int spritePartSize = isMajor ? 96 : 64;
                float yBobOffset = CalculateAttackBobOffset(enemy.CombatantID, isPlayer: false);

                // Apply Heal Bounce Offset
                var healBounce = animationManager.GetHealBounceAnimationState(enemy.CombatantID);
                if (healBounce != null)
                {
                    float progress = healBounce.Timer / BattleAnimationManager.HealBounceAnimationState.Duration;
                    float bounce = MathF.Sin(progress * MathHelper.Pi) * -BattleAnimationManager.HealBounceAnimationState.Height;
                    yBobOffset += bounce;
                }

                var spawnAnim = animationManager.GetSpawnAnimationState(enemy.CombatantID);
                float spawnYOffset = 0f;
                if (spawnAnim != null && spawnAnim.CurrentPhase == BattleAnimationManager.SpawnAnimationState.Phase.FadeIn)
                {
                    float progress = Math.Clamp(spawnAnim.Timer / BattleAnimationManager.SpawnAnimationState.FADE_DURATION, 0f, 1f);
                    spawnYOffset = MathHelper.Lerp(-BattleAnimationManager.SpawnAnimationState.DROP_HEIGHT, 0f, Easing.EaseOutCubic(progress));
                }
                else if (spawnAnim != null && spawnAnim.CurrentPhase == BattleAnimationManager.SpawnAnimationState.Phase.Flash)
                {
                    spawnYOffset = -BattleAnimationManager.SpawnAnimationState.DROP_HEIGHT;
                }

                // Apply Switch In Offset
                var switchInAnim = animationManager.GetSwitchInAnimationState(enemy.CombatantID);
                if (switchInAnim != null)
                {
                    float progress = Math.Clamp(switchInAnim.Timer / BattleAnimationManager.SwitchInAnimationState.DURATION, 0f, 1f);
                    spawnYOffset = MathHelper.Lerp(-BattleAnimationManager.SwitchInAnimationState.DROP_HEIGHT, 0f, Easing.EaseOutCubic(progress));
                }

                // Apply Switch Out Offset
                var switchOutAnim = animationManager.GetSwitchOutAnimationState(enemy.CombatantID);
                if (switchOutAnim != null)
                {
                    float progress = Math.Clamp(switchOutAnim.Timer / BattleAnimationManager.SwitchOutAnimationState.DURATION, 0f, 1f);
                    spawnYOffset = MathHelper.Lerp(0f, -BattleAnimationManager.SwitchOutAnimationState.LIFT_HEIGHT, Easing.EaseOutCubic(progress));
                }

                Vector2 spritePos = new Vector2((int)(slotCenter.X - spritePartSize / 2f), (int)(slotCenter.Y + yBobOffset + spawnYOffset));
                Rectangle spriteBounds = GetEnemyStaticSpriteBounds(enemy, spritePos);

                if (spriteBounds.IsEmpty)
                {
                    spriteBounds = new Rectangle((int)spritePos.X, (int)slotCenter.Y, spritePartSize, spritePartSize);
                }

                _currentTargets.Add(new TargetInfo
                {
                    Combatant = enemy,
                    Bounds = spriteBounds
                });
            }
        }

        private void DrawPlayerHud(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, BattleCombatant player, BattleCombatant currentActor, GameTime gameTime, BattleAnimationManager animationManager, BattleUIManager uiManager, HoverHighlightState hoverHighlightState, bool shouldGrayOutUnselectable, HashSet<BattleCombatant> selectableTargets, bool isTargetingPhase, float pulseAlpha, Dictionary<string, Color> silhouetteColors, BattleInputHandler inputHandler)
        {
            if (player == null) return;

            bool isRightSide = player.BattleSlot == 1;
            const int barWidth = 60;

            // --- NEW LAYOUT ---
            // 1. Horizontal Centering (1/4 and 3/4 of screen)
            float spriteCenterX = isRightSide ? (Global.VIRTUAL_WIDTH * 0.75f) : (Global.VIRTUAL_WIDTH * 0.25f);
            float startX = spriteCenterX - (barWidth / 2f);

            // 2. Vertical Positioning
            float heartCenterY = 99f; // Sprite Center (Moved down 1px from 98)
            float barsY = 78f;        // Bars Top (Unchanged)
            float nameY = 111f;       // Name Top (Moved up 3px from 114)

            bool isSelectable = selectableTargets.Contains(player);
            bool isSilhouetted = shouldGrayOutUnselectable && !isSelectable;
            Color? silhouetteColor = isSilhouetted ? _global.Palette_DarkerGray : null;

            _combatantVisualCenters[player.CombatantID] = new Vector2(spriteCenterX, heartCenterY);
            if (player.BattleSlot == 0) PlayerSpritePosition = new Vector2(spriteCenterX, heartCenterY);

            Color? playerSpriteTint = null;

            // --- HEAL FLASH LOGIC ---
            var healFlash = animationManager.GetHealFlashAnimationState(player.CombatantID);
            if (healFlash != null)
            {
                float progress = healFlash.Timer / BattleAnimationManager.HealFlashAnimationState.Duration;
                float flashIntensity = 1.0f - Easing.EaseOutQuad(progress);
                // Tint Greenish-White
                playerSpriteTint = Color.Lerp(Color.White, _global.Palette_LightGreen, flashIntensity * 0.8f);
            }

            bool isHighlighted = isSelectable && shouldGrayOutUnselectable;
            Color? highlightColor = null;
            if (isHighlighted && silhouetteColors.TryGetValue(player.CombatantID, out var color))
            {
                highlightColor = color;
            }

            float yBobOffset = CalculateAttackBobOffset(player.CombatantID, isPlayer: true);

            // Apply Heal Bounce Offset
            var healBounce = animationManager.GetHealBounceAnimationState(player.CombatantID);
            if (healBounce != null)
            {
                float progress = healBounce.Timer / BattleAnimationManager.HealBounceAnimationState.Duration;
                float bounce = MathF.Sin(progress * MathHelper.Pi) * -BattleAnimationManager.HealBounceAnimationState.Height;
                yBobOffset += bounce;
            }

            // Apply Recoil Offset
            Vector2 recoilOffset = Vector2.Zero;
            if (_recoilStates.TryGetValue(player.CombatantID, out var recoil))
            {
                recoilOffset = recoil.Offset;
            }

            // --- APPLY SWITCH ANIMATION OFFSETS ---
            var switchOutAnim = animationManager.GetSwitchOutAnimationState(player.CombatantID);
            var switchInAnim = animationManager.GetSwitchInAnimationState(player.CombatantID);

            float spawnYOffset = 0f;
            float spawnAlpha = 1.0f;

            if (switchOutAnim != null)
            {
                float progress = Math.Clamp(switchOutAnim.Timer / BattleConstants.SWITCH_ANIMATION_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutCubic(progress);
                // Players are at the bottom, so "Lift" means moving UP (negative Y).
                spawnYOffset = MathHelper.Lerp(0f, -BattleConstants.SWITCH_VERTICAL_OFFSET, easedProgress);
                spawnAlpha = 1.0f - easedProgress;
            }
            else if (switchInAnim != null)
            {
                float progress = Math.Clamp(switchInAnim.Timer / BattleConstants.SWITCH_ANIMATION_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutCubic(progress);
                // Drop means coming from above.
                // Start: -Height. End: 0.
                spawnYOffset = MathHelper.Lerp(-BattleConstants.SWITCH_VERTICAL_OFFSET, 0f, easedProgress);
                spawnAlpha = easedProgress;
            }

            // Apply spawn alpha to the tint color
            if (playerSpriteTint == null) playerSpriteTint = Color.White;
            playerSpriteTint = playerSpriteTint.Value * spawnAlpha;

            // --- SILHOUETTE LOGIC FOR STAT TOOLTIP ---
            bool isStatTooltipActive = player.CombatantID == _statTooltipCombatantID && _statTooltipAlpha > 0f;
            if (isStatTooltipActive)
            {
                isSilhouetted = true;
                silhouetteColor = _global.Palette_DarkerGray;
            }

            if (!_playerSprites.TryGetValue(player.CombatantID, out var sprite))
            {
                sprite = new PlayerCombatSprite(player.ArchetypeId);
                _playerSprites[player.CombatantID] = sprite;
            }

            // Apply spawnYOffset to the position
            sprite.SetPosition(new Vector2(spriteCenterX, heartCenterY + yBobOffset + spawnYOffset) + recoilOffset);

            bool isTurn = player == currentActor;
            Color outlineColor = isTurn ? _global.Palette_BrightWhite : _global.Palette_DarkGray;

            sprite.Draw(spriteBatch, animationManager, player, playerSpriteTint, isHighlighted, pulseAlpha, isSilhouetted, silhouetteColor, gameTime, highlightColor, outlineColor);

            // --- HUD VISIBILITY LOGIC ---
            var battleManager = ServiceLocator.Get<BattleManager>();
            bool isBrowsingMoves = (battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot1 || battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot2) && uiManager.UIState == BattleUIState.Default;
            bool isHoveringAction = isBrowsingMoves && uiManager.HoveredMove != null;

            bool showName;
            if (isHoveringAction)
            {
                // Show name only if it's the actor AND they are a valid target for the hovered move
                showName = (player == currentActor) && selectableTargets.Contains(player);
            }
            else if (isTargetingPhase && player == currentActor)
            {
                // Targeting Menu: Always show actor name
                showName = true;
            }
            else
            {
                // Default: Hide if silhouetted, UNLESS it's the stat tooltip causing it
                showName = !isSilhouetted || isStatTooltipActive;
            }

            // --- VISIBILITY LOGIC FOR BARS ---
            // Check if this combatant is hovered
            bool isHovered = (inputHandler.HoveredTargetIndex != -1 && _currentTargets.Count > inputHandler.HoveredTargetIndex && _currentTargets[inputHandler.HoveredTargetIndex].Combatant == player) || (uiManager.HoveredCombatantFromUI == player);

            if (isHovered)
            {
                player.HealthBarVisibleTimer = BAR_VISIBILITY_DURATION;
                player.ManaBarVisibleTimer = BAR_VISIBILITY_DURATION;
            }

            // Update Visual Alphas (Logic Alpha)
            UpdateBarAlpha(player, (float)gameTime.ElapsedGameTime.TotalSeconds);

            // Determine Draw Alphas (Logic Alpha vs Targeting Override)
            // If selectable (targeting), force 1.0. When unselected, it snaps back to VisualAlpha (which is 0.1 if timer is 0).
            float hpDrawAlpha = selectableTargets.Contains(player) ? 1.0f : player.VisualHealthBarAlpha;
            float manaDrawAlpha = selectableTargets.Contains(player) ? 1.0f : player.VisualManaBarAlpha;

            if (showName)
            {
                Color nameColor = _global.Palette_BrightWhite;
                bool isSelectionPhase = battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot1 || battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot2;
                var selectingActor = battleManager.CurrentActingCombatant;

                BitmapFont nameFontToUse = font;

                if (isSelectionPhase && selectingActor != null && player != selectingActor)
                {
                    nameColor = _global.Palette_Gray;
                    nameFontToUse = secondaryFont;
                }

                // Logic for Actor Name Color
                if (player == currentActor)
                {
                    if (isHoveringAction || isTargetingPhase)
                    {
                        // If highlighted (Yellow), stay Yellow. Otherwise DarkGray.
                        if (highlightColor.HasValue && highlightColor.Value == Color.Yellow)
                        {
                            nameColor = _global.Palette_Yellow;
                        }
                        else
                        {
                            nameColor = _global.Palette_DarkGray;
                        }
                    }
                    else if (highlightColor.HasValue && highlightColor.Value == Color.Yellow)
                    {
                        // Fallback for other phases if highlighted
                        nameColor = _global.Palette_Yellow;
                    }
                }
                else if (highlightColor.HasValue && highlightColor.Value == Color.Yellow)
                {
                    nameColor = _global.Palette_Yellow;
                }

                Vector2 nameSize = nameFontToUse.MeasureString(player.Name);

                // Center name on sprite
                float nameX = spriteCenterX - (nameSize.X / 2f);

                float finalNameY = nameY;
                if (nameFontToUse == secondaryFont)
                {
                    float heightDiff = font.LineHeight - secondaryFont.LineHeight;
                    finalNameY += heightDiff / 2f;
                }

                Vector2 namePos = new Vector2(nameX, finalNameY);
                spriteBatch.DrawStringSnapped(nameFontToUse, player.Name, namePos, nameColor);
            }

            DrawPlayerResourceBars(spriteBatch, player, new Vector2(startX, barsY), barWidth, uiManager, animationManager, gameTime, hpDrawAlpha, manaDrawAlpha);

            DrawPlayerStatusIcons(spriteBatch, player, secondaryFont, (int)barsY, startX, barWidth);

            // --- ALWAYS REGISTER HITBOX ---
            Rectangle spriteBounds = sprite.GetStaticBounds(animationManager, player);

            if (spriteBounds.IsEmpty)
            {
                spriteBounds = new Rectangle((int)startX, (int)heartCenterY - 25, barWidth, 50);
            }

            _currentTargets.Add(new TargetInfo
            {
                Combatant = player,
                Bounds = spriteBounds
            });
        }

        private void DrawBarAnimationOverlay(SpriteBatch spriteBatch, Rectangle bgRect, float maxResource, BattleAnimationManager.ResourceBarAnimationState anim)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            float percentBefore = anim.ValueBefore / maxResource;
            float percentAfter = anim.ValueAfter / maxResource;

            int widthBefore = (int)(bgRect.Width * percentBefore);
            int widthAfter = (int)(bgRect.Width * percentAfter);

            if (anim.AnimationType == BattleAnimationManager.ResourceBarAnimationState.BarAnimationType.Loss)
            {
                int previewStartX = bgRect.X + widthAfter;
                int previewWidth = widthBefore - widthAfter;
                var previewRect = new Rectangle(previewStartX, bgRect.Y, previewWidth, bgRect.Height);

                switch (anim.CurrentLossPhase)
                {
                    case BattleAnimationManager.ResourceBarAnimationState.LossPhase.Preview:
                        Color previewColor = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP)
                            ? _global.Palette_Red
                            : Color.White;
                        spriteBatch.DrawSnapped(pixel, previewRect, previewColor);
                        break;
                    case BattleAnimationManager.ResourceBarAnimationState.LossPhase.FlashBlack:
                        spriteBatch.DrawSnapped(pixel, previewRect, Color.Black);
                        break;
                    case BattleAnimationManager.ResourceBarAnimationState.LossPhase.FlashWhite:
                        spriteBatch.DrawSnapped(pixel, previewRect, Color.White);
                        break;
                    case BattleAnimationManager.ResourceBarAnimationState.LossPhase.Shrink:
                        float progress = anim.Timer / BattleAnimationManager.ResourceBarAnimationState.SHRINK_DURATION;
                        float easedProgress = Easing.EaseOutCubic(progress);
                        int shrinkingWidth = (int)(previewWidth * (1.0f - easedProgress));
                        var shrinkingRect = new Rectangle(previewRect.X, previewRect.Y, shrinkingWidth, previewRect.Height);

                        Color shrinkColor = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP)
                            ? _global.Palette_Red
                            : _global.Palette_White;

                        spriteBatch.DrawSnapped(pixel, shrinkingRect, shrinkColor);
                        break;
                }
            }
            else // Recovery
            {
                float progress = anim.Timer / BattleAnimationManager.ResourceBarAnimationState.GHOST_FILL_DURATION;
                float easedProgress = Easing.EaseOutCubic(progress);
                float currentFillPercent = MathHelper.Lerp(percentBefore, percentAfter, easedProgress);

                // Draw the "Ghost Fill" - a bright bar representing the amount being healed
                // It starts at the old value and extends to the new value.
                // We fade it out slightly as it fills.

                int ghostStartX = (int)(bgRect.X + bgRect.Width * percentBefore);
                int ghostWidth = (int)(bgRect.Width * (percentAfter - percentBefore));

                // Ensure at least 1 pixel if healing occurred
                if (percentAfter > percentBefore && ghostWidth == 0) ghostWidth = 1;

                if (ghostWidth > 0)
                {
                    var ghostRect = new Rectangle(ghostStartX, bgRect.Y, ghostWidth, bgRect.Height);

                    // Bright color for the ghost fill
                    Color ghostColor = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP)
                        ? Color.Lerp(Color.White, _global.Palette_LightGreen, 0.5f)
                        : Color.Lerp(Color.White, _global.Palette_LightBlue, 0.5f);

                    // Fade out near the end
                    float alpha = 1.0f;
                    if (progress > 0.7f)
                    {
                        alpha = 1.0f - ((progress - 0.7f) / 0.3f);
                    }

                    spriteBatch.DrawSnapped(pixel, ghostRect, ghostColor * alpha);
                }
            }
        }

        private void DrawPlayerResourceBars(SpriteBatch spriteBatch, BattleCombatant player, Vector2 position, int width, BattleUIManager uiManager, BattleAnimationManager animationManager, GameTime gameTime, float hpAlpha, float manaAlpha)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var spriteManager = ServiceLocator.Get<SpriteManager>();

            // --- HEALTH BAR ---
            float hpPercent = player.Stats.MaxHP > 0 ? Math.Clamp(player.VisualHP / player.Stats.MaxHP, 0f, 1f) : 0f;
            int hpWidth = (int)(width * hpPercent);
            if (hpPercent > 0 && hpWidth == 0) hpWidth = 1;

            var hpBgRect = new Rectangle((int)position.X, (int)position.Y + 1, width, 2);
            var hpFgRect = new Rectangle((int)position.X, (int)position.Y + 1, hpWidth, 2);

            // Draw Black Outline for HP Bar ONLY if alpha is high enough (e.g. > 0.5)
            // User requested: "When the are transparent, get rid of their outline too"
            // "Lasily, get rid of the palette_black outline around the active bars" -> Removed completely.
            // var hpBorderRect = new Rectangle(hpBgRect.X - 1, hpBgRect.Y - 1, hpBgRect.Width + 2, hpBgRect.Height + 2);
            // DrawRectangleBorder(spriteBatch, pixel, hpBorderRect, 1, _global.Palette_Black);

            spriteBatch.DrawSnapped(pixel, hpBgRect, _global.Palette_DarkGray * hpAlpha);
            spriteBatch.DrawSnapped(pixel, hpFgRect, _global.Palette_LightGreen * hpAlpha);

            var hpAnim = animationManager.GetResourceBarAnimation(player.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);
            if (hpAnim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, hpBgRect, player.Stats.MaxHP, hpAnim);
            }

            // --- MANA BAR ---
            float manaPercent = player.Stats.MaxMana > 0 ? Math.Clamp((float)player.Stats.CurrentMana / player.Stats.MaxMana, 0f, 1f) : 0f;
            int manaWidth = (int)(width * manaPercent);
            if (manaPercent > 0 && manaWidth == 0) manaWidth = 1;

            var manaBgRect = new Rectangle((int)position.X, (int)position.Y + 4, width, 1); // Changed to 1
            var manaFgRect = new Rectangle((int)position.X, (int)position.Y + 4, manaWidth, 1); // Changed to 1

            // Draw Black Outline for Mana Bar - Removed
            // var manaBorderRect = new Rectangle(manaBgRect.X - 1, manaBgRect.Y - 1, manaBgRect.Width + 2, manaBgRect.Height + 2);
            // DrawRectangleBorder(spriteBatch, pixel, manaBorderRect, 1, _global.Palette_Black);

            spriteBatch.DrawSnapped(pixel, manaBgRect, _global.Palette_DarkGray * manaAlpha);

            // --- NEW: Draw Animated Mana Bar Texture ---
            if (spriteManager.ManaBarPattern != null)
            {
                int scrollX = (int)(gameTime.TotalGameTime.TotalSeconds * 15.0);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, null, new RasterizerState { ScissorTestEnable = true });

                var sourceRect = new Rectangle(scrollX, 0, manaWidth, 1);
                spriteBatch.Draw(spriteManager.ManaBarPattern, manaFgRect, sourceRect, _global.Palette_LightBlue * manaAlpha);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            }
            else
            {
                spriteBatch.DrawSnapped(pixel, manaFgRect, _global.Palette_LightBlue * manaAlpha);
            }

            var manaAnim = animationManager.GetResourceBarAnimation(player.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.Mana);
            if (manaAnim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, manaBgRect, player.Stats.MaxMana, manaAnim);
            }

            var hoveredMove = uiManager.HoveredMove;

            // Check for ManaDump ability
            bool isManaDump = hoveredMove != null && hoveredMove.Abilities.Any(a => a is ManaDumpAbility);

            if (hoveredMove != null && hoveredMove.MoveType == MoveType.Spell && (hoveredMove.ManaCost > 0 || isManaDump))
            {
                var battleManager = ServiceLocator.Get<BattleManager>();
                if (battleManager.CurrentActingCombatant == player)
                {
                    int effectiveCost = hoveredMove.ManaCost;
                    if (isManaDump)
                    {
                        effectiveCost = player.Stats.CurrentMana;
                    }

                    if (player.Stats.CurrentMana >= effectiveCost && effectiveCost > 0)
                    {
                        const float PULSE_SPEED = 4f;
                        float pulse = (MathF.Sin(uiManager.SharedPulseTimer * PULSE_SPEED) + 1f) / 2f;
                        Color pulseColor = Color.Lerp(_global.Palette_Yellow, _global.Palette_BrightWhite, pulse);

                        float costPercent = (float)effectiveCost / player.Stats.MaxMana;
                        int costWidth = (int)(width * costPercent);

                        int previewX = (int)(position.X + manaWidth - costWidth);

                        var previewRect = new Rectangle(
                            previewX,
                            (int)position.Y + 4,
                            costWidth,
                            1 // Changed to 1
                        );

                        spriteBatch.DrawSnapped(pixel, previewRect, pulseColor * manaAlpha);
                    }
                    else
                    {
                        var previewRect = new Rectangle(
                            (int)position.X,
                            (int)position.Y + 4,
                            manaWidth,
                            1 // Changed to 1
                        );
                        spriteBatch.DrawSnapped(pixel, previewRect, _global.Palette_Red * manaAlpha);
                    }
                }
            }
        }

        private void DrawPlayerStatusIcons(SpriteBatch spriteBatch, BattleCombatant player, BitmapFont font, int hudY, float startX, int barWidth)
        {
            if (player == null || !player.ActiveStatusEffects.Any()) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            const int iconSize = 5;
            const int iconGap = 1;

            int iconY = hudY - iconSize - 2;

            int currentX;
            int step;

            if (player.BattleSlot == 0)
            {
                currentX = (int)startX;
                step = iconSize + iconGap;
            }
            else
            {
                currentX = (int)(startX + barWidth - iconSize);
                step = -(iconSize + iconGap);
            }

            foreach (var effect in player.ActiveStatusEffects)
            {
                // Apply Hop Offset
                float hopOffset = GetStatusIconOffset(player.CombatantID, effect.EffectType);
                bool isAnimating = IsStatusIconAnimating(player.CombatantID, effect.EffectType);

                var iconBounds = new Rectangle(currentX, (int)(iconY + hopOffset), iconSize, iconSize);
                _playerStatusIcons.Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });

                var iconTexture = _spriteManager.GetStatusEffectIcon(effect.EffectType);
                spriteBatch.DrawSnapped(iconTexture, iconBounds, Color.White);

                if (isAnimating)
                {
                    // Draw the icon again with Additive blending to make it glow white
                    spriteBatch.End();
                    spriteBatch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp);
                    spriteBatch.DrawSnapped(iconTexture, iconBounds, Color.White);
                    spriteBatch.End();
                    // Resume normal drawing
                    spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
                }

                if (_hoveredStatusIcon.HasValue && _hoveredStatusIcon.Value.Effect == effect)
                {
                    var borderBounds = new Rectangle(iconBounds.X - 1, iconBounds.Y - 1, iconBounds.Width + 2, iconBounds.Height + 2);
                    DrawRectangleBorder(spriteBatch, pixel, borderBounds, 1, Color.White);
                }

                currentX += step;
            }
        }

        private void DrawUITitle(SpriteBatch spriteBatch, BitmapFont secondaryFont, GameTime gameTime, BattleSubMenuState subMenuState)
        {
            string title = "";
            if (!string.IsNullOrEmpty(title))
            {
                float titleBobOffset = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * TITLE_INDICATOR_BOB_SPEED * MathF.PI) > 0) ? -1f : 0f;
                var titleSize = secondaryFont.MeasureString(title);
                var titleY = DIVIDER_Y + 3;
                var titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.Width) / 2, titleY + titleBobOffset);
                spriteBatch.DrawStringSnapped(secondaryFont, title, titlePos, _global.Palette_LightGray);
            }
        }

        private void DrawTargetingUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, BattleUIManager uiManager, BattleInputHandler inputHandler)
        {
            if (uiManager.UIState == BattleUIState.Targeting || uiManager.UIState == BattleUIState.ItemTargeting)
            {
                const float minAlpha = 0.15f;
                const float maxAlpha = 0.75f;
                float pulse = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * MathHelper.TwoPi) + 1f) / 2f;
                float alpha = MathHelper.Lerp(minAlpha, maxAlpha, pulse);

                var pixel = ServiceLocator.Get<Texture2D>();

                var targetType = uiManager.TargetTypeForSelection;
                bool isMultiTarget = targetType == TargetType.Every || targetType == TargetType.Both || targetType == TargetType.All || targetType == TargetType.Team || targetType == TargetType.RandomAll || targetType == TargetType.RandomBoth || targetType == TargetType.RandomEvery;
                bool isAnyHovered = inputHandler.HoveredTargetIndex != -1;

                // --- FIX: Filter for Valid Targets ---
                var battleManager = ServiceLocator.Get<BattleManager>();
                var currentActor = battleManager.CurrentActingCombatant;
                var allCombatants = battleManager.AllCombatants;
                var validTargets = TargetingHelper.GetValidTargets(currentActor, targetType ?? TargetType.None, allCombatants);

                for (int i = 0; i < _currentTargets.Count; i++)
                {
                    // Only draw dots if this combatant is a valid target
                    if (!validTargets.Contains(_currentTargets[i].Combatant)) continue;

                    bool shouldHighlight = false;

                    if (isMultiTarget && isAnyHovered)
                    {
                        shouldHighlight = true;
                    }
                    else if (i == inputHandler.HoveredTargetIndex)
                    {
                        shouldHighlight = true;
                    }

                    Color baseColor = shouldHighlight ? Color.Red : Color.Yellow;
                    Color boxColor = baseColor * alpha;
                    var bounds = _currentTargets[i].Bounds;

                    const int dotGap = 3;
                    int timeOffset = (int)(gameTime.TotalGameTime.TotalSeconds * 5) % dotGap;

                    int perimeter = (bounds.Width - 1) * 2 + (bounds.Height - 1) * 2;
                    if (perimeter <= 0) continue;

                    for (int p = 0; p < perimeter; p++)
                    {
                        if ((p + timeOffset) % dotGap == 0)
                        {
                            Vector2 position = GetPixelPositionOnPerimeter(p, bounds);
                            spriteBatch.DrawSnapped(pixel, position, boxColor);
                        }
                    }
                }
            }
        }

        private void DrawIndicatorArrow(SpriteBatch spriteBatch, GameTime gameTime, BattleCombatant combatant)
        {
            var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
            var arrowRects = _spriteManager.ArrowIconSourceRects;
            if (arrowSheet == null || arrowRects == null) return;

            Vector2 targetPos;
            if (_combatantVisualCenters.TryGetValue(combatant.CombatantID, out var center))
            {
                targetPos = center;
            }
            else
            {
                return;
            }

            var arrowRect = arrowRects[6];

            float attackBobOffset = CalculateAttackBobOffset(combatant.CombatantID, combatant.IsPlayerControlled);
            float idleBob = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 4f) > 0) ? -1f : 0f;

            // Apply Recoil Offset
            Vector2 recoilOffset = Vector2.Zero;
            if (_recoilStates.TryGetValue(combatant.CombatantID, out var recoil))
            {
                recoilOffset = recoil.Offset;
            }

            float topY;
            if (combatant.IsPlayerControlled)
            {
                topY = targetPos.Y - 16;
            }
            else
            {
                bool isMajor = _spriteManager.IsMajorEnemySprite(combatant.ArchetypeId);
                int height = isMajor ? 96 : 64;
                float rectTop = targetPos.Y - (height / 2f) - 4;
                topY = GetEnemySpriteStaticTopY(combatant, rectTop);
            }

            var arrowPos = new Vector2(targetPos.X - arrowRect.Width / 2, topY - arrowRect.Height - 1 + attackBobOffset + idleBob) + recoilOffset;
            spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, Color.White);
        }

        private void DrawCombatantHud(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, BattleCombatant combatant, Vector2 slotCenter, BattleCombatant currentActor, bool isTargetingPhase, bool shouldGrayOutUnselectable, HashSet<BattleCombatant> selectableTargets, BattleAnimationManager animationManager, HoverHighlightState hoverHighlightState, float pulseAlpha, GameTime gameTime, Color? highlightColor, Matrix transform, BattleInputHandler inputHandler, BattleUIManager uiManager, Color? tintOverride = null)
        {
            float yBobOffset = CalculateAttackBobOffset(combatant.CombatantID, isPlayer: false);

            // Apply Recoil Offset
            Vector2 recoilOffset = Vector2.Zero;
            if (_recoilStates.TryGetValue(combatant.CombatantID, out var recoil))
            {
                recoilOffset = recoil.Offset;
            }

            var spawnAnim = animationManager.GetSpawnAnimationState(combatant.CombatantID);
            var switchOutAnim = animationManager.GetSwitchOutAnimationState(combatant.CombatantID);
            var switchInAnim = animationManager.GetSwitchInAnimationState(combatant.CombatantID);

            float spawnYOffset = 0f;
            float spawnAlpha = 1.0f;
            float spawnSilhouetteAmount = 0f;
            Color? spawnSilhouetteColor = null;

            if (spawnAnim != null)
            {
                if (spawnAnim.CurrentPhase == BattleAnimationManager.SpawnAnimationState.Phase.Flash)
                {
                    int flashCycle = (int)(spawnAnim.Timer / BattleAnimationManager.SpawnAnimationState.FLASH_INTERVAL);
                    bool isVisible = flashCycle % 2 == 0;
                    spawnAlpha = 0f;
                    spawnSilhouetteAmount = 1.0f;
                    spawnSilhouetteColor = isVisible ? Color.White : Color.Transparent;
                    spawnYOffset = -BattleAnimationManager.SpawnAnimationState.DROP_HEIGHT;
                }
                else if (spawnAnim.CurrentPhase == BattleAnimationManager.SpawnAnimationState.Phase.FadeIn)
                {
                    float progress = Math.Clamp(spawnAnim.Timer / BattleAnimationManager.SpawnAnimationState.FADE_DURATION, 0f, 1f);
                    float easedProgress = Easing.EaseOutQuad(progress);
                    spawnAlpha = easedProgress;
                    spawnYOffset = MathHelper.Lerp(-BattleAnimationManager.SpawnAnimationState.DROP_HEIGHT, 0f, Easing.EaseOutCubic(progress));
                    spawnSilhouetteAmount = 0f;
                }
            }
            else if (switchOutAnim != null)
            {
                float progress = Math.Clamp(switchOutAnim.Timer / BattleAnimationManager.SwitchOutAnimationState.DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutCubic(progress);
                spawnYOffset = MathHelper.Lerp(0f, -BattleAnimationManager.SwitchOutAnimationState.LIFT_HEIGHT, Easing.EaseOutCubic(progress));
                spawnAlpha = 1.0f - easedProgress;
            }
            else if (switchInAnim != null)
            {
                float progress = Math.Clamp(switchInAnim.Timer / BattleAnimationManager.SwitchInAnimationState.DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutCubic(progress);
                spawnYOffset = MathHelper.Lerp(-BattleAnimationManager.SwitchInAnimationState.DROP_HEIGHT, 0f, easedProgress);
                spawnAlpha = easedProgress;
            }

            var pixel = ServiceLocator.Get<Texture2D>();
            bool isMajor = _spriteManager.IsMajorEnemySprite(combatant.ArchetypeId);
            int spritePartSize = isMajor ? 96 : 64;

            var staticRect = new Rectangle((int)(slotCenter.X - spritePartSize / 2f), (int)slotCenter.Y, spritePartSize, spritePartSize);
            _combatantVisualCenters[combatant.CombatantID] = staticRect.Center.ToVector2();

            // Apply recoil to the sprite rect
            var spriteRect = new Rectangle(
                (int)(slotCenter.X - spritePartSize / 2f + recoilOffset.X),
                (int)(slotCenter.Y + yBobOffset + spawnYOffset + recoilOffset.Y),
                spritePartSize,
                spritePartSize
            );

            float finalAlpha = combatant.VisualAlpha * spawnAlpha;
            Color baseColor = tintOverride ?? Color.White;
            Color tintColor = baseColor * finalAlpha;

            bool isTurn = combatant == currentActor;
            Color baseOutline = isTurn ? _global.Palette_BrightWhite : _global.Palette_DarkGray;
            Color outlineColor = baseOutline * finalAlpha;

            bool isSelectable = selectableTargets.Contains(combatant);

            float silhouetteFactor = spawnAnim != null ? spawnSilhouetteAmount : combatant.VisualSilhouetteAmount;
            Color silhouetteColor = spawnAnim != null ? (spawnSilhouetteColor ?? _global.Palette_DarkGray) : (combatant.VisualSilhouetteColorOverride ?? _global.Palette_DarkGray);

            bool isHighlighted = isSelectable && shouldGrayOutUnselectable;

            if (shouldGrayOutUnselectable && !isSelectable && spawnAnim == null && switchOutAnim == null && switchInAnim == null)
            {
                silhouetteFactor = 1.0f;
            }
            else if (isSelectable && shouldGrayOutUnselectable && !isTargetingPhase && spawnAnim == null && switchOutAnim == null && switchInAnim == null)
            {
                if (highlightColor == null) outlineColor = Color.Yellow * finalAlpha;
            }

            // --- SILHOUETTE LOGIC FOR STAT TOOLTIP ---
            if (combatant.CombatantID == _statTooltipCombatantID && _statTooltipAlpha > 0f)
            {
                silhouetteFactor = Math.Max(silhouetteFactor, _statTooltipAlpha);
                silhouetteColor = _global.Palette_DarkerGray;
            }

            // --- HITSTOP VISUAL OVERRIDE ---
            // If hitstop is active for this combatant, override scale and color
            float scale = 1.0f;
            var hitstopState = animationManager.GetHitstopVisualState(combatant.CombatantID);
            if (_hitstopManager.IsActive && hitstopState != null)
            {
                scale = HITSTOP_SCALE_MULTIPLIER;
                // Override tint to flash color (White for normal, Red for crit)
                tintColor = hitstopState.IsCrit ? Color.Red : Color.White;
                // Ensure full opacity
                finalAlpha = 1.0f;
                // Disable silhouette during flash
                silhouetteFactor = 0f;
            }

            if (silhouetteFactor < 1.0f && _spriteManager.ShadowBlobSprite != null)
            {
                float groundY = staticRect.Bottom;
                float heightFactor = 1.0f - Math.Clamp(Math.Abs(spawnYOffset) / 50f, 0f, 1f);

                Vector2 shadowAnimOffset = Vector2.Zero;
                if (_shadowOffsets.TryGetValue(combatant.CombatantID, out var sOffset))
                {
                    shadowAnimOffset = sOffset;
                }

                // Shadow stays on the ground, but moves with recoil X
                Vector2 shadowPos = new Vector2(spriteRect.Center.X, groundY) + shadowAnimOffset;
                Vector2 shadowOrigin = new Vector2(_spriteManager.ShadowBlobSprite.Width / 2f, _spriteManager.ShadowBlobSprite.Height / 2f);

                Color shadowTint = Color.White * (heightFactor * finalAlpha);

                spriteBatch.DrawSnapped(_spriteManager.ShadowBlobSprite, shadowPos, null, shadowTint, 0f, shadowOrigin, 1.0f, SpriteEffects.None, 0f);
            }

            Texture2D enemySprite = _spriteManager.GetEnemySprite(combatant.ArchetypeId);
            Texture2D enemySilhouette = _spriteManager.GetEnemySpriteSilhouette(combatant.ArchetypeId);

            float highestPixelY = float.MaxValue;

            if (enemySprite != null)
            {
                int numParts = enemySprite.Width / spritePartSize;
                if (!_enemySpritePartOffsets.ContainsKey(combatant.CombatantID) || _enemySpritePartOffsets[combatant.CombatantID].Length != numParts)
                {
                    _enemySpritePartOffsets[combatant.CombatantID] = new Vector2[numParts];
                    _enemyAnimationTimers[combatant.CombatantID] = new float[numParts];
                    var intervals = new float[numParts];
                    for (int i = 0; i < numParts; i++)
                    {
                        intervals[i] = (float)(_random.NextDouble() * (ENEMY_ANIM_MAX_INTERVAL - ENEMY_ANIM_MIN_INTERVAL) + ENEMY_ANIM_MIN_INTERVAL);
                        _enemyAnimationTimers[combatant.CombatantID][i] = (float)_random.NextDouble();
                    }
                    _enemyAnimationIntervals[combatant.CombatantID] = intervals;
                }

                if (_enemySpritePartOffsets.TryGetValue(combatant.CombatantID, out var offsets))
                {
                    var hitFlashState = animationManager.GetHitFlashState(combatant.CombatantID);
                    bool isFlashingWhite = hitFlashState != null && hitFlashState.IsCurrentlyWhite;
                    Vector2 shakeOffset = hitFlashState?.ShakeOffset ?? Vector2.Zero;

                    // --- NEW: Fused Outline Logic ---
                    if (enemySilhouette != null && silhouetteFactor < 1.0f && !isHighlighted)
                    {
                        // 1. Generate Composite Silhouette
                        var currentRTs = _core.GraphicsDevice.GetRenderTargets();
                        spriteBatch.End();
                        _core.GraphicsDevice.SetRenderTarget(_flattenTarget);
                        _core.GraphicsDevice.Clear(Color.Transparent);
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

                        Vector2 rtBasePos = new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);

                        for (int i = 0; i < numParts; i++)
                        {
                            var sourceRect = new Rectangle(i * spritePartSize, 0, spritePartSize, spritePartSize);
                            var partOffset = offsets[i];
                            // Draw silhouette in White
                            spriteBatch.DrawSnapped(enemySilhouette, rtBasePos + partOffset, sourceRect, Color.White);
                        }

                        spriteBatch.End();
                        _core.GraphicsDevice.SetRenderTargets(currentRTs);
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

                        // 2. Draw Outlines
                        Vector2 screenDrawPos = new Vector2(spriteRect.X, spriteRect.Y) + shakeOffset - rtBasePos;
                        Color cBlack = _global.Palette_Black * finalAlpha;
                        Color cColored = outlineColor;

                        // Layer 3: Outer Black (3px)
                        // Cardinals at 3
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(-3, 0), cBlack);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(3, 0), cBlack);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(0, -3), cBlack);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(0, 3), cBlack);
                        // Diagonals at 2
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(-2, -2), cBlack);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(2, -2), cBlack);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(-2, 2), cBlack);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(2, 2), cBlack);

                        // Layer 2: Middle Colored (2px)
                        // Cardinals at 2
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(-2, 0), cColored);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(2, 0), cColored);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(0, -2), cColored);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(0, 2), cColored);
                        // Diagonals at 1
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(-1, -1), cColored);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(1, -1), cColored);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(-1, 1), cColored);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(1, 1), cColored);

                        // Layer 1: Inner Black (1px)
                        // Cardinals at 1
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(-1, 0), cBlack);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(1, 0), cBlack);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(0, -1), cBlack);
                        DrawComposite(spriteBatch, _flattenTarget, screenDrawPos, new Vector2(0, 1), cBlack);
                    }

                    bool useFlattening = finalAlpha < 1.0f;

                    if (useFlattening)
                    {
                        var currentRTs = _core.GraphicsDevice.GetRenderTargets();
                        spriteBatch.End();
                        _core.GraphicsDevice.SetRenderTarget(_flattenTarget);
                        _core.GraphicsDevice.Clear(Color.Transparent);
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

                        Vector2 rtOffset = new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);

                        for (int i = 0; i < numParts; i++)
                        {
                            var sourceRect = new Rectangle(i * spritePartSize, 0, spritePartSize, spritePartSize);
                            var partOffset = offsets[i];
                            var localDrawPos = rtOffset + partOffset + shakeOffset;

                            if (silhouetteFactor >= 1.0f && enemySilhouette != null)
                            {
                                spriteBatch.DrawSnapped(enemySilhouette, localDrawPos, sourceRect, silhouetteColor);
                            }
                            else if (isHighlighted && enemySilhouette != null)
                            {
                                Color hColor = highlightColor ?? Color.Yellow;
                                spriteBatch.DrawSnapped(enemySilhouette, localDrawPos, sourceRect, hColor);
                            }
                            else
                            {
                                spriteBatch.DrawSnapped(enemySprite, localDrawPos, sourceRect, Color.White);
                                if (silhouetteFactor > 0f && enemySilhouette != null)
                                {
                                    spriteBatch.DrawSnapped(enemySilhouette, localDrawPos, sourceRect, silhouetteColor * silhouetteFactor);
                                }
                            }

                            if (isFlashingWhite && enemySilhouette != null)
                            {
                                spriteBatch.DrawSnapped(enemySilhouette, localDrawPos, sourceRect, Color.White * 0.8f);
                            }
                        }

                        spriteBatch.End();
                        _core.GraphicsDevice.SetRenderTargets(currentRTs);
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

                        Vector2 drawPos = spriteRect.Location.ToVector2() - new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);
                        var srcRect = new Rectangle(0, 0, spritePartSize + FLATTEN_MARGIN * 2, spritePartSize + FLATTEN_MARGIN * 2);
                        spriteBatch.Draw(_flattenTarget, drawPos, srcRect, Color.White * finalAlpha);
                    }
                    else
                    {
                        for (int i = 0; i < numParts; i++)
                        {
                            var sourceRect = new Rectangle(i * spritePartSize, 0, spritePartSize, spritePartSize);
                            var partOffset = offsets[i];
                            var drawPosition = new Vector2(spriteRect.X + partOffset.X, spriteRect.Y + partOffset.Y) + shakeOffset;

                            // Calculate center for scaling
                            Vector2 origin = new Vector2(spritePartSize / 2f, spritePartSize / 2f);
                            Vector2 centerPos = drawPosition + origin;

                            float currentPartTopY = GetEnemySpriteStaticTopY(combatant, spriteRect.Y);
                            if (currentPartTopY < highestPixelY)
                            {
                                highestPixelY = currentPartTopY;
                            }

                            if (silhouetteFactor >= 1.0f && enemySilhouette != null)
                            {
                                spriteBatch.DrawSnapped(enemySilhouette, centerPos, sourceRect, silhouetteColor * finalAlpha, 0f, origin, scale, SpriteEffects.None, 0f);
                            }
                            else if (isHighlighted && enemySilhouette != null)
                            {
                                Color hColor = highlightColor ?? Color.Yellow;
                                spriteBatch.DrawSnapped(enemySilhouette, centerPos, sourceRect, hColor * finalAlpha, 0f, origin, scale, SpriteEffects.None, 0f);
                            }
                            else
                            {
                                spriteBatch.DrawSnapped(enemySprite, centerPos, sourceRect, tintColor, 0f, origin, scale, SpriteEffects.None, 0f);
                                if (silhouetteFactor > 0f && enemySilhouette != null)
                                {
                                    spriteBatch.DrawSnapped(enemySilhouette, centerPos, sourceRect, silhouetteColor * silhouetteFactor * finalAlpha, 0f, origin, scale, SpriteEffects.None, 0f);
                                }
                            }

                            if (isFlashingWhite && enemySilhouette != null)
                            {
                                spriteBatch.DrawSnapped(enemySilhouette, centerPos, sourceRect, Color.White * 0.8f, 0f, origin, scale, SpriteEffects.None, 0f);
                            }
                        }
                    }

                    if (isHighlighted)
                    {
                        var indicator = _spriteManager.TargetingIndicatorSprite;
                        if (indicator != null)
                        {
                            Vector2 visualCenterOffset = _spriteManager.GetVisualCenterOffset(combatant.ArchetypeId);
                            Vector2 spriteCenter = new Vector2(spriteRect.Center.X, spriteRect.Center.Y);
                            Vector2 targetCenter = new Vector2(spriteCenter.X, spriteCenter.Y + visualCenterOffset.Y);

                            float t = (float)gameTime.TotalGameTime.TotalSeconds * _global.TargetIndicatorNoiseSpeed;
                            int seed = (combatant.CombatantID.GetHashCode() + 1000) * 93821;

                            float nX = _swayNoise.Noise(t, seed);
                            float nY = _swayNoise.Noise(t, seed + 100);

                            float swayX = nX * _global.TargetIndicatorOffsetX;
                            float swayY = nY * _global.TargetIndicatorOffsetY;

                            float rotation = 0f;
                            float indicatorScale = 1.0f;

                            Vector2 animatedPos = targetCenter + new Vector2(swayX, swayY) + shakeOffset;
                            Vector2 origin = new Vector2(indicator.Width / 2f, indicator.Height / 2f);

                            if (highlightColor == Color.Yellow)
                            {
                                spriteBatch.DrawSnapped(indicator, animatedPos, null, Color.White, rotation, origin, indicatorScale, SpriteEffects.None, 0f);
                            }
                        }
                    }
                }
            }
            else
            {
                spriteBatch.DrawSnapped(pixel, spriteRect, _global.Palette_Pink * finalAlpha);
                highestPixelY = spriteRect.Top;
            }

            const int barHeight = 2;
            float barY = highestPixelY - barHeight - 2 - 8;
            barY = Math.Max(1, barY);

            const int barWidth = 40;
            float barX = slotCenter.X - barWidth / 2f;

            DrawEnemyStatusIcons(spriteBatch, combatant, barX, barY, barWidth);

            if (silhouetteFactor < 1.0f)
            {
                // --- VISIBILITY LOGIC FOR BARS ---
                // Check if this combatant is hovered
                bool isHovered = (inputHandler.HoveredTargetIndex != -1 && _currentTargets.Count > inputHandler.HoveredTargetIndex && _currentTargets[inputHandler.HoveredTargetIndex].Combatant == combatant) || (uiManager.HoveredCombatantFromUI == combatant);

                if (isHovered)
                {
                    combatant.HealthBarVisibleTimer = BAR_VISIBILITY_DURATION;
                    combatant.ManaBarVisibleTimer = BAR_VISIBILITY_DURATION;
                }

                // --- UPDATE VISUAL ALPHA ---
                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

                // Health Bar Alpha
                UpdateBarAlpha(combatant, dt);

                // Determine Draw Alphas (Logic Alpha vs Targeting Override)
                // If selectable (targeting), force 1.0. When unselected, it snaps back to VisualAlpha (which is 0.1 if timer is 0).
                float hpDrawAlpha = selectableTargets.Contains(combatant) ? 1.0f : combatant.VisualHealthBarAlpha;
                float manaDrawAlpha = selectableTargets.Contains(combatant) ? 1.0f : combatant.VisualManaBarAlpha;

                DrawEnemyHealthBar(spriteBatch, combatant, barX, barY, barWidth, barHeight, animationManager, hpDrawAlpha, gameTime);

                // Draw below health bar (barY + height + 1px gap)
                float manaBarY = barY + barHeight + 1;
                DrawEnemyManaBar(spriteBatch, combatant, barX, manaBarY, barWidth, 1, animationManager, manaDrawAlpha, gameTime); // Changed height to 1
            }

            if (selectableTargets.Contains(combatant))
            {
                Rectangle spriteBounds = GetEnemyStaticSpriteBounds(combatant, new Vector2(spriteRect.X, spriteRect.Y));

                if (spriteBounds.IsEmpty)
                {
                    spriteBounds = new Rectangle((int)(slotCenter.X - spritePartSize / 2), (int)slotCenter.Y, spritePartSize, spritePartSize);
                }

                _currentTargets.Add(new TargetInfo
                {
                    Combatant = combatant,
                    Bounds = spriteBounds
                });
            }
        }

        private void UpdateBarAlpha(BattleCombatant c, float dt)
        {
            // Health
            float targetHealth = c.HealthBarVisibleTimer > 0 ? 1.0f : BAR_MIN_ALPHA;
            if (targetHealth > c.VisualHealthBarAlpha) c.VisualHealthBarAlpha = targetHealth; // Instant appear
            else c.VisualHealthBarAlpha = MathHelper.Lerp(c.VisualHealthBarAlpha, targetHealth, dt * BAR_FADE_SPEED); // Smooth fade

            // Mana
            float targetMana = c.ManaBarVisibleTimer > 0 ? 1.0f : BAR_MIN_ALPHA;
            if (targetMana > c.VisualManaBarAlpha) c.VisualManaBarAlpha = targetMana;
            else c.VisualManaBarAlpha = MathHelper.Lerp(c.VisualManaBarAlpha, targetMana, dt * BAR_FADE_SPEED);
        }

        private void DrawEnemyHealthBar(SpriteBatch spriteBatch, BattleCombatant combatant, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float alpha, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            var barRect = new Rectangle(
                (int)barX,
                (int)barY,
                barWidth,
                barHeight
            );

            float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
            int fgWidth = (int)(barRect.Width * hpPercent);
            if (hpPercent > 0 && fgWidth == 0) fgWidth = 1;

            var hpFgRect = new Rectangle(barRect.X, barRect.Y, fgWidth, barRect.Height);

            // Draw Black Outline for HP Bar ONLY if alpha is high enough
            // Removed as requested: "When the are transparent, get rid of their outline too"
            // "Lasily, get rid of the palette_black outline around the active bars" -> Removed completely.
            // if (alpha > 0.5f)
            // {
            //     var hpBorderRect = new Rectangle(barRect.X - 1, barRect.Y - 1, barRect.Width + 2, barRect.Height + 2);
            //     DrawRectangleBorder(spriteBatch, pixel, hpBorderRect, 1, _global.Palette_Black);
            // }

            spriteBatch.DrawSnapped(pixel, barRect, _global.Palette_DarkGray * alpha);
            spriteBatch.DrawSnapped(pixel, hpFgRect, _global.Palette_LightGreen * alpha);

            var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);
            if (hpAnim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, barRect, combatant.Stats.MaxHP, hpAnim);
            }
        }

        private void DrawEnemyManaBar(SpriteBatch spriteBatch, BattleCombatant combatant, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float alpha, GameTime gameTime)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            var barRect = new Rectangle(
                (int)barX,
                (int)barY,
                barWidth,
                barHeight
            );

            float manaPercent = combatant.Stats.MaxMana > 0 ? Math.Clamp((float)combatant.Stats.CurrentMana / combatant.Stats.MaxMana, 0f, 1f) : 0f;
            int fgWidth = (int)(barRect.Width * manaPercent);
            if (manaPercent > 0 && fgWidth == 0) fgWidth = 1;

            var manaFgRect = new Rectangle(barRect.X, barRect.Y, fgWidth, barRect.Height);

            // Draw Black Outline for Mana Bar ONLY if alpha is high enough
            // Removed as requested
            // if (alpha > 0.5f)
            // {
            //     var manaBorderRect = new Rectangle(barRect.X - 1, barRect.Y - 1, barRect.Width + 2, barRect.Height + 2);
            //     DrawRectangleBorder(spriteBatch, pixel, manaBorderRect, 1, _global.Palette_Black);
            // }

            spriteBatch.DrawSnapped(pixel, barRect, _global.Palette_DarkGray * alpha);

            // --- Draw Animated Mana Bar Texture ---
            if (_spriteManager.ManaBarPattern != null)
            {
                int scrollX = (int)(gameTime.TotalGameTime.TotalSeconds * 15.0);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, null, new RasterizerState { ScissorTestEnable = true });

                var sourceRect = new Rectangle(scrollX, 0, fgWidth, 1);
                spriteBatch.Draw(_spriteManager.ManaBarPattern, manaFgRect, sourceRect, _global.Palette_LightBlue * alpha);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            }
            else
            {
                spriteBatch.DrawSnapped(pixel, manaFgRect, _global.Palette_LightBlue * alpha);
            }

            var manaAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.Mana);
            if (manaAnim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, barRect, combatant.Stats.MaxMana, manaAnim);
            }
        }

        private void DrawEnemyStatusIcons(SpriteBatch spriteBatch, BattleCombatant combatant, float barX, float barY, int barWidth)
        {
            if (!combatant.ActiveStatusEffects.Any()) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            if (!_enemyStatusIcons.ContainsKey(combatant.CombatantID))
            {
                _enemyStatusIcons[combatant.CombatantID] = new List<StatusIconInfo>();
            }
            _enemyStatusIcons[combatant.CombatantID].Clear();

            const int iconSize = 5;
            const int iconGap = 1;

            int iconY = (int)barY - iconSize - 2;

            int currentX = (int)barX;
            int step = iconSize + iconGap;

            foreach (var effect in combatant.ActiveStatusEffects)
            {
                // Apply Hop Offset
                float hopOffset = GetStatusIconOffset(combatant.CombatantID, effect.EffectType);
                bool isAnimating = IsStatusIconAnimating(combatant.CombatantID, effect.EffectType);

                var iconBounds = new Rectangle(currentX, (int)(iconY + hopOffset), iconSize, iconSize);
                _enemyStatusIcons[combatant.CombatantID].Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });
                var iconTexture = _spriteManager.GetStatusEffectIcon(effect.EffectType);
                spriteBatch.DrawSnapped(iconTexture, iconBounds, Color.White);

                if (isAnimating)
                {
                    // Draw the icon again with Additive blending to make it glow white
                    spriteBatch.End();
                    spriteBatch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp);
                    spriteBatch.DrawSnapped(iconTexture, iconBounds, Color.White);
                    spriteBatch.End();
                    // Resume normal drawing
                    spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
                }

                if (_hoveredStatusIcon.HasValue && _hoveredStatusIcon.Value.Effect == effect)
                {
                    var borderBounds = new Rectangle(iconBounds.X - 1, iconBounds.Y - 1, iconBounds.Width + 2, iconBounds.Height + 2);
                    DrawRectangleBorder(spriteBatch, pixel, borderBounds, 1, Color.White);
                }

                currentX += step;
            }
        }

        private void UpdateEnemyAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var combatantIds = _enemyAnimationTimers.Keys.ToList();
            foreach (var id in combatantIds)
            {
                var combatant = combatants.FirstOrDefault(c => c.CombatantID == id);
                if (combatant == null) continue;

                if (combatant.IsDefeated)
                {
                    if (_enemySpritePartOffsets.TryGetValue(id, out var offsets))
                    {
                        for (int k = 0; k < offsets.Length; k++) offsets[k] = Vector2.Zero;
                    }
                    continue;
                }

                var timers = _enemyAnimationTimers[id];
                var intervals = _enemyAnimationIntervals[id];
                var currentOffsets = _enemySpritePartOffsets[id];

                currentOffsets[0] = Vector2.Zero;

                for (int i = 1; i < currentOffsets.Length; i++)
                {
                    timers[i] += deltaTime;
                    if (timers[i] >= intervals[i])
                    {
                        timers[i] = 0f;
                        intervals[i] = (float)(_random.NextDouble() * (ENEMY_ANIM_MAX_INTERVAL - ENEMY_ANIM_MIN_INTERVAL) + ENEMY_ANIM_MIN_INTERVAL);

                        Vector2 pos = currentOffsets[i];

                        if (pos != Vector2.Zero)
                        {
                            pos = Vector2.Zero;
                        }
                        else
                        {
                            int direction = _random.Next(4);
                            switch (direction)
                            {
                                case 0: pos = new Vector2(0, -1); break;
                                case 1: pos = new Vector2(0, 1); break;
                                case 2: pos = new Vector2(-1, 0); break;
                                case 3: pos = new Vector2(1, 0); break;
                            }
                        }
                        currentOffsets[i] = pos;
                    }
                }
            }
        }

        private void UpdateShadowAnimations(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            foreach (var combatant in combatants)
            {
                if (combatant.IsPlayerControlled || combatant.IsDefeated) continue;

                string id = combatant.CombatantID;

                if (!_shadowTimers.ContainsKey(id))
                {
                    _shadowTimers[id] = (float)_random.NextDouble();
                    _shadowIntervals[id] = (float)(_random.NextDouble() * (SHADOW_ANIM_MAX_INTERVAL - SHADOW_ANIM_MIN_INTERVAL) + SHADOW_ANIM_MIN_INTERVAL);
                    _shadowOffsets[id] = Vector2.Zero;
                }

                _shadowTimers[id] += deltaTime;
                if (_shadowTimers[id] >= _shadowIntervals[id])
                {
                    _shadowTimers[id] = 0f;
                    _shadowIntervals[id] = (float)(_random.NextDouble() * (SHADOW_ANIM_MAX_INTERVAL - SHADOW_ANIM_MIN_INTERVAL) + SHADOW_ANIM_MIN_INTERVAL);

                    Vector2 current = _shadowOffsets[id];
                    if (current != Vector2.Zero)
                    {
                        _shadowOffsets[id] = Vector2.Zero;
                    }
                    else
                    {
                        int dir = _random.Next(4);
                        switch (dir)
                        {
                            case 0: _shadowOffsets[id] = new Vector2(0, -1); break;
                            case 1: _shadowOffsets[id] = new Vector2(0, 1); break;
                            case 2: _shadowOffsets[id] = new Vector2(-1, 0); break;
                            case 3: _shadowOffsets[id] = new Vector2(1, 0); break;
                        }
                    }
                }
            }
        }

        public Vector2 GetCombatantHudCenterPosition(BattleCombatant combatant, IEnumerable<BattleCombatant> allCombatants)
        {
            if (_combatantVisualCenters.TryGetValue(combatant.CombatantID, out var pos))
            {
                return pos;
            }
            return Vector2.Zero;
        }

        public Vector2 GetCombatantVisualCenterPosition(BattleCombatant combatant, IEnumerable<BattleCombatant> allCombatants)
        {
            if (_combatantVisualCenters.TryGetValue(combatant.CombatantID, out var pos))
            {
                return pos;
            }
            return Vector2.Zero;
        }

        private float GetEnemySpriteStaticTopY(BattleCombatant enemy, float spriteRectTopY)
        {
            var staticOffsets = _spriteManager.GetEnemySpriteTopPixelOffsets(enemy.ArchetypeId);
            if (staticOffsets == null)
            {
                return spriteRectTopY;
            }

            float minTopY = float.MaxValue;
            for (int i = 0; i < staticOffsets.Length; i++)
            {
                if (staticOffsets[i] == int.MaxValue) continue;
                float currentPartTopY = staticOffsets[i];
                minTopY = Math.Min(minTopY, currentPartTopY);
            }

            return minTopY == float.MaxValue ? spriteRectTopY : spriteRectTopY + minTopY;
        }

        private bool IsNegativeStatus(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison:
                case StatusEffectType.Stun:
                case StatusEffectType.Burn:
                case StatusEffectType.Frostbite:
                case StatusEffectType.Silence:
                    return true;
                default:
                    return false;
            }
        }

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }

        private Vector2 GetPixelPositionOnPerimeter(int distance, Rectangle bounds)
        {
            int topEdgeLength = bounds.Width - 1;
            int rightEdgeLength = bounds.Height - 1;
            int bottomEdgeLength = bounds.Width - 1;

            float x, y;

            if (distance < topEdgeLength)
            {
                x = bounds.Left + distance;
                y = bounds.Top;
            }
            else if (distance < topEdgeLength + rightEdgeLength)
            {
                x = bounds.Right - 1;
                y = bounds.Top + (distance - topEdgeLength);
            }
            else if (distance < topEdgeLength + rightEdgeLength + bottomEdgeLength)
            {
                x = (bounds.Right - 1) - (distance - (topEdgeLength + rightEdgeLength));
                y = bounds.Bottom - 1;
            }
            else
            {
                x = bounds.Left;
                y = (bounds.Bottom - 1) - (distance - (topEdgeLength + rightEdgeLength + bottomEdgeLength));
            }
            return new Vector2(x, y);
        }

        private float CalculateAttackBobOffset(string combatantId, bool isPlayer)
        {
            if (_attackAnimControllers.TryGetValue(combatantId, out var controller))
            {
                return controller.GetOffset(isPlayer);
            }
            return 0f;
        }

        private void DrawComposite(SpriteBatch sb, Texture2D tex, Vector2 basePos, Vector2 offset, Color c)
        {
            sb.Draw(tex, basePos + offset, c);
        }

        private Rectangle GetEnemyStaticSpriteBounds(BattleCombatant enemy, Vector2 basePosition)
        {
            string archetypeId = enemy.ArchetypeId;
            var topOffsets = _spriteManager.GetEnemySpriteTopPixelOffsets(archetypeId);
            var leftOffsets = _spriteManager.GetEnemySpriteLeftPixelOffsets(archetypeId);
            var rightOffsets = _spriteManager.GetEnemySpriteRightPixelOffsets(archetypeId);
            var bottomOffsets = _spriteManager.GetEnemySpriteBottomPixelOffsets(archetypeId);

            if (topOffsets == null) return Rectangle.Empty;

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int i = 0; i < topOffsets.Length; i++)
            {
                if (topOffsets[i] == int.MaxValue) continue;

                Vector2 offset = Vector2.Zero;

                float partDrawX = basePosition.X + offset.X;
                float partDrawY = basePosition.Y + offset.Y;

                int partLeft = (int)partDrawX + leftOffsets[i];
                int partRight = (int)partDrawX + rightOffsets[i] + 1;
                int partTop = (int)partDrawY + topOffsets[i];
                int partBottom = (int)partDrawY + bottomOffsets[i] + 1;

                if (partLeft < minX) minX = partLeft;
                if (partRight > maxX) maxX = partRight;
                if (partTop < minY) minY = partTop;
                if (partBottom > maxY) maxY = partBottom;
            }

            if (minX == int.MaxValue) return Rectangle.Empty;

            var rect = new Rectangle(minX, minY, maxX - minX, maxY - minY);
            rect.Inflate(4, 4);
            return rect;
        }
    }
}