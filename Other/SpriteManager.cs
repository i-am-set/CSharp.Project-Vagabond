using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond
{
    public class SpriteManager
    {
        private readonly Core _core;
        private readonly TextureFactory _textureFactory;

        private readonly Dictionary<string, (Texture2D Texture, Rectangle[] Frames)> _cursorSprites = new Dictionary<string, (Texture2D, Rectangle[])>();

        private Texture2D _logoSprite;
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
        public Texture2D HealParticleSprite { get; private set; }

        public Texture2D FastForwardIcon { get; private set; }
        public Texture2D TitleLogoSpriteSheet { get; private set; }
        public Texture2D ShopXIcon { get; private set; }
        public Texture2D HealthHearts7x6SpriteSheet { get; private set; }
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
        public Texture2D CountdownNumbersSpriteSheet { get; private set; }

        public Texture2D ScoundrelCardsSpriteSheet { get; private set; }
        public Rectangle[,] ScoundrelCardRects { get; private set; }

        public Texture2D LogoSprite => _logoSprite;
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
            TitleLogoSpriteSheet = LoadTex("Sprites/UI/Splash/cwwc_logo_spritesheet", 240, 160, Color.Magenta);
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

            CountdownNumbersSpriteSheet = LoadTex("Sprites/UI/BasicIcons/Countdown_3_2_1_Numbers", 96, 32, Color.Magenta);

            ScoundrelCardsSpriteSheet = LoadTex("Sprites/Cards/base_cards_36x50_spritesheet", 504, 200, Color.Magenta);
            ScoundrelCardRects = new Rectangle[4, 14];
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 14; c++)
                {
                    ScoundrelCardRects[r, c] = new Rectangle(c * 36, r * 50, 36, 50);
                }
            }

            ShopXIcon = LoadTex("Sprites/UI/BasicIcons/X_32x32", 32, 32, Color.Red);
            HealthHearts7x6SpriteSheet = LoadTex("Sprites/UI/BattleUI/health_7x6_icon_spritesheet", 42, 6, Color.Red);

            try { NoiseTexture = _textureFactory.CreateNoiseTexture(256, 256); }
            catch { NoiseTexture = _textureFactory.CreateColoredTexture(256, 256, Color.Gray); }

            try { HealParticleSprite = _core.Content.Load<Texture2D>("Sprites/Particles/heal_heart"); }
            catch { HealParticleSprite = _textureFactory.CreateHeartParticleTexture(); }
            LoadAndCacheCursorSprite("cursor_default");
            LoadAndCacheCursorSprite("cursor_hover_clickable");
            LoadAndCacheCursorSprite("cursor_hover_clickable_hint");
            LoadAndCacheCursorSprite("cursor_hover_hint");
            LoadAndCacheCursorSprite("cursor_dragging_draggable");
        }

        public void LoadGameContent()
        {
            try { _emptySprite = _textureFactory.CreateEmptyTexture(); }
            catch { _emptySprite = _textureFactory.CreateColoredTexture(8, 8, Color.Red); }

            _speedMarkSprite = LoadTex("Sprites/speedMark", 8, 8, Color.Red);
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
    }
}