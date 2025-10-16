using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
        private readonly TooltipManager _tooltipManager;

        // State
        private List<TargetInfo> _currentTargets = new List<TargetInfo>();
        private readonly List<StatusIconInfo> _playerStatusIcons = new List<StatusIconInfo>();
        private readonly Dictionary<string, List<StatusIconInfo>> _enemyStatusIcons = new Dictionary<string, List<StatusIconInfo>>();
        private StatusIconInfo? _hoveredStatusIcon;


        // Enemy Sprite Animation
        private Dictionary<string, Vector2[]> _enemySpritePartOffsets = new Dictionary<string, Vector2[]>();
        private Dictionary<string, float[]> _enemyAnimationTimers = new Dictionary<string, float[]>();
        private Dictionary<string, float[]> _enemyAnimationIntervals = new Dictionary<string, float[]>();
        private readonly Random _random = new Random();
        private const int ENEMY_SPRITE_PART_SIZE = 64;
        private const float ENEMY_ANIM_MIN_INTERVAL = 0.25f;
        private const float ENEMY_ANIM_MAX_INTERVAL = 0.35f;

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
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
        }

        public void Reset()
        {
            _currentTargets.Clear();
            _playerStatusIcons.Clear();
            _enemyStatusIcons.Clear();
            _enemySpritePartOffsets.Clear();
            _enemyAnimationTimers.Clear();
            _enemyAnimationIntervals.Clear();
        }

        public List<TargetInfo> GetCurrentTargets() => _currentTargets;

        public void Update(GameTime gameTime, IEnumerable<BattleCombatant> combatants)
        {
            UpdateEnemyAnimations(gameTime);
            UpdatePlayerStatusIcons(combatants.FirstOrDefault(c => c.IsPlayerControlled));
            UpdateStatusIconTooltips(combatants);
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

            _currentTargets.Clear();
            var enemies = allCombatants.Where(c => !c.IsPlayerControlled).ToList();
            var player = allCombatants.FirstOrDefault(c => c.IsPlayerControlled);

            // --- Draw Enemy HUDs ---
            DrawEnemyHuds(spriteBatch, font, secondaryFont, enemies, uiManager.TargetTypeForSelection, animationManager);

            // --- Draw Player HUD ---
            DrawPlayerHud(spriteBatch, font, secondaryFont, player, gameTime, animationManager, uiManager);

            // --- Draw UI Title ---
            DrawUITitle(spriteBatch, secondaryFont, gameTime, uiManager.SubMenuState);

            // --- Draw Highlights & Indicators ---
            DrawHoverHighlights(spriteBatch, font, secondaryFont, allCombatants, uiManager.HoverHighlightState, uiManager.SharedPulseTimer);
            DrawTurnIndicator(spriteBatch, font, gameTime, currentActor, allCombatants);
            DrawTargetingUI(spriteBatch, font, gameTime, uiManager, inputHandler);

            // --- Draw Divider ---
            spriteBatch.DrawSnapped(pixel, new Rectangle(0, DIVIDER_Y, Global.VIRTUAL_WIDTH, 1), Color.White);
        }

        public void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font)
        {
            // All tooltips are now handled by the TooltipManager, which is drawn
            // in the BattleScene's final draw pass. This method is now empty.
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

        private void DrawPlayerHud(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, BattleCombatant player, GameTime gameTime, BattleAnimationManager animationManager, BattleUIManager uiManager)
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

            var offsetVector = Vector2.Zero; // No bobbing for bars
            DrawPlayerResourceBars(spriteBatch, player, offsetVector, uiManager);

            DrawPlayerStatusIcons(spriteBatch, player, secondaryFont, playerHudY);

            if (!player.IsDefeated && uiManager.TargetTypeForSelection.HasValue)
            {
                if (uiManager.TargetTypeForSelection == TargetType.SingleAll)
                {
                    _currentTargets.Add(new TargetInfo
                    {
                        Combatant = player,
                        Bounds = GetPlayerInteractionBounds(font, secondaryFont, player)
                    });
                }
            }
        }

        private void DrawPlayerResourceBars(SpriteBatch spriteBatch, BattleCombatant player, Vector2 offset, BattleUIManager uiManager)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            const int barWidth = 60;
            const int barPaddingX = 10;
            const int hpBarY = DIVIDER_Y - 9;
            const int manaBarY = hpBarY + 3; // Adjusted for 2px HP bar + 1px gap
            float startX = Global.VIRTUAL_WIDTH - barPaddingX - barWidth;

            // HP Bar
            float hpPercent = player.Stats.MaxHP > 0 ? Math.Clamp(player.VisualHP / player.Stats.MaxHP, 0f, 1f) : 0f;
            var hpBgRect = new Rectangle((int)(startX + offset.X), (int)(hpBarY + offset.Y), barWidth, 2);
            var hpFgRect = new Rectangle((int)(startX + offset.X), (int)(hpBarY + offset.Y), (int)(barWidth * hpPercent), 2);
            spriteBatch.DrawSnapped(pixel, hpBgRect, _global.Palette_DarkGray);
            spriteBatch.DrawSnapped(pixel, hpFgRect, _global.Palette_LightGreen);

            // Mana Bar
            float manaPercent = player.Stats.MaxMana > 0 ? Math.Clamp((float)player.Stats.CurrentMana / player.Stats.MaxMana, 0f, 1f) : 0f;
            var manaBgRect = new Rectangle((int)(startX + offset.X), (int)(manaBarY + offset.Y), barWidth, 2);
            var manaFgRect = new Rectangle((int)(startX + offset.X), (int)(manaBarY + offset.Y), (int)(barWidth * manaPercent), 2);
            spriteBatch.DrawSnapped(pixel, manaBgRect, _global.Palette_DarkGray);
            spriteBatch.DrawSnapped(pixel, manaFgRect, _global.Palette_LightBlue);

            // Mana Cost Preview
            var hoveredMove = uiManager.HoveredMove;
            if (hoveredMove != null && hoveredMove.MoveType == MoveType.Spell && hoveredMove.ManaCost > 0)
            {
                float currentManaWidth = barWidth * manaPercent;

                if (player.Stats.CurrentMana >= hoveredMove.ManaCost)
                {
                    // Animate color pulse
                    const float PULSE_SPEED = 4f;
                    float pulse = (MathF.Sin(uiManager.SharedPulseTimer * PULSE_SPEED) + 1f) / 2f; // Oscillates 0..1
                    Color pulseColor = Color.Lerp(_global.Palette_Yellow, _global.Palette_BrightWhite, pulse);

                    // Draw yellow cost preview
                    float costPercent = (float)hoveredMove.ManaCost / player.Stats.MaxMana;
                    int costWidth = (int)(barWidth * costPercent);
                    int previewX = (int)(startX + currentManaWidth - costWidth);
                    var previewRect = new Rectangle(
                        (int)(previewX + offset.X),
                        (int)(manaBarY + offset.Y),
                        costWidth,
                        2
                    );

                    spriteBatch.DrawSnapped(pixel, previewRect, pulseColor);
                }
                else
                {
                    // Draw red "not enough" indicator over the remaining mana
                    var previewRect = new Rectangle((int)(startX + offset.X), (int)(manaBarY + offset.Y), (int)currentManaWidth, 2);
                    spriteBatch.DrawSnapped(pixel, previewRect, _global.Palette_Red);
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

            float bobOffset = (MathF.Sin(sharedBobbingTimer * 4f) > 0) ? 1f : 0f;

            switch (move.Target)
            {
                case TargetType.Self:
                    var player = targets.FirstOrDefault(t => t.IsPlayerControlled);
                    if (player != null)
                    {
                        var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
                        if (arrowSheet == null) return;

                        // Use right-pointing arrow, same as turn indicator
                        var arrowRect = arrowRects[4];

                        // Use horizontal sway instead of vertical bob.
                        // A smooth sine wave provides the sway effect.
                        float swayOffset = MathF.Round(MathF.Sin(sharedBobbingTimer * 4f) * 1.5f);

                        // Positioning logic from DrawTurnIndicator
                        const int playerHudY = DIVIDER_Y - 10;
                        const int playerHudPaddingX = 10;
                        Vector2 nameSize = font.MeasureString(player.Name);
                        Vector2 namePos = new Vector2(playerHudPaddingX, playerHudY - font.LineHeight + 7);
                        var arrowPos = new Vector2(
                            namePos.X - arrowRect.Width - 4 + swayOffset, // Horizontal sway
                            namePos.Y + (nameSize.Y - arrowRect.Height) / 2 - 1
                        );
                        spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, flashColor);
                    }
                    break;

                case TargetType.Single:
                case TargetType.SingleAll:
                    foreach (var target in targets)
                    {
                        Rectangle sourceRect = target.IsPlayerControlled ? arrowRects[2] : arrowRects[6]; // Up for player, Down for enemy
                        DrawTargetIndicator(spriteBatch, font, secondaryFont, allCombatants, target, sourceRect, flashColor, bobOffset);
                    }
                    break;

                case TargetType.Every:
                case TargetType.EveryAll:
                    foreach (var target in targets)
                    {
                        Rectangle sourceRect = target.IsPlayerControlled ? arrowRects[2] : arrowRects[6]; // Up for player, Down for enemy
                        DrawMultiArrowIndicator(spriteBatch, font, secondaryFont, allCombatants, target, sourceRect, flashColor, bobOffset);
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

            if (combatant.IsPlayerControlled)
            {
                var playerBounds = GetPlayerInteractionBounds(font, secondaryFont, combatant);
                arrowPos = new Vector2(
                    playerBounds.Center.X - sourceRect.Width / 2f,
                    playerBounds.Bottom + 2 + bobOffset
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

                var spriteRect = new Rectangle((int)(centerPosition.X - ENEMY_SPRITE_PART_SIZE / 2), (int)(centerPosition.Y - ENEMY_SPRITE_PART_SIZE - 10), ENEMY_SPRITE_PART_SIZE, ENEMY_SPRITE_PART_SIZE);
                float highestPointY = GetEnemySpriteTopY(combatant, spriteRect.Y);

                arrowPos = new Vector2(spriteRect.Center.X - sourceRect.Width / 2f, highestPointY - sourceRect.Height - 1 + bobOffset);
            }

            if (color == _global.Palette_Red)
            {
                arrowPos.Y += 1;
            }

            spriteBatch.DrawSnapped(arrowSheet, arrowPos, sourceRect, color);
        }

        private void DrawMultiArrowIndicator(SpriteBatch spriteBatch, BitmapFont font, BitmapFont secondaryFont, IEnumerable<BattleCombatant> allCombatants, BattleCombatant combatant, Rectangle sourceRect, Color color, float bobOffset = 0f)
        {
            var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
            if (arrowSheet == null) return;

            const int arrowCount = 3;
            const int arrowGap = 1;
            int totalWidth = (sourceRect.Width * arrowCount) + (arrowGap * (arrowCount - 1));
            Vector2 groupCenterPos;

            if (combatant.IsPlayerControlled)
            {
                var playerBounds = GetPlayerInteractionBounds(font, secondaryFont, combatant);
                groupCenterPos = new Vector2(
                    playerBounds.Center.X,
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

                var spriteRect = new Rectangle((int)(centerPosition.X - ENEMY_SPRITE_PART_SIZE / 2), (int)(centerPosition.Y - ENEMY_SPRITE_PART_SIZE - 10), ENEMY_SPRITE_PART_SIZE, ENEMY_SPRITE_PART_SIZE);
                float highestPointY = GetEnemySpriteTopY(combatant, spriteRect.Y);

                groupCenterPos = new Vector2(spriteRect.Center.X, highestPointY - sourceRect.Height - 1);
            }

            float startX = groupCenterPos.X - (totalWidth / 2f);
            float yPos = groupCenterPos.Y + bobOffset;

            for (int i = 0; i < arrowCount; i++)
            {
                Vector2 arrowPos = new Vector2(
                    startX + i * (sourceRect.Width + arrowGap),
                    yPos
                );
                if (color == _global.Palette_Red)
                {
                    arrowPos.Y += 1;
                }
                spriteBatch.DrawSnapped(arrowSheet, arrowPos, sourceRect, color);
            }
        }

        private void DrawTurnIndicator(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, BattleCombatant currentActor, IEnumerable<BattleCombatant> allCombatants)
        {
            if (currentActor == null || currentActor.IsDefeated) return;

            var arrowSheet = _spriteManager.ArrowIconSpriteSheet;
            var arrowRects = _spriteManager.ArrowIconSourceRects;
            if (arrowSheet == null || arrowRects == null) return;

            if (currentActor.IsPlayerControlled)
            {
                var arrowRect = arrowRects[4]; // Right arrow
                // A smooth sine wave provides the sway effect.
                float swayOffset = MathF.Round(MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 4f) * 1.5f);

                const int playerHudY = DIVIDER_Y - 10;
                const int playerHudPaddingX = 10;
                Vector2 nameSize = font.MeasureString(currentActor.Name);
                Vector2 namePos = new Vector2(playerHudPaddingX, playerHudY - font.LineHeight + 7);
                var arrowPos = new Vector2(
                    namePos.X - arrowRect.Width - 4 + swayOffset,
                    namePos.Y + (nameSize.Y - arrowRect.Height) / 2 - 1
                );
                spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, Color.White);
            }
            else // Enemy turn
            {
                float bobOffset = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 4f) > 0) ? 1f : 0f;
                var arrowRect = arrowRects[6]; // Down arrow
                var enemies = allCombatants.Where(c => !c.IsPlayerControlled).ToList();
                int enemyIndex = enemies.FindIndex(e => e.CombatantID == currentActor.CombatantID);
                if (enemyIndex == -1) return;

                const int enemyAreaPadding = 20;
                const int enemyHudY = 80;
                int availableWidth = Global.VIRTUAL_WIDTH - (enemyAreaPadding * 2);
                int slotWidth = availableWidth / enemies.Count;
                var centerPosition = new Vector2(enemyAreaPadding + (enemyIndex * slotWidth) + (slotWidth / 2), enemyHudY);

                var spriteRect = new Rectangle((int)(centerPosition.X - ENEMY_SPRITE_PART_SIZE / 2), (int)(centerPosition.Y - ENEMY_SPRITE_PART_SIZE - 10), ENEMY_SPRITE_PART_SIZE, ENEMY_SPRITE_PART_SIZE);

                // Calculate the highest point of the sprite for this frame
                float highestPointY = GetEnemySpriteTopY(currentActor, spriteRect.Y);

                var arrowPos = new Vector2(spriteRect.Center.X - arrowRect.Width / 2, highestPointY - arrowRect.Height - 1 + bobOffset);
                spriteBatch.DrawSnapped(arrowSheet, arrowPos, arrowRect, Color.White);
            }
        }

        private void DrawCombatantHud(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, BattleCombatant combatant, Vector2 centerPosition, BattleAnimationManager animationManager)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var spriteRect = new Rectangle((int)(centerPosition.X - ENEMY_SPRITE_PART_SIZE / 2), (int)(centerPosition.Y - ENEMY_SPRITE_PART_SIZE - 10), ENEMY_SPRITE_PART_SIZE, ENEMY_SPRITE_PART_SIZE);
            Color tintColor = Color.White * combatant.VisualAlpha;

            Texture2D enemySprite = _spriteManager.GetEnemySprite(combatant.ArchetypeId);
            Texture2D enemySilhouette = _spriteManager.GetEnemySpriteSilhouette(combatant.ArchetypeId);

            if (enemySprite != null)
            {
                int numParts = enemySprite.Width / ENEMY_SPRITE_PART_SIZE;
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
                    // --- Outline Pass ---
                    Color outlineColor = _global.Palette_DarkGray * combatant.VisualAlpha;
                    if (enemySilhouette != null)
                    {
                        for (int i = 0; i < numParts; i++)
                        {
                            var sourceRect = new Rectangle(i * ENEMY_SPRITE_PART_SIZE, 0, ENEMY_SPRITE_PART_SIZE, ENEMY_SPRITE_PART_SIZE);
                            var partOffset = offsets[i];
                            var baseDrawPosition = new Vector2(spriteRect.X + partOffset.X, spriteRect.Y + partOffset.Y);

                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle((int)baseDrawPosition.X - 1, (int)baseDrawPosition.Y, spriteRect.Width, spriteRect.Height), sourceRect, outlineColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle((int)baseDrawPosition.X + 1, (int)baseDrawPosition.Y, spriteRect.Width, spriteRect.Height), sourceRect, outlineColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle((int)baseDrawPosition.X, (int)baseDrawPosition.Y - 1, spriteRect.Width, spriteRect.Height), sourceRect, outlineColor);
                            spriteBatch.DrawSnapped(enemySilhouette, new Rectangle((int)baseDrawPosition.X, (int)baseDrawPosition.Y + 1, spriteRect.Width, spriteRect.Height), sourceRect, outlineColor);
                        }
                    }

                    // --- Fill Pass ---
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

            DrawEnemyStatusIcons(spriteBatch, combatant, spriteRect);

            Vector2 nameSize = nameFont.MeasureString(combatant.Name);
            Vector2 namePos = new Vector2(centerPosition.X - nameSize.X / 2, centerPosition.Y - 8);
            spriteBatch.DrawStringSnapped(nameFont, combatant.Name, namePos, tintColor);

            DrawEnemyHealthBar(spriteBatch, combatant, centerPosition);
        }

        private void DrawEnemyHealthBar(SpriteBatch spriteBatch, BattleCombatant combatant, Vector2 centerPosition)
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
        }

        private void DrawPlayerStatusIcons(SpriteBatch spriteBatch, BattleCombatant player, BitmapFont font, int hudY)
        {
            if (player == null || !player.ActiveStatusEffects.Any()) return;

            var pixel = ServiceLocator.Get<Texture2D>();

            foreach (var iconInfo in _playerStatusIcons)
            {
                var iconTexture = _spriteManager.GetStatusEffectIcon(iconInfo.Effect.EffectType);
                spriteBatch.DrawSnapped(iconTexture, iconInfo.Bounds, Color.White);

                var borderColor = IsNegativeStatus(iconInfo.Effect.EffectType) ? _global.Palette_Red : _global.Palette_LightBlue;
                var borderBounds = new Rectangle(iconInfo.Bounds.X - 1, iconInfo.Bounds.Y - 1, iconInfo.Bounds.Width + 2, iconInfo.Bounds.Height + 2);
                float alpha = (_hoveredStatusIcon.HasValue && _hoveredStatusIcon.Value.Effect == iconInfo.Effect) ? 0.25f : 0.1f;
                DrawRectangleBorder(spriteBatch, pixel, borderBounds, 1, borderColor * alpha);
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
                        intervals[i] = (float)(_random.NextDouble() * (ENEMY_ANIM_MAX_INTERVAL - ENEMY_ANIM_MIN_INTERVAL) + ENEMY_ANIM_MIN_INTERVAL);
                        offsets[i] = new Vector2(_random.Next(-1, 1), _random.Next(-1, 1));
                    }
                }
            }
        }

        private void UpdatePlayerStatusIcons(BattleCombatant player)
        {
            // This method now only calculates the bounds for hover detection, drawing is done in DrawPlayerHud
            _playerStatusIcons.Clear();
            if (player == null || !player.ActiveStatusEffects.Any()) return;

            var secondaryFont = _core.SecondaryFont;
            const int playerHudY = DIVIDER_Y - 10;
            const int playerHudPaddingX = 10;

            const int barWidth = 60;
            float hpStartX = Global.VIRTUAL_WIDTH - playerHudPaddingX - barWidth;

            const int iconSize = 5;
            const int iconPadding = 2;
            const int iconGap = 1;
            int currentX = (int)hpStartX - iconPadding - iconSize;

            foreach (var effect in player.ActiveStatusEffects)
            {
                int iconY = (int)(playerHudY + (secondaryFont.LineHeight - iconSize) / 2f) + 1;
                var iconBounds = new Rectangle(currentX, iconY, iconSize, iconSize);
                _playerStatusIcons.Add(new StatusIconInfo { Effect = effect, Bounds = iconBounds });
                currentX -= (iconSize + iconPadding + iconGap);
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

        private Rectangle GetPlayerInteractionBounds(BitmapFont nameFont, BitmapFont secondaryFont, BattleCombatant player)
        {
            if (player == null) return Rectangle.Empty;
            const int playerHudY = DIVIDER_Y - 10;
            const int playerHudPaddingX = 10;
            Vector2 nameSize = nameFont.MeasureString(player.Name);
            Vector2 namePos = new Vector2(playerHudPaddingX, playerHudY - nameFont.LineHeight + 7);

            const int barWidth = 60;
            float hpStartX = Global.VIRTUAL_WIDTH - playerHudPaddingX - barWidth;

            int left = (int)namePos.X;
            int right = (int)(hpStartX + barWidth);
            int top = (int)Math.Min(namePos.Y, playerHudY);
            int bottom = (int)Math.Max(namePos.Y + nameSize.Y, playerHudY + 3); // 3 is height of bars
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

                const int barWidth = 60;
                float hpStartX = Global.VIRTUAL_WIDTH - playerHudPaddingX - barWidth;

                float centerX = hpStartX + barWidth / 2f;
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

        private float GetEnemySpriteTopY(BattleCombatant enemy, float spriteRectTopY)
        {
            var staticOffsets = _spriteManager.GetEnemySpriteTopPixelOffsets(enemy.ArchetypeId);
            if (staticOffsets == null || !_enemySpritePartOffsets.TryGetValue(enemy.CombatantID, out var animOffsets))
            {
                return spriteRectTopY; // Fallback to the top of the bounding box
            }

            float minTopY = float.MaxValue;
            for (int i = 0; i < staticOffsets.Length; i++)
            {
                if (staticOffsets[i] == int.MaxValue) continue; // Skip empty parts

                float currentPartTopY = staticOffsets[i] + (animOffsets.Length > i ? animOffsets[i].Y : 0);
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
                case StatusEffectType.IntelligenceDown:
                case StatusEffectType.AgilityDown:
                case StatusEffectType.TenacityDown:
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
    }
}
