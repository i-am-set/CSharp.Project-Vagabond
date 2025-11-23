using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class MainMenuScene : GameScene
    {
        private readonly SceneManager _sceneManager;
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly ParticleSystemManager _particleSystemManager;

        private readonly List<Button> _buttons = new();
        private int _selectedButtonIndex = -1;

        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private ConfirmationDialog _confirmationDialog;
        private bool _uiInitialized = false;

        public MainMenuScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            _confirmationDialog = new ConfirmationDialog(this);
        }

        private void InitializeUI()
        {
            if (_uiInitialized) return;

            _buttons.Clear();

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            const int horizontalPadding = 4;
            const int verticalPadding = 2;
            const int buttonYSpacing = 0; // Vertical gap between buttons
            float currentY = 90f;

            // Define text for buttons
            string playText = "PLAY";
            string settingsText = "SETTINGS";
            string exitText = "EXIT";

            // Measure all texts to determine the widest button
            Vector2 playSize = secondaryFont.MeasureString(playText);
            Vector2 settingsSize = secondaryFont.MeasureString(settingsText);
            Vector2 exitSize = secondaryFont.MeasureString(exitText);

            // Calculate the maximum width required
            int maxTextWidth = (int)Math.Max(playSize.X, Math.Max(settingsSize.X, exitSize.X));
            int buttonWidth = maxTextWidth + horizontalPadding * 2;

            // Calculate a common X position to center the column of buttons, then shift left by 80
            int buttonX = ((Global.VIRTUAL_WIDTH - buttonWidth) / 2) - 3 - 80;

            // --- PLAY Button ---
            int playHeight = (int)playSize.Y + verticalPadding * 2;
            var playButton = new Button(
                new Rectangle(buttonX, (int)currentY, buttonWidth, playHeight),
                playText,
                font: secondaryFont,
                alignLeft: true
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                HoverAnimation = HoverAnimationType.SlideAndHold
            };
            playButton.OnClick += () =>
            {
                var core = ServiceLocator.Get<Core>();
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
                var gameState = ServiceLocator.Get<GameState>();
                var loadingScreen = ServiceLocator.Get<LoadingScreen>();

                // Create a list of loading tasks
                var loadingTasks = new List<LoadingTask>
                {
                    new GenericTask("Loading game assets...", () => spriteManager.LoadGameContent()),
                    new GenericTask("Loading archetypes...", () => archetypeManager.LoadArchetypes("Content/Data/Archetypes")),
                    new GenericTask("Initializing world...", () =>
                    {
                        gameState.InitializeWorld();
                        gameState.InitializeRenderableEntities();
                    }),
                    new DiceWarmupTask() // This will run the hidden dice roll
                };

                // Add tasks to the loading screen
                loadingScreen.Clear();
                foreach (var task in loadingTasks)
                {
                    loadingScreen.AddTask(task);
                }

                // Define what happens when loading is complete
                loadingScreen.OnComplete += () =>
                {
                    core.SetGameLoaded(true);
                    _sceneManager.ChangeScene(GameSceneState.Split);
                };

                // Start the loading process
                loadingScreen.Start();
            };
            _buttons.Add(playButton);
            currentY += playHeight + buttonYSpacing;

            // --- SETTINGS Button ---
            int settingsHeight = (int)settingsSize.Y + verticalPadding * 2;
            var settingsButton = new Button(
                new Rectangle(buttonX, (int)currentY, buttonWidth, settingsHeight),
                settingsText,
                font: secondaryFont,
                alignLeft: true
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                HoverAnimation = HoverAnimationType.SlideAndHold
            };
            settingsButton.OnClick += () => _sceneManager.ShowModal(GameSceneState.Settings);
            _buttons.Add(settingsButton);
            currentY += settingsHeight + buttonYSpacing;

            // --- EXIT Button ---
            int exitHeight = (int)exitSize.Y + verticalPadding * 2;
            var exitButton = new Button(
                new Rectangle(buttonX, (int)currentY, buttonWidth, exitHeight),
                exitText,
                font: secondaryFont,
                alignLeft: true
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                HoverAnimation = HoverAnimationType.SlideAndHold
            };
            exitButton.OnClick += ConfirmExit;
            _buttons.Add(exitButton);

            _uiInitialized = true;
        }

        private void ConfirmExit()
        {
            _confirmationDialog.Show(
                "Are you sure you want to exit?",
                new List<Tuple<string, Action>>
                {
                    Tuple.Create("[gray]YES", new Action(() => _core.ExitApplication())),
                    Tuple.Create("NO", new Action(() => _confirmationDialog.Hide()))
                }
            );
        }

        public override void Enter()
        {
            base.Enter();
            InitializeUI(); // This will now run safely after fonts are loaded.

            _currentInputDelay = _inputDelay;
            _previousKeyboardState = Keyboard.GetState();

            // Reset animation states of all buttons
            foreach (var button in _buttons)
            {
                button.ResetAnimationState();
            }

            if (this.LastUsedInputForNav == InputDevice.Keyboard && !firstTimeOpened)
            {
                _selectedButtonIndex = 0;
                PositionMouseOnFirstSelectable();

                var firstButtonBounds = GetFirstSelectableElementBounds();
                if (firstButtonBounds.HasValue)
                {
                    Point screenPos = Core.TransformVirtualToScreen(firstButtonBounds.Value.Center);
                    var fakeMouseState = new MouseState(screenPos.X, screenPos.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    Vector2 virtualPos = Core.TransformMouse(fakeMouseState.Position);

                    foreach (var button in _buttons)
                    {
                        button.UpdateHoverState(virtualPos);
                    }
                }
            }
            else
            {
                _selectedButtonIndex = -1;
            }

            firstTimeOpened = false;
        }

        public override void Exit()
        {
            base.Exit();
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
            if (_buttons.Count > 0)
            {
                return _buttons[0].Bounds;
            }
            return null;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var currentMouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            if (IsInputBlocked)
            {
                return;
            }

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Update(gameTime);
                return;
            }

            var currentKeyboardState = Keyboard.GetState();

            if (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                _sceneManager.LastInputDevice = InputDevice.Mouse;
            }

            if (_currentInputDelay > 0)
            {
                _currentInputDelay -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Update(currentMouseState);
                if (_buttons[i].IsHovered)
                {
                    _selectedButtonIndex = i;
                }
            }

            if (_currentInputDelay <= 0)
            {
                bool upPressed = KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState);
                bool downPressed = KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState);

                if (upPressed || downPressed)
                {
                    if (_selectedButtonIndex <= -1) { _selectedButtonIndex = 0; }

                    _sceneManager.LastInputDevice = InputDevice.Keyboard;
                    var selectedButton = _buttons[_selectedButtonIndex];
                    if (selectedButton.IsHovered)
                    {
                        if (upPressed) _selectedButtonIndex = (_selectedButtonIndex - 1 + _buttons.Count) % _buttons.Count;
                        else _selectedButtonIndex = (_selectedButtonIndex + 1) % _buttons.Count;

                        Point screenPos = Core.TransformVirtualToScreen(_buttons[_selectedButtonIndex].Bounds.Center);
                        Mouse.SetPosition(screenPos.X, screenPos.Y);

                        keyboardNavigatedLastFrame = true;
                    }
                    else
                    {
                        Point screenPos = Core.TransformVirtualToScreen(selectedButton.Bounds.Center);
                        Mouse.SetPosition(screenPos.X, screenPos.Y);

                        keyboardNavigatedLastFrame = true;
                    }
                }

                if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
                {
                    if (_selectedButtonIndex <= -1) _selectedButtonIndex = 0;

                    _sceneManager.LastInputDevice = InputDevice.Keyboard;
                    var selectedButton = _buttons[_selectedButtonIndex];
                    if (selectedButton.IsHovered)
                    {
                        selectedButton.TriggerClick();
                    }
                    else
                    {
                        Point screenPos = Core.TransformVirtualToScreen(selectedButton.Bounds.Center);
                        Mouse.SetPosition(screenPos.X, screenPos.Y);

                        keyboardNavigatedLastFrame = true;
                    }
                }

                if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
                {
                    ConfirmExit();
                }
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            spriteBatch.DrawSnapped(_spriteManager.LogoSprite, new Vector2(screenWidth / 2 - _spriteManager.LogoSprite.Width / 2, 25), Color.White);

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool forceHover = (i == _selectedButtonIndex) && (_sceneManager.LastInputDevice == InputDevice.Keyboard || keyboardNavigatedLastFrame);
                _buttons[i].Draw(spriteBatch, font, gameTime, transform, forceHover);
            }

            if (_selectedButtonIndex >= 0 && _selectedButtonIndex < _buttons.Count)
            {
                var selectedButton = _buttons[_selectedButtonIndex];
                if (selectedButton.IsHovered)
                {
                    var bounds = selectedButton.Bounds;
                    var color = _global.ButtonHoverColor;
                    var fontToUse = selectedButton.Font ?? secondaryFont;

                    string leftArrow = ">";
                    var arrowSize = fontToUse.MeasureString(leftArrow);

                    float pressOffset = selectedButton.IsPressed ? 2f : 0f;

                    var leftPos = new Vector2(bounds.Left - arrowSize.Width - 4 + pressOffset, bounds.Center.Y - arrowSize.Height / 2f + selectedButton.TextRenderOffset.Y);

                    spriteBatch.DrawStringSnapped(fontToUse, leftArrow, leftPos, color);
                }
            }

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.DrawContent(spriteBatch, font, gameTime, transform);
            }
        }

        public override void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            // This scene no longer draws particles directly. The Core engine handles it.
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.DrawOverlay(spriteBatch);
            }
        }
    }
}