using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        BodyNormal = 4,
        BodyAlt = 5,
        Sleep = 6
    }
    public class SpriteManager
    {
        private readonly Core _core;
        private readonly TextureFactory _textureFactory;

        public Texture2D ActionButtonsSpriteSheet { get; private set; }
        public Texture2D ActionButtonTemplateSpriteSheet { get; private set; }
        public Texture2D ActiveSpellsSpriteSheet { get; private set; }
        public Texture2D ActionTooltipBackgroundSprite { get; private set; }
        public Texture2D ActionIconsSpriteSheet { get; private set; }
        public Texture2D ActionButtonUsesSpriteSheet { get; private set; }

        public Texture2D StatChangeIconsSpriteSheet { get; private set; }
        public Texture2D StatChangeIconsSpriteSheetSilhouette { get; private set; }
        public Texture2D BetTicketSpriteSheet { get; private set; }

        public Texture2D PermanentStatusIconsSpriteSheet { get; private set; }

        public Texture2D MiniActionButtonSprite { get; private set; }

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
        public Texture2D StunnedIconSpriteSheet { get; private set; }
        public Texture2D TenacityBreakSpriteSheet { get; private set; }
        public Texture2D TenacityRestoreSpriteSheet { get; private set; }
        public Texture2D TenacityPipTexture { get; private set; }
        public Texture2D StatModIconsTexture { get; private set; }
        public Texture2D CardFlipIcon { get; private set; }
        public Texture2D LevelIconSprite { get; private set; }

        public Texture2D FastForwardIcon { get; private set; }
        public Texture2D TitleLogoSpriteSheet { get; private set; }

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

        private readonly Dictionary<string, (Texture2D Original, Texture2D Silhouette)> _itemSprites = new Dictionary<string, (Texture2D, Texture2D)>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (Texture2D Original, Texture2D Silhouette)> _smallItemSprites = new Dictionary<string, (Texture2D, Texture2D)>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (Texture2D Texture, Rectangle[] Frames)> _cursorSprites = new Dictionary<string, (Texture2D, Rectangle[])>();
        private readonly Dictionary<int, Rectangle> _playerSpriteBoundsCache = new Dictionary<int, Rectangle>();

        private Texture2D _logoSprite;
        private Texture2D _playerSprite;
        private Texture2D _emptySprite;
        private Texture2D _speedMarkSprite;
        private Texture2D _mapMarkerSprite;
        private Texture2D _circleTextureSprite;
        private Texture2D _ringTextureSprite;
        private Texture2D _settingsIconSprite;
        private Texture2D _downArrowSprite;
        private Texture2D _circleParticleSprite;
        private Texture2D _emberParticleSprite;
        private Texture2D _softParticleSprite;
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
        public Texture2D SplitNodeCombat { get; private set; }
        public Texture2D SplitNodeCombatSilhouette { get; private set; }
        public Texture2D SplitNodeEasyCombat { get; private set; }
        public Texture2D SplitNodeEasyCombatSilhouette { get; private set; }
        public Texture2D SplitNodeHardCombat { get; private set; }
        public Texture2D SplitNodeHardCombatSilhouette { get; private set; }
        public Texture2D MapNodePlayerSprite { get; private set; }
        public Texture2D MapNodePlayerSpriteSilhouette { get; private set; }

        public Texture2D SplitNodeRest { get; private set; }
        public Texture2D SplitNodeRestSilhouette { get; private set; }
        public Texture2D SplitNodeRecruit { get; private set; }
        public Texture2D SplitNodeRecruitSilhouette { get; private set; }

        public Texture2D SplitMapInventoryButton { get; private set; }
        public Texture2D SplitMapHeaderBorder { get; private set; }
        public Texture2D SplitMapCloseInventoryButton { get; private set; }
        public Texture2D SplitMapSettingsButton { get; private set; }
        public Texture2D InventoryBorderHeader { get; private set; }
        public Texture2D InventoryBorderWeapons { get; private set; }
        public Texture2D InventoryBorderEquip { get; private set; }
        public Texture2D InventoryBorderEquipSubmenu { get; private set; }
        public Texture2D InventoryBorderEquipInfoPanelLeft { get; private set; }
        public Texture2D InventoryBorderEquipInfoPanelRight { get; private set; }
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
        public Texture2D HealthHeartsSpriteSheet { get; private set; }
        public Texture2D HealthHearts3x3SpriteSheet { get; private set; }

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
        public Texture2D DownArrowSprite => _downArrowSprite;
        public Texture2D CircleParticleSprite => _circleParticleSprite;
        public Texture2D EmberParticleSprite => _emberParticleSprite;
        public Texture2D SoftParticleSprite => _softParticleSprite;
        public Texture2D ScratchParticleSprite { get; private set; }

        public SpriteManager()
        {
            _core = ServiceLocator.Get<Core>();
            _textureFactory = ServiceLocator.Get<TextureFactory>();
        }

        private Texture2D LoadTex(string path, int fallbackWidth, int fallbackHeight, Color fallbackColor)
        {
            try { return _core.Content.Load<Texture2D>(path); }
            catch { return _textureFactory.CreateColoredTexture(fallbackWidth, fallbackHeight, fallbackColor); }
        }

        private Texture2D LoadTexWithSilhouette(string path, int fallbackWidth, int fallbackHeight, Color fallbackColor, out Texture2D silhouette)
        {
            Texture2D tex = LoadTex(path, fallbackWidth, fallbackHeight, fallbackColor);
            silhouette = CreateSilhouette(tex);
            return tex;
        }

        public void LoadEssentialContent()
        {
            _logoSprite = LoadTex("Sprites/logo", 8, 8, Color.Red);
            TitleLogoSpriteSheet = LoadTex("Sprites/UI/Logo/cwwc_logo_spritesheet", 240, 160, Color.Magenta);
            _mapMarkerSprite = LoadTex("Sprites/map_marker", 8, 8, Color.Magenta);

            try { _circleTextureSprite = _textureFactory.CreateCircleTexture(); }
            catch { _circleTextureSprite = _textureFactory.CreateColoredTexture(16, 16, Color.Red); }

            try { _ringTextureSprite = _textureFactory.CreateRingTexture(); }
            catch { _ringTextureSprite = _textureFactory.CreateColoredTexture(16, 16, Color.White); }

            _settingsIconSprite = LoadTex("Sprites/UI/BasicIcons/ui_settings_icon", 8, 8, Color.Red);
            _downArrowSprite = LoadTex("Sprites/UI/BasicIcons/down_arrow", 9, 9, Color.Red);
            FastForwardIcon = LoadTex("Sprites/UI/BasicIcons/fastforward_icon", 32, 32, Color.Yellow);

            try { _circleParticleSprite = _textureFactory.CreateCircleParticleTexture(); }
            catch { _circleParticleSprite = _textureFactory.CreateColoredTexture(4, 4, Color.Red); }

            _emberParticleSprite = LoadTex("Sprites/Particles/ember_particle", 9, 9, Color.Red);

            try { _softParticleSprite = _textureFactory.CreateSoftCircleParticleTexture(); }
            catch { _softParticleSprite = _textureFactory.CreateColoredTexture(16, 16, Color.Red); }

            ScratchParticleSprite = LoadTex("Sprites/Particles/scratch_particle", 12, 2, Color.White);
            BattleEnemyFloorSprite = LoadTex("Sprites/UI/BattleUI/battle_enemy_floor", 128, 128, Color.DarkGray);
            BattlePlayerFloorSprite = LoadTex("Sprites/UI/BattleUI/party_member_enemy_floor", 128, 128, Color.DarkBlue);
            ArrowIconSpriteSheet = LoadTex("Sprites/UI/BasicIcons/arrow_icon_spritesheet", 48, 48, Color.Magenta);
            ActionButtonsSpriteSheet = LoadTex("Sprites/UI/BattleUI/ui_action_buttons_icon_spritesheet", 192, 129, Color.Magenta);
            ActionButtonTemplateSpriteSheet = LoadTex("Sprites/UI/BattleUI/ui_action_button_template_spritesheet", 1099, 17, Color.Magenta);
            ActionTooltipBackgroundSprite = LoadTex("Sprites/UI/BattleUI/ui_action_tooltip_background", 319, 178, Color.DarkGray);
            ActionIconsSpriteSheet = LoadTex("Sprites/UI/BasicIcons/ui_action_icons_spritesheet_9x9", 45, 9, Color.Magenta);
            ActionButtonUsesSpriteSheet = LoadTex("Sprites/UI/BattleUI/ui_action_button_uses_spritesheet", 471, 17, Color.Magenta);
            ActiveSpellsSpriteSheet = LoadTex("Sprites/UI/BattleUI/active_spells_9x9_spritesheet", 27, 9, Color.Magenta);

            StatChangeIconsSpriteSheet = LoadTexWithSilhouette("Sprites/UI/BasicIcons/stat_change_icons_spritesheet", 9, 3, Color.Magenta, out var statSilhouette);
            StatChangeIconsSpriteSheetSilhouette = statSilhouette;

            PermanentStatusIconsSpriteSheet = LoadTex("Sprites/UI/BasicIcons/status_effect_icon_spritesheet", 20, 10, Color.Magenta);
            ItemWeaponsSpriteSheet = LoadTex("Sprites/Items/item_weapons_spritesheet", 128, 256, Color.Magenta);
            BattleBorderMain = LoadTex("Sprites/UI/BattleUI/battle_border_main", 320, 180, Color.Magenta);
            BattleBorderMain2 = LoadTex("Sprites/UI/BattleUI/battle_border_main_2", 320, 180, Color.Magenta);
            BattleBorderCombat = LoadTex("Sprites/UI/BattleUI/battle_border_combat", 320, 180, Color.Magenta);
            BattleBorderAction = LoadTex("Sprites/UI/BattleUI/battle_border_action", 320, 180, Color.Magenta);
            BattleBorderItem = LoadTex("Sprites/UI/BattleUI/battle_border_item", 320, 180, Color.Magenta);
            BattleBorderTarget = LoadTex("Sprites/UI/BattleUI/battle_border_target", 320, 180, Color.Magenta);
            BattleBorderSwitch = LoadTex("Sprites/UI/BattleUI/battle_border_switch", 320, 180, Color.Magenta);

            MousePromptBlank = LoadTexWithSilhouette("Sprites/UI/KeyPrompts/mouse/ui_mouse_blank", 5, 7, Color.Magenta, out var mpbSil);
            MousePromptBlankSilhouette = mpbSil;

            MousePromptLeftClick = LoadTexWithSilhouette("Sprites/UI/KeyPrompts/mouse/ui_mouse_left_click", 5, 7, Color.Magenta, out var mplcSil);
            MousePromptLeftClickSilhouette = mplcSil;

            MousePromptRightClick = LoadTexWithSilhouette("Sprites/UI/KeyPrompts/mouse/ui_mouse_right_click", 5, 7, Color.Magenta, out var mprcSil);
            MousePromptRightClickSilhouette = mprcSil;

            MousePromptMiddleClick = LoadTexWithSilhouette("Sprites/UI/KeyPrompts/mouse/ui_mouse_middle_click", 5, 7, Color.Magenta, out var mpmcSil);
            MousePromptMiddleClickSilhouette = mpmcSil;

            MousePromptDisabled = LoadTexWithSilhouette("Sprites/UI/KeyPrompts/mouse/ui_mouse_disabled", 5, 7, Color.Magenta, out var mpdSil);
            MousePromptDisabledSilhouette = mpdSil;

            SpellbookPageSprite = LoadTex("Sprites/SpellBook/spellbook_page", 35, 35, Color.Magenta);
            SpellbookClosedSprite = LoadTex("Sprites/SpellBook/spellbook_closed", 64, 64, Color.Magenta);

            SplitNodeStart = LoadTexWithSilhouette("Sprites/MapNodes/MapNode_Start", 64, 32, Color.Green, out var snsSil);
            SplitNodeStartSilhouette = snsSil;

            SplitNodeCombat = LoadTexWithSilhouette("Sprites/MapNodes/MapNode_Combat", 64, 32, Color.Red, out var sncSil);
            SplitNodeCombatSilhouette = sncSil;

            SplitNodeEasyCombat = LoadTexWithSilhouette("Sprites/MapNodes/MapNode_EasyCombat", 64, 32, Color.LightGreen, out var snecSil);
            SplitNodeEasyCombatSilhouette = snecSil;

            SplitNodeHardCombat = LoadTexWithSilhouette("Sprites/MapNodes/MapNode_HardCombat", 64, 32, Color.DarkRed, out var snhcSil);
            SplitNodeHardCombatSilhouette = snhcSil;

            SplitNodeRest = LoadTexWithSilhouette("Sprites/MapNodes/MapNode_Rest", 64, 32, Color.GreenYellow, out var snrSil);
            SplitNodeRestSilhouette = snrSil;

            SplitNodeRecruit = LoadTexWithSilhouette("Sprites/MapNodes/MapNode_Recuit", 64, 32, Color.CornflowerBlue, out var snrecSil);
            SplitNodeRecruitSilhouette = snrecSil;

            MapNodePlayerSprite = LoadTexWithSilhouette("Sprites/MapNodes/MapNode_Player", 64, 32, Color.Cyan, out var mnpsSil);
            MapNodePlayerSpriteSilhouette = mnpsSil;

            SplitMapInventoryButton = LoadTex("Sprites/UI/BasicIcons/SplitMap_Inventory_Button", 32, 16, Color.Magenta);
            SplitMapCloseInventoryButton = LoadTex("Sprites/UI/BasicIcons/SplitMap_Close_Inventory_Button", 32, 16, Color.Magenta);
            SplitMapSettingsButton = LoadTex("Sprites/UI/BasicIcons/SplitMap_Settings_Button", 32, 16, Color.Magenta);
            InventoryBorderHeader = LoadTex("Sprites/UI/Inventory/inventory_border_header", 320, 180, Color.Magenta);
            InventoryBorderWeapons = LoadTex("Sprites/UI/Inventory/inventory_border_weapons", 320, 180, Color.Magenta);
            InventoryBorderEquip = LoadTex("Sprites/UI/Inventory/inventory_border_equip", 320, 180, Color.Magenta);
            InventoryBorderEquipSubmenu = LoadTex("Sprites/UI/Inventory/inventory_border_equip_submenu", 320, 180, Color.Magenta);
            InventoryBorderEquipInfoPanelLeft = LoadTex("Sprites/UI/Inventory/inventory_border_equip_info_panel_left", 320, 180, Color.DarkBlue);
            InventoryBorderEquipInfoPanelRight = LoadTex("Sprites/UI/Inventory/inventory_border_equip_info_panel_right", 320, 180, Color.DarkBlue);
            EquipSlotButtonSprite = LoadTex("Sprites/UI/Inventory/equip_slot_button", 180, 16, Color.HotPink);
            InventoryEquipHoverSprite = LoadTex("Sprites/UI/Inventory/inventory_equip_hover", 180, 16, Color.HotPink);
            InventoryEquipSelectedSprite = LoadTex("Sprites/UI/Inventory/inventory_equip_selected", 180, 16, Color.Gold);
            InventoryScrollArrowsSprite = LoadTex("Sprites/UI/Inventory/inventory_scroll_arrows", 10, 5, Color.Magenta);
            InventoryEmptySlotSprite = LoadTex("Sprites/UI/Inventory/inventory_16x16_empty_slot_sprite", 16, 16, Color.DarkGray);
            InventorySlotEquipIconSprite = LoadTex("Sprites/UI/Inventory/inventory_slot_equip_icon", 64, 32, Color.Magenta);
            TargetingIndicatorSprite = LoadTex("Sprites/UI/BasicIcons/targeting_indicator", 32, 32, Color.Red);
            ShopBorderMain = LoadTex("Sprites/UI/Shop/shop_border_main", 320, 180, Color.Magenta);
            ShopXIcon = LoadTex("Sprites/UI/BasicIcons/X_32x32", 32, 32, Color.Red);
            RestBorderMain = LoadTex("Sprites/UI/Rest/rest_border_main", 320, 180, Color.Magenta);
            RestActionIconsSpriteSheet = LoadTex("Sprites/UI/Rest/rest_action_icons", 24, 32, Color.Magenta);
            TargetingButtonSpriteSheet = LoadTex("Sprites/UI/BattleUI/ui_choose_a_target_button_spritesheet", 450, 22, Color.Magenta);
            HealthHeartsSpriteSheet = LoadTex("Sprites/UI/BattleUI/health_5x5_icon_spritesheet", 45, 5, Color.Red);
            HealthHearts3x3SpriteSheet = LoadTex("Sprites/UI/BattleUI/health_3x3_icon_spritesheet", 27, 3, Color.Red);
            InventoryPlayerHealthBarEmpty = LoadTex("Sprites/UI/Inventory/inventory_player_health_bar_empty", 66, 7, Color.DarkGray);
            InventoryPlayerHealthBarDisabled = LoadTex("Sprites/UI/Inventory/inventory_player_health_bar_disabled", 66, 7, Color.Black);
            InventoryPlayerHealthBarFull = LoadTex("Sprites/UI/Inventory/inventory_player_health_bar_full", 64, 7, Color.Red);
            InventoryPlayerHealthBarOverlay = LoadTex("Sprites/UI/Inventory/inventory_player_health_bar_overlay", 64, 7, Color.LimeGreen);

            try { NoiseTexture = _textureFactory.CreateNoiseTexture(256, 256); }
            catch { NoiseTexture = _textureFactory.CreateColoredTexture(256, 256, Color.Gray); }

            try { HealParticleSprite = _core.Content.Load<Texture2D>("Sprites/Particles/heal_plus"); }
            catch { HealParticleSprite = _textureFactory.CreatePlusParticleTexture(); }

            InventorySpellSlotButtonSpriteSheet = LoadTex("Sprites/UI/Inventory/inventory_spell_slot_button", 192, 8, Color.Magenta);
            StunnedIconSpriteSheet = LoadTex("Sprites/UI/BattleUI/stunned_16x16_spritesheet", 48, 16, Color.Magenta);
            TenacityPipTexture = LoadTex("Sprites/UI/BattleUI/tenacity_5x5_icon_spritesheet", 20, 5, Color.Magenta);
            TenacityBreakSpriteSheet = LoadTex("Sprites/UI/BattleUI/tenacity_break_32x32_spritesheet", 352, 32, Color.Cyan);
            TenacityRestoreSpriteSheet = LoadTex("Sprites/UI/BattleUI/tenacity_restored_32x32_spritesheet", 352, 32, Color.Lime);
            StatModIconsTexture = LoadTex("Sprites/UI/BasicIcons/stat_mod_icons_spritesheet", 24, 6, Color.Magenta);
            CardFlipIcon = LoadTex("Sprites/UI/BasicIcons/rotate_icon_8x8_spritesheet", 16, 8, Color.Yellow);
            LevelIconSprite = LoadTex("Sprites/UI/BasicIcons/level_text_icon", 5, 3, Color.Magenta);

            try { MiniActionButtonSprite = _textureFactory.CreateMiniActionButtonTexture(80); }
            catch { MiniActionButtonSprite = _textureFactory.CreateColoredTexture(80, 6, Color.Magenta); }

            LoadAndCacheCursorSprite("cursor_default");
            LoadAndCacheCursorSprite("cursor_hover_clickable");
            LoadAndCacheCursorSprite("cursor_hover_clickable_hint");
            LoadAndCacheCursorSprite("cursor_hover_hint");
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
            StatChangeIconSourceRects[0] = new Rectangle(0, 0, iconSize, iconSize);
            StatChangeIconSourceRects[1] = new Rectangle(iconSize, 0, iconSize, iconSize);
            StatChangeIconSourceRects[2] = new Rectangle(iconSize * 2, 0, iconSize, iconSize);
        }

        private void InitializeTargetingButtonRects()
        {
            TargetingButtonSourceRects = new Rectangle[3];
            const int frameWidth = 150;
            const int frameHeight = 13;
            TargetingButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            TargetingButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
            TargetingButtonSourceRects[2] = new Rectangle(frameWidth * 2, 0, frameWidth, frameHeight);
        }

        private void InitializeInventorySpellSlotButtonRects()
        {
            InventorySpellSlotButtonSourceRects = new Rectangle[3];
            const int frameWidth = 64;
            const int frameHeight = 8;
            InventorySpellSlotButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            InventorySpellSlotButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
            InventorySpellSlotButtonSourceRects[2] = new Rectangle(frameWidth * 2, 0, frameWidth, frameHeight);
        }

        private void InitializeInventoryScrollArrowRects()
        {
            InventoryScrollArrowRects = new Rectangle[2];
            InventoryScrollArrowRects[0] = new Rectangle(0, 0, 5, 5);
            InventoryScrollArrowRects[1] = new Rectangle(5, 0, 5, 5);
        }

        private void InitializeInventoryArrowButtonRects()
        {
            const int frameWidth = 5;
            const int frameHeight = 5;

            InventoryLeftArrowButtonSourceRects = new Rectangle[2];
            InventoryLeftArrowButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            InventoryLeftArrowButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);

            InventoryRightArrowButtonSourceRects = new Rectangle[2];
            InventoryRightArrowButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            InventoryRightArrowButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
        }

        private void InitializeInventoryHeaderButtonRects()
        {
            InventoryHeaderButtonSourceRects = new Rectangle[3];
            const int frameWidth = 32;
            const int frameHeight = 32;
            InventoryHeaderButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            InventoryHeaderButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
            InventoryHeaderButtonSourceRects[2] = new Rectangle(frameWidth * 2, 0, frameWidth, frameHeight);
        }

        private void InitializeSplitMapInventoryButtonRects()
        {
            SplitMapInventoryButtonSourceRects = new Rectangle[2];
            const int frameWidth = 16;
            const int frameHeight = 16;
            SplitMapInventoryButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            SplitMapInventoryButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
        }

        private void InitializeSplitMapCloseInventoryButtonRects()
        {
            SplitMapCloseInventoryButtonSourceRects = new Rectangle[2];
            const int frameWidth = 16;
            const int frameHeight = 16;
            SplitMapCloseInventoryButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            SplitMapCloseInventoryButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
        }

        private void InitializeSplitMapSettingsButtonRects()
        {
            SplitMapSettingsButtonSourceRects = new Rectangle[2];
            const int frameWidth = 16;
            const int frameHeight = 16;
            SplitMapSettingsButtonSourceRects[0] = new Rectangle(0, 0, frameWidth, frameHeight);
            SplitMapSettingsButtonSourceRects[1] = new Rectangle(frameWidth, 0, frameWidth, frameHeight);
        }

        private void InitializeSpellUsesRects()
        {
            if (ActionButtonUsesSpriteSheet == null) return;
            int spriteWidth = 157;
            int spriteHeight = 17;
            SpellUsesSourceRects[3] = new Rectangle(0 * spriteWidth, 0, spriteWidth, spriteHeight);
            SpellUsesSourceRects[2] = new Rectangle(1 * spriteWidth, 0, spriteWidth, spriteHeight);
            SpellUsesSourceRects[1] = new Rectangle(2 * spriteWidth, 0, spriteWidth, spriteHeight);
        }

        private void InitializeArrowSourceRects()
        {
            var spriteSheetCoords = new Point[9]
            {
                new Point(0, 1),
                new Point(0, 0),
                new Point(1, 0),
                new Point(2, 0),
                new Point(2, 1),
                new Point(2, 2),
                new Point(1, 2),
                new Point(0, 2),
                new Point(1, 1)
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
            ActionButtonSourceRects = new Rectangle[6];
            int spriteWidth = 192 / 2;
            int spriteHeight = 129 / 3;

            for (int i = 0; i < 3; i++)
            {
                ActionButtonSourceRects[i * 2] = new Rectangle(0, i * spriteHeight, spriteWidth, spriteHeight);
                ActionButtonSourceRects[i * 2 + 1] = new Rectangle(spriteWidth, i * spriteHeight, spriteWidth, spriteHeight);
            }
        }

        private void InitializeActionIconsSourceRects()
        {
            ActionIconSourceRects = new Rectangle[6];
            const int iconSize = 9;
            for (int i = 0; i < 6; i++)
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

            try
            {
                sprite = _core.Content.Load<Texture2D>($"Sprites/Enemies/Major/{archetypeId.ToLower()}");
                isMajor = true;
            }
            catch
            {
                try
                {
                    sprite = _core.Content.Load<Texture2D>($"Sprites/Enemies/{archetypeId.ToLower()}");
                    isMajor = false;
                }
                catch
                {
                    _enemySprites[archetypeId] = (null, null, false);
                    return null;
                }
            }

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

            if (imagePath != null)
            {
                if (imagePath.StartsWith("Sprites/Items/Weapons/"))
                {
                    if (int.TryParse(imagePath.Substring("Sprites/Items/Weapons/".Length), out int id))
                    {
                        return ExtractSpriteFromSheet(ItemWeaponsSpriteSheet, id, cacheKey);
                    }
                }
            }

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
            const int columns = 8;

            int col = index % columns;
            int row = index / columns;

            var sourceRect = new Rectangle(col * spriteSize, row * spriteSize, spriteSize, spriteSize);

            var extractedTexture = new Texture2D(_core.GraphicsDevice, spriteSize, spriteSize);
            var data = new Color[spriteSize * spriteSize];

            sheet.GetData(0, sourceRect, data, 0, data.Length);
            extractedTexture.SetData(data);

            var silhouetteTexture = CreateSilhouette(extractedTexture);

            var tuple = (extractedTexture, silhouetteTexture);
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

                for (int y = 0; y < partSize; y++) { for (int x = 0; x < partSize; x++) { if (pixelData[(y * sprite.Width) + (partStartX + x)].A > 0) { topY = y; goto FoundTopPixel; } } }
            FoundTopPixel: topOffsets[i] = topY != -1 ? topY : int.MaxValue;

                for (int x = 0; x < partSize; x++) { for (int y = 0; y < partSize; y++) { if (pixelData[(y * sprite.Width) + (partStartX + x)].A > 0) { leftX = x; goto FoundLeftPixel; } } }
            FoundLeftPixel: leftOffsets[i] = leftX != -1 ? leftX : int.MaxValue;

                for (int x = partSize - 1; x >= 0; x--) { for (int y = 0; y < partSize; y++) { if (pixelData[(y * sprite.Width) + (partStartX + x)].A > 0) { rightX = x; goto FoundRightPixel; } } }
            FoundRightPixel: rightOffsets[i] = rightX;

                for (int y = partSize - 1; y >= 0; y--) { for (int x = 0; x < partSize; x++) { if (pixelData[(y * sprite.Width) + (partStartX + x)].A > 0) { bottomY = y; goto FoundBottomPixel; } } }
            FoundBottomPixel: bottomOffsets[i] = bottomY;
            }

            _enemySpriteTopPixelOffsets[archetypeId] = topOffsets;
            _enemySpriteLeftPixelOffsets[archetypeId] = leftOffsets;
            _enemySpriteRightPixelOffsets[archetypeId] = rightOffsets;
            _enemySpriteBottomPixelOffsets[archetypeId] = bottomOffsets;

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

            _speedMarkSprite = LoadTex("Sprites/speedMark", 8, 8, Color.Red);

            PlayerHeartSpriteSheet = LoadTexWithSilhouette("Sprites/Player/player_heart_spritesheet", 32, 32, Color.DeepPink, out var phsSil);
            PlayerHeartSpriteSheetSilhouette = phsSil;
            PreCalculateSpriteBounds(PlayerHeartSpriteSheet, "player", 32);

            InventoryStatBarEmpty = LoadTex("Sprites/UI/Inventory/inventory_stat_bar_empty", 40, 3, Color.DarkGray);
            InventoryStatBarDisabled = LoadTex("Sprites/UI/Inventory/inventory_stat_bar_disabled", 40, 3, Color.Black);
            InventoryStatBarFull = LoadTex("Sprites/UI/Inventory/inventory_stat_bar_full", 40, 3, Color.White);

            StatChangeIconsSpriteSheet = LoadTexWithSilhouette("Sprites/UI/BasicIcons/stat_change_icons_spritesheet", 9, 3, Color.Magenta, out var scisSil);
            StatChangeIconsSpriteSheetSilhouette = scisSil;

            BetTicketSpriteSheet = LoadTex("Sprites/UI/Betting/bet_ticket_30x44_spritesheet", 76, 31, Color.White);

            LoadPlayerPortraits();
        }

        private void LoadPlayerPortraits()
        {
            PlayerMasterSpriteSheet = LoadTexWithSilhouette("Sprites/Player/cat_portraits_32x32_spritesheet", 32, 32, Color.Magenta, out var pmsSil);
            PlayerMasterSpriteSheetSilhouette = pmsSil;
        }

        public Rectangle GetPlayerSourceRect(int memberIndex, PlayerSpriteType type)
        {
            if (PlayerMasterSpriteSheet == null) return Rectangle.Empty;

            const int spriteSize = 32;
            int row = (int)type;
            int col = memberIndex;

            int maxCols = PlayerMasterSpriteSheet.Width / spriteSize;
            int maxRows = PlayerMasterSpriteSheet.Height / spriteSize;

            if (col >= maxCols || row >= maxRows)
            {
                return new Rectangle(0, 0, spriteSize, spriteSize);
            }

            return new Rectangle(col * spriteSize, row * spriteSize, spriteSize, spriteSize);
        }

        public Rectangle GetPlayerSpriteBounds(int portraitIndex, PlayerSpriteType type)
        {
            int cacheKey = portraitIndex | ((int)type << 16);
            if (_playerSpriteBoundsCache.TryGetValue(cacheKey, out var bounds))
                return bounds;

            var sourceRect = GetPlayerSourceRect(portraitIndex, type);
            if (PlayerMasterSpriteSheet == null) return new Rectangle(-8, -8, 16, 16);

            Color[] data = new Color[sourceRect.Width * sourceRect.Height];
            PlayerMasterSpriteSheet.GetData(0, sourceRect, data, 0, data.Length);

            int minX = sourceRect.Width, maxX = 0, minY = sourceRect.Height, maxY = 0;
            bool found = false;

            for (int y = 0; y < sourceRect.Height; y++)
            {
                for (int x = 0; x < sourceRect.Width; x++)
                {
                    if (data[y * sourceRect.Width + x].A > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                bounds = new Rectangle(-8, -8, 16, 16);
            }
            else
            {
                // Origin is 16, 16
                bounds = new Rectangle(minX - 16, minY - 16, maxX - minX + 1, maxY - minY + 1);
            }

            _playerSpriteBoundsCache[cacheKey] = bounds;
            return bounds;
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

        public Rectangle GetAnimatedIconSourceRect(Texture2D texture, GameTime gameTime)
        {
            if (texture == null) return Rectangle.Empty;

            int frameSize = texture.Height;
            if (frameSize == 0) return Rectangle.Empty;

            int frameCount = texture.Width / frameSize;
            if (frameCount <= 1) return new Rectangle(0, 0, texture.Width, texture.Height);

            const float frameDuration = 0.15f;
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

        public Rectangle GetStunnedAnimRect(GameTime gameTime)
        {
            const int frameWidth = 16;
            const int frameCount = 3;
            const float frameDuration = 0.15f;

            int frameIndex = (int)(gameTime.TotalGameTime.TotalSeconds / frameDuration) % frameCount;
            return new Rectangle(frameIndex * frameWidth, 0, frameWidth, 16);
        }
    }
}