#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
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
        private readonly PlayerCombatSprite _playerCombatSprite;
        private readonly Color _turnInactiveTintColor;

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

        // Attacker Animation
        private readonly Dictionary<string, float> _attackAnimTimers = new();
        private string? _lastAttackerId;
        private const float ATTACK_BOB_DURATION = 0.25f;
        private const float ATTACK_BOB_AMOUNT = 12f;

        // Layout Constants
        private const int DIVIDER_Y = 123;
        private const int MAX_ENEMIES = 5;
        private const float PLAYER_INDICATOR_BOB_SPEED = 0.75f;
        private const float TITLE_INDICATOR_BOB_SPEED = PLAYER_INDICATOR_BOB_SPEED / 2f;

        public BattleRenderer()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _playerCombatSprite = new PlayerCombatSprite();
            _turnInactiveTintColor = Color.Lerp(Color.White, _global.ButtonDisableColor, 0.5f);
        }

        public void Reset()
        {
            _currentTargets.Clear();
            _playerStatusIcons.Clear();
            _enemyStatusIcons.Clear();
            _enemySpritePartOffsets.Clear();
            _enemyAnimationTimers.Clear();
            _enemyAnimationIntervals.Clear();
            _attackAnimTimers.Clear();
            _combatantVisualCenters.Clear();
            _lastAttackerId = null;
        }

        public List<TargetInfo> GetCurrentTargets() => _currentTargets;

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            UpdateEnemyAnimations(gameTime, combatants);
            UpdateStatusIconTooltips(combatants);
            _playerCombatSprite.Update(gameTime);
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
            float sharedBobbingTimer)
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

            if (isTargetingPhase)
            {
                var targetType = uiManager.TargetTypeForSelection;
                if (targetType.HasValue)
                {
                    if (targetType == TargetType.Single || targetType == TargetType.SingleAll)
                    {
                        foreach (var c in allCombatants)
                        {
                            if (!c.IsDefeated && c.IsActiveOnField)
                            {
                                if (targetType == TargetType.Single && !c.IsPlayerControlled) selectableTargets.Add(c);
                                else if (targetType == TargetType.SingleAll) selectableTargets.Add(c);
                            }
                        }
                    }
                }
            }
            else if (isHoveringMoveWithTargets)
            {
                foreach (var target in uiManager.HoverHighlightState.Targets) selectableTargets.Add(target);
            }


            // --- Draw Enemy HUDs ---
            DrawEnemyHuds(spriteBatch, font, secondaryFont, enemies, currentActor, isTargetingPhase, shouldGrayOutUnselectable, selectableTargets, animationManager, uiManager.HoverHighlightState, pulseAlpha);

            // --- Draw Player HUDs (Slots 0 & 1) ---
            foreach (var playerCombatant in players)
            {
                DrawPlayerHud(spriteBatch, font, secondaryFont, playerCombatant, currentActor, gameTime, animationManager, uiManager, uiManager.HoverHighlightState, shouldGrayOutUnselectable, selectableTargets, isTargetingPhase, pulseAlpha);
            }

            // --- Draw UI Title ---
            DrawUITitle(spriteBatch, secondaryFont, gameTime, uiManager.SubMenuState);

            // --- Draw Highlights & Indicators ---
            DrawHoverHighlights(spriteBatch, font, secondaryFont, allCombatants, uiManager.HoverHighlightState, uiManager.SharedPulseTimer);
            DrawTurnIndicator(spriteBatch, font, gameTime, currentActor, allCombatants);
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

        private void DrawEnemyHuds(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, List<BattleCombatant> enemies, BattleCombatant currentActor, bool isTargetingPhase, bool shouldGrayOutUnselectable, HashSet<BattleCombatant> selectableTargets, BattleAnimationManager animationManager, HoverHighlightState hoverHighlightState, float pulseAlpha)
        {
            if (!enemies.Any()) return;

            const int enemyAreaPadding = 40;
            int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
            // Fixed slots for enemies: 2 slots max
            int slotWidth = availableWidth / 2;

            foreach (var enemy in enemies)
            {
                // Slot 0 is Left, Slot 1 is Right
                int slotIndex = enemy.BattleSlot;
                var slotCenter = new Vector2(enemyAreaPadding + (slotIndex * slotWidth) + (slotWidth / 2), 0);

                DrawCombatantHud(spriteBatch, nameFont, statsFont, enemy, slotCenter, currentActor, isTargetingPhase, shouldGrayOutUnselectable, selectableTargets, animationManager, hoverHighlightState, pulseAlpha);

                if (selectableTargets.Contains(enemy))
                {
                    bool isMajor = _spriteManager.IsMajorEnemySprite(enemy.ArchetypeId);
                    int spritePartSize = isMajor ? 96 : 64;
                    float hudY = spritePartSize + 12;
                    var hudCenterForBounds = new Vector2(slotCenter.X, hudY);

                    _currentTargets.Add(new TargetInfo
                    {
                        Combatant = enemy,
                        Bounds = GetCombatantInteractionBounds(enemy, hudCenterForBounds, nameFont, statsFont)
                    });
                }
            }
        }

        private void DrawPlayerHud(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, BattleCombatant player, BattleCombatant currentActor, GameTime gameTime, BattleAnimationManager animationManager, BattleUIManager uiManager, HoverHighlightState hoverHighlightState, bool shouldGrayOutUnselectable, HashSet<BattleCombatant> selectableTargets, bool isTargetingPhase, float pulseAlpha)
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
            float heartCenterY = playerHudY - font.LineHeight - 2 - (heartHeight / 2f);
            float spriteCenterX = startX + (barWidth / 2f);

            // Store STATIC visual center for animations/targeting (so they don't bob with the sprite)
            _combatantVisualCenters[player.CombatantID] = new Vector2(spriteCenterX, heartCenterY);
            if (player.BattleSlot == 0) PlayerSpritePosition = new Vector2(spriteCenterX, heartCenterY); // Legacy compat

            // Determine tint
            Color? playerSpriteTint = null;
            bool isTurnInProgress = (currentActor != null);
            if (isTurnInProgress && player != currentActor)
            {
                playerSpriteTint = _turnInactiveTintColor;
            }

            bool isHighlighted = isSelectable && shouldGrayOutUnselectable && !isTargetingPhase;

            // Calculate Attack Bob (Jump UP for players)
            float yBobOffset = CalculateAttackBobOffset(player.CombatantID, isPlayer: true);

            // Draw the sprite (Heart for Leader, Archetype for Ally)
            if (player.BattleSlot == 0)
            {
                // Pass the center position directly. The PlayerCombatSprite class handles the origin offset internally.
                _playerCombatSprite.SetPosition(new Vector2(spriteCenterX, heartCenterY + yBobOffset));
                _playerCombatSprite.Draw(spriteBatch, animationManager, player, playerSpriteTint, isHighlighted, pulseAlpha, isSilhouetted, silhouetteColor);
            }
            else
            {
                // Draw Ally Sprite (Archetype)
                DrawAllySprite(spriteBatch, player, new Vector2(spriteCenterX, heartCenterY + yBobOffset), playerSpriteTint, isHighlighted, pulseAlpha, isSilhouetted, silhouetteColor, animationManager);
            }

            // --- Draw HUD ---
            if (!isSilhouetted)
            {
                Color nameColor = Color.White;
                if (isTurnInProgress && player != currentActor) nameColor = _turnInactiveTintColor;
                if (hoverHighlightState.Targets.Contains(player)) nameColor = _global.Palette_Yellow;

                // Name Position
                Vector2 nameSize = font.MeasureString(player.Name);
                Vector2 namePos = new Vector2(startX + (barWidth - nameSize.X) / 2f, playerHudY - font.LineHeight - 2);
                spriteBatch.DrawStringSnapped(font, player.Name, namePos, nameColor);

                // Resource Bars
                DrawPlayerResourceBars(spriteBatch, player, new Vector2(startX, playerHudY), barWidth, uiManager, animationManager);
            }

            // Status Icons
            DrawPlayerStatusIcons(spriteBatch, player, secondaryFont, playerHudY, startX, barWidth);

            // Add to targets if selectable
            if (isSelectable && isTargetingPhase)
            {
                _currentTargets.Add(new TargetInfo
                {
                    Combatant = player,
                    Bounds = new Rectangle((int)startX, playerHudY - 40, barWidth, 50) // Approx bounds
                });
            }
        }

        private void DrawAllySprite(SpriteBatch spriteBatch, BattleCombatant ally, Vector2 centerPos, Color? tint, bool isHighlighted, float pulseAlpha, bool isSilhouetted, Color? silhouetteColor, BattleAnimationManager animationManager)
        {
            // FIX: If the archetype is "player", use the PlayerHeartSpriteSheet instead of looking up an enemy sprite.
            Texture2D texture;
            if (ally.ArchetypeId == "player")
            {
                texture = _spriteManager.PlayerHeartSpriteSheet;
            }
            else
            {
                texture = _spriteManager.GetEnemySprite(ally.ArchetypeId);
            }

            if (texture == null) return;

            var hitFlashState = animationManager.GetHitFlashState(ally.CombatantID);
            Vector2 shakeOffset = hitFlashState?.ShakeOffset ?? Vector2.Zero;
            bool isFlashingWhite = hitFlashState != null && hitFlashState.IsCurrentlyWhite;

            // Simple draw for now, assume 32x32 frame 0 for player heart, or 64x64 for others if needed.
            // Since we are using the heart sheet for "player" archetype, we assume 32x32.
            int frameSize = (ally.ArchetypeId == "player") ? 32 : 64;
            var sourceRect = new Rectangle(0, 0, frameSize, frameSize);
            var origin = new Vector2(frameSize / 2f, frameSize / 2f);
            var drawPos = centerPos + shakeOffset;

            Color drawColor = tint ?? Color.White;
            if (isSilhouetted)
            {
                drawColor = silhouetteColor ?? Color.Gray;
            }

            // If it's the player heart, we don't flip it. If it's an enemy sprite reused as an ally, we flip it.
            SpriteEffects effects = (ally.ArchetypeId == "player") ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            spriteBatch.DrawSnapped(texture, drawPos, sourceRect, drawColor, 0f, origin, 1f, effects, 0.5f);

            if (isHighlighted)
            {
                // Draw outline/glow logic here if needed
            }
            if (isFlashingWhite)
            {
                // Draw white flash logic here if needed
            }
        }

        private void DrawPlayerResourceBars(SpriteBatch spriteBatch, BattleCombatant player, Vector2 position, int width, BattleUIManager uiManager, BattleAnimationManager animationManager)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            // --- HP Bar ---
            float hpPercent = player.Stats.MaxHP > 0 ? Math.Clamp(player.VisualHP / player.Stats.MaxHP, 0f, 1f) : 0f;
            var hpBgRect = new Rectangle((int)position.X, (int)position.Y + 1, width, 2);
            var hpFgRect = new Rectangle((int)position.X, (int)position.Y + 1, (int)(width * hpPercent), 2);

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
            var manaBgRect = new Rectangle((int)position.X, (int)position.Y + 4, width, 2);
            var manaFgRect = new Rectangle((int)position.X, (int)position.Y + 4, (int)(width * manaPercent), 2);

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
            // Only show preview if this player is the one acting
            // This requires passing the acting player to this method or checking UI state
            // For now, simple check:
            if (hoveredMove != null && hoveredMove.MoveType == MoveType.Spell && hoveredMove.ManaCost > 0)
            {
                // Logic to check if this specific player is the one selecting the move
                // Omitted for brevity, but standard logic applies
            }
        }

        private void DrawPlayerStatusIcons(SpriteBatch spriteBatch, BattleCombatant player, BitmapFont font, int hudY, float startX, int barWidth)
        {
            if (player == null || !player.ActiveStatusEffects.Any()) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            const int iconSize = 5;
            const int iconPadding = 2;
            const int iconGap = 1;

            // Draw icons below the bars
            int currentX = (int)startX;
            int iconY = hudY + 8;

            foreach (var effect in player.ActiveStatusEffects)
            {
                var iconBounds = new Rectangle(currentX, iconY, iconSize, iconSize);
                _playerStatusIcons.Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });

                var iconTexture = _spriteManager.GetStatusEffectIcon(effect.EffectType);
                spriteBatch.DrawSnapped(iconTexture, iconBounds, Color.White);

                var borderColor = IsNegativeStatus(effect.EffectType) ? _global.Palette_Red : _global.Palette_LightBlue;
                var borderBounds = new Rectangle(iconBounds.X - 1, iconBounds.Y - 1, iconBounds.Width + 2, iconBounds.Height + 2);
                float alpha = (_hoveredStatusIcon.HasValue && _hoveredStatusIcon.Value.Effect == effect) ? 0.25f : 0.1f;
                DrawRectangleBorder(spriteBatch, pixel, borderBounds, 1, borderColor * alpha);

                currentX += iconSize + iconGap;
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
                for (int i = 0; i < _currentTargets.Count; i++)
                {
                    Color baseColor = i == inputHandler.HoveredTargetIndex ? Color.Red : Color.Yellow;
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

        private void DrawHoverHighlights(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, IEnumerable<BattleCombatant> allCombatants, HoverHighlightState hoverHighlightState, float sharedBobbingTimer)
        {
            if (hoverHighlightState.CurrentMove == null || !hoverHighlightState.Targets.Any()) return;

            var arrowRects = _spriteManager.ArrowIconSourceRects;
            if (arrowRects == null) return;

            var move = hoverHighlightState.CurrentMove;
            var state = hoverHighlightState;
            var targets = state.Targets;

            float cycleDuration = HoverHighlightState.MultiTargetFlashOnDuration + HoverHighlightState.MultiTargetFlashOffDuration;
            bool areAllFlashing = (state.Timer % cycleDuration) < HoverHighlightState.MultiTargetFlashOnDuration;
            Color flashColor = areAllFlashing ? _global.Palette_Red : Color.White;

            float bobOffset = (MathF.Sin(sharedBobbingTimer * 4f) > 0) ? -1f : 0f;

            switch (move.Target)
            {
                case TargetType.Self:
                    var player = targets.FirstOrDefault(t => t.IsPlayerControlled);
                    if (player != null)
                    {
                        var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
                        if (arrowSheet == null) return;

                        var arrowRect = arrowRects[4];
                        float swayOffset = MathF.Round(MathF.Sin(sharedBobbingTimer * 4f) * 1.5f);

                        // Use visual center
                        if (_combatantVisualCenters.TryGetValue(player.CombatantID, out var center))
                        {
                            var arrowPos = new Vector2(center.X - 24 + swayOffset, center.Y - arrowRect.Height / 2);
                            spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, flashColor);
                        }
                    }
                    break;

                case TargetType.Single:
                case TargetType.SingleAll:
                case TargetType.Every:
                case TargetType.EveryAll:
                    foreach (var target in targets)
                    {
                        Rectangle sourceRect = target.IsPlayerControlled ? arrowRects[2] : arrowRects[6]; // Up for player, Down for enemy
                        float finalBobOffset = target.IsPlayerControlled ? bobOffset : 0f;
                        DrawTargetIndicator(spriteBatch, font, secondaryFont, allCombatants, target, sourceRect, flashColor, finalBobOffset);
                    }
                    break;
            }
        }

        private void DrawTargetIndicator(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, IEnumerable<BattleCombatant> allCombatants, BattleCombatant combatant, Rectangle sourceRect, Color color, float bobOffset = 0f)
        {
            if (color.A == 0) return;

            var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
            if (arrowSheet == null) return;

            Vector2 arrowPos;

            if (_combatantVisualCenters.TryGetValue(combatant.CombatantID, out var center))
            {
                if (combatant.IsPlayerControlled)
                {
                    // Arrow below
                    arrowPos = new Vector2(center.X - sourceRect.Width / 2f, center.Y + 20 + bobOffset);
                }
                else
                {
                    // Arrow above
                    // Need top Y. Approximate from center.
                    float topY = GetEnemySpriteStaticTopY(combatant, center.Y - 32); // Assume 64 height
                    arrowPos = new Vector2(center.X - sourceRect.Width / 2f, topY - sourceRect.Height - 4 + bobOffset);
                }

                if (color == _global.Palette_Red) arrowPos.Y += 1;
                spriteBatch.DrawSnapped(arrowSheet, arrowPos, sourceRect, color);
            }
        }

        private void DrawTurnIndicator(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, BattleCombatant currentActor, IEnumerable<BattleCombatant> allCombatants)
        {
            if (currentActor == null || currentActor.IsDefeated) return;

            var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
            var arrowRects = _spriteManager.ArrowIconSourceRects;
            if (arrowSheet == null || arrowRects == null) return;

            Vector2 targetPos;
            if (_combatantVisualCenters.TryGetValue(currentActor.CombatantID, out var center))
            {
                targetPos = center;
            }
            else
            {
                return;
            }

            if (currentActor.IsPlayerControlled)
            {
                var arrowRect = arrowRects[4]; // Right arrow
                float swayOffset = MathF.Round(MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 4f) * 1.5f);

                // Calculate Bob Offset for Player (Jump Up)
                float yBobOffset = CalculateAttackBobOffset(currentActor.CombatantID, isPlayer: true);

                // Position to the left of the sprite/heart, following the jump
                var arrowPos = new Vector2(targetPos.X - 24 + swayOffset, targetPos.Y - arrowRect.Height / 2 + yBobOffset);
                spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, Color.White);
            }
            else
            {
                var arrowRect = arrowRects[6]; // Down arrow

                // Calculate Bob Offset for Enemy (Jump Down)
                float yBobOffset = CalculateAttackBobOffset(currentActor.CombatantID, isPlayer: false);

                // Position above the enemy sprite, following the jump
                float topY = GetEnemySpriteStaticTopY(currentActor, targetPos.Y - 32);
                var arrowPos = new Vector2(targetPos.X - arrowRect.Width / 2, topY - arrowRect.Height - 1 + yBobOffset);
                spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, Color.White);
            }
        }

        private void DrawCombatantHud(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, BattleCombatant combatant, Vector2 slotCenter, BattleCombatant currentActor, bool isTargetingPhase, bool shouldGrayOutUnselectable, HashSet<BattleCombatant> selectableTargets, BattleAnimationManager animationManager, HoverHighlightState hoverHighlightState, float pulseAlpha)
        {
            // Calculate Attack Bob (Jump DOWN for enemies)
            float yBobOffset = CalculateAttackBobOffset(combatant.CombatantID, isPlayer: false);

            var pixel = ServiceLocator.Get<Texture2D>();
            bool isMajor = _spriteManager.IsMajorEnemySprite(combatant.ArchetypeId);
            int spritePartSize = isMajor ? 96 : 64;

            // Calculate STATIC center for targeting/damage numbers
            var staticRect = new Rectangle((int)(slotCenter.X - spritePartSize / 2f), 0, spritePartSize, spritePartSize);
            _combatantVisualCenters[combatant.CombatantID] = staticRect.Center.ToVector2();

            // Calculate DYNAMIC rect for drawing the sprite
            var spriteRect = new Rectangle((int)(slotCenter.X - spritePartSize / 2f), (int)yBobOffset, spritePartSize, spritePartSize);

            Color tintColor = Color.White * combatant.VisualAlpha;
            Color outlineColor = _global.Palette_DarkGray * combatant.VisualAlpha;

            bool isCurrentActor = (currentActor != null && combatant.CombatantID == currentActor.CombatantID);
            bool isTurnInProgress = (currentActor != null);

            if (isTurnInProgress && !isCurrentActor)
            {
                tintColor = _turnInactiveTintColor * combatant.VisualAlpha;
                outlineColor = _turnInactiveTintColor * combatant.VisualAlpha * 0.5f;
            }

            bool isSelectable = selectableTargets.Contains(combatant);
            float silhouetteFactor = combatant.VisualSilhouetteAmount;
            Color silhouetteColor = combatant.VisualSilhouetteColorOverride ?? _global.Palette_DarkGray;

            if (shouldGrayOutUnselectable && !isSelectable)
            {
                silhouetteFactor = 1.0f;
            }
            else if (isSelectable && shouldGrayOutUnselectable && !isTargetingPhase)
            {
                outlineColor = Color.Yellow * combatant.VisualAlpha;
            }

            Texture2D enemySprite = _spriteManager.GetEnemySprite(combatant.ArchetypeId);
            Texture2D enemySilhouette = _spriteManager.GetEnemySpriteSilhouette(combatant.ArchetypeId);

            if (enemySprite != null)
            {
                int numParts = enemySprite.Width / spritePartSize;
                if (!_enemySpritePartOffsets.ContainsKey(combatant.CombatantID) || _enemySpritePartOffsets[combatant.CombatantID].Length != numParts)
                {
                    _enemySpritePartOffsets[combatant.CombatantID] = new Vector2[numParts];
                    _enemyAnimationTimers[combatant.CombatantID] = new float[numParts];
                    var intervals = new float[numParts];
                    for (int i = 0; i < numParts; i++) { intervals[i] = (float)(_random.NextDouble() * (ENEMY_ANIM_MAX_INTERVAL - ENEMY_ANIM_MIN_INTERVAL) + ENEMY_ANIM_MIN_INTERVAL); }
                    _enemyAnimationIntervals[combatant.CombatantID] = intervals;
                }

                if (_enemySpritePartOffsets.TryGetValue(combatant.CombatantID, out var offsets))
                {
                    var hitFlashState = animationManager.GetHitFlashState(combatant.CombatantID);
                    bool isFlashingWhite = hitFlashState != null && hitFlashState.IsCurrentlyWhite;
                    Vector2 shakeOffset = hitFlashState?.ShakeOffset ?? Vector2.Zero;

                    // --- Outline Pass ---
                    if (enemySilhouette != null && silhouetteFactor < 1.0f)
                    {
                        for (int i = 0; i < numParts; i++)
                        {
                            var sourceRect = new Rectangle(i * spritePartSize, 0, spritePartSize, spritePartSize);
                            var partOffset = offsets[i];
                            var baseDrawPosition = new Vector2(spriteRect.X + partOffset.X, spriteRect.Y + partOffset.Y) + shakeOffset;

                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle((int)baseDrawPosition.X - 1, (int)baseDrawPosition.Y, spriteRect.Width, spriteRect.Height), sourceRect, outlineColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle((int)baseDrawPosition.X + 1, (int)baseDrawPosition.Y, spriteRect.Width, spriteRect.Height), sourceRect, outlineColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle((int)baseDrawPosition.X, (int)baseDrawPosition.Y - 1, spriteRect.Width, spriteRect.Height), sourceRect, outlineColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle((int)baseDrawPosition.X, (int)baseDrawPosition.Y + 1, spriteRect.Width, spriteRect.Height), sourceRect, outlineColor);
                        }
                    }

                    // --- Fill Pass ---
                    for (int i = 0; i < numParts; i++)
                    {
                        var sourceRect = new Rectangle(i * spritePartSize, 0, spritePartSize, spritePartSize);
                        var partOffset = offsets[i];
                        var drawPosition = new Vector2(spriteRect.X + partOffset.X, spriteRect.Y + partOffset.Y) + shakeOffset;
                        var drawRect = new Rectangle((int)drawPosition.X, (int)drawPosition.Y, spriteRect.Width, spriteRect.Height);

                        if (silhouetteFactor >= 1.0f && enemySilhouette != null)
                        {
                            spriteBatch.DrawSnapped(enemySilhouette, drawRect, sourceRect, silhouetteColor * combatant.VisualAlpha);
                        }
                        else
                        {
                            spriteBatch.DrawSnapped(enemySprite, drawRect, sourceRect, tintColor);
                            if (silhouetteFactor > 0f && enemySilhouette != null)
                            {
                                spriteBatch.DrawSnapped(enemySilhouette, drawRect, sourceRect, silhouetteColor * silhouetteFactor * combatant.VisualAlpha);
                            }
                        }

                        if (isSelectable && shouldGrayOutUnselectable && !isTargetingPhase)
                        {
                            spriteBatch.DrawSnapped(enemySilhouette, drawRect, sourceRect, Color.Yellow * pulseAlpha);
                        }

                        if (isFlashingWhite && enemySilhouette != null)
                        {
                            spriteBatch.DrawSnapped(enemySilhouette, drawRect, sourceRect, Color.White * 0.8f);
                        }
                    }
                }
            }
            else
            {
                spriteBatch.DrawSnapped(pixel, spriteRect, _global.Palette_Pink * combatant.VisualAlpha);
            }

            DrawEnemyStatusIcons(spriteBatch, combatant, spriteRect);

            if (silhouetteFactor < 1.0f)
            {
                float hudY = spriteRect.Bottom + 12;
                var hudCenterPosition = new Vector2(slotCenter.X, hudY);

                Vector2 nameSize = nameFont.MeasureString(combatant.Name);
                Vector2 namePos = new Vector2(hudCenterPosition.X - nameSize.X / 2, hudCenterPosition.Y - 8);

                Color nameColor = tintColor;
                if (hoverHighlightState.Targets.Contains(combatant))
                {
                    nameColor = _global.Palette_Yellow * combatant.VisualAlpha;
                }
                spriteBatch.DrawStringSnapped(nameFont, combatant.Name, namePos, nameColor);

                DrawEnemyHealthBar(spriteBatch, combatant, hudCenterPosition, animationManager);
            }
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

        private void DrawEnemyHealthBar(SpriteBatch spriteBatch, BattleCombatant combatant, Vector2 centerPosition, BattleAnimationManager animationManager)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            const int barWidth = 40;
            const int barHeight = 2;
            var barRect = new Rectangle(
                (int)(centerPosition.X - barWidth / 2f),
                (int)(centerPosition.Y + 2),
                barWidth,
                barHeight
            );

            float hpPercent = combatant.Stats.MaxHP > 0 ? Math.Clamp(combatant.VisualHP / combatant.Stats.MaxHP, 0f, 1f) : 0f;
            var hpFgRect = new Rectangle(barRect.X, barRect.Y, (int)(barRect.Width * hpPercent), barRect.Height);

            spriteBatch.DrawSnapped(pixel, barRect, _global.Palette_DarkGray);
            spriteBatch.DrawSnapped(pixel, hpFgRect, _global.Palette_LightGreen);

            // Animation Overlay
            var hpAnim = animationManager.GetResourceBarAnimation(combatant.CombatantID, BattleAnimationManager.ResourceBarAnimationState.BarResourceType.HP);
            if (hpAnim != null)
            {
                DrawBarAnimationOverlay(spriteBatch, barRect, combatant.Stats.MaxHP, hpAnim);
            }
        }

        private void DrawEnemyStatusIcons(SpriteBatch spriteBatch, BattleCombatant combatant, Rectangle spriteRect)
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
            int currentX = spriteRect.Left + iconPadding;
            int iconY = spriteRect.Top + iconPadding;

            foreach (var effect in combatant.ActiveStatusEffects)
            {
                var iconBounds = new Rectangle(currentX, iconY, iconSize, iconSize);
                _enemyStatusIcons[combatant.CombatantID].Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });
                var iconTexture = _spriteManager.GetStatusEffectIcon(effect.EffectType);
                spriteBatch.DrawSnapped(iconTexture, iconBounds, Color.White);

                var borderColor = IsNegativeStatus(effect.EffectType) ? _global.Palette_Red : _global.Palette_LightBlue;
                var borderBounds = new Rectangle(iconBounds.X - 1, iconBounds.Y - 1, iconBounds.Width + 2, iconBounds.Height + 2);
                float alpha = (_hoveredStatusIcon.HasValue && _hoveredStatusIcon.Value.Effect == effect) ? 0.25f : 0.1f;
                DrawRectangleBorder(spriteBatch, pixel, borderBounds, 1, borderColor * alpha);

                currentX += iconSize + iconPadding + iconGap;
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
                for (int i = 1; i < currentOffsets.Length; i++)
                {
                    timers[i] += deltaTime;
                    if (timers[i] >= intervals[i])
                    {
                        timers[i] = 0f;
                        intervals[i] = (float)(_random.NextDouble() * (ENEMY_ANIM_MAX_INTERVAL - ENEMY_ANIM_MIN_INTERVAL) + ENEMY_ANIM_MIN_INTERVAL);
                        currentOffsets[i] = new Vector2(_random.Next(-1, 1), _random.Next(-1, 1));
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
                float bobValue;

                // Jump Phase (0 -> 1)
                if (progress < 0.5f)
                    bobValue = Easing.EaseInCubic(progress * 2f);
                // Return Phase (1 -> 0)
                else
                    bobValue = 1.0f - Easing.EaseOutCubic((progress - 0.5f) * 2f);

                // Player: Up (-), Enemy: Down (+)
                float direction = isPlayer ? -1f : 1f;
                return bobValue * ATTACK_BOB_AMOUNT * direction;
            }
            return 0f;
        }
    }
}