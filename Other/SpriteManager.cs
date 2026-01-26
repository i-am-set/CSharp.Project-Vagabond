using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Battle;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond
{
public enum PlayerSpriteType
    {
        Portrait5x5 = 0,
        Portrait8x8 = 1,
        Normal = 2,
        Alt = 3,
        Sleep = 4
    }
    public class SpriteManager
    {
        private readonly Core _core;
        private readonly TextureFactory _textureFactory;

        public Texture2D ActionButtonsSpriteSheet { get; private set; }
        public Texture2D ActionButtonTemplateSpriteSheet { get; private set; }
        public Texture2D ActionMovesBackgroundSprite { get; private set; }
        public Texture2D ActionTooltipBackgroundSprite { get; private set; }
        public Texture2D ActionIconsSpriteSheet { get; private set; }
        public Texture2D ActionButtonUsesSpriteSheet { get; private set; }
        public Texture2D StatChangeIconsSpriteSheet { get; private set; }
        public Texture2D StatChangeIconsSpriteSheetSilhouette { get; private set; }

        // NEW: Mini Action Button
        public Texture2D MiniActionButtonSprite { get; private set; }

        public Texture2D ItemRelicsSpriteSheet { get; private set; }
        public Texture2D ItemWeaponsSpriteSheet { get; private set; }
        public Texture2D BattleBorderMain { get; private set; }
        public Texture2D BattleBorderMain2 { get; private set; }
        public Texture2D BattleBorderCombat { get; private set; }
        public Texture2D BattleBorderAction { get; private set; }
        public Texture2D BattleBorderItem { get; private set; }
        public Texture2D BattleBorderTarget { get; private set; }
        public Texture2D BattleBorderSwitch { get; private set; }
        public Texture2D PlayerMasterSpriteSheet { get; private set; }
        public Texture2D PlayerMasterSpriteSheetSilhouette { get; private set; }
        public Texture2D InventoryPlayerHealthBarEmpty { get; private set; }
        public Texture2D InventoryPlayerHealthBarDisabled { get; private set; }
        public Texture2D InventoryPlayerHealthBarFull { get; private set; }
        public Texture2D InventoryPlayerHealthBarOverlay { get; private set; }
        public Texture2D InventoryStatBarEmpty { get; private set; }
        public Texture2D InventoryStatBarDisabled { get; private set; }
        public Texture2D InventoryStatBarFull { get; private set; }
        public Texture2D InventorySpellSlotButtonSpriteSheet { get; private set; }
        public Texture2D ManaBarPattern { get; private set; }
        public Texture2D TenacityPipTexture { get; private set; }

        public Rectangle[] ActionButtonSourceRects { get; private set; }
        public Rectangle[] ActionIconSourceRects { get; private set; }
        public Dictionary<int, Rectangle> SpellUsesSourceRects { get; private set; } = new Dictionary<int, Rectangle>();
        public Rectangle[] SplitMapInventoryButtonSourceRects { get; private set; }
        public Rectangle[] SplitMapCloseInventoryButtonSourceRects { get; private set; }
        public Rectangle[] SplitMapSettingsButtonSourceRects { get; private set; }
        public Rectangle[] InventoryHeaderButtonSourceRects { get; private set; }
        public Rectangle[] InventoryLeftArrowButtonSourceRects { get; private set; }
        public Rectangle[] InventoryRightArrowButtonSourceRects { get; private set; }
        public Rectangle[] InventoryScrollArrowRects { get; private set; }
        public Rectangle[] InventorySpellSlotButtonSourceRects { get; private set; }
        public Rectangle[] TargetingButtonSourceRects { get; private set; }
        public Rectangle[] StatChangeIconSourceRects { get; private set; }

        private readonly Dictionary<string, (Texture2D Original, Texture2D Silhouette, bool IsMajor)> _enemySprites = new Dictionary<string, (Texture2D, Texture2D, bool IsMajor)>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int[]> _enemySpriteTopPixelOffsets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int[]> _enemySpriteLeftPixelOffsets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int[]> _enemySpriteRightPixelOffsets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int[]> _enemySpriteBottomPixelOffsets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Vector2> _visualCenterOffsets = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<StatusEffectType, Texture2D> _statusEffectIcons = new Dictionary<StatusEffectType, Texture2D>();
        private readonly Dictionary<string, (Texture2D Original, Texture2D Silhouette)> _itemSprites = new Dictionary<string, (Texture2D, Texture2D)>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (Texture2D Original, Texture2D Silhouette)> _smallItemSprites = new Dictionary<string, (Texture2D, Texture2D)>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (Texture2D Texture, Rectangle[] Frames)> _cursorSprites = new Dictionary<string, (Texture2D, Rectangle[])>();

        private Texture2D _logoSprite;
        private Texture2D _playerSprite;
        private Texture2D _emptySprite;
        private Texture2D _speedMarkSprite;
        private Texture2D _mapMarkerSprite;
        private Texture2D _circleTextureSprite;
        private Texture2D _ringTextureSprite;
        private Texture2D _settingsIconSprite;
        private Texture2D _turnIndicatorSprite;
        private Texture2D _circleParticleSprite;
        private Texture2D _emberParticleSprite;
        private Texture2D _softParticleSprite;
        public Effect FireballParticleShaderEffect { get; private set; }
        public Texture2D ArrowIconSpriteSheet { get; private set; }
        public Rectangle[] ArrowIconSourceRects { get; private set; }
        public Texture2D SpellbookPageSprite { get; private set; }
        public Texture2D SpellbookClosedSprite { get; private set; }
        public Texture2D PlayerHeartSpriteSheet { get; private set; }
        public Texture2D PlayerHeartSpriteSheetSilhouette { get; private set; }
        public Texture2D BattleEnemyFloorSprite { get; private set; }
        public Texture2D BattlePlayerFloorSprite { get; private set; }
        public Texture2D HealParticleSprite { get; private set; }

        public Texture2D SplitNodeStart { get; private set; }
        public Texture2D SplitNodeStartSilhouette { get; private set; }
        public Texture2D SplitNodeNarrative { get; private set; }
        public Texture2D SplitNodeNarrativeSilhouette { get; private set; }
        public Texture2D SplitNodeCombat { get; private set; }
        public Texture2D SplitNodeCombatSilhouette { get; private set; }
        public Texture2D SplitNodeRecruit { get; private set; }
        public Texture2D SplitNodeRecruitSilhouette { get; private set; }
        public Texture2D SplitNodeRest { get; private set; }
        public Texture2D SplitNodeRestSilhouette { get; private set; }
        public Texture2D SplitNodeShop { get; private set; }
        public Texture2D SplitNodeShopSilhouette { get; private set; }
        public Texture2D MapNodePlayerSprite { get; private set; }
        public Texture2D MapNodePlayerSpriteSilhouette { get; private set; }

        public Texture2D SplitMapInventoryButton { get; private set; }
        public Texture2D SplitMapHeaderBorder { get; private set; }
        public Texture2D SplitMapCloseInventoryButton { get; private set; }
        public Texture2D SplitMapSettingsButton { get; private set; }
        public Texture2D InventoryBorderHeader { get; private set; }
        public Texture2D InventoryBorderRelics { get; private set; }
        public Texture2D InventoryBorderWeapons { get; private set; }
        public Texture2D InventoryBorderEquip { get; private set; }
        public Texture2D InventoryBorderEquipSubmenu { get; private set; }
        public Texture2D InventoryBorderEquipInfoPanelLeft { get; private set; }
        public Texture2D InventoryBorderEquipInfoPanelRight { get; private set; }
        public Texture2D InventoryHeaderButtonWeapons { get; private set; }
        public Texture2D InventoryHeaderButtonRelics { get; private set; }
        public Texture2D InventoryHeaderButtonEquip { get; private set; }
        public Texture2D InventorySlotHoverSprite { get; private set; }
        public Texture2D InventorySlotSelectedSprite { get; private set; }
        public Texture2D InventoryLeftArrowButton { get; private set; }
        public Texture2D InventoryRightArrowButton { get; private set; }
        public Texture2D EquipSlotButtonSprite { get; private set; }
        public Texture2D InventoryEquipHoverSprite { get; private set; }
        public Texture2D InventoryEquipSelectedSprite { get; private set; }
        public Texture2D InventoryScrollArrowsSprite { get; private set; }
        public Texture2D InventoryEmptySlotSprite { get; private set; }
        public Texture2D InventorySlotEquipIconSprite { get; private set; }
        public Texture2D TargetingIndicatorSprite { get; private set; }
        public Texture2D ShopBorderMain { get; private set; }
        public Texture2D ShopXIcon { get; private set; }
        public Texture2D RestBorderMain { get; private set; }
        public Texture2D RestActionIconsSpriteSheet { get; private set; }
        public Texture2D TargetingButtonSpriteSheet { get; private set; }

        public Texture2D NoiseTexture { get; private set; }

        public Texture2D MousePromptBlank { get; private set; }
        public Texture2D MousePromptBlankSilhouette { get; private set; }
        public Texture2D MousePromptLeftClick { get; private set; }
        public Texture2D MousePromptLeftClickSilhouette { get; private set; }
        public Texture2D MousePromptRightClick { get; private set; }
        public Texture2D MousePromptRightClickSilhouette { get; private set; }
        public Texture2D MousePromptMiddleClick { get; private set; }
        public Texture2D MousePromptMiddleClickSilhouette { get; private set; }
        public Texture2D MousePromptDisabled { get; private set; }
        public Texture2D MousePromptDisabledSilhouette { get; private set; }

        public Texture2D LogoSprite => _logoSprite;
        public Texture2D PlayerSprite => _playerSprite;
        public Texture2D EmptySprite => _emptySprite;
        public Texture2D SpeedMarkSprite => _speedMarkSprite;
        public Texture2D MapMarkerSprite => _mapMarkerSprite;
        public Texture2D CircleTextureSprite => _circleTextureSprite;
        public Texture2D RingTextureSprite => _ringTextureSprite;
        public Texture2D SettingsIconSprite => _settingsIconSprite;
        public Texture2D TurnIndicatorSprite => _turnIndicatorSprite;
        public Texture2D CircleParticleSprite => _circleParticleSprite;
        public Texture2D EmberParticleSprite => _emberParticleSprite;
        public Texture2D SoftParticleSprite => _softParticleSprite;

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

            try { _ringTextureSprite = _textureFactory.CreateRingTexture(); }
            catch { _ringTextureSprite = _textureFactory.CreateColoredTexture(16, 16, Color.White); }

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

            try { BattleEnemyFloorSprite = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/battle_enemy_floor"); }
            catch { BattleEnemyFloorSprite = _textureFactory.CreateColoredTexture(128, 128, Color.DarkGray); }

            try { BattlePlayerFloorSprite = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/party_member_enemy_floor"); }
            catch { BattlePlayerFloorSprite = _textureFactory.CreateColoredTexture(128, 128, Color.DarkBlue); }

            try { ArrowIconSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/arrow_icon_spritesheet"); }
            catch { ArrowIconSpriteSheet = _textureFactory.CreateColoredTexture(48, 48, Color.Magenta); }

            try { FireballParticleShaderEffect = _core.Content.Load<Effect>("Shaders/FireballParticleShader"); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] Could not load shader 'Shaders/FireballParticleShader'. Please ensure it's in the Content project. {ex.Message}"); }

            try { ActionButtonsSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_buttons_icon_spritesheet"); }
            catch { ActionButtonsSpriteSheet = _textureFactory.CreateColoredTexture(192, 129, Color.Magenta); }

            try { ActionButtonTemplateSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_button_template_spritesheet"); }
            catch { ActionButtonTemplateSpriteSheet = _textureFactory.CreateColoredTexture(1099, 17, Color.Magenta); }

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

            try { ActionIconsSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/ui_action_icons_spritesheet_9x9"); }
            catch { ActionIconsSpriteSheet = _textureFactory.CreateColoredTexture(36, 9, Color.Magenta); }

            try { ActionButtonUsesSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_action_button_uses_spritesheet"); }
            catch { ActionButtonUsesSpriteSheet = _textureFactory.CreateColoredTexture(471, 17, Color.Magenta); }

            try
            {
                StatChangeIconsSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/stat_change_icons_spritesheet");
                StatChangeIconsSpriteSheetSilhouette = CreateSilhouette(StatChangeIconsSpriteSheet);
            }
            catch
            {
                StatChangeIconsSpriteSheet = _textureFactory.CreateColoredTexture(9, 3, Color.Magenta);
                StatChangeIconsSpriteSheetSilhouette = _textureFactory.CreateColoredTexture(9, 3, Color.White);
            }

            // Load Item Sprite Sheets
            try { ItemRelicsSpriteSheet = _core.Content.Load<Texture2D>("Sprites/Items/item_relics_spritesheet"); }
            catch { ItemRelicsSpriteSheet = _textureFactory.CreateColoredTexture(128, 256, Color.Magenta); }

            try { ItemWeaponsSpriteSheet = _core.Content.Load<Texture2D>("Sprites/Items/item_weapons_spritesheet"); }
            catch { ItemWeaponsSpriteSheet = _textureFactory.CreateColoredTexture(128, 256, Color.Magenta); }


            // Load Battle Borders
            try { BattleBorderMain = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/battle_border_main"); }
            catch { BattleBorderMain = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }

            try { BattleBorderMain2 = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/battle_border_main_2"); }
            catch { BattleBorderMain2 = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }

            try { BattleBorderCombat = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/battle_border_combat"); }
            catch { BattleBorderCombat = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }

            try { BattleBorderAction = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/battle_border_action"); }
            catch { BattleBorderAction = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }

            try { BattleBorderItem = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/battle_border_item"); }
            catch { BattleBorderItem = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }

            try { BattleBorderTarget = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/battle_border_target"); }
            catch { BattleBorderTarget = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }

            try { BattleBorderSwitch = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/battle_border_switch"); }
            catch { BattleBorderSwitch = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }

            // Load Mouse Prompt Sprites
            try { MousePromptBlank = _core.Content.Load<Texture2D>("Sprites/UI/KeyPrompts/mouse/ui_mouse_blank"); }
            catch { MousePromptBlank = _textureFactory.CreateColoredTexture(5, 7, Color.Magenta); }
            MousePromptBlankSilhouette = CreateSilhouette(MousePromptBlank);

            try { MousePromptLeftClick = _core.Content.Load<Texture2D>("Sprites/UI/KeyPrompts/mouse/ui_mouse_left_click"); }
            catch { MousePromptLeftClick = _textureFactory.CreateColoredTexture(5, 7, Color.Magenta); }
            MousePromptLeftClickSilhouette = CreateSilhouette(MousePromptLeftClick);

            try { MousePromptRightClick = _core.Content.Load<Texture2D>("Sprites/UI/KeyPrompts/mouse/ui_mouse_right_click"); }
            catch { MousePromptRightClick = _textureFactory.CreateColoredTexture(5, 7, Color.Magenta); }
            MousePromptRightClickSilhouette = CreateSilhouette(MousePromptRightClick);

            try { MousePromptMiddleClick = _core.Content.Load<Texture2D>("Sprites/UI/KeyPrompts/mouse/ui_mouse_middle_click"); }
            catch { MousePromptMiddleClick = _textureFactory.CreateColoredTexture(5, 7, Color.Magenta); }
            MousePromptMiddleClickSilhouette = CreateSilhouette(MousePromptMiddleClick);

            try { MousePromptDisabled = _core.Content.Load<Texture2D>("Sprites/UI/KeyPrompts/mouse/ui_mouse_disabled"); }
            catch { MousePromptDisabled = _textureFactory.CreateColoredTexture(5, 7, Color.Magenta); }
            MousePromptDisabledSilhouette = CreateSilhouette(MousePromptDisabled);

            try { SpellbookPageSprite = _core.Content.Load<Texture2D>("Sprites/SpellBook/spellbook_page"); }
            catch { SpellbookPageSprite = _textureFactory.CreateColoredTexture(35, 35, Color.Magenta); }

            try { SpellbookClosedSprite = _core.Content.Load<Texture2D>("Sprites/SpellBook/spellbook_closed"); }
            catch { SpellbookClosedSprite = _textureFactory.CreateColoredTexture(64, 64, Color.Magenta); }

            // Load Split Map Node Sprites & Generate Silhouettes
            try { SplitNodeStart = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Start"); }
            catch { SplitNodeStart = _textureFactory.CreateColoredTexture(64, 32, Color.Green); }
            SplitNodeStartSilhouette = CreateSilhouette(SplitNodeStart);

            try { SplitNodeNarrative = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Event"); }
            catch { SplitNodeNarrative = _textureFactory.CreateColoredTexture(64, 32, Color.Blue); }
            SplitNodeNarrativeSilhouette = CreateSilhouette(SplitNodeNarrative);

            try { SplitNodeCombat = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Combat"); }
            catch { SplitNodeCombat = _textureFactory.CreateColoredTexture(64, 32, Color.Red); }
            SplitNodeCombatSilhouette = CreateSilhouette(SplitNodeCombat);

            try { SplitNodeRecruit = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Recuit"); }
            catch { SplitNodeRecruit = _textureFactory.CreateColoredTexture(64, 32, Color.Cyan); }
            SplitNodeRecruitSilhouette = CreateSilhouette(SplitNodeRecruit);

            try { SplitNodeRest = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Rest"); }
            catch { SplitNodeRest = _textureFactory.CreateColoredTexture(64, 32, Color.Green); }
            SplitNodeRestSilhouette = CreateSilhouette(SplitNodeRest);

            try { SplitNodeShop = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Shop"); }
            catch { SplitNodeShop = _textureFactory.CreateColoredTexture(64, 32, Color.Gold); }
            SplitNodeShopSilhouette = CreateSilhouette(SplitNodeShop);

            try { MapNodePlayerSprite = _core.Content.Load<Texture2D>("Sprites/MapNodes/MapNode_Player"); }
            catch { MapNodePlayerSprite = _textureFactory.CreateColoredTexture(64, 32, Color.Cyan); }
            MapNodePlayerSpriteSilhouette = CreateSilhouette(MapNodePlayerSprite);

            try { SplitMapInventoryButton = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/SplitMap_Inventory_Button"); }
            catch { SplitMapInventoryButton = _textureFactory.CreateColoredTexture(32, 16, Color.Magenta); }
            try { SplitMapCloseInventoryButton = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/SplitMap_Close_Inventory_Button"); }
            catch { SplitMapCloseInventoryButton = _textureFactory.CreateColoredTexture(32, 16, Color.Magenta); }
            try { SplitMapSettingsButton = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/SplitMap_Settings_Button"); }
            catch { SplitMapSettingsButton = _textureFactory.CreateColoredTexture(32, 16, Color.Magenta); }
            try { InventoryBorderHeader = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_header"); }
            catch { InventoryBorderHeader = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { InventoryBorderRelics = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_relics"); }
            catch { InventoryBorderRelics = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { InventoryBorderWeapons = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_weapons"); }
            catch { InventoryBorderWeapons = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { InventoryBorderEquip = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_equip"); }
            catch { InventoryBorderEquip = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { InventoryBorderEquipSubmenu = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_equip_submenu"); }
            catch { InventoryBorderEquipSubmenu = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }

            try { InventoryBorderEquipInfoPanelLeft = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_equip_info_panel_left"); }
            catch { InventoryBorderEquipInfoPanelLeft = _textureFactory.CreateColoredTexture(320, 180, Color.DarkBlue); }

            try { InventoryBorderEquipInfoPanelRight = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_border_equip_info_panel_right"); }
            catch { InventoryBorderEquipInfoPanelRight = _textureFactory.CreateColoredTexture(320, 180, Color.DarkBlue); }

            try { InventoryHeaderButtonWeapons = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_header_button_weapons"); }
            catch { InventoryHeaderButtonWeapons = _textureFactory.CreateColoredTexture(96, 32, Color.Magenta); }
            try { InventoryHeaderButtonRelics = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_header_button_relics"); }
            catch { InventoryHeaderButtonRelics = _textureFactory.CreateColoredTexture(96, 32, Color.Magenta); }
            try { InventoryHeaderButtonEquip = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_header_button_equip"); }
            catch { InventoryHeaderButtonEquip = _textureFactory.CreateColoredTexture(96, 32, Color.Magenta); }
            try { InventorySlotHoverSprite = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_slot_hover"); }
            catch { InventorySlotHoverSprite = _textureFactory.CreateColoredTexture(48, 48, Color.Magenta); }
            try { InventorySlotSelectedSprite = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_slot_selected"); }
            catch { InventorySlotSelectedSprite = _textureFactory.CreateColoredTexture(48, 48, Color.Gold); }
            try { InventoryLeftArrowButton = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_left_arrow_button"); }
            catch { InventoryLeftArrowButton = _textureFactory.CreateColoredTexture(10, 5, Color.Magenta); }
            try { InventoryRightArrowButton = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_right_arrow_button"); }
            catch { InventoryRightArrowButton = _textureFactory.CreateColoredTexture(10, 5, Color.Magenta); }
            try { EquipSlotButtonSprite = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/equip_slot_button"); }
            catch { EquipSlotButtonSprite = _textureFactory.CreateColoredTexture(180, 16, Color.HotPink); }
            try { InventoryEquipHoverSprite = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_equip_hover"); }
            catch { InventoryEquipHoverSprite = _textureFactory.CreateColoredTexture(180, 16, Color.HotPink); }
            try { InventoryEquipSelectedSprite = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_equip_selected"); }
            catch { InventoryEquipSelectedSprite = _textureFactory.CreateColoredTexture(180, 16, Color.Gold); }
            try { InventoryScrollArrowsSprite = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_scroll_arrows"); }
            catch { InventoryScrollArrowsSprite = _textureFactory.CreateColoredTexture(10, 5, Color.Magenta); }
            try { InventoryEmptySlotSprite = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_16x16_empty_slot_sprite"); }
            catch { InventoryEmptySlotSprite = _textureFactory.CreateColoredTexture(16, 16, Color.DarkGray); }
            try { InventorySlotEquipIconSprite = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_slot_equip_icon"); }
            catch { InventorySlotEquipIconSprite = _textureFactory.CreateColoredTexture(64, 32, Color.Magenta); }
            try { TargetingIndicatorSprite = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/targeting_indicator"); }
            catch { TargetingIndicatorSprite = _textureFactory.CreateColoredTexture(32, 32, Color.Red); }
            try { ShopBorderMain = _core.Content.Load<Texture2D>("Sprites/UI/Shop/shop_border_main"); }
            catch { ShopBorderMain = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { ShopXIcon = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/X_32x32"); }
            catch { ShopXIcon = _textureFactory.CreateColoredTexture(32, 32, Color.Red); }
            try { RestBorderMain = _core.Content.Load<Texture2D>("Sprites/UI/Rest/rest_border_main"); }
            catch { RestBorderMain = _textureFactory.CreateColoredTexture(320, 180, Color.Magenta); }
            try { RestActionIconsSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/Rest/rest_action_icons"); }
            catch { RestActionIconsSpriteSheet = _textureFactory.CreateColoredTexture(24, 32, Color.Magenta); }
            try { TargetingButtonSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/ui_choose_a_target_button_spritesheet"); }
            catch { TargetingButtonSpriteSheet = _textureFactory.CreateColoredTexture(450, 22, Color.Magenta); }

            // Load Health Bar Sprites
            try { InventoryPlayerHealthBarEmpty = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_player_health_bar_empty"); }
            catch { InventoryPlayerHealthBarEmpty = _textureFactory.CreateColoredTexture(66, 7, Color.DarkGray); }
            try { InventoryPlayerHealthBarDisabled = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_player_health_bar_disabled"); }
            catch { InventoryPlayerHealthBarDisabled = _textureFactory.CreateColoredTexture(66, 7, Color.Black); }
            try { InventoryPlayerHealthBarFull = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_player_health_bar_full"); }
            catch { InventoryPlayerHealthBarFull = _textureFactory.CreateColoredTexture(64, 7, Color.Red); }

            try { InventoryPlayerHealthBarOverlay = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_player_health_bar_overlay"); }
            catch { InventoryPlayerHealthBarOverlay = _textureFactory.CreateColoredTexture(64, 7, Color.LimeGreen); }

            // Generate Noise Texture
            try { NoiseTexture = _textureFactory.CreateNoiseTexture(256, 256); }
            catch { NoiseTexture = _textureFactory.CreateColoredTexture(256, 256, Color.Gray); }

            // Generate Mana Bar Pattern Texture
            try { ManaBarPattern = _textureFactory.CreateManaPatternTexture(); }
            catch { ManaBarPattern = _textureFactory.CreateColoredTexture(16, 16, Color.White); }

            // Load Heal Particle Sprite
            try { HealParticleSprite = _core.Content.Load<Texture2D>("Sprites/Particles/heal_plus"); }
            catch { HealParticleSprite = _textureFactory.CreatePlusParticleTexture(); }

            // Load Spell Slot Button Sprite
            try { InventorySpellSlotButtonSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_spell_slot_button"); }
            catch { InventorySpellSlotButtonSpriteSheet = _textureFactory.CreateColoredTexture(192, 8, Color.Magenta); }

            // Load Tenacity Pip Sprite Sheet
            try { TenacityPipTexture = _core.Content.Load<Texture2D>("Sprites/UI/BattleUI/tenacity_3x3_icon"); }
            catch { TenacityPipTexture = _textureFactory.CreateColoredTexture(6, 3, Color.Magenta); }

            // --- NEW: Create Mini Action Button Texture ---
            // Width 80px, Height 6px
            try { MiniActionButtonSprite = _textureFactory.CreateMiniActionButtonTexture(80); }
            catch { MiniActionButtonSprite = _textureFactory.CreateColoredTexture(80, 6, Color.Magenta); }

            LoadAndCacheCursorSprite("cursor_default");
            LoadAndCacheCursorSprite("cursor_hover_clickable");
            LoadAndCacheCursorSprite("cursor_hover_clickable_hint"); // Added new cursor
            LoadAndCacheCursorSprite("cursor_hover_hint"); // Added new cursor
            LoadAndCacheCursorSprite("cursor_dragging_draggable");

            InitializeArrowSourceRects();
            InitializeActionButtonsSourceRects();
            InitializeActionIconsSourceRects();
            InitializeSpellUsesRects();
            InitializeSplitMapInventoryButtonRects();
            InitializeSplitMapCloseInventoryButtonRects();
            InitializeSplitMapSettingsButtonRects();
            InitializeInventoryHeaderButtonRects();
            InitializeInventoryArrowButtonRects();
            InitializeInventoryScrollArrowRects();
            InitializeInventorySpellSlotButtonRects();
            InitializeTargetingButtonRects();
            InitializeStatChangeIconRects();
        }

        private void InitializeStatChangeIconRects()
        {
            StatChangeIconSourceRects = new Rectangle[3];
            const int iconSize = 3;
            // Frame 0: Neutral
            StatChangeIconSourceRects[0] = new Rectangle(0, 0, iconSize, iconSize);
            // Frame 1: Up
            StatChangeIconSourceRects[1] = new Rectangle(iconSize, 0, iconSize, iconSize);
            // Frame 2: Down
            StatChangeIconSourceRects[2] = new Rectangle(iconSize * 2, 0, iconSize, iconSize);
        }

        private void InitializeTargetingButtonRects()
        {
            TargetingButtonSourceRects = new Rectangle[3];
            const int frameWidth = 150;
            const int frameHeight = 13; // Changed from 22 to 13
            // Frame 0: Idle
            TargetingButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            // Frame 1: Hover
            TargetingButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
            // Frame 2: Disabled
            TargetingButtonSourceRects[2] = new Rectangle(frameWidth * 2, 0, frameWidth, frameHeight);
        }

        private void InitializeInventorySpellSlotButtonRects()
        {
            InventorySpellSlotButtonSourceRects = new Rectangle[3];
            const int frameWidth = 64;
            const int frameHeight = 8;
            // Frame 0: Empty
            InventorySpellSlotButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            // Frame 1: Filled
            InventorySpellSlotButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
            // Frame 2: Hover
            InventorySpellSlotButtonSourceRects[2] = new Rectangle(frameWidth * 2, 0, frameWidth, frameHeight);
        }

        private void InitializeInventoryScrollArrowRects()
        {
            InventoryScrollArrowRects = new Rectangle[2];
            InventoryScrollArrowRects[0] = new Rectangle(0, 0, 5, 5); // Up
            InventoryScrollArrowRects[1] = new Rectangle(5, 0, 5, 5); // Down
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

        private void InitializeSplitMapSettingsButtonRects()
        {
            SplitMapSettingsButtonSourceRects = new Rectangle[2];
            const int frameWidth = 16;
            const int frameHeight = 16;
            // Frame 0: Idle
            SplitMapSettingsButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            // Frame 1: Hover
            SplitMapSettingsButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
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
            int partSize = isMajor ? 96 : 64;
            PreCalculateSpriteBounds(sprite, archetypeId, partSize);
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

        public Texture2D GetItemSpriteSilhouette(string imagePath, string? fallbackPath = null)
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

            return LoadAndCacheItem(imagePath, imagePath, fallbackPath).Silhouette;
        }

        public Texture2D GetItemSprite(string imagePath, string? fallbackPath = null)
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

            return LoadAndCacheItem(imagePath, imagePath, fallbackPath).Original;
        }

        private (Texture2D Original, Texture2D Silhouette) LoadAndCacheItem(string cacheKey, string? imagePath, string? fallbackPath = null)
        {
            if (_itemSprites.TryGetValue(cacheKey, out var cachedTuple))
            {
                return cachedTuple;
            }

            // --- NEW LOGIC: Check for Sprite Sheet Paths ---
            if (imagePath != null)
            {
                if (imagePath.StartsWith("Sprites/Items/Weapons/"))
                {
                    if (int.TryParse(imagePath.Substring("Sprites/Items/Weapons/".Length), out int id))
                    {
                        return ExtractSpriteFromSheet(ItemWeaponsSpriteSheet, id, cacheKey);
                    }
                }
                else if (imagePath.StartsWith("Sprites/Items/Relics/"))
                {
                    if (int.TryParse(imagePath.Substring("Sprites/Items/Relics/".Length), out int id))
                    {
                        return ExtractSpriteFromSheet(ItemRelicsSpriteSheet, id, cacheKey);
                    }
                }
            }

            // --- FALLBACK: Legacy Individual File Loading ---
            Debug.WriteLine($"[SpriteManager] LoadAndCacheItem called for legacy path: '{imagePath}'");

            Texture2D originalTexture;
            try
            {
                if (imagePath != null)
                {
                    originalTexture = _core.Content.Load<Texture2D>(imagePath);
                }
                else
                {
                    throw new Exception("Image path is null");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpriteManager] [WARNING] FAILED to load: '{imagePath}'. Exception: {ex.Message}");

                // Try fallback
                if (!string.IsNullOrEmpty(fallbackPath))
                {
                    try
                    {
                        Debug.WriteLine($"[SpriteManager] Attempting fallback: '{fallbackPath}'");
                        originalTexture = _core.Content.Load<Texture2D>(fallbackPath);
                    }
                    catch
                    {
                        Debug.WriteLine($"[SpriteManager] [ERROR] Fallback failed too. Using placeholder.");
                        originalTexture = _textureFactory.CreateColoredTexture(32, 32, Color.Magenta);
                    }
                }
                else
                {
                    Debug.WriteLine($"[SpriteManager] No fallback provided. Using placeholder.");
                    originalTexture = _textureFactory.CreateColoredTexture(32, 32, Color.Magenta);
                }
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

        private (Texture2D Original, Texture2D Silhouette) ExtractSpriteFromSheet(Texture2D sheet, int index, string cacheKey)
        {
            if (sheet == null)
            {
                Debug.WriteLine($"[SpriteManager] ERROR: Sprite sheet is null for item index {index}.");
                var placeholder = _textureFactory.CreateColoredTexture(16, 16, Color.Magenta);
                var placeholderTuple = (placeholder, placeholder);
                _itemSprites[cacheKey] = placeholderTuple;
                return placeholderTuple;
            }

            const int spriteSize = 16;
            const int columns = 8; // 128 / 16

            int col = index % columns;
            int row = index / columns;

            var sourceRect = new Rectangle(col * spriteSize, row * spriteSize, spriteSize, spriteSize);

            // Create new texture for the extracted sprite
            var extractedTexture = new Texture2D(_core.GraphicsDevice, spriteSize, spriteSize);
            var data = new Color[spriteSize * spriteSize];

            // Get data from the sheet
            // We need to get the full sheet data first? No, GetData allows specifying a rectangle.
            sheet.GetData(0, sourceRect, data, 0, data.Length);
            extractedTexture.SetData(data);

            // Create Silhouette
            var silhouetteTexture = CreateSilhouette(extractedTexture);

            var tuple = (extractedTexture, silhouetteTexture);
            _itemSprites[cacheKey] = tuple;
            return tuple;
        }

        public Texture2D GetSmallRelicSprite(string imagePath)
        {
            return LoadAndCacheSmallItem(imagePath, imagePath).Original;
        }

        public Texture2D GetSmallRelicSpriteSilhouette(string imagePath)
        {
            return LoadAndCacheSmallItem(imagePath, imagePath).Silhouette;
        }

        private (Texture2D Original, Texture2D Silhouette) LoadAndCacheSmallItem(string cacheKey, string? imagePath)
        {
            if (_smallItemSprites.TryGetValue(cacheKey, out var cachedTuple))
            {
                return cachedTuple;
            }

            // Get the original large texture (32x32 or 16x16)
            Texture2D largeTexture = GetItemSprite(imagePath); // This handles loading/caching of the large one

            // If the texture is already 16x16 (which it is for the new sprite sheets), just reuse it.
            if (largeTexture.Width == 16 && largeTexture.Height == 16)
            {
                var silhouette = GetItemSpriteSilhouette(imagePath);
                var tuple = (largeTexture, silhouette);
                _smallItemSprites[cacheKey] = tuple;
                return tuple;
            }

            // Otherwise, downscale (legacy support for old 32x32 sprites)
            int width = 16;
            int height = 16;

            RenderTarget2D renderTarget = new RenderTarget2D(_core.GraphicsDevice, width, height);
            _core.GraphicsDevice.SetRenderTarget(renderTarget);
            _core.GraphicsDevice.Clear(Color.Transparent);

            var spriteBatch = ServiceLocator.Get<SpriteBatch>();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(largeTexture, new Rectangle(0, 0, width, height), Color.White);
            spriteBatch.End();

            _core.GraphicsDevice.SetRenderTarget(null);

            Color[] data = new Color[width * height];
            renderTarget.GetData(data);

            Texture2D smallTexture = new Texture2D(_core.GraphicsDevice, width, height);
            smallTexture.SetData(data);

            renderTarget.Dispose();

            Texture2D smallSilhouette = CreateSilhouette(smallTexture);

            var tuple2 = (smallTexture, smallSilhouette);
            _smallItemSprites[cacheKey] = tuple2;
            return tuple2;
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

        private void PreCalculateSpriteBounds(Texture2D sprite, string archetypeId, int partSize)
        {
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

            // --- NEW: Calculate Visual Center Offset (Union) ---
            if (numParts > 0)
            {
                int globalMinY = int.MaxValue;
                int globalMaxY = int.MinValue;

                for (int i = 0; i < numParts; i++)
                {
                    if (topOffsets[i] != int.MaxValue && topOffsets[i] < globalMinY) globalMinY = topOffsets[i];
                    if (bottomOffsets[i] != -1 && bottomOffsets[i] > globalMaxY) globalMaxY = bottomOffsets[i];
                }

                if (globalMinY != int.MaxValue && globalMaxY != int.MinValue)
                {
                    float centerY = (globalMinY + globalMaxY) / 2f;
                    float frameCenterY = partSize / 2f;
                    // X is 0 (Geometric Center), Y is Visual Center
                    _visualCenterOffsets[archetypeId] = new Vector2(0, centerY - frameCenterY);
                }
                else
                {
                    _visualCenterOffsets[archetypeId] = Vector2.Zero;
                }
            }
        }

        public Vector2 GetVisualCenterOffset(string archetypeId)
        {
            if (_visualCenterOffsets.TryGetValue(archetypeId, out var offset))
            {
                return offset;
            }
            return Vector2.Zero;
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
            try { _playerSprite = _core.Content.Load<Texture2D>("Sprites/player"); }
            catch { _playerSprite = _textureFactory.CreatePlayerTexture(); }

            try { _emptySprite = _textureFactory.CreateEmptyTexture(); }
            catch { _emptySprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _speedMarkSprite = _core.Content.Load<Texture2D>("Sprites/speedMark"); }
            catch { _speedMarkSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try
            {
                PlayerHeartSpriteSheet = _core.Content.Load<Texture2D>("Sprites/Player/player_heart_spritesheet");
                PlayerHeartSpriteSheetSilhouette = CreateSilhouette(PlayerHeartSpriteSheet);
                PreCalculateSpriteBounds(PlayerHeartSpriteSheet, "player", 32);
            }
            catch
            {
                PlayerHeartSpriteSheet = _textureFactory.CreateColoredTexture(32, 32, Color.DeepPink);
                PlayerHeartSpriteSheetSilhouette = _textureFactory.CreateColoredTexture(32, 32, Color.White);
            }
            // Load Stat Bars
            try { InventoryStatBarEmpty = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_stat_bar_empty"); }
            catch { InventoryStatBarEmpty = _textureFactory.CreateColoredTexture(40, 3, Color.DarkGray); }
            try { InventoryStatBarDisabled = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_stat_bar_disabled"); }
            catch { InventoryStatBarDisabled = _textureFactory.CreateColoredTexture(40, 3, Color.Black); }
            try { InventoryStatBarFull = _core.Content.Load<Texture2D>("Sprites/UI/Inventory/inventory_stat_bar_full"); }
            catch { InventoryStatBarFull = _textureFactory.CreateColoredTexture(40, 3, Color.White); }

            try
            {
                StatChangeIconsSpriteSheet = _core.Content.Load<Texture2D>("Sprites/UI/BasicIcons/stat_change_icons_spritesheet");
                StatChangeIconsSpriteSheetSilhouette = CreateSilhouette(StatChangeIconsSpriteSheet);
            }
            catch
            {
                StatChangeIconsSpriteSheet = _textureFactory.CreateColoredTexture(9, 3, Color.Magenta);
                StatChangeIconsSpriteSheetSilhouette = _textureFactory.CreateColoredTexture(9, 3, Color.White);
            }

            LoadPlayerPortraits();
        }

        private void LoadPlayerPortraits()
        {
            try
            {
                // Load the single master sheet
                PlayerMasterSpriteSheet = _core.Content.Load<Texture2D>("Sprites/Player/cat_portraits_32x32_spritesheet");
                PlayerMasterSpriteSheetSilhouette = CreateSilhouette(PlayerMasterSpriteSheet);
            }
            catch
            {
                // Fallback
                PlayerMasterSpriteSheet = _textureFactory.CreateColoredTexture(32, 32, Color.Magenta);
                PlayerMasterSpriteSheetSilhouette = _textureFactory.CreateColoredTexture(32, 32, Color.White);
            }
        }

        /// <summary>
        /// Calculates the source rectangle for a specific player sprite type and member index.
        /// </summary>
        /// <param name="memberIndex">The column index (0-based) representing the party member.</param>
        /// <param name="type">The row type (Normal, Alt, Sleep, etc.).</param>
        /// <returns>The 32x32 source rectangle.</returns>
        public Rectangle GetPlayerSourceRect(int memberIndex, PlayerSpriteType type)
        {
            if (PlayerMasterSpriteSheet == null) return Rectangle.Empty;

            const int spriteSize = 32;
            int row = (int)type;
            int col = memberIndex;

            // Safety check for bounds
            int maxCols = PlayerMasterSpriteSheet.Width / spriteSize;
            int maxRows = PlayerMasterSpriteSheet.Height / spriteSize;

            if (col >= maxCols || row >= maxRows)
            {
                // Return empty or default if out of bounds
                return new Rectangle(0, 0, spriteSize, spriteSize);
            }

            return new Rectangle(col * spriteSize, row * spriteSize, spriteSize, spriteSize);
        }

        private bool IsFrameNotEmpty(Color[] data, int texWidth, int x, int y, int w, int h)
        {
            for (int py = y; py < y + h; py++)
            {
                for (int px = x; px < x + w; px++)
                {
                    if (data[py * texWidth + px].A > 0) return true;
                }
            }
            return false;
        }

        [Obsolete("LoadSpriteContent is deprecated, please use LoadEssentialContent and LoadGameContent instead.")]
        public void LoadSpriteContent()
        {
            LoadEssentialContent();
            LoadGameContent();
        }

        public Rectangle GetAnimatedIconSourceRect(Texture2D texture, GameTime gameTime)
        {
            if (texture == null) return Rectangle.Empty;

            // Assume square frames
            int frameSize = texture.Height;
            if (frameSize == 0) return Rectangle.Empty;

            int frameCount = texture.Width / frameSize;
            if (frameCount <= 1) return new Rectangle(0, 0, texture.Width, texture.Height);

            const float frameDuration = 0.15f; // Standard animation speed
            int frameIndex = (int)(gameTime.TotalGameTime.TotalSeconds / frameDuration) % frameCount;

            return new Rectangle(frameIndex * frameSize, 0, frameSize, frameSize);
        }

        public Rectangle GetEquipIconSourceRect(GameTime gameTime)
        {
            const float frameDuration = 0.5f;
            int frameIndex = (int)(gameTime.TotalGameTime.TotalSeconds / frameDuration) % 2;
            return new Rectangle(frameIndex * 32, 0, 32, 32);
        }

        public Rectangle GetRestActionIconRect(int actionIndex, int stateIndex)
        {
            return new Rectangle(stateIndex * 8, actionIndex * 8, 8, 8);
        }
    }
}
