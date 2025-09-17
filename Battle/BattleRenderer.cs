using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public struct TargetInfo { public BattleCombatant Combatant; public Rectangle Bounds; }
    public struct StatusIconInfo { public StatusEffectInstance Effect; public Rectangle Bounds; }

    /// <summary>
    /// Handles all rendering for the battle scene, including combatants, HUDs, and indicators.
    /// </summary>
    public class BattleRenderer
    {
        // Dependencies
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly Core _core;

        // State
        private List<TargetInfo> _currentTargets = new List<TargetInfo>();
        private readonly List<StatusIconInfo> _playerStatusIcons = new List<StatusIconInfo>();
        private string _hoveredStatusEffectText = null;
        private Vector2? _hoveredStatusIconAnchorPosition = null;


        // Enemy Sprite Animation
        private Dictionary<string, Vector2[]> _enemySpritePartOffsets = new Dictionary<string, Vector2[]>();
        private Dictionary<string, float[]> _enemyAnimationTimers = new Dictionary<string, float[]>();
        private Dictionary<string, float[]> _enemyAnimationIntervals = new Dictionary<string, float[]>();
        private readonly Random _random = new Random();
        private const int ENEMY_SPRITE_PART_SIZE = 64;

        // Layout Constants
        private const int DIVIDER_Y = 105;
        private const int MAX_ENEMIES = 5;
        private const float PLAYER_INDICATOR_BOB_SPEED = 1.5f;
        private const float TITLE_INDICATOR_BOB_SPEED = PLAYER_INDICATOR_BOB_SPEED / 2f;

        public BattleRenderer()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _core = ServiceLocator.Get<Core>();
        }

        public void Reset()
        {
            _currentTargets.Clear();
            _playerStatusIcons.Clear();
            _enemySpritePartOffsets.Clear();
            _enemyAnimationTimers.Clear();
            _enemyAnimationIntervals.Clear();
        }

        public List<TargetInfo> GetCurrentTargets() => _currentTargets;

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            UpdateEnemyAnimations(gameTime);
            UpdatePlayerStatusIcons(combatants.FirstOrDefault(c => c.IsPlayerControlled));
        }

        public void Draw(
            SpriteBatch spriteBatch,
            BitmapFont font,
            GameTime gameTime,
            IEnumerable<BattleCombatant> allCombatants,
            BattleCombatant currentActor,
            BattleUIManager uiManager,
            BattleInputHandler inputHandler,
            BattleAnimationManager animationManager)
        {
            var secondaryFont = _core.SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            _currentTargets.Clear();
            var enemies = allCombatants.Where(c => !c.IsPlayerControlled).ToList();
            var player = allCombatants.FirstOrDefault(c => c.IsPlayerControlled);

            // --- Draw Enemy HUDs ---
            DrawEnemyHuds(spriteBatch, font, secondaryFont, enemies, uiManager.TargetTypeForSelection, animationManager);

            // --- Draw Player HUD ---
            DrawPlayerHud(spriteBatch, font, secondaryFont, player, gameTime, animationManager, uiManager.TargetTypeForSelection);

            // --- Draw UI Title ---
            DrawUITitle(spriteBatch, secondaryFont, gameTime, uiManager.SubMenuState);

            // --- Draw Highlights & Indicators ---
            DrawHoverHighlights(spriteBatch, font, secondaryFont, allCombatants, uiManager.HoverHighlightState);
            DrawTurnIndicator(spriteBatch, font, gameTime, currentActor, allCombatants);
            DrawTargetingUI(spriteBatch, font, gameTime, uiManager, inputHandler);

            // --- Draw Divider ---
            spriteBatch.DrawSnapped(pixel, new Rectangle(0, DIVIDER_Y, Global.VIRTUAL_WIDTH, 1), Color.White);
        }

        public void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font)
        {
            if (!string.IsNullOrEmpty(_hoveredStatusEffectText) && _hoveredStatusIconAnchorPosition.HasValue)
            {
                var secondaryFont = _core.SecondaryFont;
                var pixel = ServiceLocator.Get<Texture2D>();
                var anchorPosition = _hoveredStatusIconAnchorPosition.Value;

                Vector2 textSize = secondaryFont.MeasureString(_hoveredStatusEffectText);

                const int paddingX = 8;
                const int paddingY = 4;
                int tooltipWidth = (int)textSize.X + paddingX;
                int tooltipHeight = (int)textSize.Y + paddingY;

                // Position the tooltip above the icon, centered horizontally.
                Vector2 finalTopLeftPosition = new Vector2(
                    anchorPosition.X - tooltipWidth / 2f,
                    anchorPosition.Y - tooltipHeight - 2 // 2 pixels of space
                );

                // Clamp to screen bounds
                if (finalTopLeftPosition.X < 0) finalTopLeftPosition.X = 0;
                if (finalTopLeftPosition.Y < 0) finalTopLeftPosition.Y = 0;
                if (finalTopLeftPosition.X + tooltipWidth > Global.VIRTUAL_WIDTH)
                {
                    finalTopLeftPosition.X = Global.VIRTUAL_WIDTH - tooltipWidth;
                }
                if (finalTopLeftPosition.Y + tooltipHeight > Global.VIRTUAL_HEIGHT)
                {
                    finalTopLeftPosition.Y = Global.VIRTUAL_HEIGHT - tooltipHeight;
                }

                // Round the final position *before* creating the drawing primitives to prevent desync.
                Vector2 snappedPosition = new Vector2(MathF.Round(finalTopLeftPosition.X), MathF.Round(finalTopLeftPosition.Y));

                Rectangle tooltipBg = new Rectangle((int)snappedPosition.X, (int)snappedPosition.Y, tooltipWidth, tooltipHeight);
                Vector2 textPosition = new Vector2(snappedPosition.X + (paddingX / 2), snappedPosition.Y + (paddingY / 2));

                // The DrawSnapped methods will now receive already-snapped integer coordinates, preventing further rounding.
                spriteBatch.DrawSnapped(pixel, tooltipBg, _global.ToolTipBGColor * 0.9f);
                spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, tooltipBg.Width, 1), _global.ToolTipBorderColor);
                spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBg.X, tooltipBg.Bottom - 1, tooltipBg.Width, 1), _global.ToolTipBorderColor);
                spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBg.X, tooltipBg.Y, 1, tooltipBg.Height), _global.ToolTipBorderColor);
                spriteBatch.DrawSnapped(pixel, new Rectangle(tooltipBg.Right - 1, tooltipBg.Y, 1, tooltipBg.Height), _global.ToolTipBorderColor);

                spriteBatch.DrawStringSnapped(secondaryFont, _hoveredStatusEffectText, textPosition, _global.ToolTipTextColor);
            }
        }

        private void DrawEnemyHuds(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, List<BattleCombatant> enemies, TargetType? currentTargetType, BattleAnimationManager animationManager)
        {
            if (!enemies.Any()) return;

            const int enemyAreaPadding = 20;
            const int enemyHudY = 80;
            int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
            int slotWidth = availableWidth / enemies.Count;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                var centerPosition = new Vector2(enemyAreaPadding + (i * slotWidth) + (slotWidth / 2), enemyHudY);
                DrawCombatantHud(spriteBatch, nameFont, statsFont, enemy, centerPosition, animationManager);

                if (!enemy.IsDefeated && currentTargetType.HasValue)
                {
                    bool isTarget = currentTargetType == TargetType.Single || currentTargetType == TargetType.SingleAll;
                    if (isTarget)
                    {
                        _currentTargets.Add(new TargetInfo
                        {
                            Combatant = enemy,
                            Bounds = GetCombatantInteractionBounds(enemy, centerPosition, nameFont, statsFont)
                        });
                    }
                }
            }
        }

        private void DrawPlayerHud(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, BattleCombatant player, GameTime gameTime, BattleAnimationManager animationManager, TargetType? currentTargetType)
        {
            if (player == null) return;

            const int playerHudY = DIVIDER_Y - 10;
            const int playerHudPaddingX = 10;
            float yOffset = 0;

            if (player.IsPlayerControlled && !player.IsDefeated) // Check if it's the player's turn to select an action
            {
                yOffset = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * PLAYER_INDICATOR_BOB_SPEED * MathF.PI) > 0) ? -1f : 0f;
            }

            spriteBatch.DrawStringSnapped(font, player.Name, new Vector2(playerHudPaddingX, playerHudY - font.LineHeight + 7 + yOffset), Color.White);

            string hpText = $"HP: {((int)Math.Round(player.VisualHP))}/{player.Stats.MaxHP}";
            Vector2 hpTextSize = secondaryFont.MeasureString(hpText);
            float hpStartX = Global.VIRTUAL_WIDTH - playerHudPaddingX - hpTextSize.X;
            DrawHpLine(spriteBatch, secondaryFont, player, new Vector2(hpStartX, playerHudY + yOffset), 1.0f, animationManager);

            DrawPlayerStatusIcons(spriteBatch);

            if (!player.IsDefeated && currentTargetType.HasValue)
            {
                if (currentTargetType == TargetType.SingleAll)
                {
                    _currentTargets.Add(new TargetInfo
                    {
                        Combatant = player,
                        Bounds = GetPlayerInteractionBounds(font, secondaryFont, player)
                    });
                }
            }
        }

        private void DrawUITitle(SpriteBatch spriteBatch, BitmapFont secondaryFont, GameTime gameTime, BattleSubMenuState subMenuState)
        {
            string title = "";
            if (subMenuState == BattleSubMenuState.ActionMoves) title = "ACTIONS";
            else if (subMenuState == BattleSubMenuState.Item) title = "ITEMS";

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
                for (int i = 0; i < _currentTargets.Count; i++)
                {
                    Color boxColor = i == inputHandler.HoveredTargetIndex ? Color.Red : Color.Yellow;
                    var bounds = _currentTargets[i].Bounds;
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Right, bounds.Top), boxColor);
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Bottom), new Vector2(bounds.Right, bounds.Bottom), boxColor);
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Left, bounds.Bottom), boxColor);
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Right, bounds.Top), new Vector2(bounds.Right, bounds.Bottom), boxColor);
                }
            }
        }

        private void DrawHoverHighlights(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, IEnumerable<BattleCombatant> allCombatants, HoverHighlightState hoverHighlightState)
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

            switch (move.Target)
            {
                case TargetType.Single:
                case TargetType.SingleAll:
                case TargetType.Self:
                    foreach (var target in targets)
                    {
                        Rectangle sourceRect = target.IsPlayerControlled ? arrowRects[2] : arrowRects[6]; // Up for player, Down for enemy
                        DrawTargetIndicator(spriteBatch, font, secondaryFont, allCombatants, target, sourceRect, flashColor);
                    }
                    break;

                case TargetType.Every:
                case TargetType.EveryAll:
                    Rectangle starRect = arrowRects[8]; // Center star icon
                    foreach (var target in targets)
                    {
                        DrawTargetIndicator(spriteBatch, font, secondaryFont, allCombatants, target, starRect, flashColor);
                    }
                    break;
            }
        }

        private void DrawTargetIndicator(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, IEnumerable<BattleCombatant> allCombatants, BattleCombatant combatant, Rectangle sourceRect, Color color)
        {
            if (color.A == 0) return;

            var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
            if (arrowSheet == null) return;

            Vector2 arrowPos;

            if (combatant.IsPlayerControlled)
            {
                var playerBounds = GetPlayerInteractionBounds(font, secondaryFont, combatant);
                arrowPos = new Vector2(
                    playerBounds.Center.X - sourceRect.Width / 2f,
                    playerBounds.Bottom + 2
                );
            }
            else
            {
                var enemies = allCombatants.Where(c => !c.IsPlayerControlled).ToList();
                int enemyIndex = enemies.FindIndex(e => e.CombatantID == combatant.CombatantID);
                if (enemyIndex == -1) return;

                const int enemyAreaPadding = 20;
                const int enemyHudY = 80;
                int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
                int slotWidth = availableWidth / enemies.Count;
                var centerPosition = new Vector2(enemyAreaPadding + (enemyIndex * slotWidth) + (slotWidth / 2), enemyHudY);

                const int spriteSize = 64;
                float spriteTop = centerPosition.Y - spriteSize - 10;

                arrowPos = new Vector2(
                    centerPosition.X - sourceRect.Width / 2f,
                    spriteTop - sourceRect.Height - 1
                );
            }

            if (color == _global.Palette_Red)
            {
                arrowPos.Y += 1;
            }

            spriteBatch.DrawSnapped(arrowSheet, arrowPos, sourceRect, color);
        }

        private void DrawTurnIndicator(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, BattleCombatant currentActor, IEnumerable<BattleCombatant> allCombatants)
        {
            if (currentActor == null || currentActor.IsDefeated) return;

            var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
            var arrowRects = _spriteManager.ArrowIconSourceRects;
            if (arrowSheet == null || arrowRects == null) return;

            float bobOffset = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 4f) > 0) ? 1f : 0f;

            if (currentActor.IsPlayerControlled)
            {
                var arrowRect = arrowRects[4]; // Right arrow
                const int playerHudY = DIVIDER_Y - 10;
                const int playerHudPaddingX = 10;
                Vector2 nameSize = font.MeasureString(currentActor.Name);
                Vector2 namePos = new Vector2(playerHudPaddingX, playerHudY - font.LineHeight + 7);
                var arrowPos = new Vector2(
                    namePos.X - arrowRect.Width - 4 + bobOffset,
                    namePos.Y + (nameSize.Y - arrowRect.Height) / 2 - 1
                );
                spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, Color.White);
            }
            else // Enemy turn
            {
                var arrowRect = arrowRects[6]; // Down arrow
                var enemies = allCombatants.Where(c => !c.IsPlayerControlled).ToList();
                int enemyIndex = enemies.FindIndex(e => e.CombatantID == currentActor.CombatantID);
                if (enemyIndex == -1) return;

                const int enemyAreaPadding = 20;
                const int enemyHudY = 80;
                int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
                int slotWidth = availableWidth / enemies.Count;
                var centerPosition = new Vector2(enemyAreaPadding + (enemyIndex * slotWidth) + (slotWidth / 2), enemyHudY);

                const int spriteSize = 64;
                var spriteRect = new Rectangle((int)(centerPosition.X - spriteSize / 2), (int)(centerPosition.Y - spriteSize - 10), spriteSize, spriteSize);
                var arrowPos = new Vector2(spriteRect.Center.X - arrowRect.Width / 2, spriteRect.Top - arrowRect.Height - 1 + bobOffset);
                spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, Color.White);
            }
        }

        private void DrawCombatantHud(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, BattleCombatant combatant, Vector2 centerPosition, BattleAnimationManager animationManager)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var spriteRect = new Rectangle((int)(centerPosition.X - ENEMY_SPRITE_PART_SIZE / 2), (int)(centerPosition.Y - ENEMY_SPRITE_PART_SIZE - 10), ENEMY_SPRITE_PART_SIZE, ENEMY_SPRITE_PART_SIZE);
            Color tintColor = Color.White * combatant.VisualAlpha;

            Texture2D enemySprite = _spriteManager.GetEnemySprite(combatant.ArchetypeId);
            if (enemySprite != null)
            {
                int numParts = enemySprite.Width / ENEMY_SPRITE_PART_SIZE;
                if (!_enemySpritePartOffsets.ContainsKey(combatant.CombatantID) || _enemySpritePartOffsets[combatant.CombatantID].Length != numParts)
                {
                    _enemySpritePartOffsets[combatant.CombatantID] = new Vector2[numParts];
                    _enemyAnimationTimers[combatant.CombatantID] = new float[numParts];
                    var intervals = new float[numParts];
                    for (int i = 0; i < numParts; i++) { intervals[i] = (float)(_random.NextDouble() * (0.5f - 0.1f) + 0.1f); }
                    _enemyAnimationIntervals[combatant.CombatantID] = intervals;
                }

                if (_enemySpritePartOffsets.TryGetValue(combatant.CombatantID, out var offsets))
                {
                    for (int i = 0; i < numParts; i++)
                    {
                        var sourceRect = new Rectangle(i * ENEMY_SPRITE_PART_SIZE, 0, ENEMY_SPRITE_PART_SIZE, ENEMY_SPRITE_PART_SIZE);
                        var partOffset = offsets[i];
                        var drawPosition = new Vector2(spriteRect.X + partOffset.X, spriteRect.Y + partOffset.Y);
                        var drawRect = new Rectangle((int)drawPosition.X, (int)drawPosition.Y, spriteRect.Width, spriteRect.Height);
                        spriteBatch.DrawSnapped(enemySprite, drawRect, sourceRect, tintColor);
                    }
                }
            }
            else
            {
                spriteBatch.DrawSnapped(pixel, spriteRect, _global.Palette_Pink * combatant.VisualAlpha);
            }

            Vector2 nameSize = nameFont.MeasureString(combatant.Name);
            Vector2 namePos = new Vector2(centerPosition.X - nameSize.X / 2, centerPosition.Y - 8);
            spriteBatch.DrawStringSnapped(nameFont, combatant.Name, namePos, tintColor);

            Vector2 hpSize = statsFont.MeasureString($"HP: {((int)Math.Round(combatant.VisualHP))}/{combatant.Stats.MaxHP}");
            Vector2 hpPos = new Vector2(centerPosition.X - hpSize.X / 2, centerPosition.Y + 2);
            DrawHpLine(spriteBatch, statsFont, combatant, hpPos, combatant.VisualAlpha, animationManager);
        }

        private void DrawHpLine(SpriteBatch spriteBatch, BitmapFont statsFont, BattleCombatant combatant, Vector2 position, float alpha, BattleAnimationManager animationManager)
        {
            Color labelColor = _global.Palette_LightGray * alpha;
            Color numberColor = Color.White * alpha;
            Vector2 drawPosition = position;

            var hitAnim = animationManager.GetHitAnimationState(combatant.CombatantID);
            var healBounceAnim = animationManager.GetHealBounceAnimationState(combatant.CombatantID);
            var healFlashAnim = animationManager.GetHealFlashAnimationState(combatant.CombatantID);

            if (hitAnim != null)
            {
                float progress = hitAnim.Timer / BattleAnimationManager.HitAnimationState.Duration;
                float easeOutProgress = Easing.EaseOutCubic(progress);
                float shakeMagnitude = 4.0f * (1.0f - easeOutProgress);
                drawPosition.X += (float)(_random.NextDouble() * 2 - 1) * shakeMagnitude;
                drawPosition.Y += (float)(_random.NextDouble() * 2 - 1) * shakeMagnitude;
                Color flashColor = _global.Palette_Red;
                labelColor = Color.Lerp(flashColor, _global.Palette_LightGray, easeOutProgress) * alpha;
                numberColor = Color.Lerp(flashColor, Color.White, easeOutProgress) * alpha;
            }
            else if (healFlashAnim != null)
            {
                float flashProgress = healFlashAnim.Timer / BattleAnimationManager.HealFlashAnimationState.Duration;
                Color flashColor = _global.Palette_LightGreen;
                labelColor = Color.Lerp(flashColor, _global.Palette_LightGray, Easing.EaseOutQuad(flashProgress)) * alpha;
                numberColor = Color.Lerp(flashColor, Color.White, Easing.EaseOutQuad(flashProgress)) * alpha;
            }

            if (healBounceAnim != null)
            {
                float bounceProgress = healBounceAnim.Timer / BattleAnimationManager.HealBounceAnimationState.Duration;
                float hopAmount = MathF.Sin(bounceProgress * MathHelper.Pi) * -3f; // Hop up
                drawPosition.Y += hopAmount;
            }

            string hpLabel = "HP: ";
            string currentHp = ((int)Math.Round(combatant.VisualHP)).ToString();
            string separator = "/";
            string maxHp = combatant.Stats.MaxHP.ToString();

            spriteBatch.DrawStringSnapped(statsFont, hpLabel, drawPosition, labelColor);
            float currentX = drawPosition.X + statsFont.MeasureString(hpLabel).Width;
            spriteBatch.DrawStringSnapped(statsFont, currentHp, new Vector2(currentX, drawPosition.Y), numberColor);
            currentX += statsFont.MeasureString(currentHp).Width;
            spriteBatch.DrawStringSnapped(statsFont, separator, new Vector2(currentX, drawPosition.Y), labelColor);
            currentX += statsFont.MeasureString(separator).Width;
            spriteBatch.DrawStringSnapped(statsFont, maxHp, new Vector2(currentX, drawPosition.Y), numberColor);
        }

        private void DrawPlayerStatusIcons(SpriteBatch spriteBatch)
        {
            foreach (var iconInfo in _playerStatusIcons)
            {
                var iconTexture = _spriteManager.GetStatusEffectIcon(iconInfo.Effect.EffectType);
                spriteBatch.DrawSnapped(iconTexture, iconInfo.Bounds, Color.White);
            }
        }

        private void UpdateEnemyAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var combatantIds = _enemyAnimationTimers.Keys.ToList();
            foreach (var id in combatantIds)
            {
                var timers = _enemyAnimationTimers[id];
                var intervals = _enemyAnimationIntervals[id];
                var offsets = _enemySpritePartOffsets[id];
                for (int i = 1; i < offsets.Length; i++)
                {
                    timers[i] += deltaTime;
                    if (timers[i] >= intervals[i])
                    {
                        timers[i] = 0f;
                        intervals[i] = (float)(_random.NextDouble() * 5);
                        offsets[i] = new Vector2(_random.Next(-1, 1), _random.Next(-1, 1));
                    }
                }
            }
        }

        private void UpdatePlayerStatusIcons(BattleCombatant player)
        {
            _playerStatusIcons.Clear();
            _hoveredStatusEffectText = null; // Reset hover text each frame
            _hoveredStatusIconAnchorPosition = null; // Reset anchor position
            if (player == null || !player.ActiveStatusEffects.Any()) return;

            var secondaryFont = _core.SecondaryFont;
            var virtualMousePos = Core.TransformMouse(Mouse.GetState().Position);
            const int playerHudY = DIVIDER_Y - 10;
            const int playerHudPaddingX = 10;
            string hpText = $"HP: {((int)Math.Round(player.VisualHP))}/{player.Stats.MaxHP}";
            Vector2 hpTextSize = secondaryFont.MeasureString(hpText);
            float hpStartX = Global.VIRTUAL_WIDTH - playerHudPaddingX - hpTextSize.X;

            const int iconSize = 5;
            const int iconPadding = 2;
            int currentX = (int)hpStartX - iconPadding - iconSize;

            foreach (var effect in player.ActiveStatusEffects)
            {
                int iconY = (int)(playerHudY + (secondaryFont.LineHeight - iconSize) / 2f) + 1;
                var iconBounds = new Rectangle(currentX, iconY, iconSize, iconSize);
                _playerStatusIcons.Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });

                if (iconBounds.Contains(virtualMousePos))
                {
                    _hoveredStatusEffectText = effect.GetTooltipText();
                    _hoveredStatusIconAnchorPosition = new Vector2(iconBounds.Center.X, iconBounds.Top);
                }
                currentX -= (iconSize + iconPadding);
            }
        }

        private Rectangle GetCombatantInteractionBounds(BattleCombatant combatant, Vector2 centerPosition, BitmapFont nameFont, BitmapFont statsFont)
        {
            const int spriteSize = 64;
            float spriteTop = centerPosition.Y - spriteSize - 10;
            Vector2 nameSize = nameFont.MeasureString(combatant.Name);
            float nameY = centerPosition.Y - 8;
            string hpText = $"HP: {((int)Math.Round(combatant.VisualHP))}/{combatant.Stats.MaxHP}";
            Vector2 hpSize = statsFont.MeasureString(hpText);
            float hpY = centerPosition.Y + 2;
            float hpBottom = hpY + statsFont.LineHeight;
            float top = spriteTop;
            float bottom = hpBottom;
            float maxWidth = Math.Max(spriteSize, Math.Max(nameSize.X, hpSize.X));
            float left = centerPosition.X - maxWidth / 2;
            float width = maxWidth;
            float height = bottom - top;
            const int padding = 2;
            return new Rectangle((int)left - padding, (int)top - padding, (int)width + padding * 2, (int)height + padding * 2);
        }

        private Rectangle GetPlayerInteractionBounds(BitmapFont nameFont, BitmapFont statsFont, BattleCombatant player)
        {
            if (player == null) return Rectangle.Empty;
            const int playerHudY = DIVIDER_Y - 10;
            const int playerHudPaddingX = 10;
            Vector2 nameSize = nameFont.MeasureString(player.Name);
            Vector2 namePos = new Vector2(playerHudPaddingX, playerHudY - nameFont.LineHeight + 7);
            string hpText = $"HP: {((int)Math.Round(player.VisualHP))}/{player.Stats.MaxHP}";
            Vector2 hpTextSize = statsFont.MeasureString(hpText);
            float hpStartX = Global.VIRTUAL_WIDTH - playerHudPaddingX - hpTextSize.X;
            int left = (int)namePos.X;
            int right = (int)(hpStartX + hpTextSize.X);
            int top = (int)Math.Min(namePos.Y, playerHudY);
            int bottom = (int)Math.Max(namePos.Y + nameSize.Y, playerHudY + hpTextSize.Y);
            const int padding = 2;
            return new Rectangle(left - padding, top - padding, (right - left) + padding * 2, (bottom - top) + padding * 2);
        }

        public Vector2 GetCombatantHudCenterPosition(BattleCombatant combatant, IEnumerable<BattleCombatant> allCombatants)
        {
            if (combatant.IsPlayerControlled)
            {
                var secondaryFont = _core.SecondaryFont;
                const int playerHudY = DIVIDER_Y - 10;
                const int playerHudPaddingX = 10;

                string hpText = $"HP: {((int)Math.Round(combatant.VisualHP))}/{combatant.Stats.MaxHP}";
                Vector2 hpTextSize = secondaryFont.MeasureString(hpText);
                float hpStartX = Global.VIRTUAL_WIDTH - playerHudPaddingX - hpTextSize.X;

                float centerX = hpStartX + hpTextSize.X / 2f;
                return new Vector2(centerX, playerHudY);
            }
            else
            {
                var enemies = allCombatants.Where(c => !c.IsPlayerControlled).ToList();
                int enemyIndex = enemies.FindIndex(e => e.CombatantID == combatant.CombatantID);
                if (enemyIndex == -1) return Vector2.Zero;

                const int enemyAreaPadding = 20;
                const int enemyHudY = 80;
                int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
                int slotWidth = availableWidth / enemies.Count;
                return new Vector2(enemyAreaPadding + (enemyIndex * slotWidth) + (slotWidth / 2), enemyHudY);
            }
        }
    }
}