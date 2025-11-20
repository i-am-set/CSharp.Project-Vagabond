#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
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
        public Rectangle[] ActionButtonSourceRects { get; private set; }
        public Dictionary<int, Rectangle> ElementIconSourceRects { get; private set; } = new Dictionary<int, Rectangle>();
        public Rectangle[] ActionIconSourceRects { get; private set; }
        public Dictionary<int, Rectangle> RarityBackgroundSourceRects { get; private set; } = new Dictionary<int, Rectangle>();
        public Dictionary<int, Rectangle> SpellUsesSourceRects { get; private set; } = new Dictionary<int, Rectangle>();
        public Rectangle[] SplitMapInventoryButtonSourceRects { get; private set; }
        public Rectangle[] SplitMapCloseInventoryButtonSourceRects { get; private set; }
        public Rectangle[] InventoryHeaderButtonSourceRects { get; private set; }
        public Rectangle[] InventorySlotSourceRects { get; private set; }
        public Rectangle[] InventoryLeftArrowButtonSourceRects { get; private set; }
        public Rectangle[] InventoryRightArrowButtonSourceRects { get; private set; }


        // Enemy Sprite Cache
        private readonly Dictionary<string, (Texture2D Original, Texture2D Silhouette, bool IsMajor)> _enemySprites = new Dictionary<string, (Texture2D, Texture2D, bool IsMajor)>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int[]> _enemySpriteTopPixelOffsets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int[]> _enemySpriteLeftPixelOffsets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int[]> _enemySpriteRightPixelOffsets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int[]> _enemySpriteBottomPixelOffsets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<StatusEffectType, Texture2D> _statusEffectIcons = new Dictionary<StatusEffectType, Texture2D>();

        // General Item/Relic Sprite Cache
        private readonly Dictionary<string, (Texture2D Original, Texture2D Silhouette)> _itemSprites = new Dictionary<string, (Texture2D, Texture2D)>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, (Texture2D Texture, Rectangle[] Frames)> _cursorSprites = new Dictionary<string, (Texture2D, Rectangle[])>();


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
        public Texture2D PlayerHeartSpriteSheet { get; private set; }
        public Texture2D PlayerHeartSpriteSheetSilhouette { get; private set; }

        // Split Map Node Sprites
        public Texture2D SplitNodeStart { get; private set; }
        public Texture2D SplitNodeNarrative { get; private set; }
        public Texture2D SplitNodeReward { get; private set; }
        public Texture2D SplitNodeBoss { get; private set; }
        public Texture2D CombatNodeNormalSprite { get; private set; }
        public Texture2D CombatNodePlayerSprite { get; private set; }
        public Texture2D CombatNodeEasySprite { get; private set; }
        public Texture2D CombatNodeHardSprite { get; private set; }
        public Texture2D SplitNodeCastle { get; private set; }
        public Texture2D SplitNodeChurch { get; private set; }
        public Texture2D SplitNodeFarm { get; private set; }
        public Texture2D SplitNodeHouse { get; private set; }
        public Texture2D SplitNodeHouse2 { get; private set; }
        public Texture2D SplitNodeHouse3 { get; private set; }
        public Texture2D SplitNodeTower { get; private set; }
        public Texture2D SplitNodeTower2 { get; private set; }
        public Texture2D SplitNodeTower3 { get; private set; }
        public Texture2D SplitNodeTown { get; private set; }
        public Texture2D SplitNodeTown2 { get; private set; }
        public Texture2D SplitMapInventoryButton { get; private set; }
        public Texture2D SplitMapHeaderBorder { get; private set; }
        public Texture2D SplitMapCloseInventoryButton { get; private set; }
        public Texture2D InventoryBorderArmor { get; private set; }
        public Texture2D InventoryBorderHeader { get; private set; }
        public Texture2D InventoryBorderRelics { get; private set; }
        public Texture2D InventoryBorderSpells { get; private set; }
        public Texture2D InventoryBorderWeapons { get; private set; }
        public Texture2D InventoryBorderConsumables { get; private set; }
        public Texture2D InventoryHeaderButtonWeapons { get; private set; }
        public Texture2D InventoryHeaderButtonArmor { get; private set; }
        public Texture2D InventoryHeaderButtonSpells { get; private set; }
        public Texture2D InventoryHeaderButtonRelics { get; private set; }
        public Texture2D InventoryHeaderButtonConsumables { get; private set; }
        public Texture2D InventorySlotIdleSpriteSheet { get; private set; }
        public Texture2D InventorySlotHoverSprite { get; private set; }
        public Texture2D InventorySlotSelectedSprite { get; private set; }
        public Texture2D InventoryLeftArrowButton { get; private set; }
        public Texture2D InventoryRightArrowButton { get; private set; }


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
            catch { ActionButtonsSpriteSheet = _textureFactory.CreateColoredTexture(192, 129, Color.Magenta); }

            try { ActionButtonTemplateSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_button_template_spritesheet"); }
            catch { ActionButtonTemplateSpriteSheet = _textureFactory.CreateColoredTexture(1099, 17, Color.Magenta); }

            try { ActionButtonTemplateSecondarySprite = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_button_template_secondary"); }
            catch { ActionButtonTemplateSecondarySprite = _textureFactory.CreateColoredTexture(60, 13, Color.Magenta); }

            try
            {
                ActionMovesBackgroundSprite = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_moves_button_area_background");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SpriteManager] [ERROR] Failed to load 'ui_action_moves_button_area_background'.");
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
            try { SplitNodeStart = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Start"); }
            catch { SplitNodeStart = _textureFactory.CreateColoredTexture(64, 32, Color.Green); }
            try { SplitNodeNarrative = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Event"); }
            catch { SplitNodeNarrative = _textureFactory.CreateColoredTexture(64, 32, Color.Blue); }
            try { SplitNodeReward = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Treasure"); }
            catch { SplitNodeReward = _textureFactory.CreateColoredTexture(64, 32, Color.Gold); }
            try { SplitNodeBoss = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Boss"); }
            catch { SplitNodeBoss = _textureFactory.CreateColoredTexture(64, 32, Color.Purple); }
            try { CombatNodeNormalSprite = _core.Content.Load<Texture2D>("Sprites/MapNodes/CombatNode_Normal"); }
            catch { CombatNodeNormalSprite = _textureFactory.CreateColoredTexture(64, 32, Color.Gray); }
            try { CombatNodePlayerSprite = _core.Content.Load<Texture2D>("Sprites/MapNodes/Combat_Node_Player"); }
            catch { CombatNodePlayerSprite = _textureFactory.CreateColoredTexture(64, 32, Color.Cyan); }
            try { CombatNodeEasySprite = _core.Content.Load<Texture2D>("Sprites/MapNodes/CombatNode_Easy"); }
            catch { CombatNodeEasySprite = _textureFactory.CreateColoredTexture(64, 32, Color.Green); }
            try { CombatNodeHardSprite = _core.Content.Load<Texture2D>("Sprites/MapNodes/CombatNode_Hard"); }
            catch { CombatNodeHardSprite = _textureFactory.CreateColoredTexture(64, 32, Color.DarkRed); }
            try { SplitNodeCastle = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Castle"); }
            catch { SplitNodeCastle = _textureFactory.CreateColoredTexture(64, 32, Color.SlateGray); }
            try { SplitNodeChurch = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Church"); }
            catch { SplitNodeChurch = _textureFactory.CreateColoredTexture(64, 32, Color.LightGoldenrodYellow); }
            try { SplitNodeFarm = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Farm"); }
            catch { SplitNodeFarm = _textureFactory.CreateColoredTexture(64, 32, Color.SaddleBrown); }
            try { SplitNodeHouse = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_House"); }
            catch { SplitNodeHouse = _textureFactory.CreateColoredTexture(64, 32, Color.BurlyWood); }
            try { SplitNodeHouse2 = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_House2"); }
            catch { SplitNodeHouse2 = _textureFactory.CreateColoredTexture(64, 32, Color.RosyBrown); }
            try { SplitNodeHouse3 = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_House3"); }
            catch { SplitNodeHouse3 = _textureFactory.CreateColoredTexture(64, 32, Color.SandyBrown); }
            try { SplitNodeTower = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Tower"); }
            catch { SplitNodeTower = _textureFactory.CreateColoredTexture(64, 32, Color.DarkGray); }
            try { SplitNodeTower2 = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Tower2"); }
            catch { SplitNodeTower2 = _textureFactory.CreateColoredTexture(64, 32, Color.DimGray); }
            try { SplitNodeTower3 = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Tower3"); }
            catch { SplitNodeTower3 = _textureFactory.CreateColoredTexture(64, 32, Color.SlateBlue); }
            try { SplitNodeTown = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Town"); }
            catch { SplitNodeTown = _textureFactory.CreateColoredTexture(64, 32, Color.IndianRed); }
            try { SplitNodeTown2 = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Town2"); }
            catch { SplitNodeTown2 = _textureFactory.CreateColoredTexture(64, 32, Color.Maroon); }
            try { SplitMapInventoryButton = _core.Content.Load<Texture2D>("Sprites/UI/SplitMap/SplitMap_Inventory_Button"); }
            catch { SplitMapInventoryButton = _textureFactory.CreateColoredTexture(32, 16, Color.Magenta); }
            try { SplitMapHeaderBorder = _core.Content.Load<Texture2D>("Sprites/UI/SplitMap/SplitMap_Header_Border"); }
            catch { SplitMapHeaderBorder = _textureFactory.CreateColoredTexture(320, 28, Color.Magenta); }
            try { SplitMapCloseInventoryButton = _core.Content.Load<Texture2D>("Sprites/UI/SplitMap/SplitMap_Close_Inventory_Button"); }
            catch { SplitMapCloseInventoryButton = _textureFactory.CreateColoredTexture(32, 16, Color.Magenta); }
            try { InventoryBorderArmor = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_armor"); }
            catch { InventoryBorderArmor = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { InventoryBorderHeader = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_header"); }
            catch { InventoryBorderHeader = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { InventoryBorderRelics = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_relics"); }
            catch { InventoryBorderRelics = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { InventoryBorderSpells = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_spells"); }
            catch { InventoryBorderSpells = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { InventoryBorderWeapons = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_weapons"); }
            catch { InventoryBorderWeapons = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { InventoryBorderConsumables = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_consumables"); }
            catch { InventoryBorderConsumables = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { InventoryHeaderButtonWeapons = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_header_button_weapons"); }
            catch { InventoryHeaderButtonWeapons = _textureFactory.CreateColoredTexture(96, 32, Color.Magenta); }
            try { InventoryHeaderButtonArmor = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_header_button_armor"); }
            catch { InventoryHeaderButtonArmor = _textureFactory.CreateColoredTexture(96, 32, Color.Magenta); }
            try { InventoryHeaderButtonSpells = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_header_button_spells"); }
            catch { InventoryHeaderButtonSpells = _textureFactory.CreateColoredTexture(96, 32, Color.Magenta); }
            try { InventoryHeaderButtonRelics = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_header_button_relics"); }
            catch { InventoryHeaderButtonRelics = _textureFactory.CreateColoredTexture(96, 32, Color.Magenta); }
            try { InventoryHeaderButtonConsumables = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_header_button_consumables"); }
            catch { InventoryHeaderButtonConsumables = _textureFactory.CreateColoredTexture(96, 32, Color.Magenta); }
            try { InventorySlotIdleSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_slot_idle"); }
            catch { InventorySlotIdleSpriteSheet = _textureFactory.CreateColoredTexture(48, 48, Color.Magenta); }
            try { InventorySlotHoverSprite = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_slot_hover"); }
            catch { InventorySlotHoverSprite = _textureFactory.CreateColoredTexture(48, 48, Color.Magenta); }
            try { InventorySlotSelectedSprite = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_slot_selected"); }
            catch { InventorySlotSelectedSprite = _textureFactory.CreateColoredTexture(48, 48, Color.Gold); }
            try { InventoryLeftArrowButton = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_left_arrow_button"); }
            catch { InventoryLeftArrowButton = _textureFactory.CreateColoredTexture(10, 5, Color.Magenta); }
            try { InventoryRightArrowButton = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_right_arrow_button"); }
            catch { InventoryRightArrowButton = _textureFactory.CreateColoredTexture(10, 5, Color.Magenta); }


            LoadAndCacheCursorSprite("cursor_default");
            LoadAndCacheCursorSprite("cursor_hover_clickable");
            LoadAndCacheCursorSprite("cursor_dragging_draggable");

            InitializeArrowSourceRects();
            InitializeActionButtonsSourceRects();
            InitializeElementIconsSourceRects();
            InitializeActionIconsSourceRects();
            InitializeRarityBackgrounds();
            InitializeSpellUsesRects();
            InitializeSplitMapInventoryButtonRects();
            InitializeSplitMapCloseInventoryButtonRects();
            InitializeInventoryHeaderButtonRects();
            InitializeInventorySlotRects();
            InitializeInventoryArrowButtonRects();
        }

        private void InitializeInventoryArrowButtonRects()
        {
            const int frameWidth = 5;
            const int frameHeight = 5;

            InventoryLeftArrowButtonSourceRects = new Rectangle[2];
            InventoryLeftArrowButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight); // Idle
            InventoryLeftArrowButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight); // Hover

            InventoryRightArrowButtonSourceRects = new Rectangle[2];
            InventoryRightArrowButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight); // Idle
            InventoryRightArrowButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight); // Hover
        }

        private void InitializeInventorySlotRects()
        {
            if (InventorySlotIdleSpriteSheet == null) return;
            const int frameSize = 48; // Frames are 48x48
            int frameCount = InventorySlotIdleSpriteSheet.Width / frameSize;
            InventorySlotSourceRects = new Rectangle[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                InventorySlotSourceRects[i] = new Rectangle(i * frameSize, 0, frameSize, frameSize);
            }
        }

        private void InitializeInventoryHeaderButtonRects()
        {
            InventoryHeaderButtonSourceRects = new Rectangle[3];
            const int frameWidth = 32;
            const int frameHeight = 32;
            // Frame 0: Default/Unselected
            InventoryHeaderButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            // Frame 1: Hover
            InventoryHeaderButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
            // Frame 2: Selected
            InventoryHeaderButtonSourceRects[2] = new Rectangle(frameWidth * 2, 0, frameWidth, frameHeight);
        }

        private void InitializeSplitMapInventoryButtonRects()
        {
            SplitMapInventoryButtonSourceRects = new Rectangle[2];
            const int frameWidth = 16;
            const int frameHeight = 16;
            // Frame 0: Idle
            SplitMapInventoryButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            // Frame 1: Hover
            SplitMapInventoryButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
        }

        private void InitializeSplitMapCloseInventoryButtonRects()
        {
            SplitMapCloseInventoryButtonSourceRects = new Rectangle[2];
            const int frameWidth = 16;
            const int frameHeight = 16;
            // Frame 0: Idle
            SplitMapCloseInventoryButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            // Frame 1: Hover
            SplitMapCloseInventoryButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
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
            // The new sheet is a 2x3 grid (2 columns: Normal, Hover; 3 rows: Act, Item, Run)
            // Each sprite is 96x43 pixels.
            ActionButtonSourceRects = new Rectangle[6];
            int spriteWidth = 192 / 2;
            int spriteHeight = 129 / 3;

            for (int i = 0; i < 3; i++) // 3 rows for Act, Item, Run
            {
                // Normal state (column 0)
                ActionButtonSourceRects[i * 2] = new Rectangle(0, i * spriteHeight, spriteWidth, spriteHeight);
                // Hover state (column 1)
                ActionButtonSourceRects[i * 2 + 1] = new Rectangle(spriteWidth, i * spriteHeight, spriteWidth, spriteHeight);
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
                return cachedSprite.Original;
            }

            Texture2D sprite = null;
            bool isMajor = false;

            // Try loading major sprite first
            try
            {
                sprite = _core.Content.Load<Texture2D>($"Sprites/Enemies/Major/{archetypeId.ToLower()}");
                isMajor = true;
            }
            catch
            {
                // Major sprite not found, fall back to normal
                try
                {
                    sprite = _core.Content.Load<Texture2D>($"Sprites/Enemies/{archetypeId.ToLower()}");
                    isMajor = false;
                }
                catch
                {
                    // Neither found, cache null
                    _enemySprites[archetypeId] = (null, null, false);
                    return null;
                }
            }

            // If a sprite was loaded (either major or normal)
            var silhouette = CreateSilhouette(sprite);
            _enemySprites[archetypeId] = (sprite, silhouette, isMajor);
            PreCalculateSpriteBounds(sprite, archetypeId);
            return sprite;
        }

        public Texture2D GetEnemySpriteSilhouette(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId)) return null;

            if (_enemySprites.TryGetValue(archetypeId, out var cachedSprite))
            {
                return cachedSprite.Silhouette;
            }

            // This will load and cache both original and silhouette
            GetEnemySprite(archetypeId);

            if (_enemySprites.TryGetValue(archetypeId, out var newlyCachedSprite))
            {
                return newlyCachedSprite.Silhouette;
            }

            return null;
        }

        public bool IsMajorEnemySprite(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId)) return false;
            // Ensure it's loaded if not already
            GetEnemySprite(archetypeId);
            if (_enemySprites.TryGetValue(archetypeId, out var cachedSprite))
            {
                return cachedSprite.IsMajor;
            }
            return false;
        }

        private Texture2D CreateSilhouette(Texture2D source)
        {
            var graphicsDevice = _core.GraphicsDevice;
            var data = new Color[source.Width * source.Height];
            source.GetData(data);

            var silhouetteData = new Color[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].A > 0)
                {
                    silhouetteData[i] = Color.White;
                }
                else
                {
                    silhouetteData[i] = Color.Transparent;
                }
            }

            var silhouetteTexture = new Texture2D(graphicsDevice, source.Width, source.Height);
            silhouetteTexture.SetData(silhouetteData);
            return silhouetteTexture;
        }

        public Texture2D GetRelicSprite(string imagePath)
        {
            return GetItemSprite(imagePath);
        }

        public Texture2D GetRelicSpriteSilhouette(string imagePath)
        {
            return GetItemSpriteSilhouette(imagePath);
        }

        public Texture2D GetItemSpriteSilhouette(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                LoadAndCacheItem("placeholder", null);
                return _itemSprites["placeholder"].Silhouette;
            }

            if (_itemSprites.TryGetValue(imagePath, out var cachedSprite))
            {
                return cachedSprite.Silhouette;
            }

            return LoadAndCacheItem(imagePath, imagePath).Silhouette;
        }

        public Texture2D GetItemSprite(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                LoadAndCacheItem("placeholder", null);
                return _itemSprites["placeholder"].Original;
            }

            if (_itemSprites.TryGetValue(imagePath, out var cachedSprite))
            {
                return cachedSprite.Original;
            }

            return LoadAndCacheItem(imagePath, imagePath).Original;
        }

        private (Texture2D Original, Texture2D Silhouette) LoadAndCacheItem(string cacheKey, string? imagePath)
        {
            if (_itemSprites.TryGetValue(cacheKey, out var cachedTuple))
            {
                return cachedTuple;
            }

            Debug.WriteLine($"[SpriteManager] LoadAndCacheItem called for: '{imagePath}'");

            Texture2D originalTexture;
            try
            {
                if (imagePath != null)
                {
                    originalTexture = _core.Content.Load<Texture2D>(imagePath);
                }
                else
                {
                    originalTexture = _textureFactory.CreateColoredTexture(32, 32, Color.Magenta);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpriteManager] [ERROR] FAILED to load: '{imagePath}'. Exception: {ex.Message}");
                Debug.WriteLine($"[SpriteManager] Content Root Directory: '{_core.Content.RootDirectory}'");
                originalTexture = _textureFactory.CreateColoredTexture(32, 32, Color.Magenta);
            }

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
            _itemSprites[cacheKey] = tuple;
            return tuple;
        }


        public int[] GetEnemySpriteTopPixelOffsets(string archetypeId)
        {
            _enemySpriteTopPixelOffsets.TryGetValue(archetypeId, out var offsets);
            return offsets;
        }

        public int[] GetEnemySpriteLeftPixelOffsets(string archetypeId)
        {
            _enemySpriteLeftPixelOffsets.TryGetValue(archetypeId, out var offsets);
            return offsets;
        }

        public int[] GetEnemySpriteRightPixelOffsets(string archetypeId)
        {
            _enemySpriteRightPixelOffsets.TryGetValue(archetypeId, out var offsets);
            return offsets;
        }

        public int[] GetEnemySpriteBottomPixelOffsets(string archetypeId)
        {
            _enemySpriteBottomPixelOffsets.TryGetValue(archetypeId, out var offsets);
            return offsets;
        }

        private void PreCalculateSpriteBounds(Texture2D sprite, string archetypeId)
        {
            bool isMajor = _enemySprites[archetypeId].IsMajor;
            int partSize = isMajor ? 96 : 64;

            int numParts = sprite.Width / partSize;
            var topOffsets = new int[numParts];
            var leftOffsets = new int[numParts];
            var rightOffsets = new int[numParts];
            var bottomOffsets = new int[numParts];
            var pixelData = new Color[sprite.Width * sprite.Height];
            sprite.GetData(pixelData);

            for (int i = 0; i < numParts; i++)
            {
                int partStartX = i * partSize;
                int topY = -1, leftX = -1, rightX = -1, bottomY = -1;

                // Find Top
                for (int y = 0; y < partSize; y++) { for (int x = 0; x < partSize; x++) { if (pixelData[(y * sprite.Width) + (partStartX + x)].A > 0) { topY = y; goto FoundTopPixel; } } }
            FoundTopPixel: topOffsets[i] = topY != -1 ? topY : int.MaxValue;

                // Find Left
                for (int x = 0; x < partSize; x++) { for (int y = 0; y < partSize; y++) { if (pixelData[(y * sprite.Width) + (partStartX + x)].A > 0) { leftX = x; goto FoundLeftPixel; } } }
            FoundLeftPixel: leftOffsets[i] = leftX != -1 ? leftX : int.MaxValue;

                // Find Right
                for (int x = partSize - 1; x >= 0; x--) { for (int y = 0; y < partSize; y++) { if (pixelData[(y * sprite.Width) + (partStartX + x)].A > 0) { rightX = x; goto FoundRightPixel; } } }
            FoundRightPixel: rightOffsets[i] = rightX;

                // Find Bottom
                for (int y = partSize - 1; y >= 0; y--) { for (int x = 0; x < partSize; x++) { if (pixelData[(y * sprite.Width) + (partStartX + x)].A > 0) { bottomY = y; goto FoundBottomPixel; } } }
            FoundBottomPixel: bottomOffsets[i] = bottomY;
            }

            _enemySpriteTopPixelOffsets[archetypeId] = topOffsets;
            _enemySpriteLeftPixelOffsets[archetypeId] = leftOffsets;
            _enemySpriteRightPixelOffsets[archetypeId] = rightOffsets;
            _enemySpriteBottomPixelOffsets[archetypeId] = bottomOffsets;
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

        private void LoadAndCacheCursorSprite(string assetName)
        {
            if (_cursorSprites.ContainsKey(assetName)) return;

            try
            {
                var texture = _core.Content.Load<Texture2D>($"Sprites/UI/Cursor/{assetName}");
                const int frameSize = 16;
                if (texture.Height != frameSize)
                {
                    Debug.WriteLine($"[SpriteManager] [WARNING] Cursor sprite '{assetName}' has an incorrect height. Expected {frameSize}, but got {texture.Height}.");
                }

                int frameCount = texture.Width / frameSize;
                var frames = new Rectangle[frameCount];
                for (int i = 0; i < frameCount; i++)
                {
                    frames[i] = new Rectangle(i * frameSize, 0, frameSize, frameSize);
                }
                _cursorSprites[assetName] = (texture, frames);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpriteManager] [ERROR] Failed to load cursor '{assetName}': {ex.Message}. Using placeholder.");
                var placeholder = _textureFactory.CreateColoredTexture(16, 16, Color.Magenta);
                _cursorSprites[assetName] = (placeholder, new[] { new Rectangle(0, 0, 16, 16) });
            }
        }

        public (Texture2D Texture, Rectangle[] Frames) GetCursorAnimation(string assetName)
        {
            if (!_cursorSprites.ContainsKey(assetName))
            {
                LoadAndCacheCursorSprite(assetName);
            }
            return _cursorSprites[assetName];
        }

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

            try
            {
                PlayerHeartSpriteSheet = _core.Content.Load<Texture2D>("Sprites/Player/player_heart_spritesheet");
                PlayerHeartSpriteSheetSilhouette = CreateSilhouette(PlayerHeartSpriteSheet);
            }
            catch
            {
                PlayerHeartSpriteSheet = _textureFactory.CreateColoredTexture(32, 32, Color.DeepPink);
                PlayerHeartSpriteSheetSilhouette = _textureFactory.CreateColoredTexture(32, 32, Color.White);
            }
        }

        [Obsolete("LoadSpriteContent is deprecated, please use LoadEssentialContent and LoadGameContent instead.")]
        public void LoadSpriteContent()
        {
            LoadEssentialContent();
            LoadGameContent();
        }
    }
}