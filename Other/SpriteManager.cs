using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ProjectVagabond
{
    public class SpriteManager
    {
        private readonly Core _core;
        private readonly TextureFactory _textureFactory;

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
        private Texture2D _shortRestSprite;
        private Texture2D _longRestSprite;
        private Texture2D _emptySprite;
        private Texture2D _speedMarkSprite;
        private Texture2D _worldMapHoverSelectorSprite;
        private Texture2D _circleTextureSprite;
        private Texture2D _settingsIconSprite;
        private Texture2D _turnIndicatorSprite;
        private Texture2D _handIdleSprite;
        private Texture2D _handHoldSprite;
        private Texture2D _enemySprite;
        private Texture2D _cardBaseSprite;
        public Effect CardShaderEffect { get; private set; }

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
        public Texture2D ShortRestSprite => _shortRestSprite;
        public Texture2D LongRestSprite => _longRestSprite;
        public Texture2D EmptySprite => _emptySprite;
        public Texture2D SpeedMarkSprite => _speedMarkSprite;
        public Texture2D WorldMapHoverSelectorSprite => _worldMapHoverSelectorSprite;
        public Texture2D CircleTextureSprite => _circleTextureSprite;
        public Texture2D SettingsIconSprite => _settingsIconSprite;
        public Texture2D TurnIndicatorSprite => _turnIndicatorSprite;
        public Texture2D HandIdleSprite => _handIdleSprite;
        public Texture2D HandHoldSprite => _handHoldSprite;
        public Texture2D EnemySprite => _enemySprite;
        public Texture2D CardBaseSprite => _cardBaseSprite;

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

            try { _worldMapHoverSelectorSprite = _core.Content.Load<Texture2D>("Sprites/UI/ui_world_map_selector"); }
            catch { _worldMapHoverSelectorSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _circleTextureSprite = _textureFactory.CreateCircleTexture(); }
            catch { _circleTextureSprite = _textureFactory.CreateColoredTexture(16, 16, Color.Red); }

            try { _settingsIconSprite = _core.Content.Load<Texture2D>("Sprites/UI/ButtonIcons/ui_settings_icon"); }
            catch { _settingsIconSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _turnIndicatorSprite = _core.Content.Load<Texture2D>("Sprites/UI/ui_turn_indicator"); }
            catch { _turnIndicatorSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }
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

            try { _shortRestSprite = _core.Content.Load<Texture2D>("Sprites/shortRest"); }
            catch { _shortRestSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _longRestSprite = _core.Content.Load<Texture2D>("Sprites/longRest"); }
            catch { _longRestSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _emptySprite = _textureFactory.CreateEmptyTexture(); }
            catch { _emptySprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            try { _speedMarkSprite = _core.Content.Load<Texture2D>("Sprites/speedMark"); }
            catch { _speedMarkSprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            // Load placeholder combat hand sprites
            try { _handIdleSprite = _core.Content.Load<Texture2D>("Sprites/Combat/hand_idle"); }
            catch { _handIdleSprite = _textureFactory.CreateColoredTexture(128, 256, Color.DarkGray); }

            try { _handHoldSprite = _core.Content.Load<Texture2D>("Sprites/Combat/hand_hold"); }
            catch { _handHoldSprite = _textureFactory.CreateColoredTexture(128, 256, Color.CornflowerBlue); }

            try { _enemySprite = _core.Content.Load<Texture2D>("Sprites/Combat/enemy_placeholder"); }
            catch { _enemySprite = _textureFactory.CreateEnemyPlaceholderTexture(); }

            try { _cardBaseSprite = _core.Content.Load<Texture2D>("Sprites/Cards/card_base"); }
            catch { _cardBaseSprite = _textureFactory.CreateColoredTexture(120, 168, Color.Magenta); }

            try { CardShaderEffect = _core.Content.Load<Effect>("Shaders/CardShader"); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ERROR] Could not load shader 'Shaders/CardShader'. Please ensure it's in the Content project. {ex.Message}"); }
        }

        [Obsolete("LoadSpriteContent is deprecated, please use LoadEssentialContent and LoadGameContent instead.")]
        public void LoadSpriteContent()
        {
            LoadEssentialContent();
            LoadGameContent();
        }
    }
}
