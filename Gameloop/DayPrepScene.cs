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
    public class DayPrepScene : GameScene
    {
        private readonly Global _global;
        private readonly GameState _gameState;
        private readonly SceneManager _sceneManager;
        private readonly TransitionManager _transitionManager;
        private readonly InputManager _inputManager;
        private readonly HapticsManager _hapticsManager;

        private float _timer;
        private bool _plinksStarted;
        private bool _isBankrupt;

        private PlinkAnimator _plinkFee;
        private PlinkAnimator _plinkGold;
        private TextOverImageButton _proceedButton;
        private NavigationGroup _navigationGroup;

        public DayPrepScene()
        {
            _global = ServiceLocator.Get<Global>();
            _gameState = ServiceLocator.Get<GameState>();
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

            var textureFactory = ServiceLocator.Get<TextureFactory>();
            Texture2D btnBg = textureFactory.CreateColoredTexture(120, 24, _global.Palette_Sun);

            _proceedButton = new TextOverImageButton(
                new Rectangle(Global.VIRTUAL_WIDTH / 2 - 60, Global.VIRTUAL_HEIGHT - 50, 120, 24),
                "ENTER ARENA",
                btnBg,
                font: ServiceLocator.Get<Core>().DefaultFont,
                startVisible: false
            )
            {
                CustomDefaultTextColor = _global.Palette_Off,
                CustomHoverTextColor = _global.Palette_Off,
                TintBackgroundOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                TriggerHapticOnHover = true
            };

            _proceedButton.OnClick += OnProceedClicked;
            _navigationGroup.Add(_proceedButton);

            _plinkFee = new PlinkAnimator();
            _plinkGold = new PlinkAnimator();
        }

        public override void Enter()
        {
            base.Enter();
            _timer = 0f;
            _plinksStarted = false;
            _isBankrupt = _gameState.PlayerState.Gold < _gameState.CurrentEntryFee;

            _proceedButton.SetHiddenForEntrance();
            _plinkFee.Start(999f);
            _plinkGold.Start(999f);

            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
            {
                _navigationGroup.SelectFirst();
            }
            else
            {
                _navigationGroup.DeselectAll();
            }
        }

        private void OnProceedClicked()
        {
            if (_transitionManager.IsTransitioning) return;
            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

            _gameState.PlayerState.Gold -= _gameState.CurrentEntryFee;
            _sceneManager.ChangeScene(GameSceneState.Arena, _transitionManager.GetRandomTransition(), _transitionManager.GetRandomTransition());
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (_transitionManager.IsTransitioning) return;

            if (_isBankrupt)
            {
                _sceneManager.ChangeScene(GameSceneState.GameOver, TransitionType.FadeOff, TransitionType.FadeOff);
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _timer += dt;

            if (_timer >= 2.0f && !_plinksStarted)
            {
                _plinksStarted = true;
                _plinkFee.Start(0f, 0.3f);
                _plinkGold.Start(0.15f, 0.3f);
                _proceedButton.PlayEntrance(0.3f);
            }

            if (_plinksStarted)
            {
                _plinkFee.Update(gameTime, new Vector2(Global.VIRTUAL_WIDTH / 2f, 70));
                _plinkGold.Update(gameTime, new Vector2(Global.VIRTUAL_WIDTH / 2f, 90));

                var mouseState = _inputManager.GetEffectiveMouseState();
                _proceedButton.Update(mouseState);

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
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

            var defaultFont = ServiceLocator.Get<Core>().DefaultFont;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            string dayText = $"DAY {_gameState.CurrentDay}";
            Vector2 daySize = defaultFont.MeasureString(dayText);

            float startY = MathF.Round(Global.VIRTUAL_HEIGHT / 2f - daySize.Y / 2f);
            float endY = 20f;
            float currentY = startY;

            if (_timer > 1.0f)
            {
                float progress = Math.Clamp((_timer - 1.0f) / 1.0f, 0f, 1f);
                currentY = MathF.Round(MathHelper.Lerp(startY, endY, Easing.EaseInOutCubic(progress)));
            }

            Vector2 dayPos = new Vector2(MathF.Round(Global.VIRTUAL_WIDTH / 2f - daySize.X / 2f), currentY);
            spriteBatch.DrawStringSnapped(defaultFont, dayText, dayPos, _global.Palette_Sun);

            if (_plinksStarted)
            {
                if (_plinkFee.Scale > 0.01f)
                {
                    string feeText = $"ENTRY FEE: {_gameState.CurrentEntryFee}G";
                    Vector2 feeSize = secondaryFont.MeasureString(feeText);
                    Vector2 feePos = new Vector2(Global.VIRTUAL_WIDTH / 2f, 70);
                    Vector2 feeOrigin = new Vector2(MathF.Round(feeSize.X / 2f), MathF.Round(feeSize.Y / 2f));

                    spriteBatch.DrawStringSnapped(secondaryFont, feeText, feePos, _global.Palette_Rust, _plinkFee.Rotation, feeOrigin, _plinkFee.Scale, SpriteEffects.None, 0f);
                }

                if (_plinkGold.Scale > 0.01f)
                {
                    string goldText = $"CURRENT GOLD: {_gameState.PlayerState.Gold}G";
                    Vector2 goldSize = secondaryFont.MeasureString(goldText);
                    Vector2 goldPos = new Vector2(Global.VIRTUAL_WIDTH / 2f, 90);
                    Vector2 goldOrigin = new Vector2(MathF.Round(goldSize.X / 2f), MathF.Round(goldSize.Y / 2f));

                    spriteBatch.DrawStringSnapped(secondaryFont, goldText, goldPos, _global.Palette_Sky, _plinkGold.Rotation, goldOrigin, _plinkGold.Scale, SpriteEffects.None, 0f);
                }

                _proceedButton.Draw(spriteBatch, defaultFont, gameTime, transform);
            }
        }
    }
}