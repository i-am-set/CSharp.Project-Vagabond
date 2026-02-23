using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class NewGameIntroScene : GameScene
    {
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly InputManager _inputManager;
        private readonly SceneManager _sceneManager;
        private readonly TransitionManager _transitionManager;
        private readonly HapticsManager _hapticsManager;

        // Carousel State
        private List<string> _characterIds = new();
        private int _focusedIndex = 0;
        private float _carouselSlideOffset = 0f;
        private const float CAROUSEL_SLIDE_SPEED = 15f;

        // Spacing Tuning
        private const int SPACING_CENTER = 48;
        private const int SPACING_OUTER = 32;

        // UI Elements
        private Button _leftArrow;
        private Button _rightArrow;
        private Button _selectButton;
        private readonly NavigationGroup _navigationGroup;

        // Intro Text State
        private const string INTRO_TEXT = "CHOOSE AN ADVENTURER";
        private string _currentText = "";
        private float _textTimer = 0f;
        private int _textIndex = 0;
        private const float TYPEWRITER_SPEED = 0.01f;
        private float _titleWaveTimer = 0f;

        // Idle Animation State
        private float _idleTimer = 0f;

        private enum IntroState
        {
            TypingText,
            Selection
        }

        private IntroState _currentState = IntroState.TypingText;

        public NewGameIntroScene()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _navigationGroup = new NavigationGroup(wrapNavigation: true);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Enter()
        {
            base.Enter();
            InitializeData();
            InitializeUI();

            _currentState = IntroState.TypingText;
            _currentText = "";
            _textIndex = 0;
            _textTimer = 0f;
            _carouselSlideOffset = 0f;
            _titleWaveTimer = 0f;
            _idleTimer = 0f;

            _navigationGroup.DeselectAll();

            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
            {
                _navigationGroup.SelectFirst();
            }
        }

        private void InitializeData()
        {
            _characterIds = BattleDataCache.PartyMembers.Keys.ToList();
            _characterIds.Sort((a, b) =>
            {
                if (int.TryParse(a, out int idA) && int.TryParse(b, out int idB))
                    return idA.CompareTo(idB);
                return string.Compare(a, b, StringComparison.Ordinal);
            });

            int oakleyIndex = _characterIds.IndexOf("0");
            _focusedIndex = oakleyIndex != -1 ? oakleyIndex : 0;
        }

        private void InitializeUI()
        {
            _navigationGroup.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            int centerX = Global.VIRTUAL_WIDTH / 2;
            int centerY = 80;

            // Visual Y for arrows (centered vertically on the sprite row)
            int arrowY = centerY;

            int halfWidth = Global.VIRTUAL_WIDTH / 2;
            int buttonHeight = 16; // FIXED: 16px height
            int buttonY = arrowY - (buttonHeight / 2);

            // Calculate Text Offsets
            // We want the text to be 10px away from the center of the screen.
            // Left Arrow Text X: CenterX - 10 - TextWidth/2 (approx)
            // Right Arrow Text X: CenterX + 10 + TextWidth/2 (approx)

            // Button Center X (Left): VW / 4
            // Button Center X (Right): 3 * VW / 4

            // Offset = DesiredScreenX - ButtonCenterX

            // Left Arrow:
            // Desired X is CenterX - 20 (10px gap + approx 10px for half sprite width/padding)
            // Let's just use a hard offset from the button center to the screen center minus gap.
            float leftButtonCenterX = halfWidth / 2f;
            float leftTextTargetX = centerX - 24; // 16px (half sprite) + 8px gap
            float leftOffset = leftTextTargetX - leftButtonCenterX;

            _leftArrow = new Button(
                new Rectangle(0, buttonY, halfWidth, buttonHeight),
                "<",
                font: secondaryFont
            )
            {
                TriggerHapticOnHover = true,
                EnableHoverSway = true, // FIXED: Enable sway for hover lift
                HoverAnimation = HoverAnimationType.Lift, // FIXED: Use Lift animation
                HoverLiftOffset = -1f, // FIXED: Move up 1 pixel on hover
                EnableTextWave = false,
                TextRenderOffset = new Vector2(leftOffset, 0)
            };
            _leftArrow.OnClick += () => CycleCharacter(-1);
            _navigationGroup.Add(_leftArrow);

            // Right Arrow:
            float rightButtonCenterX = halfWidth + (halfWidth / 2f);
            float rightTextTargetX = centerX + 24; // 16px (half sprite) + 8px gap
            float rightOffset = rightTextTargetX - rightButtonCenterX;

            _rightArrow = new Button(
                new Rectangle(halfWidth, buttonY, halfWidth, buttonHeight),
                ">",
                font: secondaryFont
            )
            {
                TriggerHapticOnHover = true,
                EnableHoverSway = true, // FIXED: Enable sway for hover lift
                HoverAnimation = HoverAnimationType.Lift, // FIXED: Use Lift animation
                HoverLiftOffset = -1f, // FIXED: Move up 1 pixel on hover
                EnableTextWave = false,
                TextRenderOffset = new Vector2(rightOffset, 0)
            };
            _rightArrow.OnClick += () => CycleCharacter(1);
            _navigationGroup.Add(_rightArrow);

            // --- Select Button ---
            string selectText = "SELECT";
            Vector2 selectSize = secondaryFont.MeasureString(selectText);
            int selectW = (int)selectSize.X + 10;
            int selectH = (int)selectSize.Y + 6;

            _selectButton = new Button(
                new Rectangle(centerX - selectW / 2, centerY + 35, selectW, selectH),
                selectText,
                font: secondaryFont
            )
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop
            };
            _selectButton.OnClick += ConfirmSelection;
            _navigationGroup.Add(_selectButton);

            _selectButton.NeighborLeft = _leftArrow;
            _selectButton.NeighborRight = _rightArrow;
            _selectButton.NeighborUp = _leftArrow;

            _leftArrow.NeighborRight = _selectButton;
            _leftArrow.NeighborDown = _selectButton;

            _rightArrow.NeighborLeft = _selectButton;
            _rightArrow.NeighborDown = _selectButton;
        }

        private void CycleCharacter(int direction)
        {
            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

            _focusedIndex += direction;

            if (_focusedIndex < 0) _focusedIndex = _characterIds.Count - 1;
            if (_focusedIndex >= _characterIds.Count) _focusedIndex = 0;

            _carouselSlideOffset = direction;
        }

        private void ConfirmSelection()
        {
            if (_transitionManager.IsTransitioning) return;
            if (_characterIds.Count == 0) return;

            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

            string selectedId = _characterIds[_focusedIndex];
            var core = ServiceLocator.Get<Core>();
            var gameState = ServiceLocator.Get<GameState>();

            var loadingTasks = new List<LoadingTask>
            {
                new GenericTask("Initializing world...", () =>
                {
                    gameState.InitializeWorld(selectedId);
                })
            };

            core.SetGameLoaded(true);

            var transitionOut = TransitionType.None;
            var transitionIn = _transitionManager.GetRandomTransition();
            _sceneManager.ChangeScene(GameSceneState.Split, transitionOut, transitionIn, 0f, loadingTasks);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_transitionManager.IsTransitioning) return;

            _carouselSlideOffset = MathHelper.Lerp(_carouselSlideOffset, 0f, dt * CAROUSEL_SLIDE_SPEED);
            if (Math.Abs(_carouselSlideOffset) < 0.01f) _carouselSlideOffset = 0f;

            _idleTimer += dt;
            _titleWaveTimer += dt;

            if (_currentState == IntroState.TypingText)
            {
                UpdateTypewriter(dt);
                HandleSkipping();
            }
            else if (_currentState == IntroState.Selection)
            {
                var currentMouseState = _inputManager.GetEffectiveMouseState();

                _selectButton.Update(currentMouseState);

                if (!_selectButton.IsHovered)
                {
                    _leftArrow.Update(currentMouseState);
                    _rightArrow.Update(currentMouseState);
                }
                else
                {
                    _leftArrow.IsHovered = false;
                    _rightArrow.IsHovered = false;
                }

                if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
                {
                    _navigationGroup.UpdateInput(_inputManager);

                    if (!_leftArrow.IsSelected && !_rightArrow.IsSelected && !_selectButton.IsSelected)
                    {
                        if (_inputManager.NavigateLeft) CycleCharacter(-1);
                        if (_inputManager.NavigateRight) CycleCharacter(1);
                        if (_inputManager.Confirm) ConfirmSelection();
                    }
                }

                if (_inputManager.Back)
                {
                    _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.None, TransitionType.None);
                }
            }
        }

        private void UpdateTypewriter(float dt)
        {
            _textTimer += dt;
            if (_textTimer >= TYPEWRITER_SPEED)
            {
                _textTimer = 0f;
                if (_textIndex < INTRO_TEXT.Length)
                {
                    _currentText += INTRO_TEXT[_textIndex];
                    _textIndex++;
                }
                else
                {
                    _currentState = IntroState.Selection;
                }
            }
        }

        private void HandleSkipping()
        {
            bool skip = _inputManager.Confirm || (_inputManager.IsMouseActive && _inputManager.GetEffectiveMouseState().LeftButton == ButtonState.Pressed);
            if (skip)
            {
                if (_inputManager.IsMouseActive) _inputManager.ConsumeMouseClick();
                _currentText = INTRO_TEXT;
                _currentState = IntroState.Selection;
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, null, null, null, transform);
            spriteBatch.Draw(_spriteManager.EmptySprite, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            if (!string.IsNullOrEmpty(_currentText))
            {
                Vector2 textSize = font.MeasureString(INTRO_TEXT);
                var pos = new Vector2((Global.VIRTUAL_WIDTH - textSize.X) / 2, 24);
                TextAnimator.DrawTextWithEffect(spriteBatch, font, _currentText, pos, _global.Palette_White, TextEffectType.RainbowWave, _titleWaveTimer);
            }

            if (_currentState == IntroState.Selection && _characterIds.Count > 0)
            {
                DrawCarousel(spriteBatch, font);

                _leftArrow.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                _rightArrow.Draw(spriteBatch, font, gameTime, Matrix.Identity);
                _selectButton.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }
        }

        private void DrawCarousel(SpriteBatch spriteBatch, BitmapFont font)
        {
            int centerX = Global.VIRTUAL_WIDTH / 2;
            int centerY = 80;
            var sheet = _spriteManager.PlayerMasterSpriteSheet;
            int count = _characterIds.Count;

            int[] drawOrder = { -3, 3, -2, 2, -1, 1, 0 };

            foreach (int offset in drawOrder)
            {
                int charIndex = (_focusedIndex + offset) % count;
                if (charIndex < 0) charIndex += count;

                string charId = _characterIds[charIndex];
                bool isCenter = (offset == 0);

                float visualOffset = offset + _carouselSlideOffset;

                float xPos = centerX;
                if (visualOffset != 0)
                {
                    float dir = Math.Sign(visualOffset);
                    float absOffset = Math.Abs(visualOffset);

                    float dist = SPACING_CENTER;

                    if (absOffset > 1f)
                    {
                        dist += (absOffset - 1f) * SPACING_OUTER;
                    }
                    else
                    {
                        dist *= absOffset;
                    }

                    xPos += dir * dist;
                }

                int spriteIndex = int.Parse(charId);

                PlayerSpriteType spriteType;
                if (Math.Abs(offset) >= 3) spriteType = PlayerSpriteType.Portrait5x5;
                else if (Math.Abs(offset) >= 1) spriteType = PlayerSpriteType.Portrait8x8;
                else spriteType = PlayerSpriteType.Normal;

                float yPos = centerY;
                if (isCenter)
                {
                    float bob = MathF.Sin(_idleTimer * 4f);
                    yPos += (bob > 0 ? -1f : 0f);

                    if (bob > 0) spriteType = PlayerSpriteType.Alt;
                }

                var sourceRect = _spriteManager.GetPlayerSourceRect(spriteIndex, spriteType);

                float opacity = isCenter ? 1.0f : 0.6f;
                if (Math.Abs(offset) >= 2) opacity = 0.3f;

                Vector2 position = new Vector2(MathF.Round(xPos), MathF.Round(yPos));
                Vector2 origin = new Vector2(16, 16);

                spriteBatch.Draw(
                    sheet,
                    position,
                    sourceRect,
                    Color.White * opacity,
                    0f,
                    origin,
                    1.0f,
                    SpriteEffects.None,
                    0f
                );

                if (isCenter && BattleDataCache.PartyMembers.TryGetValue(charId, out var data))
                {
                    string name = data.Name.ToUpper();
                    Vector2 nameSize = font.MeasureString(name);
                    Vector2 namePos = new Vector2(centerX - nameSize.X / 2, centerY + 20);
                    spriteBatch.DrawStringSnapped(font, name, namePos, _global.Palette_DarkPale);
                }
            }
        }
    }
}