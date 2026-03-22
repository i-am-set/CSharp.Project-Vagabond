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

            // Future proofing: Add slides here. 
            // For now, just one empty slide that lasts 2 seconds.
            _slides.Add(new StartupSlide
            {
                Duration = 2.0f,
                DrawAction = (sb, font, gt, transform) => { }
            });
        }

        public override void Enter()
        {
            base.Enter();
            _currentSlideIndex = 0;
            _slideTimer = 0f;
            _transitionTriggered = false;
            _lastMouseState = _inputManager.GetEffectiveMouseState();

            // Start the ambient startup sound
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
            _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff);
        }

        private void FinishStartup()
        {
            _transitionTriggered = true;
            // Do NOT force transition the audio here, let it play out naturally and transition on its own
            _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff);
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off);

            if (_currentSlideIndex < _slides.Count)
            {
                _slides[_currentSlideIndex].DrawAction?.Invoke(spriteBatch, font, gameTime, transform);
            }
        }
    }
}