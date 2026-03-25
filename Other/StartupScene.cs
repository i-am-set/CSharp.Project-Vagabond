using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Transitions;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Scenes
{
    public class StartupSlide
    {
        public float Duration { get; set; }
        public Action<SpriteBatch, BitmapFont, GameTime, Matrix> DrawAction { get; set; }
    }

    public class StartupScene : GameScene
    {
        private readonly SceneManager _sceneManager;
        private readonly Global _global;
        private readonly TransitionManager _transitionManager;
        private readonly InputManager _inputManager;

        private List<StartupSlide> _slides = new List<StartupSlide>();
        private int _currentSlideIndex = 0;
        private float _slideTimer = 0f;
        private bool _transitionTriggered = false;
        private MouseState _lastMouseState;

        public StartupScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _global = ServiceLocator.Get<Global>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _inputManager = ServiceLocator.Get<InputManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            base.Initialize();

            var core = ServiceLocator.Get<Core>();
            var pixel = ServiceLocator.Get<Texture2D>();

            _slides.Add(new StartupSlide
            {
                Duration = 1.0f,
                DrawAction = (sb, font, gt, transform) => { }
            });

            _slides.Add(new StartupSlide
            {
                Duration = 2.0f,
                DrawAction = (sb, font, gt, transform) =>
                {
                    sb.DrawStringSnapped(core.TertiaryFont, "WAIT", new Vector2(16, 16), _global.Palette_LightPale);
                }
            });

            _slides.Add(new StartupSlide
            {
                Duration = 4.0f,
                DrawAction = (sb, font, gt, transform) =>
                {
                    var defFont = core.DefaultFont;
                    var secFont = core.SecondaryFont;
                    var tertFont = core.TertiaryFont;

                    string statusText = "STATUS ";
                    string okText = "OK";
                    Vector2 statusSize = secFont.MeasureString(statusText);
                    Vector2 okSize = secFont.MeasureString(okText);

                    int rectW = (int)(statusSize.X + okSize.X) + 32;
                    int rectH = (int)Math.Max(statusSize.Y, okSize.Y) + 6;
                    int rectX = (Global.VIRTUAL_WIDTH - rectW) / 2;
                    int rectY = (Global.VIRTUAL_HEIGHT - rectH) / 2;

                    Vector2 statusPos = new Vector2(
                        rectX + 16,
                        (Global.VIRTUAL_HEIGHT - statusSize.Y) / 2f
                    );
                    sb.DrawStringSnapped(secFont, statusText, statusPos, _global.Palette_LightPale);

                    float cycle = (float)gt.TotalGameTime.TotalSeconds % 2.0f;
                    float okAlpha = cycle < 1.0f ? 1.0f : 0.0f;

                    Vector2 okPos = new Vector2(statusPos.X + statusSize.X, statusPos.Y);
                    sb.DrawStringSnapped(defFont, okText, okPos, _global.Palette_Leaf * okAlpha);

                    string copy1 = "Firmware and Set-Up Screens Copyright @ 1991";
                    string copy2 = "Station Computing Corporation";

                    Vector2 copy1Size = tertFont.MeasureString(copy1);
                    Vector2 copy2Size = tertFont.MeasureString(copy2);

                    float copy1Y = Global.VIRTUAL_HEIGHT * 0.9f;
                    float copy2Y = copy1Y + tertFont.LineHeight + 2;

                    Vector2 copy1Pos = new Vector2((Global.VIRTUAL_WIDTH - copy1Size.X) / 2f, copy1Y);
                    Vector2 copy2Pos = new Vector2((Global.VIRTUAL_WIDTH - copy2Size.X) / 2f, copy2Y);

                    sb.DrawStringSnapped(tertFont, copy1, copy1Pos, _global.Palette_LightPale);
                    sb.DrawStringSnapped(tertFont, copy2, copy2Pos, _global.Palette_LightPale);
                }
            });
        }

        public override void Enter()
        {
            base.Enter();
            _currentSlideIndex = 0;
            _slideTimer = 0f;
            _transitionTriggered = false;
            _lastMouseState = _inputManager.GetEffectiveMouseState();

            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayAmbient("ambient_hdd_startup", 1.0f);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_transitionTriggered || _transitionManager.IsTransitioning) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var mouseState = _inputManager.GetEffectiveMouseState();

            bool mouseClicked = mouseState.LeftButton == ButtonState.Pressed && _lastMouseState.LeftButton == ButtonState.Released;

            if (_inputManager.Confirm || mouseClicked)
            {
                SkipStartup();
                return;
            }

            _lastMouseState = mouseState;

            if (_currentSlideIndex < _slides.Count)
            {
                _slideTimer += dt;
                if (_slideTimer >= _slides[_currentSlideIndex].Duration)
                {
                    _slideTimer = 0f;
                    _currentSlideIndex++;

                    if (_currentSlideIndex >= _slides.Count)
                    {
                        FinishStartup();
                    }
                }
            }
            else
            {
                FinishStartup();
            }
        }

        private void SkipStartup()
        {
            _transitionTriggered = true;
            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().ForceTransitionAmbient("ambient_hdd_startup");
            _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.None, TransitionType.None);
        }

        private void FinishStartup()
        {
            _transitionTriggered = true;
            _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff);
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.DrawSnapped(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off);

            if (_currentSlideIndex < _slides.Count)
            {
                _slides[_currentSlideIndex].DrawAction?.Invoke(spriteBatch, font, gameTime, transform);
            }
        }
    }
}