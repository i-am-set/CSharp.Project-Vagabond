using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
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
        private const string INTRO_LINE_1 = "CHOOSE AN";
        private const string INTRO_LINE_2 = "ADVENTURER";

        private string _currentText1 = "";
        private string _currentText2 = "";

        private float _textTimer = 0f;
        private int _textIndex = 0;
        private const float TYPEWRITER_SPEED = 0.02f;
        private float _titleWaveTimer = 0f;

        // Animation State Variables
        private float _centerSpriteAlpha = 0f;
        private float _surroundAnimTimer = 0f;
        private float _uiAlpha = 0f;

        // Idle Animation State
        private float _idleTimer = 0f;

        // Select Button Hop State
        private float _selectButtonHopTimer = 0f;
        private const float SELECT_HOP_DURATION = 0.0f;

        // Arrow Simulation State
        private float _leftArrowSimTimer = 0f;
        private float _rightArrowSimTimer = 0f;
        private const float ARROW_SIM_DURATION = 0.15f;

        // Cached Data for Display
        private (string Name, string Description)? _cachedAbilityInfo;

        // HP Normalization Cache
        private int _globalMinHP = 0;
        private int _globalMaxHP = 0;

        private enum IntroState
        {
            TypingTitle,
            FadeInCenter,
            FadeInSurround,
            FadeInUI,
            Selection
        }

        private IntroState _currentState = IntroState.TypingTitle;

        // Layout Constants
        private const int BASE_CENTER_Y = 56;

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
            UpdateCachedAbilityInfo();

            _currentState = IntroState.TypingTitle;
            _currentText1 = "";
            _currentText2 = "";
            _textIndex = 0;
            _textTimer = 0f;

            _centerSpriteAlpha = 0f;
            _surroundAnimTimer = 0f;
            _uiAlpha = 0f;

            _carouselSlideOffset = 0f;
            _titleWaveTimer = 0f;
            _idleTimer = 0f;
            _selectButtonHopTimer = 0f;
            _leftArrowSimTimer = 0f;
            _rightArrowSimTimer = 0f;

            _navigationGroup.DeselectAll();
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

            // Calculate Min/Max HP for normalization
            _globalMinHP = int.MaxValue;
            _globalMaxHP = int.MinValue;

            foreach (var id in _characterIds)
            {
                if (BattleDataCache.PartyMembers.TryGetValue(id, out var data))
                {
                    if (data.MaxHP < _globalMinHP) _globalMinHP = data.MaxHP;
                    if (data.MaxHP > _globalMaxHP) _globalMaxHP = data.MaxHP;
                }
            }

            // Safety if list is empty or single item
            if (_globalMinHP == int.MaxValue) _globalMinHP = 0;
            if (_globalMaxHP == int.MinValue) _globalMaxHP = 100;
            if (_globalMaxHP == _globalMinHP) _globalMaxHP++; // Prevent divide by zero
        }

        private void InitializeUI()
        {
            _navigationGroup.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            int centerX = Global.VIRTUAL_WIDTH / 2;
            int centerY = BASE_CENTER_Y;

            int arrowY = centerY;
            int halfWidth = Global.VIRTUAL_WIDTH / 2;
            int buttonHeight = 16;
            int buttonY = arrowY - (buttonHeight / 2);

            // Calculate Text Offsets
            float leftButtonCenterX = halfWidth / 2f;
            float leftTextTargetX = centerX - 24;
            float leftOffset = leftTextTargetX - leftButtonCenterX;

            _leftArrow = new Button(
                new Rectangle(0, buttonY, halfWidth, buttonHeight),
                "<",
                font: secondaryFont
            )
            {
                TriggerHapticOnHover = true,
                EnableHoverSway = true,
                HoverAnimation = HoverAnimationType.None,
                EnableTextWave = false,
                TextRenderOffset = new Vector2(leftOffset, 0)
            };
            _leftArrow.OnClick += () => CycleCharacter(-1);

            float rightButtonCenterX = halfWidth + (halfWidth / 2f);
            float rightTextTargetX = centerX + 24;
            float rightOffset = rightTextTargetX - rightButtonCenterX;

            _rightArrow = new Button(
                new Rectangle(halfWidth, buttonY, halfWidth, buttonHeight),
                ">",
                font: secondaryFont
            )
            {
                TriggerHapticOnHover = true,
                EnableHoverSway = true,
                HoverAnimation = HoverAnimationType.None,
                EnableTextWave = false,
                TextRenderOffset = new Vector2(rightOffset, 0)
            };
            _rightArrow.OnClick += () => CycleCharacter(1);

            // --- Select Button ---
            int contentStartY = centerY + 48;

            // Stats Height
            int statsHeight = (int)secondaryFont.LineHeight * 2 + 2; // 2 rows + gap

            // Ability Height
            int abilityLabelHeight = (int)tertiaryFont.LineHeight + 2; // Label + gap
            int abilityNameHeight = (int)secondaryFont.LineHeight + 1; // Name + gap
            int abilityDescHeight = ((int)tertiaryFont.LineHeight + 2) * 3; // 3 lines + gaps

            // Ability name Y is: contentStartY + statsHeight + 4 + abilityLabelHeight
            int abilityNameY = contentStartY + statsHeight + 4 + abilityLabelHeight;

            // Select button is 6px below the ability DESCRIPTION area (which is 3 lines tall)
            // Ability description starts at abilityNameY + abilityNameHeight
            int abilityDescStartY = abilityNameY + abilityNameHeight;
            int selectButtonY = abilityDescStartY + abilityDescHeight + 6;

            string selectText = "SELECT";
            Vector2 selectSize = secondaryFont.MeasureString(selectText);
            int selectW = (int)selectSize.X + 10;
            int selectH = (int)selectSize.Y + 6;

            _selectButton = new Button(
                new Rectangle(centerX - selectW / 2, selectButtonY, selectW, selectH),
                selectText,
                font: secondaryFont
            )
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop
            };
            _selectButton.OnClick += ConfirmSelection;

            _navigationGroup.Add(_selectButton);
        }

        private void CycleCharacter(int direction)
        {
            if (_currentState != IntroState.Selection) return;

            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

            // Trigger visual simulation only if not using mouse (mouse has its own natural interaction)
            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
            {
                if (direction == -1) _leftArrowSimTimer = ARROW_SIM_DURATION;
                else _rightArrowSimTimer = ARROW_SIM_DURATION;
            }

            _focusedIndex += direction;

            if (_focusedIndex < 0) _focusedIndex = _characterIds.Count - 1;
            if (_focusedIndex >= _characterIds.Count) _focusedIndex = 0;

            _carouselSlideOffset = direction;
            _selectButtonHopTimer = SELECT_HOP_DURATION;

            UpdateCachedAbilityInfo();
        }

        private void UpdateCachedAbilityInfo()
        {
            if (_characterIds.Count == 0) return;
            string charId = _characterIds[_focusedIndex];
            if (!BattleDataCache.PartyMembers.TryGetValue(charId, out var data)) return;

            if (data.PassiveAbilityPool != null && data.PassiveAbilityPool.Any())
            {
                // Take the first passive from the pool as a preview
                var passiveDict = data.PassiveAbilityPool.First();
                if (passiveDict.Count > 0)
                {
                    var kvp = passiveDict.First();
                    _cachedAbilityInfo = GetAbilityInfo(kvp.Key, kvp.Value);
                    return;
                }
            }
            _cachedAbilityInfo = ("NONE", "");
        }

        private (string Name, string Description) GetAbilityInfo(string abilityId, string overrideDesc)
        {
            string friendlyName = abilityId;
            string description = overrideDesc;

            try
            {
                var typeName = $"ProjectVagabond.Battle.Abilities.{abilityId}Ability";
                var type = Type.GetType(typeName);

                if (type == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = asm.GetType(typeName);
                        if (type != null) break;
                    }
                }

                if (type != null)
                {
                    var instance = Activator.CreateInstance(type) as IAbility;
                    if (instance != null)
                    {
                        friendlyName = instance.Name;
                        if (string.IsNullOrEmpty(description))
                        {
                            description = instance.Description;
                        }
                    }
                }
            }
            catch { }

            return (friendlyName, description);
        }

        private void ConfirmSelection()
        {
            if (_currentState != IntroState.Selection) return;
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

            if (_selectButtonHopTimer > 0f)
            {
                _selectButtonHopTimer -= dt;
                if (_selectButtonHopTimer < 0f) _selectButtonHopTimer = 0f;
            }

            if (_leftArrowSimTimer > 0f)
            {
                _leftArrowSimTimer -= dt;
                if (_leftArrowSimTimer < 0f) _leftArrowSimTimer = 0f;
            }

            if (_rightArrowSimTimer > 0f)
            {
                _rightArrowSimTimer -= dt;
                if (_rightArrowSimTimer < 0f) _rightArrowSimTimer = 0f;
            }

            UpdateIntroSequence(dt);

            if (_currentState == IntroState.Selection)
            {
                var currentMouseState = _inputManager.GetEffectiveMouseState();

                // Mouse Updates
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

                // Input Handling
                if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
                {
                    _navigationGroup.DeselectAll();
                }
                else
                {
                    if (!_selectButton.IsSelected)
                    {
                        _navigationGroup.Select(0);
                    }

                    if (_inputManager.NavigateLeft) CycleCharacter(-1);
                    else if (_inputManager.NavigateRight) CycleCharacter(1);

                    _navigationGroup.UpdateInput(_inputManager);
                }

                if (_inputManager.Back)
                {
                    _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.None, TransitionType.None);
                }
            }
        }

        private void UpdateIntroSequence(float dt)
        {
            bool skipPressed = _inputManager.Confirm || (_inputManager.IsMouseActive && _inputManager.GetEffectiveMouseState().LeftButton == ButtonState.Pressed);

            if (skipPressed && _currentState != IntroState.Selection && _inputManager.IsMouseActive)
            {
                _inputManager.ConsumeMouseClick();
            }

            switch (_currentState)
            {
                case IntroState.TypingTitle:
                    if (skipPressed)
                    {
                        _currentText1 = INTRO_LINE_1;
                        _currentText2 = INTRO_LINE_2;
                        _textIndex = INTRO_LINE_1.Length + INTRO_LINE_2.Length;
                    }
                    else
                    {
                        _textTimer += dt;
                        if (_textTimer >= TYPEWRITER_SPEED)
                        {
                            _textTimer = 0f;
                            int totalLen = INTRO_LINE_1.Length + INTRO_LINE_2.Length;
                            if (_textIndex < totalLen)
                            {
                                if (_textIndex < INTRO_LINE_1.Length)
                                {
                                    _currentText1 += INTRO_LINE_1[_textIndex];
                                }
                                else
                                {
                                    _currentText2 += INTRO_LINE_2[_textIndex - INTRO_LINE_1.Length];
                                }
                                _textIndex++;
                            }
                        }
                    }

                    if (_textIndex >= INTRO_LINE_1.Length + INTRO_LINE_2.Length)
                    {
                        _currentState = IntroState.FadeInCenter;
                    }
                    break;

                case IntroState.FadeInCenter:
                    if (skipPressed) _centerSpriteAlpha = 1.0f;
                    else _centerSpriteAlpha += dt * 2.0f;

                    if (_centerSpriteAlpha >= 1.0f)
                    {
                        _centerSpriteAlpha = 1.0f;
                        _currentState = IntroState.FadeInSurround;
                    }
                    break;

                case IntroState.FadeInSurround:
                    if (skipPressed) _surroundAnimTimer = 10.0f;
                    else _surroundAnimTimer += dt;

                    if (_surroundAnimTimer >= 1.0f)
                    {
                        _currentState = IntroState.FadeInUI;
                    }
                    break;

                case IntroState.FadeInUI:
                    if (skipPressed) _uiAlpha = 1.0f;
                    else _uiAlpha += dt * 2.0f;

                    if (_uiAlpha >= 1.0f)
                    {
                        _uiAlpha = 1.0f;
                        _currentState = IntroState.Selection;
                    }
                    break;

                case IntroState.Selection:
                    _centerSpriteAlpha = 1.0f;
                    _uiAlpha = 1.0f;
                    _surroundAnimTimer = 10.0f;
                    break;
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var core = ServiceLocator.Get<Core>();
            var secondaryFont = core.SecondaryFont;
            var tertiaryFont = core.TertiaryFont;
            Matrix staticTransform = Matrix.CreateScale(core.FinalScale, core.FinalScale, 1.0f) *
                                     Matrix.CreateTranslation(core.FinalRenderRectangle.X, core.FinalRenderRectangle.Y, 0);

            spriteBatch.End();

            // Background
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, null, null, null, staticTransform);
            spriteBatch.Draw(_spriteManager.EmptySprite, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off);
            spriteBatch.End();

            // Title
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, staticTransform);

            float titleY = 24f;

            if (!string.IsNullOrEmpty(_currentText1))
            {
                Vector2 size1 = secondaryFont.MeasureString(INTRO_LINE_1);
                var pos1 = new Vector2((Global.VIRTUAL_WIDTH - size1.X) / 2, titleY - 2);
                TextAnimator.DrawTextWithEffect(spriteBatch, secondaryFont, _currentText1, pos1, _global.Palette_DarkPale, TextEffectType.None, 0f);
            }

            if (!string.IsNullOrEmpty(_currentText2))
            {
                Vector2 size2 = font.MeasureString(INTRO_LINE_2);
                var pos2 = new Vector2((Global.VIRTUAL_WIDTH - size2.X) / 2, titleY + secondaryFont.LineHeight + 2);
                TextAnimator.DrawTextWithEffect(spriteBatch, font, _currentText2, pos2, _global.Palette_White, TextEffectType.RainbowWave, _titleWaveTimer);
            }

            spriteBatch.End();

            // Carousel
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
            if (_characterIds.Count > 0)
            {
                DrawCarousel(spriteBatch, font, tertiaryFont);
            }
            spriteBatch.End();

            // UI Elements
            if (_uiAlpha > 0f)
            {
                Color GetTint(Button btn)
                {
                    Color baseColor = (btn.IsHovered || btn.IsSelected)
                                      ? _global.ButtonHoverColor
                                      : _global.GameTextColor;
                    return baseColor * _uiAlpha;
                }

                // --- Left Arrow ---
                float leftY = 0f;
                Color leftColor = GetTint(_leftArrow);

                if (_leftArrowSimTimer > 0f)
                {
                    // Simulated Click Animation
                    float progress = 1f - (_leftArrowSimTimer / ARROW_SIM_DURATION);
                    if (progress < 0.5f)
                    {
                        leftY = -1f; // Pop Up
                        leftColor = _global.ButtonHoverColor * _uiAlpha;
                    }
                    else
                    {
                        leftY = 1f; // Down
                        leftColor = _global.Palette_Fruit * _uiAlpha;
                    }
                }
                else if (_leftArrow.IsHovered)
                {
                    leftY = -1f;
                    if (_inputManager.GetEffectiveMouseState().LeftButton == ButtonState.Pressed)
                        leftY = 1f;
                }

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateTranslation(0, leftY, 0) * staticTransform);
                _leftArrow.Draw(spriteBatch, font, gameTime, Matrix.Identity, false, null, null, leftColor);
                spriteBatch.End();

                // --- Right Arrow ---
                float rightY = 0f;
                Color rightColor = GetTint(_rightArrow);

                if (_rightArrowSimTimer > 0f)
                {
                    // Simulated Click Animation
                    float progress = 1f - (_rightArrowSimTimer / ARROW_SIM_DURATION);
                    if (progress < 0.5f)
                    {
                        rightY = -1f; // Pop Up
                        rightColor = _global.ButtonHoverColor * _uiAlpha;
                    }
                    else
                    {
                        rightY = 1f; // Down
                        rightColor = _global.Palette_Fruit * _uiAlpha;
                    }
                }
                else if (_rightArrow.IsHovered)
                {
                    rightY = -1f;
                    if (_inputManager.GetEffectiveMouseState().LeftButton == ButtonState.Pressed)
                        rightY = 1f;
                }

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateTranslation(0, rightY, 0) * staticTransform);
                _rightArrow.Draw(spriteBatch, font, gameTime, Matrix.Identity, false, null, null, rightColor);
                spriteBatch.End();

                // --- Select Button ---
                float hopY = (_selectButtonHopTimer > 0f) ? -1f : 0f;
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateTranslation(0, hopY, 0) * staticTransform);
                _selectButton.Draw(spriteBatch, font, gameTime, Matrix.Identity, false, null, null, GetTint(_selectButton));
                spriteBatch.End();

                // --- Stats and Abilities ---
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, staticTransform);
                DrawStatsAndAbilities(spriteBatch, secondaryFont, tertiaryFont, _uiAlpha);
                spriteBatch.End();
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
        }

        private Color GetStatColor(int value)
        {
            if (value >= 8) return _global.Palette_Leaf;
            if (value >= 4) return _global.Palette_LightPale;
            return _global.Palette_Rust;
        }

        private void DrawStatsAndAbilities(SpriteBatch spriteBatch, BitmapFont secondaryFont, BitmapFont tertiaryFont, float alpha)
        {
            if (_characterIds.Count == 0) return;
            string charId = _characterIds[_focusedIndex];
            if (!BattleDataCache.PartyMembers.TryGetValue(charId, out var data)) return;

            int centerX = Global.VIRTUAL_WIDTH / 2;
            int startY = BASE_CENTER_Y + 32;
            int currentY = startY;

            // --- Stats Block ---
            string[] labels = { "STR", "INT", "TEN", "AGI" };
            int[] values = { data.Strength, data.Intelligence, data.Tenacity, data.Agility };

            int statBlockX = centerX - 30;

            // Calculate alignment reference based on "STR" (standard 3-letter width)
            float standardLabelWidth = secondaryFont.MeasureString("STR").Width;

            // 1. HP Row
            string hpLabel = "HP";
            float hpLabelWidth = secondaryFont.MeasureString(hpLabel).Width;

            // Right-align HP label with other labels
            float hpLabelX = statBlockX + (standardLabelWidth - hpLabelWidth);

            spriteBatch.DrawStringSnapped(secondaryFont, hpLabel, new Vector2(hpLabelX, currentY), _global.Palette_DarkestPale * alpha);

            // Center HP Value in the bar area
            // Bar area starts at statBlockX + 19 (from original code loop)
            // Bar width comes from texture
            Texture2D statBg = _spriteManager.InventoryStatBarEmpty;
            float barAreaWidth = (statBg != null) ? statBg.Width : 40f;
            float barStartX = statBlockX + 19;
            float barCenterX = barStartX + (barAreaWidth / 2f);

            string hpValue = data.MaxHP.ToString();
            Vector2 hpValueSize = secondaryFont.MeasureString(hpValue);

            // Calculate normalized HP score (1-10)
            int hpScore = 1;
            if (_globalMaxHP > _globalMinHP)
            {
                hpScore = 1 + (int)Math.Round((double)(data.MaxHP - _globalMinHP) * 9 / (_globalMaxHP - _globalMinHP));
            }
            else
            {
                hpScore = 5; // Default if all same
            }
            hpScore = Math.Clamp(hpScore, 1, 10);
            Color hpColor = GetStatColor(hpScore);

            // Draw HP Value centered
            spriteBatch.DrawStringSnapped(secondaryFont, hpValue, new Vector2(barCenterX - hpValueSize.X / 2f, currentY), hpColor * alpha);

            currentY += secondaryFont.LineHeight + 1;

            // 2. Stat Rows
            Texture2D statFull = _spriteManager.InventoryStatBarFull;

            for (int i = 0; i < labels.Length; i++)
            {
                spriteBatch.DrawStringSnapped(secondaryFont, labels[i], new Vector2(statBlockX, currentY), _global.Palette_DarkestPale * alpha);

                if (statBg != null)
                {
                    float pipX = statBlockX + 19;
                    float pipY = currentY + MathF.Ceiling((secondaryFont.LineHeight - statBg.Height) / 2f);
                    spriteBatch.DrawSnapped(statBg, new Vector2(pipX, pipY), Color.White * alpha);

                    if (statFull != null)
                    {
                        int val = values[i];
                        int basePoints = Math.Clamp(val, 0, 10);
                        if (basePoints > 0)
                        {
                            var srcBase = new Rectangle(0, 0, basePoints * 4, 3);
                            Color pipColor = GetStatColor(basePoints);
                            spriteBatch.DrawSnapped(statFull, new Vector2(pipX, pipY), srcBase, pipColor * alpha);
                        }
                    }
                }
                currentY += secondaryFont.LineHeight + 1;
            }

            currentY += 4; // Gap between Stats and Ability

            // --- Passive Ability Block ---
            // "ABILITY" Label (Tertiary Font)
            string abilityLabel = "ABILITY";
            Vector2 abilityLabelSize = tertiaryFont.MeasureString(abilityLabel);
            spriteBatch.DrawStringSnapped(tertiaryFont, abilityLabel, new Vector2(centerX - abilityLabelSize.X / 2f, currentY), _global.Palette_DarkestPale * alpha);
            currentY += tertiaryFont.LineHeight + 2; // 2px gap

            if (_cachedAbilityInfo.HasValue)
            {
                var (name, desc) = _cachedAbilityInfo.Value;

                // Name (Secondary Font, Centered)
                Vector2 nameSize = secondaryFont.MeasureString(name);
                spriteBatch.DrawStringSnapped(secondaryFont, name, new Vector2(centerX - nameSize.X / 2f, currentY), _global.Palette_LightPale * alpha);
                currentY += secondaryFont.LineHeight + 1;

                // Description (Centered & Wrapped)
                float descLineHeight = tertiaryFont.LineHeight + 2;

                if (!string.IsNullOrEmpty(desc))
                {
                    float maxWidth = 120f;
                    var words = desc.Split(' ');
                    string line = "";
                    float localY = currentY;

                    foreach (var word in words)
                    {
                        string testLine = string.IsNullOrEmpty(line) ? word : line + " " + word;
                        if (tertiaryFont.MeasureString(testLine).Width > maxWidth)
                        {
                            Vector2 lineSize = tertiaryFont.MeasureString(line);
                            spriteBatch.DrawStringSnapped(tertiaryFont, line, new Vector2(centerX - lineSize.X / 2f, localY), _global.Palette_Pale * alpha);
                            localY += descLineHeight;
                            line = word;
                        }
                        else
                        {
                            line = testLine;
                        }
                    }
                    if (!string.IsNullOrEmpty(line))
                    {
                        Vector2 lineSize = tertiaryFont.MeasureString(line);
                        spriteBatch.DrawStringSnapped(tertiaryFont, line, new Vector2(centerX - lineSize.X / 2f, localY), _global.Palette_Pale * alpha);
                    }
                }
            }
        }

        private void DrawCarousel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont tertiaryFont)
        {
            int centerX = Global.VIRTUAL_WIDTH / 2;
            int centerY = BASE_CENTER_Y;
            var sheet = _spriteManager.PlayerMasterSpriteSheet;
            int count = _characterIds.Count;

            int[] drawOrder = { -3, 3, -2, 2, -1, 1, 0 };

            // Tuning for circular curve
            // 0.5f rads per index * 100f radius gives ~48px gap at center, diminishing outwards
            const float SPREAD_FACTOR = 0.5f;
            const float RADIUS = 100f;

            foreach (int offset in drawOrder)
            {
                int charIndex = (_focusedIndex + offset) % count;
                if (charIndex < 0) charIndex += count;

                string charId = _characterIds[charIndex];
                bool isCenter = (offset == 0);

                float baseOpacity = isCenter ? 1.0f : 0.6f;
                if (Math.Abs(offset) >= 2) baseOpacity = 0.3f;

                float entryAlpha = 0f;
                if (isCenter)
                {
                    entryAlpha = _centerSpriteAlpha;
                }
                else
                {
                    float staggerDelay = 0.1f;
                    float fadeDuration = 0.5f;
                    int distance = Math.Abs(offset);
                    float progress = (_surroundAnimTimer - ((distance - 1) * staggerDelay)) / fadeDuration;
                    entryAlpha = Math.Clamp(progress, 0f, 1f);
                }

                float finalOpacity = baseOpacity * entryAlpha;
                if (finalOpacity <= 0.01f) continue;

                float visualOffset = offset + _carouselSlideOffset;

                // Sinusoidal spacing for circular effect
                float xOffset = MathF.Sin(visualOffset * SPREAD_FACTOR) * RADIUS;
                float xPos = centerX + xOffset;

                int spriteIndex = int.Parse(charId);

                PlayerSpriteType spriteType;
                if (Math.Abs(offset) >= 3) spriteType = PlayerSpriteType.Portrait5x5;
                else if (Math.Abs(offset) >= 1) spriteType = PlayerSpriteType.Portrait8x8;
                else spriteType = PlayerSpriteType.Normal;

                float curveY = MathF.Pow(Math.Abs(visualOffset), 1.5f) * 2.0f;
                float yPos = centerY - curveY;

                if (isCenter)
                {
                    float bob = MathF.Sin(_idleTimer * 4f);
                    yPos += (bob > 0 ? -1f : 0f);
                    if (bob > 0) spriteType = PlayerSpriteType.Alt;
                }

                var sourceRect = _spriteManager.GetPlayerSourceRect(spriteIndex, spriteType);
                Vector2 position = new Vector2(MathF.Round(xPos), MathF.Round(yPos));
                Vector2 origin = new Vector2(16, 16);

                spriteBatch.Draw(
                    sheet,
                    position,
                    sourceRect,
                    Color.White * finalOpacity,
                    0f,
                    origin,
                    1.0f,
                    SpriteEffects.None,
                    0f
                );

                if (isCenter && _uiAlpha > 0f && BattleDataCache.PartyMembers.TryGetValue(charId, out var data))
                {
                    string name = data.Name.ToUpper();
                    Vector2 nameSize = font.MeasureString(name);
                    Vector2 namePos = new Vector2(centerX - nameSize.X / 2, centerY + 20);
                    spriteBatch.DrawStringSnapped(font, name, namePos, _global.Palette_LightPale * _uiAlpha);

                    // Draw Number
                    string numberText = (spriteIndex + 1).ToString();
                    Vector2 numSize = tertiaryFont.MeasureString(numberText);
                    Vector2 numPos = new Vector2(centerX - numSize.X / 2, centerY + 14);
                    spriteBatch.DrawStringSnapped(tertiaryFont, numberText, numPos, _global.Palette_LightPale * _uiAlpha);
                }
            }
        }
    }
}