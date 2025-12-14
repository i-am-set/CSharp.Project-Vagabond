using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
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
        // Dependencies
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly Core _core;
        private readonly TooltipManager _tooltipManager;

        // Sprite Management
        private readonly Dictionary<string, PlayerCombatSprite> _playerSprites = new Dictionary<string, PlayerCombatSprite>();

        // Rendering Resources
        private RenderTarget2D _flattenTarget;
        private const int FLATTEN_TARGET_SIZE = 256;
        private const int FLATTEN_MARGIN = 16; // Margin to prevent clipping of animated parts

        // State
        private List<TargetInfo> _currentTargets = new List<TargetInfo>();
        private readonly List<StatusIconInfo> _playerStatusIcons = new List<StatusIconInfo>();
        private readonly Dictionary<string, List<StatusIconInfo>> _enemyStatusIcons = new Dictionary<string, List<StatusIconInfo>>();
        private StatusIconInfo? _hoveredStatusIcon;

        // We now need to track positions for multiple players
        public Vector2 PlayerSpritePosition { get; private set; } // Kept for legacy animation compatibility
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
        private const float SHADOW_ANIM_MIN_INTERVAL = 0.8f; // Slower than limbs
        private const float SHADOW_ANIM_MAX_INTERVAL = 1.2f;

        // Attacker Animation
        private readonly Dictionary<string, float> _attackAnimTimers = new();
        private string? _lastAttackerId;
        private const float ATTACK_BOB_DURATION = 0.35f; // Slightly increased to make the bounce readable
        private const float ATTACK_BOB_AMOUNT = 6f; // Reduced for a "short" jump

        // Layout Constants
        private const int DIVIDER_Y = 123;
        private const int MAX_ENEMIES = 5;
        private const float PLAYER_INDICATOR_BOB_SPEED = 0.75f;
        private const float TITLE_INDICATOR_BOB_SPEED = PLAYER_INDICATOR_BOB_SPEED / 2f;
        private const int ENEMY_SLOT_Y_OFFSET = 16; // Shift enemies down

        // --- Targeting Indicator Animation Tuning ---
        // Note: Rotation and Scale tuning variables are no longer used, but kept in Global for compatibility if needed later.
        // We only use Position Strength (Offset) and Speed now.

        // Noise generator for organic sway
        private static readonly SeededPerlin _swayNoise = new SeededPerlin(9999);

        public BattleRenderer()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();

            // Initialize the render target for flattening transparent sprites
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
            _attackAnimTimers.Clear();
            _combatantVisualCenters.Clear();
            _playerSprites.Clear();
            _lastAttackerId = null;
        }

        public List<TargetInfo> GetCurrentTargets() => _currentTargets;

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants, BattleAnimationManager animationManager, BattleCombatant? currentActor)
        {
            UpdateEnemyAnimations(gameTime, combatants);
            UpdateShadowAnimations(gameTime, combatants);
            UpdateStatusIconTooltips(combatants);

            foreach (var combatant in combatants)
            {
                if (combatant.IsPlayerControlled && _playerSprites.TryGetValue(combatant.CombatantID, out var sprite))
                {
                    // Only animate if this specific combatant is the one currently acting/selecting
                    bool isActive = currentActor == combatant;
                    sprite.Update(gameTime, isActive);
                }
            }
        }

        public void TriggerAttackAnimation(string combatantId)
        {
            _attackAnimTimers[combatantId] = 0f;
        }

        private void UpdateStatusIconTooltips(IEnumerable<BattleCombatant> allCombatants)
        {
            var virtualMousePos = Core.TransformMouse(Mouse.GetState().Position);
            _hoveredStatusIcon = null;

            // Player Icons
            foreach (var iconInfo in _playerStatusIcons)
            {
                if (iconInfo.Bounds.Contains(virtualMousePos))
                {
                    _tooltipManager.RequestTooltip(iconInfo.Effect, iconInfo.Effect.GetTooltipText(), new Vector2(iconInfo.Bounds.Center.X, iconInfo.Bounds.Top));
                    _hoveredStatusIcon = iconInfo;
                    return;
                }
            }

            // Enemy Icons
            foreach (var combatantEntry in _enemyStatusIcons)
            {
                foreach (var iconInfo in combatantEntry.Value)
                {
                    if (iconInfo.Bounds.Contains(virtualMousePos))
                    {
                        _tooltipManager.RequestTooltip(iconInfo.Effect, iconInfo.Effect.GetTooltipText(), new Vector2(iconInfo.Bounds.Center.X, iconInfo.Bounds.Top));
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

            // --- Update Attacker Animation State ---
            var currentAttackerId = (currentActor != null) ? currentActor.CombatantID : null;
            if (currentAttackerId != _lastAttackerId)
            {
                if (currentAttackerId != null)
                {
                    _attackAnimTimers[currentAttackerId] = 0f;
                }
                _lastAttackerId = currentAttackerId;
            }

            var idsToRemove = new List<string>();
            foreach (var id in _attackAnimTimers.Keys.ToList())
            {
                _attackAnimTimers[id] += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_attackAnimTimers[id] >= ATTACK_BOB_DURATION)
                {
                    idsToRemove.Add(id);
                }
            }
            foreach (var id in idsToRemove) _attackAnimTimers.Remove(id);

            // --- Capture Hovered Combatant from InputHandler BEFORE clearing targets ---
            BattleCombatant? hoveredCombatant = null;
            if (inputHandler.HoveredTargetIndex >= 0 && inputHandler.HoveredTargetIndex < _currentTargets.Count)
            {
                hoveredCombatant = _currentTargets[inputHandler.HoveredTargetIndex].Combatant;
            }

            _currentTargets.Clear();
            _playerStatusIcons.Clear(); // Clear and rebuild every frame for multiple players

            var enemies = allCombatants.Where(c => !c.IsPlayerControlled && c.IsActiveOnField).ToList();
            var players = allCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();

            float pulseAlpha = 0.10f + ((MathF.Sin(sharedBobbingTimer * 6f) + 1f) / 2f) * 0.10f;

            // --- Pre-calculate selectable targets ---
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
                    // Determine valid targets based on the specific TargetType
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
                                case TargetType.Single:
                                    if (!isUser) isValid = true;
                                    break;
                                case TargetType.SingleTeam:
                                    if (!isOpponent) isValid = true; // User or Ally
                                    break;
                                case TargetType.SingleAll:
                                    isValid = true;
                                    break;
                                case TargetType.Self:
                                    if (isUser) isValid = true;
                                    break;
                                case TargetType.Ally:
                                    if (isAlly) isValid = true;
                                    break;
                                case TargetType.Both:
                                case TargetType.RandomBoth:
                                    if (isOpponent) isValid = true;
                                    break;
                                case TargetType.Every:
                                case TargetType.RandomEvery:
                                    if (isOpponent || isAlly) isValid = true;
                                    break;
                                case TargetType.Team:
                                    if (!isOpponent) isValid = true;
                                    break;
                                case TargetType.All:
                                case TargetType.RandomAll:
                                    isValid = true;
                                    break;
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

            // --- Calculate Silhouette Colors for Cycling/Blinking ---
            var silhouetteColors = new Dictionary<string, Color>();

            if (isTargetingPhase)
            {
                // --- TARGETING MENU LOGIC ---
                // 1. Default all valid targets to Red (Idle)
                foreach (var target in selectableTargets)
                {
                    silhouetteColors[target.CombatantID] = _global.Palette_Red;
                }

                // 2. If hovering a valid target, turn it (or all if multi) Yellow (Active)
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
                // --- MOVE PREVIEW LOGIC (Cycling/Blinking) ---
                // Use the shared timer from BattleUIManager to ensure reset on move change
                float targetingTimer = uiManager.HoverHighlightState.Timer;

                // Sort targets for stable cycling order
                // Desired Order: Enemies (Slot 0->1), Ally, User
                var sortedTargets = allCombatants
                    .Where(c => selectableTargets.Contains(c))
                    .OrderBy(c => c.IsPlayerControlled ? 1 : 0) // Enemies (0) first, Players (1) second
                    .ThenBy(c => c.IsPlayerControlled ? (c == currentActor ? 1 : 0) : 0) // Within Players: Allies (0) first, User (1) last
                    .ThenBy(c => c.BattleSlot) // Tie-breaker for Enemies and multiple Allies
                    .ToList();

                bool isMultiTarget = activeTargetType == TargetType.Every || activeTargetType == TargetType.Both || activeTargetType == TargetType.All || activeTargetType == TargetType.Team || activeTargetType == TargetType.RandomAll || activeTargetType == TargetType.RandomBoth || activeTargetType == TargetType.RandomEvery;

                if (isMultiTarget)
                {
                    // If there is only 1 target, treat it as solid yellow (no flash)
                    if (sortedTargets.Count == 1)
                    {
                        silhouetteColors[sortedTargets[0].CombatantID] = Color.Yellow;
                    }
                    else
                    {
                        // Blink all in sync
                        // Flash Yellow (Active) then Red (Idle)
                        bool isFlash = (targetingTimer % _global.TargetingMultiBlinkSpeed) < (_global.TargetingMultiBlinkSpeed / 2f);
                        Color color = isFlash ? Color.Yellow : _global.Palette_Red;
                        foreach (var target in sortedTargets)
                        {
                            silhouetteColors[target.CombatantID] = color;
                        }
                    }
                }
                else // Single Target (Single or SingleTeam or Self or Ally or SingleAll)
                {
                    // Cycle through targets one by one
                    int count = sortedTargets.Count;
                    if (count > 0)
                    {
                        // Wrap the timer to prevent overflow and ensure clean cycling
                        float cycleDuration = count * _global.TargetingSingleCycleSpeed;
                        float currentCycleTime = targetingTimer % cycleDuration;

                        int activeIndex = (int)(currentCycleTime / _global.TargetingSingleCycleSpeed);
                        // Clamp just in case of float imprecision at the very end of the cycle
                        activeIndex = Math.Clamp(activeIndex, 0, count - 1);

                        for (int i = 0; i < count; i++)
                        {
                            // Active index gets Yellow (Flash), others get Red (Idle)
                            silhouetteColors[sortedTargets[i].CombatantID] = (i == activeIndex) ? Color.Yellow : _global.Palette_Red;
                        }
                    }
                }
            }


            // --- Draw Enemy HUDs ---
            DrawEnemyHuds(spriteBatch, font, secondaryFont, enemies, currentActor, isTargetingPhase, shouldGrayOutUnselectable, selectableTargets, animationManager, uiManager.HoverHighlightState, pulseAlpha, gameTime, silhouetteColors, transform);

            // --- Draw Player HUDs (Slots 0 & 1) ---
            foreach (var playerCombatant in players)
            {
                DrawPlayerHud(spriteBatch, font, secondaryFont, playerCombatant, currentActor, gameTime, animationManager, uiManager, uiManager.HoverHighlightState, shouldGrayOutUnselectable, selectableTargets, isTargetingPhase, pulseAlpha, silhouetteColors);
            }

            // --- Draw UI Title ---
            DrawUITitle(spriteBatch, secondaryFont, gameTime, uiManager.SubMenuState);

            // --- Draw Highlights & Indicators ---
            // 1. Draw Turn Indicator (Current Actor)
            if (currentActor != null && !currentActor.IsDefeated)
            {
                DrawIndicatorArrow(spriteBatch, gameTime, currentActor);
            }

            // 2. Draw Targeting Indicators (Highlighted Targets)
            foreach (var kvp in silhouetteColors)
            {
                if (kvp.Value == Color.Yellow) // Yellow indicates active targeting highlight
                {
                    var combatant = allCombatants.FirstOrDefault(c => c.CombatantID == kvp.Key);
                    if (combatant != null && !combatant.IsDefeated)
                    {
                        // Only draw if it's NOT the current actor (to avoid double arrows)
                        // Or if you want double arrows (one for turn, one for target), remove this check.
                        // Usually, if you target yourself, you want to see the target arrow.
                        if (combatant != currentActor)
                        {
                            DrawIndicatorArrow(spriteBatch, gameTime, combatant);
                        }
                    }
                }
            }

            DrawTargetingUI(spriteBatch, font, gameTime, uiManager, inputHandler);

            // --- Draw Divider ---
            spriteBatch.DrawSnapped(pixel, new Rectangle(0, DIVIDER_Y, Global.VIRTUAL_WIDTH, 1), Color.White);

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                foreach (var target in _currentTargets)
                {
                    spriteBatch.DrawSnapped(pixel, target.Bounds, Color.Cyan * 0.5f);
                }
            }
        }

        public void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font)
        {
        }

        private void DrawEnemyHuds(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, List<BattleCombatant> enemies, BattleCombatant currentActor, bool isTargetingPhase, bool shouldGrayOutUnselectable, HashSet<BattleCombatant> selectableTargets, BattleAnimationManager animationManager, HoverHighlightState hoverHighlightState, float pulseAlpha, GameTime gameTime, Dictionary<string, Color> silhouetteColors, Matrix transform)
        {
            const int enemyAreaPadding = 40;
            int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
            int slotWidth = availableWidth / 2;

            // --- DRAW FLOORS (Always Visible, Static) ---
            for (int slotIndex = 0; slotIndex < 2; slotIndex++)
            {
                var slotCenter = new Vector2(enemyAreaPadding + (slotIndex * slotWidth) + (slotWidth / 2), ENEMY_SLOT_Y_OFFSET);
                var enemyInSlot = enemies.FirstOrDefault(e => e.BattleSlot == slotIndex);

                int spriteSize = 64; // Default
                if (enemyInSlot != null)
                {
                    bool isMajor = _spriteManager.IsMajorEnemySprite(enemyInSlot.ArchetypeId);
                    spriteSize = isMajor ? 96 : 64;
                }

                // Ground Y is at the bottom of the sprite rect (Top=0 + Height=Size) + Offset
                float groundY = spriteSize + ENEMY_SLOT_Y_OFFSET;

                // Draw Floor
                if (_spriteManager.BattleEnemyFloorSprite != null)
                {
                    Vector2 floorOrigin = new Vector2(_spriteManager.BattleEnemyFloorSprite.Width / 2f, _spriteManager.BattleEnemyFloorSprite.Height / 2f);
                    // Draw at (CenterX, GroundY)
                    spriteBatch.DrawSnapped(_spriteManager.BattleEnemyFloorSprite, new Vector2(slotCenter.X, groundY), null, Color.White, 0f, floorOrigin, 1f, SpriteEffects.None, 0f);
                }
            }

            if (!enemies.Any()) return;

            foreach (var enemy in enemies)
            {
                // Slot 0 is Left, Slot 1 is Right
                int slotIndex = enemy.BattleSlot;
                var slotCenter = new Vector2(enemyAreaPadding + (slotIndex * slotWidth) + (slotWidth / 2), ENEMY_SLOT_Y_OFFSET);

                Color? highlightColor = null;
                if (silhouetteColors.TryGetValue(enemy.CombatantID, out var color))
                {
                    highlightColor = color;
                }

                DrawCombatantHud(spriteBatch, nameFont, statsFont, enemy, slotCenter, currentActor, isTargetingPhase, shouldGrayOutUnselectable, selectableTargets, animationManager, hoverHighlightState, pulseAlpha, gameTime, highlightColor, transform);

                if (selectableTargets.Contains(enemy))
                {
                    // Calculate precise bounds for targeting
                    // We need to reconstruct the base position used in DrawCombatantHud
                    bool isMajor = _spriteManager.IsMajorEnemySprite(enemy.ArchetypeId);
                    int spritePartSize = isMajor ? 96 : 64;
                    float yBobOffset = CalculateAttackBobOffset(enemy.CombatantID, isPlayer: false);

                    // Get spawn offset
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

                    Vector2 spritePos = new Vector2((int)(slotCenter.X - spritePartSize / 2f), (int)(slotCenter.Y + yBobOffset + spawnYOffset));

                    // Use STATIC bounds for the targeting box to prevent resizing during animation
                    Rectangle spriteBounds = GetEnemyStaticSpriteBounds(enemy, spritePos);

                    // Fallback if empty (e.g. invisible)
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
        }

        private void DrawPlayerHud(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, BattleCombatant player, BattleCombatant currentActor, GameTime gameTime, BattleAnimationManager animationManager, BattleUIManager uiManager, HoverHighlightState hoverHighlightState, bool shouldGrayOutUnselectable, HashSet<BattleCombatant> selectableTargets, bool isTargetingPhase, float pulseAlpha, Dictionary<string, Color> silhouetteColors)
        {
            if (player == null) return;

            bool isRightSide = player.BattleSlot == 1;
            const int playerHudY = DIVIDER_Y - 10;
            const int playerHudPaddingX = 10;
            const int barWidth = 60;

            // Calculate X positions based on slot
            float startX;
            if (isRightSide)
            {
                // Right side alignment
                startX = Global.VIRTUAL_WIDTH - playerHudPaddingX - barWidth;
            }
            else
            {
                // Left side alignment (Standard)
                startX = playerHudPaddingX;
            }

            // Determine visual state
            bool isSelectable = selectableTargets.Contains(player);
            bool isSilhouetted = shouldGrayOutUnselectable && !isSelectable;
            Color? silhouetteColor = isSilhouetted ? _global.Palette_DarkerGray : null;

            // --- Draw Sprite ---
            // Calculate sprite position relative to HUD
            const int heartHeight = 32;
            float heartCenterY = playerHudY - font.LineHeight - 2 - (heartHeight / 2f) + 10 + 3;
            float spriteCenterX = startX + (barWidth / 2f);

            // Store STATIC visual center for animations/targeting (so they don't bob with the sprite)
            _combatantVisualCenters[player.CombatantID] = new Vector2(spriteCenterX, heartCenterY);
            if (player.BattleSlot == 0) PlayerSpritePosition = new Vector2(spriteCenterX, heartCenterY); // Legacy compat

            // Determine tint
            Color? playerSpriteTint = null;

            bool isHighlighted = isSelectable && shouldGrayOutUnselectable;
            Color? highlightColor = null;
            if (isHighlighted && silhouetteColors.TryGetValue(player.CombatantID, out var color))
            {
                highlightColor = color;
            }

            // Calculate Attack Bob (Jump UP for players)
            float yBobOffset = CalculateAttackBobOffset(player.CombatantID, isPlayer: true);

            // Get or Create Sprite Instance
            if (!_playerSprites.TryGetValue(player.CombatantID, out var sprite))
            {
                sprite = new PlayerCombatSprite(player.ArchetypeId);
                _playerSprites[player.CombatantID] = sprite;
            }

            // Draw the sprite
            sprite.SetPosition(new Vector2(spriteCenterX, heartCenterY + yBobOffset));
            sprite.Draw(spriteBatch, animationManager, player, playerSpriteTint, isHighlighted, pulseAlpha, isSilhouetted, silhouetteColor, gameTime, highlightColor);

            // --- Draw HUD ---
            if (!isSilhouetted)
            {
                Color nameColor = Color.White;

                // --- Name Dimming & Font Logic ---
                var battleManager = ServiceLocator.Get<BattleManager>();
                bool isSelectionPhase = battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot1 || battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection_Slot2;
                var selectingActor = battleManager.CurrentActingCombatant;

                BitmapFont nameFontToUse = font;

                // If we are in a selection phase, and this player is NOT the one selecting, dim their name and use small font.
                if (isSelectionPhase && selectingActor != null && player != selectingActor)
                {
                    nameColor = _global.Palette_Gray;
                    nameFontToUse = secondaryFont;
                }

                if (hoverHighlightState.Targets.Contains(player)) nameColor = _global.Palette_Yellow;

                // --- Name Position Logic ---
                Vector2 nameSize = nameFontToUse.MeasureString(player.Name);
                float nameX;
                const int centerPadding = 10; // Space from the exact center line

                // Align text vertically with the bars (approximate center of bar stack)
                float nameY = playerHudY - 2;

                // Center smaller font vertically relative to where the large font would be
                if (nameFontToUse == secondaryFont)
                {
                    float heightDiff = font.LineHeight - secondaryFont.LineHeight;
                    nameY += heightDiff / 2f;
                }

                if (isRightSide)
                {
                    // Slot 1: Name is on the LEFT of the RIGHT half (against center)
                    nameX = (Global.VIRTUAL_WIDTH / 2) + centerPadding;
                }
                else
                {
                    // Slot 0: Name is on the RIGHT of the LEFT half (against center)
                    nameX = (Global.VIRTUAL_WIDTH / 2) - nameSize.X - centerPadding;
                }

                Vector2 namePos = new Vector2(nameX, nameY);
                spriteBatch.DrawStringSnapped(nameFontToUse, player.Name, namePos, nameColor);

                // Resource Bars (Keep existing logic for position)
                DrawPlayerResourceBars(spriteBatch, player, new Vector2(startX, playerHudY), barWidth, uiManager, animationManager);
            }

            // Status Icons
            DrawPlayerStatusIcons(spriteBatch, player, secondaryFont, playerHudY, startX, barWidth);

            // Add to targets if selectable
            if (isSelectable && isTargetingPhase)
            {
                // Use the STATIC bounds for the targeting box
                Rectangle spriteBounds = sprite.GetStaticBounds(animationManager, player);

                // Fallback if empty (e.g. invisible)
                if (spriteBounds.IsEmpty)
                {
                    spriteBounds = new Rectangle((int)startX, playerHudY - 40, barWidth, 50);
                }

                _currentTargets.Add(new TargetInfo
                {
                    Combatant = player,
                    Bounds = spriteBounds
                });
            }
        }

        private void DrawPlayerResourceBars(SpriteBatch spriteBatch, BattleCombatant player, Vector2 position, int width, BattleUIManager uiManager, BattleAnimationManager animationManager)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            // --- HP Bar ---
            float hpPercent = player.Stats.MaxHP > 0 ? Math.Clamp(player.VisualHP / player.Stats.MaxHP, 0f, 1f) : 0f;
            int hpWidth = (int)(width * hpPercent);
            if (hpPercent > 0 && hpWidth == 0) hpWidth = 1;

            var hpBgRect = new Rectangle((int)position.X, (int)position.Y + 1, width, 2);
            var hpFgRect = new Rectangle((int)position.X, (int)position.Y + 1, hpWidth, 2);

            spriteBatch.DrawSnapped(pixel, hpBgRect, _global.Palette_DarkGray);
            spriteBatch.DrawSnapped(pixel, hpFgRect, _global.Palette_LightGreen);

            // HP Animation Overlay
            var hpAnim = animationManager.GetResourceBarAnimation(player.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);
            if (hpAnim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, hpBgRect, player.Stats.MaxHP, hpAnim);
            }

            // --- Mana Bar ---
            float manaPercent = player.Stats.MaxMana > 0 ? Math.Clamp((float)player.Stats.CurrentMana / player.Stats.MaxMana, 0f, 1f) : 0f;
            int manaWidth = (int)(width * manaPercent);
            if (manaPercent > 0 && manaWidth == 0) manaWidth = 1;

            var manaBgRect = new Rectangle((int)position.X, (int)position.Y + 4, width, 2);
            var manaFgRect = new Rectangle((int)position.X, (int)position.Y + 4, manaWidth, 2);

            spriteBatch.DrawSnapped(pixel, manaBgRect, _global.Palette_DarkGray);
            spriteBatch.DrawSnapped(pixel, manaFgRect, _global.Palette_LightBlue);

            // Mana Animation Overlay
            var manaAnim = animationManager.GetResourceBarAnimation(player.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.Mana);
            if (manaAnim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, manaBgRect, player.Stats.MaxMana, manaAnim);
            }

            // Mana Cost Preview
            var hoveredMove = uiManager.HoveredMove;

            if (hoveredMove != null && hoveredMove.MoveType == MoveType.Spell && hoveredMove.ManaCost > 0)
            {
                var battleManager = ServiceLocator.Get<BattleManager>();
                // Only draw preview if this player is the one currently acting
                if (battleManager.CurrentActingCombatant == player)
                {
                    if (player.Stats.CurrentMana >= hoveredMove.ManaCost)
                    {
                        // Animate color pulse
                        const float PULSE_SPEED = 4f;
                        float pulse = (MathF.Sin(uiManager.SharedPulseTimer * PULSE_SPEED) + 1f) / 2f; // Oscillates 0..1
                        Color pulseColor = Color.Lerp(_global.Palette_Yellow, _global.Palette_BrightWhite, pulse);

                        // Draw yellow cost preview
                        float costPercent = (float)hoveredMove.ManaCost / player.Stats.MaxMana;
                        int costWidth = (int)(width * costPercent);

                        // Calculate position: Start of bar + Current Width - Cost Width
                        int previewX = (int)(position.X + manaWidth - costWidth);

                        var previewRect = new Rectangle(
                            previewX,
                            (int)position.Y + 4,
                            costWidth,
                            2
                        );

                        spriteBatch.DrawSnapped(pixel, previewRect, pulseColor);
                    }
                    else
                    {
                        // Draw red "not enough" indicator over the remaining mana
                        var previewRect = new Rectangle(
                            (int)position.X,
                            (int)position.Y + 4,
                            manaWidth,
                            2
                        );
                        spriteBatch.DrawSnapped(pixel, previewRect, _global.Palette_Red);
                    }
                }
            }
        }

        private void DrawPlayerStatusIcons(SpriteBatch spriteBatch, BattleCombatant player, BitmapFont font, int hudY, float startX, int barWidth)
        {
            if (player == null || !player.ActiveStatusEffects.Any()) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            const int iconSize = 5;
            const int iconPadding = 2;
            const int iconGap = 1;

            // Draw icons ABOVE the bars
            // hudY is the top of the HP bar area.
            // We want to draw above it.
            int iconY = hudY - iconSize - 2;

            int currentX;
            int step;

            if (player.BattleSlot == 0)
            {
                // Slot 0 (Left): Start at left edge, expand right
                currentX = (int)startX;
                step = iconSize + iconGap;
            }
            else
            {
                // Slot 1 (Right): Start at right edge, expand left
                currentX = (int)(startX + barWidth - iconSize);
                step = -(iconSize + iconGap);
            }

            foreach (var effect in player.ActiveStatusEffects)
            {
                var iconBounds = new Rectangle(currentX, iconY, iconSize, iconSize);
                _playerStatusIcons.Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });

                var iconTexture = _spriteManager.GetStatusEffectIcon(effect.EffectType);
                spriteBatch.DrawSnapped(iconTexture, iconBounds, Color.White);

                // Draw White Border if Hovered
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
            // if (subMenuState == BattleSubMenuState.Item) title = "ITEMS";

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

                // --- NEW LOGIC FOR MULTI-TARGET HIGHLIGHTING ---
                var targetType = uiManager.TargetTypeForSelection;
                bool isMultiTarget = targetType == TargetType.Every || targetType == TargetType.Both || targetType == TargetType.All || targetType == TargetType.Team || targetType == TargetType.RandomAll || targetType == TargetType.RandomBoth || targetType == TargetType.RandomEvery;
                bool isAnyHovered = inputHandler.HoveredTargetIndex != -1;

                for (int i = 0; i < _currentTargets.Count; i++)
                {
                    bool shouldHighlight = false;

                    if (isMultiTarget && isAnyHovered)
                    {
                        // If it's a multi-target move and the user is hovering ANY valid target, highlight ALL of them.
                        shouldHighlight = true;
                    }
                    else if (i == inputHandler.HoveredTargetIndex)
                    {
                        // Standard single target hover
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

            // Always use Down Arrow (Index 6)
            var arrowRect = arrowRects[6];

            // Calculate Attack Bob (Jump with sprite)
            float attackBobOffset = CalculateAttackBobOffset(combatant.CombatantID, combatant.IsPlayerControlled);

            // Idle Bob (Up 1 pixel then down)
            float idleBob = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 4f) > 0) ? -1f : 0f;

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

            var arrowPos = new Vector2(targetPos.X - arrowRect.Width / 2, topY - arrowRect.Height - 1 + attackBobOffset + idleBob);
            spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, Color.White);
        }

        private void DrawCombatantHud(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, BattleCombatant combatant, Vector2 slotCenter, BattleCombatant currentActor, bool isTargetingPhase, bool shouldGrayOutUnselectable, HashSet<BattleCombatant> selectableTargets, BattleAnimationManager animationManager, HoverHighlightState hoverHighlightState, float pulseAlpha, GameTime gameTime, Color? highlightColor, Matrix transform)
        {
            // Calculate Attack Bob (Jump DOWN for enemies)
            float yBobOffset = CalculateAttackBobOffset(combatant.CombatantID, isPlayer: false);

            // --- SPAWN ANIMATION LOGIC ---
            var spawnAnim = animationManager.GetSpawnAnimationState(combatant.CombatantID);
            float spawnYOffset = 0f;
            float spawnAlpha = 1.0f;
            float spawnSilhouetteAmount = 0f;
            Color? spawnSilhouetteColor = null;

            if (spawnAnim != null)
            {
                if (spawnAnim.CurrentPhase == BattleAnimationManager.SpawnAnimationState.Phase.Flash)
                {
                    // Flash Phase: Toggle silhouette, hide normal sprite
                    int flashCycle = (int)(spawnAnim.Timer / BattleAnimationManager.SpawnAnimationState.FLASH_INTERVAL);
                    bool isVisible = flashCycle % 2 == 0;
                    spawnAlpha = 0f; // Hide base sprite
                    spawnSilhouetteAmount = 1.0f;
                    spawnSilhouetteColor = isVisible ? Color.White : Color.Transparent;
                    spawnYOffset = -BattleAnimationManager.SpawnAnimationState.DROP_HEIGHT; // Start high
                }
                else if (spawnAnim.CurrentPhase == BattleAnimationManager.SpawnAnimationState.Phase.FadeIn)
                {
                    // Fade Phase: Fade in, drop down
                    float progress = Math.Clamp(spawnAnim.Timer / BattleAnimationManager.SpawnAnimationState.FADE_DURATION, 0f, 1f);
                    float easedProgress = Easing.EaseOutQuad(progress);
                    spawnAlpha = easedProgress;
                    // Use EaseOutCubic for a smooth, floaty descent
                    spawnYOffset = MathHelper.Lerp(-BattleAnimationManager.SpawnAnimationState.DROP_HEIGHT, 0f, Easing.EaseOutCubic(progress));
                    spawnSilhouetteAmount = 0f;
                }
            }

            var pixel = ServiceLocator.Get<Texture2D>();
            bool isMajor = _spriteManager.IsMajorEnemySprite(combatant.ArchetypeId);
            int spritePartSize = isMajor ? 96 : 64;

            // Calculate STATIC center for targeting/damage numbers
            var staticRect = new Rectangle((int)(slotCenter.X - spritePartSize / 2f), (int)slotCenter.Y, spritePartSize, spritePartSize);
            _combatantVisualCenters[combatant.CombatantID] = staticRect.Center.ToVector2();

            // Calculate DYNAMIC rect for drawing the sprite (includes bob and spawn drop)
            var spriteRect = new Rectangle((int)(slotCenter.X - spritePartSize / 2f), (int)(slotCenter.Y + yBobOffset + spawnYOffset), spritePartSize, spritePartSize);

            // Apply spawn alpha override if active
            float finalAlpha = combatant.VisualAlpha * spawnAlpha;
            Color tintColor = Color.White * finalAlpha;
            Color outlineColor = _global.Palette_DarkGray * finalAlpha;

            bool isSelectable = selectableTargets.Contains(combatant);

            // Apply spawn silhouette overrides if active
            float silhouetteFactor = spawnAnim != null ? spawnSilhouetteAmount : combatant.VisualSilhouetteAmount;
            Color silhouetteColor = spawnAnim != null ? (spawnSilhouetteColor ?? _global.Palette_DarkGray) : (combatant.VisualSilhouetteColorOverride ?? _global.Palette_DarkGray);

            // Highlight logic: Active if selectable and graying out is active (targeting or hover)
            bool isHighlighted = isSelectable && shouldGrayOutUnselectable;

            if (shouldGrayOutUnselectable && !isSelectable && spawnAnim == null)
            {
                silhouetteFactor = 1.0f;
            }
            else if (isSelectable && shouldGrayOutUnselectable && !isTargetingPhase && spawnAnim == null)
            {
                // Default hover highlight if no specific color provided
                if (highlightColor == null) outlineColor = Color.Yellow * finalAlpha;
            }

            // --- DRAW SHADOW ---
            if (silhouetteFactor < 1.0f && _spriteManager.ShadowBlobSprite != null)
            {
                // Calculate shadow position
                // Ground Y is roughly at the bottom of the sprite rect when at rest (spawnYOffset = 0)
                // We use the static rect bottom as the ground reference
                float groundY = staticRect.Bottom;

                // Calculate height factor for Alpha fading (0 to 1, where 1 is on ground)
                // spawnYOffset is negative when in air
                float heightFactor = 1.0f - Math.Clamp(Math.Abs(spawnYOffset) / 50f, 0f, 1f);

                // Get Shadow Animation Offset
                Vector2 shadowAnimOffset = Vector2.Zero;
                if (_shadowOffsets.TryGetValue(combatant.CombatantID, out var sOffset))
                {
                    shadowAnimOffset = sOffset;
                }

                // Center horizontally on the sprite, vertically on the ground line, plus animation offset
                Vector2 shadowPos = new Vector2(spriteRect.Center.X, groundY) + shadowAnimOffset;
                Vector2 shadowOrigin = new Vector2(_spriteManager.ShadowBlobSprite.Width / 2f, _spriteManager.ShadowBlobSprite.Height / 2f);

                // Use White so the texture colors show through. 
                // Fade alpha based on height and spawn state.
                Color shadowTint = Color.White * (heightFactor * finalAlpha);

                spriteBatch.DrawSnapped(_spriteManager.ShadowBlobSprite, shadowPos, null, shadowTint, 0f, shadowOrigin, 1.0f, SpriteEffects.None, 0f);
            }

            Texture2D enemySprite = _spriteManager.GetEnemySprite(combatant.ArchetypeId);
            Texture2D enemySilhouette = _spriteManager.GetEnemySpriteSilhouette(combatant.ArchetypeId);

            // --- CALCULATE HIGHEST PIXEL Y FOR HEALTH BAR ---
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
                        // Initialize timers randomly to desync multiple enemies
                        _enemyAnimationTimers[combatant.CombatantID][i] = (float)_random.NextDouble();
                    }
                    _enemyAnimationIntervals[combatant.CombatantID] = intervals;
                }

                if (_enemySpritePartOffsets.TryGetValue(combatant.CombatantID, out var offsets))
                {
                    var hitFlashState = animationManager.GetHitFlashState(combatant.CombatantID);
                    bool isFlashingWhite = hitFlashState != null && hitFlashState.IsCurrentlyWhite;
                    Vector2 shakeOffset = hitFlashState?.ShakeOffset ?? Vector2.Zero;

                    // --- Outline Pass ---
                    // Only draw outline if NOT highlighted (since highlight will be the full shape)
                    if (enemySilhouette != null && silhouetteFactor < 1.0f && !isHighlighted)
                    {
                        Color outerBorderColor = _global.Palette_Black * finalAlpha;

                        for (int i = 0; i < numParts; i++)
                        {
                            var sourceRect = new Rectangle(i * spritePartSize, 0, spritePartSize, spritePartSize);
                            var partOffset = offsets[i];
                            var baseDrawPosition = new Vector2(spriteRect.X + partOffset.X, spriteRect.Y + partOffset.Y) + shakeOffset;
                            int x = (int)baseDrawPosition.X;
                            int y = (int)baseDrawPosition.Y;
                            int w = spriteRect.Width;
                            int h = spriteRect.Height;

                            // Outer Black Border (Distance 2 Cardinals + Distance 1 Diagonals)
                            // Cardinals (2px)
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x - 2, y, w, h), sourceRect, outerBorderColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x + 2, y, w, h), sourceRect, outerBorderColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x, y - 2, w, h), sourceRect, outerBorderColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x, y + 2, w, h), sourceRect, outerBorderColor);

                            // Diagonals (1px) - Fills the corners of the outer border
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x - 1, y - 1, w, h), sourceRect, outerBorderColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x + 1, y - 1, w, h), sourceRect, outerBorderColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x - 1, y + 1, w, h), sourceRect, outerBorderColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x + 1, y + 1, w, h), sourceRect, outerBorderColor);

                            // Inner Colored Border (Distance 1 Cardinals)
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x - 1, y, w, h), sourceRect, outlineColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x + 1, y, w, h), sourceRect, outlineColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x, y - 1, w, h), sourceRect, outlineColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle(x, y + 1, w, h), sourceRect, outlineColor); // Added bottom
                        }
                    }

                    // --- Fill Pass ---
                    // Check if we need to flatten transparency
                    bool useFlattening = finalAlpha < 1.0f;

                    if (useFlattening)
                    {
                        // 1. Save current RTs
                        var currentRTs = _core.GraphicsDevice.GetRenderTargets();

                        // 2. End current batch
                        spriteBatch.End();

                        // 3. Switch to flatten target
                        _core.GraphicsDevice.SetRenderTarget(_flattenTarget);
                        _core.GraphicsDevice.Clear(Color.Transparent);

                        // 4. Begin local batch
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

                        // 5. Draw parts to RT
                        // We draw relative to (FLATTEN_MARGIN, FLATTEN_MARGIN) in the RT to avoid clipping negative offsets.
                        Vector2 rtOffset = new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);

                        for (int i = 0; i < numParts; i++)
                        {
                            var sourceRect = new Rectangle(i * spritePartSize, 0, spritePartSize, spritePartSize);
                            var partOffset = offsets[i];
                            // Draw at local position + shake
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

                        // 6. End local batch
                        spriteBatch.End();

                        // 7. Restore RTs
                        _core.GraphicsDevice.SetRenderTargets(currentRTs);

                        // 8. Restart main batch with original transform
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

                        // 9. Draw the RT
                        // The sprite top-left in the RT is at (FLATTEN_MARGIN, FLATTEN_MARGIN).
                        // We want that point to align with `spriteRect.Location`.
                        // So we draw the RT at `spriteRect.Location - (FLATTEN_MARGIN, FLATTEN_MARGIN)`.
                        Vector2 drawPos = spriteRect.Location.ToVector2() - new Vector2(FLATTEN_MARGIN, FLATTEN_MARGIN);

                        // Optimization: Draw only the relevant part of the RT
                        var srcRect = new Rectangle(0, 0, spritePartSize + FLATTEN_MARGIN * 2, spritePartSize + FLATTEN_MARGIN * 2);
                        spriteBatch.Draw(_flattenTarget, drawPos, srcRect, Color.White * finalAlpha);
                    }
                    else
                    {
                        // Standard drawing loop for opaque sprites
                        for (int i = 0; i < numParts; i++)
                        {
                            var sourceRect = new Rectangle(i * spritePartSize, 0, spritePartSize, spritePartSize);
                            var partOffset = offsets[i];
                            var drawPosition = new Vector2(spriteRect.X + partOffset.X, spriteRect.Y + partOffset.Y) + shakeOffset;
                            var drawRect = new Rectangle((int)drawPosition.X, (int)drawPosition.Y, spriteRect.Width, spriteRect.Height);

                            // --- TRACK HIGHEST PIXEL ---
                            float currentPartTopY = GetEnemySpriteStaticTopY(combatant, spriteRect.Y);
                            if (currentPartTopY < highestPixelY)
                            {
                                highestPixelY = currentPartTopY;
                            }

                            if (silhouetteFactor >= 1.0f && enemySilhouette != null)
                            {
                                spriteBatch.DrawSnapped(enemySilhouette, drawRect, sourceRect, silhouetteColor * finalAlpha);
                            }
                            else if (isHighlighted && enemySilhouette != null)
                            {
                                Color hColor = highlightColor ?? Color.Yellow;
                                spriteBatch.DrawSnapped(enemySilhouette, drawRect, sourceRect, hColor * finalAlpha);
                            }
                            else
                            {
                                spriteBatch.DrawSnapped(enemySprite, drawRect, sourceRect, tintColor);
                                if (silhouetteFactor > 0f && enemySilhouette != null)
                                {
                                    spriteBatch.DrawSnapped(enemySilhouette, drawRect, sourceRect, silhouetteColor * silhouetteFactor * finalAlpha);
                                }
                            }

                            if (isFlashingWhite && enemySilhouette != null)
                            {
                                spriteBatch.DrawSnapped(enemySilhouette, drawRect, sourceRect, Color.White * 0.8f);
                            }
                        }
                    }

                    // --- Indicator Pass ---
                    if (isHighlighted)
                    {
                        var indicator = _spriteManager.TargetingIndicatorSprite;
                        if (indicator != null)
                        {
                            // Calculate Visual Center Offset
                            Vector2 visualCenterOffset = _spriteManager.GetVisualCenterOffset(combatant.ArchetypeId);

                            // Base center of the sprite rect
                            Vector2 spriteCenter = new Vector2(spriteRect.Center.X, spriteRect.Center.Y);

                            // Apply visual center offset
                            // X is geometric center, Y is visual center (center of mass)
                            Vector2 targetCenter = new Vector2(spriteCenter.X, spriteCenter.Y + visualCenterOffset.Y);

                            // Apply Animation Math (Perlin Noise)
                            float t = (float)gameTime.TotalGameTime.TotalSeconds * _global.TargetIndicatorNoiseSpeed;

                            int seed = (combatant.CombatantID.GetHashCode() + 1000) * 93821;

                            // Noise lookups (offsets ensure different axes don't sync)
                            float nX = _swayNoise.Noise(t, seed);
                            float nY = _swayNoise.Noise(t, seed + 100);

                            float swayX = nX * _global.TargetIndicatorOffsetX;
                            float swayY = nY * _global.TargetIndicatorOffsetY;

                            float rotation = 0f;
                            float scale = 1.0f;

                            Vector2 animatedPos = targetCenter + new Vector2(swayX, swayY) + shakeOffset;
                            Vector2 origin = new Vector2(indicator.Width / 2f, indicator.Height / 2f);

                            // Only draw indicator if the highlight color is Yellow (Flash state)
                            if (highlightColor == Color.Yellow)
                            {
                                spriteBatch.DrawSnapped(indicator, animatedPos, null, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
                            }
                        }
                    }
                }
            }
            else
            {
                spriteBatch.DrawSnapped(pixel, spriteRect, _global.Palette_Pink * finalAlpha);
                highestPixelY = spriteRect.Top; // Fallback
            }

            // Calculate Health Bar Y Position
            const int barHeight = 2;
            // Calculate Y position: 2 pixels above the highest sprite pixel
            float barY = highestPixelY - barHeight - 2 - 8;
            // Clamp to screen top (1px margin)
            barY = Math.Max(1, barY);

            // Calculate Bar X Position (Centered on slot)
            const int barWidth = 40;
            float barX = slotCenter.X - barWidth / 2f;

            // Draw Status Icons (ABOVE the health bar)
            DrawEnemyStatusIcons(spriteBatch, combatant, barX, barY, barWidth);

            if (silhouetteFactor < 1.0f)
            {
                // --- HEALTH BAR LOGIC ---
                // Visible if damaged or animating damage/heal
                bool isDamaged = combatant.Stats.CurrentHP < combatant.Stats.MaxHP;
                bool isVisuallyDamaged = combatant.VisualHP < (combatant.Stats.MaxHP - 0.1f);

                if (isDamaged || isVisuallyDamaged)
                {
                    // Draw Health Bar
                    DrawEnemyHealthBar(spriteBatch, combatant, barX, barY, barWidth, barHeight, animationManager, 1.0f);
                }
            }

            if (selectableTargets.Contains(combatant))
            {
                // Calculate precise bounds for targeting
                // Use STATIC bounds for the targeting box to prevent resizing during animation
                Rectangle spriteBounds = GetEnemyStaticSpriteBounds(combatant, new Vector2(spriteRect.X, spriteRect.Y));

                // Fallback if empty (e.g. invisible)
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

        private Rectangle GetEnemyDynamicSpriteBounds(BattleCombatant enemy, Vector2 basePosition)
        {
            string archetypeId = enemy.ArchetypeId;
            var topOffsets = _spriteManager.GetEnemySpriteTopPixelOffsets(archetypeId);
            var leftOffsets = _spriteManager.GetEnemySpriteLeftPixelOffsets(archetypeId);
            var rightOffsets = _spriteManager.GetEnemySpriteRightPixelOffsets(archetypeId);
            var bottomOffsets = _spriteManager.GetEnemySpriteBottomPixelOffsets(archetypeId);

            if (topOffsets == null) return Rectangle.Empty;

            // Get current animation offsets
            if (!_enemySpritePartOffsets.TryGetValue(enemy.CombatantID, out var partOffsets))
            {
                return Rectangle.Empty;
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int i = 0; i < topOffsets.Length; i++)
            {
                // Skip empty parts
                if (topOffsets[i] == int.MaxValue) continue;

                Vector2 offset = partOffsets[i];

                // Calculate part bounds in screen space
                // The basePosition is the top-left of the sprite rect (which includes bob/spawn offset)
                // The part is drawn at basePosition + offset
                float partDrawX = basePosition.X + offset.X;
                float partDrawY = basePosition.Y + offset.Y;

                int partLeft = (int)partDrawX + leftOffsets[i];
                int partRight = (int)partDrawX + rightOffsets[i] + 1; // +1 because right index is inclusive pixel
                int partTop = (int)partDrawY + topOffsets[i];
                int partBottom = (int)partDrawY + bottomOffsets[i] + 1;

                if (partLeft < minX) minX = partLeft;
                if (partRight > maxX) maxX = partRight;
                if (partTop < minY) minY = partTop;
                if (partBottom > maxY) maxY = partBottom;
            }

            if (minX == int.MaxValue) return Rectangle.Empty;

            var rect = new Rectangle(minX, minY, maxX - minX, maxY - minY);
            rect.Inflate(2, 2); // Add 2px padding
            return rect;
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

                // Use Zero offset for static bounds
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
            rect.Inflate(4, 4); // Increased padding (was 2,2)
            return rect;
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

                int ghostStartX = (int)(bgRect.X + bgRect.Width * percentBefore);
                int ghostWidth = (int)(bgRect.Width * (currentFillPercent - percentBefore));
                var ghostRect = new Rectangle(ghostStartX, bgRect.Y, ghostWidth, bgRect.Height);

                Color ghostColor = (anim.ResourceType == BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP) ? _global.Palette_LightGreen : _global.Palette_LightBlue;
                float alpha = 1.0f - easedProgress;

                spriteBatch.DrawSnapped(pixel, ghostRect, ghostColor * alpha * 0.75f);
            }
        }

        private void DrawEnemyHealthBar(SpriteBatch spriteBatch, BattleCombatant combatant, float barX, float barY, int barWidth, int barHeight, BattleAnimationManager animationManager, float alpha)
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

            spriteBatch.DrawSnapped(pixel, barRect, _global.Palette_DarkGray * alpha);
            spriteBatch.DrawSnapped(pixel, hpFgRect, _global.Palette_LightGreen * alpha);

            // Animation Overlay
            var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);
            if (hpAnim != null)
            {
                // We need to pass alpha to DrawBarAnimationOverlay, but it doesn't support it yet.
                // For now, the overlay will draw at full opacity if active, which is acceptable as it implies recent damage.
                // Ideally, refactor DrawBarAnimationOverlay to accept alpha.
                DrawBarAnimationOverlay(spriteBatch, barRect, combatant.Stats.MaxHP, hpAnim);
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
            const int iconPadding = 1;
            const int iconGap = 1;

            // Draw icons ABOVE the bars
            // barY is the top of the HP bar.
            // We want to draw above it.
            int iconY = (int)barY - iconSize - 2;

            // Start at Left edge of bar, expand Right
            int currentX = (int)barX;
            int step = iconSize + iconGap;

            foreach (var effect in combatant.ActiveStatusEffects)
            {
                var iconBounds = new Rectangle(currentX, iconY, iconSize, iconSize);
                _enemyStatusIcons[combatant.CombatantID].Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });
                var iconTexture = _spriteManager.GetStatusEffectIcon(effect.EffectType);
                spriteBatch.DrawSnapped(iconTexture, iconBounds, Color.White);

                // Draw White Border if Hovered
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

                        // Incremental movement logic
                        Vector2 pos = currentOffsets[i];

                        // New Logic Here
                        if (pos != Vector2.Zero)
                        {
                            // If currently at a cardinal offset, return to center
                            pos = Vector2.Zero;
                        }
                        else
                        {
                            // If at center, move to a random cardinal direction
                            int direction = _random.Next(4);
                            switch (direction)
                            {
                                case 0: pos = new Vector2(0, -1); break; // Up
                                case 1: pos = new Vector2(0, 1); break;  // Down
                                case 2: pos = new Vector2(-1, 0); break; // Left
                                case 3: pos = new Vector2(1, 0); break;  // Right
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

        private Rectangle GetCombatantInteractionBounds(BattleCombatant combatant, Vector2 hudCenterPosition, BitmapFont nameFont, BitmapFont statsFont)
        {
            bool isMajor = _spriteManager.IsMajorEnemySprite(combatant.ArchetypeId);
            int spriteSize = isMajor ? 96 : 64;

            // Calculate bounds based on the visual center and size
            float width = Math.Max(spriteSize, 60);
            float height = spriteSize + 30; // Sprite + Text + Bar

            return new Rectangle(
                (int)(hudCenterPosition.X - width / 2),
                (int)(hudCenterPosition.Y - spriteSize - 10), // Approximate top
                (int)width,
                (int)height
            );
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
                case StatusEffectType.Freeze:
                case StatusEffectType.Blind:
                case StatusEffectType.Confuse:
                case StatusEffectType.Silence:
                case StatusEffectType.Fear:
                case StatusEffectType.Root:
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

            if (distance < topEdgeLength) // Top edge
            {
                x = bounds.Left + distance;
                y = bounds.Top;
            }
            else if (distance < topEdgeLength + rightEdgeLength) // Right edge
            {
                x = bounds.Right - 1;
                y = bounds.Top + (distance - topEdgeLength);
            }
            else if (distance < topEdgeLength + rightEdgeLength + bottomEdgeLength) // Bottom edge
            {
                x = (bounds.Right - 1) - (distance - (topEdgeLength + rightEdgeLength));
                y = bounds.Bottom - 1;
            }
            else // Left edge
            {
                x = bounds.Left;
                y = (bounds.Bottom - 1) - (distance - (topEdgeLength + rightEdgeLength + bottomEdgeLength));
            }
            return new Vector2(x, y);
        }

        private float CalculateAttackBobOffset(string combatantId, bool isPlayer)
        {
            if (_attackAnimTimers.TryGetValue(combatantId, out float animTimer))
            {
                float progress = Math.Clamp(animTimer / ATTACK_BOB_DURATION, 0f, 1f);
                float bobValue = 0f;

                // Phase 1: Main Jump (0% to 60% of duration)
                if (progress < 0.6f)
                {
                    float p = progress / 0.6f;
                    bobValue = MathF.Sin(p * MathHelper.Pi);
                }
                // Phase 2: The Bounce (60% to 100% of duration)
                else
                {
                    float p = (progress - 0.6f) / 0.4f;
                    bobValue = MathF.Sin(p * MathHelper.Pi) * 0.3f; // 30% height bounce
                }

                // Player: Up (-), Enemy: Down (+)
                float direction = isPlayer ? -1f : 1f;
                return bobValue * ATTACK_BOB_AMOUNT * direction;
            }
            return 0f;
        }
    }
}
