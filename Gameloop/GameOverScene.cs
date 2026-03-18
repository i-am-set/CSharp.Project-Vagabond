using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Scenes
{
    public class GameOverScene : GameScene
    {
        private readonly Global _global;
        private readonly SceneManager _sceneManager;
        private readonly TransitionManager _transitionManager;
        private readonly InputManager _inputManager;
        private readonly HapticsManager _hapticsManager;

        private Button _mainMenuButton;
        private NavigationGroup _navigationGroup;
        private float _timer;

        public GameOverScene()
        {
            _global = ServiceLocator.Get<Global>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _navigationGroup = new NavigationGroup(wrapNavigation: false);
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            base.Initialize();

            _mainMenuButton = new Button(
                new Rectangle(Global.VIRTUAL_WIDTH / 2 - 50, Global.VIRTUAL_HEIGHT - 60, 100, 20),
                "MAIN MENU",
                font: ServiceLocator.Get<Core>().SecondaryFont
            )
            {
                HoverAnimation = HoverAnimationType.Hop,
                TriggerHapticOnHover = false
            };

            _mainMenuButton.OnClick += () =>
            {
                if (_transitionManager.IsTransitioning) return;
                _hapticsManager.TriggerZoomPulse(_global.HapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff);
            };

            _navigationGroup.Add(_mainMenuButton);
        }

        public override void Enter()
        {
            base.Enter();
            _timer = 0f;
            _mainMenuButton.SetHiddenForEntrance();

            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
            {
                _navigationGroup.SelectFirst();
            }
            else
            {
                _navigationGroup.DeselectAll();
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);

            if (_transitionManager.IsTransitioning) return;

            float dt = (float)effectiveGameTime.ElapsedGameTime.TotalSeconds;
            _timer += dt;

            if (_timer >= 1.5f && !_mainMenuButton.Plink.IsActive && _mainMenuButton.HoverAnimator.CurrentOffset == 0f && !_mainMenuButton.IsHovered)
            {
                if (_mainMenuButton.Plink.Scale == 0f)
                {
                    _mainMenuButton.PlayEntrance(0f);
                }
            }

            if (_timer >= 1.5f)
            {
                var mouseState = _inputManager.GetEffectiveMouseState();
                _mainMenuButton.Update(mouseState);

                if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
                {
                    _navigationGroup.DeselectAll();
                }
                else
                {
                    _navigationGroup.UpdateInput(_inputManager);
                }
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);

            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off);

            var defaultFont = ServiceLocator.Get<Core>().DefaultFont;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            if (_timer > 0.5f)
            {
                string title = "BANKRUPT";
                Vector2 titleSize = defaultFont.MeasureString(title);
                Vector2 titlePos = new Vector2(Global.VIRTUAL_WIDTH / 2f - titleSize.X / 2f, 50);
                spriteBatch.DrawStringSnapped(defaultFont, title, titlePos, _global.Palette_Rust);
            }

            if (_timer > 1.0f)
            {
                string sub = "You cannot afford the next entry fee.";
                Vector2 subSize = secondaryFont.MeasureString(sub);
                Vector2 subPos = new Vector2(Global.VIRTUAL_WIDTH / 2f - subSize.X / 2f, 75);
                spriteBatch.DrawStringSnapped(secondaryFont, sub, subPos, _global.Palette_DarkPale);
            }

            if (_timer >= 1.5f)
            {
                _mainMenuButton.Draw(spriteBatch, secondaryFont, effectiveGameTime, transform);
            }
        }
    }
}