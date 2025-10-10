using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond
{
    public class SpriteManager
    {
        private readonly Core _core;
        private readonly TextureFactory _textureFactory;

        // UI Sprite Sheets
        public Texture2D ActionButtonsSpriteSheet { get; private set; }
        public Texture2D ActionButtonTemplateSpriteSheet { get; private set; }
        public Texture2D ActionButtonTemplateSecondarySprite { get; private set; }
        public Texture2D ActionMovesBackgroundSprite { get; private set; }
        public Texture2D ActionTooltipBackgroundSprite { get; private set; }
        public Texture2D ElementIconsSpriteSheet { get; private set; }
        public Texture2D ActionIconsSpriteSheet { get; private set; }
        public Texture2D ActionButtonUsesSpriteSheet { get; private set; }

        // Source Rectangles for UI elements
        public Rectangle[] ActionButtonSourceRects { get; private set; } // 0-2: Act, 3-5: Item, 6-8: Flee (Normal, Hover, Clicked)
        public Dictionary<int, Rectangle> ElementIconSourceRects { get; private set; } = new Dictionary<int, Rectangle>();
        public Rectangle[] ActionIconSourceRects { get; private set; } // 0: Strike, 1: Dodge, 2: Stall
        public Dictionary<int, Rectangle> RarityBackgroundSourceRects { get; private set; } = new Dictionary<int, Rectangle>();
        public Dictionary<int, Rectangle> SpellUsesSourceRects { get; private set; } = new Dictionary<int, Rectangle>();


        // Enemy Sprite Cache
        private readonly Dictionary<string, Texture2D> _enemySprites = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int[]> _enemySpriteTopPixelOffsets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<StatusEffectType, Texture2D> _statusEffectIcons = new Dictionary<StatusEffectType, Texture2D>();
        private readonly Dictionary<string, (Texture2D Original, Texture2D Silhouette)> _relicSprites = new Dictionary<string, (Texture2D, Texture2D)>(StringComparer.OrdinalIgnoreCase);


        private Texture2D _logoSprite;
        private Texture2D _waterSprite;
        private Texture2D _flatlandSprite;
        private Texture2D _hillSprite;
        private Texture2D _mountainSprite;
        private Texture2D _peakSprite;
        private Texture2D _playerSprite;
        private Texture2D _pathSprite;
        private Texture2D _runPathSprite;
        private Texture2D _pathEndSprite;
        private Texture2D _emptySprite;
        private Texture2D _speedMarkSprite;
        private Texture2D _mapMarkerSprite;
        private Texture2D _circleTextureSprite;
        private Texture2D _settingsIconSprite;
        private Texture2D _turnIndicatorSprite;
        private Texture2D _circleParticleSprite;
        private Texture2D _emberParticleSprite;
        private Texture2D _softParticleSprite;
        private Texture2D _fogOfWarSprite;
        public Effect FireballParticleShaderEffect { get; private set; }
        public Texture2D ArrowIconSpriteSheet { get; private set; }
        public Rectangle[] ArrowIconSourceRects { get; private set; }
        public Texture2D SpellbookPageSprite { get; private set; }
        public Texture2D SpellbookClosedSprite { get; private set; }

        // Split Map Node Sprites
        public Texture2D SplitNodeStart { get; private set; }
        public Texture2D SplitNodeBattle { get; private set; }
        public Texture2D SplitNodeNarrative { get; private set; }
        public Texture2D SplitNodeReward { get; private set; }
        public Texture2D SplitNodeBoss { get; private set; }


        // Mouse Prompt Sprites
        public Texture2D MousePromptBlank { get; private set; }
        public Texture2D MousePromptLeftClick { get; private set; }
        public Texture2D MousePromptRightClick { get; private set; }
        public Texture2D MousePromptMiddleClick { get; private set; }
        public Texture2D MousePromptDisabled { get; private set; }

        public Texture2D LogoSprite => _logoSprite;
        public Texture2D WaterSprite => _waterSprite;
        public Texture2D FlatlandSprite => _flatlandSprite;
        public Texture2D HillSprite => _hillSprite;
        public Texture2D MountainSprite => _mountainSprite;
        public Texture2D PeakSprite => _peakSprite;
        public Texture2D PlayerSprite => _playerSprite;
        public Texture2D PathSprite => _pathSprite;
        public Texture2D RunPathSprite => _runPathSprite;
        public Texture2D PathEndSprite => _pathEndSprite;
        public Texture2D EmptySprite => _emptySprite;
        public Texture2D SpeedMarkSprite => _speedMarkSprite;
        public Texture2D MapMarkerSprite => _mapMarkerSprite;
        public Texture2D CircleTextureSprite => _circleTextureSprite;
        public Texture2D SettingsIconSprite => _settingsIconSprite;
        public Texture2D TurnIndicatorSprite => _turnIndicatorSprite;
        public Texture2D CircleParticleSprite => _circleParticleSprite;
        public Texture2D EmberParticleSprite => _emberParticleSprite;
        public Texture2D SoftParticleSprite => _softParticleSprite;
        public Texture2D FogOfWarSprite => _fogOfWarSprite;

        public SpriteManager()
        {
            _core = ServiceLocator.Get<Core>();
            _textureFactory = ServiceLocator.Get<TextureFactory>();
        }

        /// <summary>
        /// Loads assets required for the main menu and essential UI elements that are always present.
        /// </summary>
        public void LoadEssentialContent()
        {
            try { _logoSprite = _core.Content.Load<Texture2D>("Sprites/logo"); }
            catch { _logoSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _mapMarkerSprite = _core.Content.Load<Texture2D>("Sprites/map_marker"); }
            catch { _mapMarkerSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Magenta); }

            try { _circleTextureSprite = _textureFactory.CreateCircleTexture(); }
            catch { _circleTextureSprite = _textureFactory.CreateColoredTexture(16, 16, Color.Red); }

            try { _settingsIconSprite = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/ui_settings_icon"); }
            catch { _settingsIconSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _turnIndicatorSprite = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/ui_turn_indicator"); }
            catch { _turnIndicatorSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _circleParticleSprite = _textureFactory.CreateCircleParticleTexture(); }
            catch { _circleParticleSprite = _textureFactory.CreateColoredTexture(4, 4, Color.Red); }

            try { _emberParticleSprite = _core.Content.Load<Texture2D>("Sprites/Particles/ember_particle"); }
            catch { _emberParticleSprite = _textureFactory.CreateColoredTexture(9, 9, Color.Red); }

            try { _softParticleSprite = _textureFactory.CreateSoftCircleParticleTexture(); }
            catch { _softParticleSprite = _textureFactory.CreateColoredTexture(16, 16, Color.Red); }

            try { ArrowIconSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/arrow_icon_spritesheet"); }
            catch { ArrowIconSpriteSheet = _textureFactory.CreateColoredTexture(48, 48, Color.Magenta); }

            try { FireballParticleShaderEffect = _core.Content.Load<Effect>("Shaders/FireballParticleShader"); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] Could not load shader 'Shaders/FireballParticleShader'. Please ensure it's in the Content project. {ex.Message}"); }

            try { ActionButtonsSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_buttons_icon_spritesheet"); }
            catch { ActionButtonsSpriteSheet = _textureFactory.CreateColoredTexture(288, 150, Color.Magenta); }

            try { ActionButtonTemplateSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_button_template_spritesheet"); }
            catch { ActionButtonTemplateSpriteSheet = _textureFactory.CreateColoredTexture(1099, 17, Color.Magenta); }

            try { ActionButtonTemplateSecondarySprite = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_button_template_secondary"); }
            catch { ActionButtonTemplateSecondarySprite = _textureFactory.CreateColoredTexture(104, 17, Color.Magenta); }

            try
            {
                ActionMovesBackgroundSprite = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_moves_button_area_background");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SpriteManager] [ERROR] Failed to load 'ui_action_moves_button_area_background'. This is a critical UI asset.");
                Debug.WriteLine("[SpriteManager] [ACTION REQUIRED] Please ensure the following:");
                Debug.WriteLine("1. The file exists at 'Content/Sprites/UI/BattleUI/ui_action_moves_button_area_background.png'");
                Debug.WriteLine("2. The file has been added to your 'Content.mgcb' file.");
                Debug.WriteLine("3. The file's 'Build Action' in the content pipeline is set to 'Build'.");
                Debug.WriteLine($"4. Detailed error: {ex.Message}");
                ActionMovesBackgroundSprite = _textureFactory.CreateColoredTexture(294, 47, Color.Magenta);
            }

            try { ActionTooltipBackgroundSprite = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_tooltip_background"); }
            catch { ActionTooltipBackgroundSprite = _textureFactory.CreateColoredTexture(319, 178, Color.DarkGray); }

            try { ElementIconsSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/ui_element_icons_9x9_spritesheet"); }
            catch { ElementIconsSpriteSheet = _textureFactory.CreateColoredTexture(45, 45, Color.Magenta); }

            try { ActionIconsSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/ui_action_icons_spritesheet_9x9"); }
            catch { ActionIconsSpriteSheet = _textureFactory.CreateColoredTexture(36, 9, Color.Magenta); }

            try { ActionButtonUsesSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_button_uses_spritesheet"); }
            catch { ActionButtonUsesSpriteSheet = _textureFactory.CreateColoredTexture(471, 17, Color.Magenta); }

            // Load Mouse Prompt Sprites
            try { MousePromptBlank = _core.Content.Load<Texture2D>("Sprites/UI/KeyPrompts/mouse/ui_mouse_blank"); }
            catch { MousePromptBlank = _textureFactory.CreateColoredTexture(5, 7, Color.Magenta); }

            try { MousePromptLeftClick = _core.Content.Load<Texture2D>("Sprites/UI/KeyPrompts/mouse/ui_mouse_left_click"); }
            catch { MousePromptLeftClick = _textureFactory.CreateColoredTexture(5, 7, Color.Magenta); }

            try { MousePromptRightClick = _core.Content.Load<Texture2D>("Sprites/UI/KeyPrompts/mouse/ui_mouse_right_click"); }
            catch { MousePromptRightClick = _textureFactory.CreateColoredTexture(5, 7, Color.Magenta); }

            try { MousePromptMiddleClick = _core.Content.Load<Texture2D>("Sprites/UI/KeyPrompts/mouse/ui_mouse_middle_click"); }
            catch { MousePromptMiddleClick = _textureFactory.CreateColoredTexture(5, 7, Color.Magenta); }

            try { MousePromptDisabled = _core.Content.Load<Texture2D>("Sprites/UI/KeyPrompts/mouse/ui_mouse_disabled"); }
            catch { MousePromptDisabled = _textureFactory.CreateColoredTexture(5, 7, Color.Magenta); }

            try { SpellbookPageSprite = _core.Content.Load<Texture2D>("Sprites/SpellBook/spellbook_page"); }
            catch { SpellbookPageSprite = _textureFactory.CreateColoredTexture(35, 35, Color.Magenta); }

            try { SpellbookClosedSprite = _core.Content.Load<Texture2D>("Sprites/SpellBook/spellbook_closed"); }
            catch { SpellbookClosedSprite = _textureFactory.CreateColoredTexture(64, 64, Color.Magenta); }

            // Load Split Map Node Sprites
            try { SplitNodeStart = _core.Content.Load<Texture2D>("Sprites/UI/SplitMap/split_node_start"); }
            catch { SplitNodeStart = _textureFactory.CreateColoredTexture(16, 16, Color.Green); }
            try { SplitNodeBattle = _core.Content.Load<Texture2D>("Sprites/UI/SplitMap/split_node_battle"); }
            catch { SplitNodeBattle = _textureFactory.CreateColoredTexture(16, 16, Color.Red); }
            try { SplitNodeNarrative = _core.Content.Load<Texture2D>("Sprites/UI/SplitMap/split_node_narrative"); }
            catch { SplitNodeNarrative = _textureFactory.CreateColoredTexture(16, 16, Color.Blue); }
            try { SplitNodeReward = _core.Content.Load<Texture2D>("Sprites/UI/SplitMap/split_node_reward"); }
            catch { SplitNodeReward = _textureFactory.CreateColoredTexture(16, 16, Color.Gold); }
            try { SplitNodeBoss = _core.Content.Load<Texture2D>("Sprites/UI/SplitMap/split_node_boss"); }
            catch { SplitNodeBoss = _textureFactory.CreateColoredTexture(16, 16, Color.Purple); }


            InitializeArrowSourceRects();
            InitializeActionButtonsSourceRects();
            InitializeElementIconsSourceRects();
            InitializeActionIconsSourceRects();
            InitializeRarityBackgrounds();
            InitializeSpellUsesRects();
        }

        private void InitializeRarityBackgrounds()
        {
            if (ActionButtonTemplateSpriteSheet == null) return;

            int spriteWidth = 157;
            int spriteHeight = 17;
            int[] rarityMap = { 0, 1, 2, 3, 4, 5, -1 }; // The rarity values corresponding to each frame

            for (int i = 0; i < rarityMap.Length; i++)
            {
                int rarityValue = rarityMap[i];
                RarityBackgroundSourceRects[rarityValue] = new Rectangle(i * spriteWidth, 0, spriteWidth, spriteHeight);
            }
        }

        private void InitializeSpellUsesRects()
        {
            if (ActionButtonUsesSpriteSheet == null) return;
            int spriteWidth = 157;
            int spriteHeight = 17;
            // Corrected mapping based on user feedback.
            // Assumes the sprite sheet is ordered: [3 uses], [2 uses], [1 use]
            SpellUsesSourceRects[3] = new Rectangle(0 * spriteWidth, 0, spriteWidth, spriteHeight); // 3 uses left (leftmost sprite)
            SpellUsesSourceRects[2] = new Rectangle(1 * spriteWidth, 0, spriteWidth, spriteHeight); // 2 uses left (middle sprite)
            SpellUsesSourceRects[1] = new Rectangle(2 * spriteWidth, 0, spriteWidth, spriteHeight); // 1 use left (rightmost sprite)
        }

        private void InitializeArrowSourceRects()
        {
            // Indices 0-7 are the directional arrows in a circle starting from West, going clockwise.
            // Index 8 is the center star.
            var spriteSheetCoords = new Point[9]
            {
                new Point(0, 1), // 0: W
                new Point(0, 0), // 1: NW
                new Point(1, 0), // 2: N (Up)
                new Point(2, 0), // 3: NE
                new Point(2, 1), // 4: E (Right)
                new Point(2, 2), // 5: SE
                new Point(1, 2), // 6: S (Down)
                new Point(0, 2), // 7: SW
                new Point(1, 1)  // 8: Center (Star)
            };

            ArrowIconSourceRects = new Rectangle[9];
            int spriteWidth = ArrowIconSpriteSheet.Width / 3;
            int spriteHeight = ArrowIconSpriteSheet.Height / 3;

            for (int i = 0; i < 9; i++)
            {
                ArrowIconSourceRects[i] = new Rectangle(
                    spriteSheetCoords[i].X * spriteWidth,
                    spriteSheetCoords[i].Y * spriteHeight,
                    spriteWidth,
                    spriteHeight
                );
            }
        }

        private void InitializeActionButtonsSourceRects()
        {
            ActionButtonSourceRects = new Rectangle[9];
            int spriteWidth = ActionButtonsSpriteSheet.Width / 3;
            int spriteHeight = ActionButtonsSpriteSheet.Height / 3;
            for (int i = 0; i < 9; i++)
            {
                int row = i / 3;
                int col = i % 3;
                ActionButtonSourceRects[i] = new Rectangle(col * spriteWidth, row * spriteHeight, spriteWidth, spriteHeight);
            }
        }

        private void InitializeElementIconsSourceRects()
        {
            const int iconSize = 9;
            const int columns = 5;
            // There are 13 offensive elements, from ID 1 to 13.
            for (int i = 0; i < 13; i++)
            {
                int elementId = i + 1;
                int col = i % columns;
                int row = i / columns;
                ElementIconSourceRects[elementId] = new Rectangle(col * iconSize, row * iconSize, iconSize, iconSize);
            }
        }

        private void InitializeActionIconsSourceRects()
        {
            ActionIconSourceRects = new Rectangle[4];
            const int iconSize = 9;
            for (int i = 0; i < 4; i++)
            {
                ActionIconSourceRects[i] = new Rectangle(i * iconSize, 0, iconSize, iconSize);
            }
        }

        public Texture2D GetEnemySprite(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId)) return null;

            if (_enemySprites.TryGetValue(archetypeId, out var cachedSprite))
            {
                return cachedSprite;
            }

            try
            {
                var sprite = _core.Content.Load<Texture2D>($"Sprites/Enemies/{archetypeId.ToLower()}");
                _enemySprites[archetypeId] = sprite;
                PreCalculateTopPixelOffsets(sprite, archetypeId); // Pre-calculate on first load
                return sprite;
            }
            catch
            {
                _enemySprites[archetypeId] = null;
                return null;
            }
        }

        public Texture2D GetRelicSprite(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                LoadAndCacheRelic("placeholder", null);
                return _relicSprites["placeholder"].Original;
            }

            if (_relicSprites.TryGetValue(imagePath, out var cachedSprite))
            {
                return cachedSprite.Original;
            }

            return LoadAndCacheRelic(imagePath, imagePath).Original;
        }

        public Texture2D GetRelicSpriteSilhouette(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                LoadAndCacheRelic("placeholder", null);
                return _relicSprites["placeholder"].Silhouette;
            }

            if (_relicSprites.TryGetValue(imagePath, out var cachedSprite))
            {
                return cachedSprite.Silhouette;
            }

            return LoadAndCacheRelic(imagePath, imagePath).Silhouette;
        }

        private (Texture2D Original, Texture2D Silhouette) LoadAndCacheRelic(string cacheKey, string? imagePath)
        {
            // If it's already cached, return immediately.
            if (_relicSprites.TryGetValue(cacheKey, out var cachedTuple))
            {
                return cachedTuple;
            }

            Texture2D originalTexture;
            try
            {
                if (imagePath != null)
                {
                    originalTexture = _core.Content.Load<Texture2D>(imagePath);
                }
                else // This is the placeholder case
                {
                    originalTexture = _textureFactory.CreateColoredTexture(32, 32, Color.White);
                }
            }
            catch
            {
                Debug.WriteLine($"[SpriteManager] [WARNING] Could not load relic sprite at '{imagePath}'. Using white placeholder.");
                originalTexture = _textureFactory.CreateColoredTexture(32, 32, Color.White);
            }

            // Generate silhouette
            var originalData = new Color[originalTexture.Width * originalTexture.Height];
            originalTexture.GetData(originalData);

            var silhouetteData = new Color[originalData.Length];
            for (int i = 0; i < originalData.Length; i++)
            {
                if (originalData[i].A > 0)
                {
                    silhouetteData[i] = Color.White;
                }
                else
                {
                    silhouetteData[i] = Color.Transparent;
                }
            }

            var silhouetteTexture = new Texture2D(_core.GraphicsDevice, originalTexture.Width, originalTexture.Height);
            silhouetteTexture.SetData(silhouetteData);

            var tuple = (originalTexture, silhouetteTexture);
            _relicSprites[cacheKey] = tuple;
            return tuple;
        }


        public int[] GetEnemySpriteTopPixelOffsets(string archetypeId)
        {
            _enemySpriteTopPixelOffsets.TryGetValue(archetypeId, out var offsets);
            return offsets;
        }

        private void PreCalculateTopPixelOffsets(Texture2D sprite, string archetypeId)
        {
            const int partSize = 64;
            int numParts = sprite.Width / partSize;
            var offsets = new int[numParts];
            var pixelData = new Color[sprite.Width * sprite.Height];
            sprite.GetData(pixelData);

            for (int i = 0; i < numParts; i++)
            {
                int partStartX = i * partSize;
                int topY = -1;

                for (int y = 0; y < partSize; y++)
                {
                    for (int x = 0; x < partSize; x++)
                    {
                        int index = (y * sprite.Width) + (partStartX + x);
                        if (pixelData[index].A > 0)
                        {
                            topY = y;
                            goto FoundTopPixel; // Break out of both loops
                        }
                    }
                }

            FoundTopPixel:
                offsets[i] = topY != -1 ? topY : int.MaxValue; // Use MaxValue for empty parts
            }

            _enemySpriteTopPixelOffsets[archetypeId] = offsets;
        }

        public Texture2D GetStatusEffectIcon(StatusEffectType effectType)
        {
            if (_statusEffectIcons.TryGetValue(effectType, out var cachedIcon))
            {
                return cachedIcon;
            }

            try
            {
                string iconName = effectType.ToString().ToLowerInvariant();
                var icon = _core.Content.Load<Texture2D>($"Sprites/UI/BasicIcons/StatusEffects/{iconName}_status_icon");
                _statusEffectIcons[effectType] = icon;
                return icon;
            }
            catch
            {
                Debug.WriteLine($"[SpriteManager] [WARNING] Could not load status icon for '{effectType}'. Using placeholder.");
                var placeholder = _textureFactory.CreateColoredTexture(5, 5, Color.Magenta);
                _statusEffectIcons[effectType] = placeholder; // Cache the placeholder to avoid repeated load attempts
                return placeholder;
            }
        }

        /// <summary>
        /// Loads all assets related to the main game world, combat, and entities.
        /// </summary>
        public void LoadGameContent()
        {
            try { _waterSprite = _core.Content.Load<Texture2D>("Sprites/water"); }
            catch { _waterSprite = _textureFactory.CreateWaterTexture(); }

            try { _flatlandSprite = _core.Content.Load<Texture2D>("Sprites/flatland"); }
            catch { _flatlandSprite = _textureFactory.CreateColoredTexture(8, 8, Color.White); }

            try { _hillSprite = _core.Content.Load<Texture2D>("Sprites/hill"); }
            catch { _hillSprite = _textureFactory.CreateColoredTexture(8, 8, Color.White); }

            try { _mountainSprite = _core.Content.Load<Texture2D>("Sprites/mountain"); }
            catch { _mountainSprite = _textureFactory.CreateColoredTexture(8, 8, Color.White); }

            try { _peakSprite = _core.Content.Load<Texture2D>("Sprites/peak"); }
            catch { _peakSprite = _textureFactory.CreateColoredTexture(8, 8, Color.White); }

            try { _playerSprite = _core.Content.Load<Texture2D>("Sprites/player"); }
            catch { _playerSprite = _textureFactory.CreatePlayerTexture(); }

            try { _pathSprite = _core.Content.Load<Texture2D>("Sprites/path"); }
            catch { _pathSprite = _textureFactory.CreatePathTexture(); }

            try { _runPathSprite = _core.Content.Load<Texture2D>("Sprites/runPathEnd"); }
            catch { _runPathSprite = _textureFactory.CreateRunPathTexture(); }

            try { _pathEndSprite = _core.Content.Load<Texture2D>("Sprites/pathEnd"); }
            catch { _pathEndSprite = _textureFactory.CreatePathEndTexture(); }

            try { _emptySprite = _textureFactory.CreateEmptyTexture(); }
            catch { _emptySprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _speedMarkSprite = _core.Content.Load<Texture2D>("Sprites/speedMark"); }
            catch { _speedMarkSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _fogOfWarSprite = _core.Content.Load<Texture2D>("Sprites/UI/GameMap/ui_map_fog_of_war"); }
            catch { _fogOfWarSprite = _textureFactory.CreateColoredTexture(5, 5, Color.Black); }
        }

        [Obsolete("LoadSpriteContent is deprecated, please use LoadEssentialContent and LoadGameContent instead.")]
        public void LoadSpriteContent()
        {
            LoadEssentialContent();
            LoadGameContent();
        }
    }
}