using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Particles;
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
        private readonly ParticleSystemManager _particleSystemManager;

        // Carousel State
        private List<string> _characterIds = new();
        private int _focusedIndex = 0;
        private float _carouselSlideOffset = 0f;
        private const float CAROUSEL_SLIDE_SPEED = 15f;

        // UI Elements
        private Button _leftArrow;
        private Button _rightArrow;
        private Button _selectButton;
        private readonly NavigationGroup _navigationGroup;

        // Intro Text
        private const string INTRO_LINE_1 = "CHOOSE AN";
        private const string INTRO_LINE_2 = "ADVENTURER";

        // --- Plink Animation State ---
        private bool _isPlinkingIn = true;
        private PlinkAnimator _plinkTitle1;
        private PlinkAnimator _plinkTitle2;
        private PlinkAnimator _plinkStats;
        private PlinkAnimator[] _plinkCarousel = new PlinkAnimator[7];
        private List<PlinkAnimator> _allPlinks = new List<PlinkAnimator>();

        // Idle Animation State
        private float _idleTimer = 0f;
        private float _titleWaveTimer = 0f;

        // Arrow Simulation State
        private float _leftArrowSimTimer = 0f;
        private float _rightArrowSimTimer = 0f;
        private const float ARROW_SIM_DURATION = 0.15f;

        // Scroll State
        private int _lastScrollWheelValue;
        private int _scrollAccumulator;

        // Cached Data for Display
        private (string Name, string Description)? _cachedAbilityInfo;

        // HP Normalization Cache
        private int _globalMinHP = 0;
        private int _globalMaxHP = 0;

        // Layout Constants
        private const int BASE_CENTER_Y = 50;

        public NewGameIntroScene()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
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

            _carouselSlideOffset = 0f;
            _titleWaveTimer = 0f;
            _idleTimer = 0f;
            _leftArrowSimTimer = 0f;
            _rightArrowSimTimer = 0f;

            _lastScrollWheelValue = _inputManager.GetEffectiveMouseState().ScrollWheelValue;
            _scrollAccumulator = 0;

            _navigationGroup.DeselectAll();

            // --- Setup Randomized Plink Stagger ---
            _isPlinkingIn = true;
            _allPlinks.Clear();

            var randomPlinks = new List<PlinkAnimator>();

            _plinkTitle1 = new PlinkAnimator(); randomPlinks.Add(_plinkTitle1);
            _plinkTitle2 = new PlinkAnimator(); randomPlinks.Add(_plinkTitle2);
            _plinkStats = new PlinkAnimator(); randomPlinks.Add(_plinkStats);

            for (int i = 0; i < 7; i++)
            {
                _plinkCarousel[i] = new PlinkAnimator();
                randomPlinks.Add(_plinkCarousel[i]);
            }

            // Shuffle ONLY the titles, stats, and carousel elements
            var rng = new Random();
            randomPlinks = randomPlinks.OrderBy(x => rng.Next()).ToList();

            float delay = 0f;
            float stagger = 0.05f; // Increased stagger so the sequence is clearly visible

            foreach (var p in randomPlinks)
            {
                p.Start(delay, 0.25f);
                delay += stagger;
            }

            // Explicitly use PlayEntrance for buttons so they properly hide during the delay
            _leftArrow.PlayEntrance(delay);
            delay += stagger;

            _rightArrow.PlayEntrance(delay);
            delay += stagger;

            _selectButton.PlayEntrance(delay);

            // Add everything to _allPlinks so the Update loop knows when the entire sequence is done
            _allPlinks.AddRange(randomPlinks);
            _allPlinks.Add(_leftArrow.Plink);
            _allPlinks.Add(_rightArrow.Plink);
            _allPlinks.Add(_selectButton.Plink);
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
            int contentStartY = centerY + 54;

            // Stats Height
            int statsHeight = (int)secondaryFont.LineHeight * 2 + 2; // 2 rows + gap

            // Ability Height
            int abilityLabelHeight = (int)tertiaryFont.LineHeight + 2; // Label + gap
            int abilityNameHeight = (int)secondaryFont.LineHeight + 1; // Name + gap
            int abilityDescHeight = ((int)tertiaryFont.LineHeight + 2) * 3; // 3 lines + gaps

            // Ability name Y is: contentStartY + statsHeight + 4 + abilityLabelHeight
            int abilityNameY = contentStartY + statsHeight + 4 + abilityLabelHeight;

            // Select button is 6px below the ability DESCRIPTION area (which is 3 lines tall)
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
            if (_isPlinkingIn) return;

            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

            // Trigger visual simulation only if not using mouse
            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
            {
                if (direction == -1) _leftArrowSimTimer = ARROW_SIM_DURATION;
                else _rightArrowSimTimer = ARROW_SIM_DURATION;
            }

            _focusedIndex += direction;

            if (_focusedIndex < 0) _focusedIndex = _characterIds.Count - 1;
            if (_focusedIndex >= _characterIds.Count) _focusedIndex = 0;

            _carouselSlideOffset = direction;

            UpdateCachedAbilityInfo();
        }

        private void UpdateCachedAbilityInfo()
        {
            if (_characterIds.Count == 0) return;
            string charId = _characterIds[_focusedIndex];
            if (!BattleDataCache.PartyMembers.TryGetValue(charId, out var data)) return;

            if (data.PassiveAbilityPool != null && data.PassiveAbilityPool.Any())
            {
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
            if (_isPlinkingIn) return;
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

            var transitionOut = _transitionManager.GetRandomTransition();
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

            var currentMouseState = _inputManager.GetEffectiveMouseState();

            if (_isPlinkingIn)
            {
                bool skipPressed = _inputManager.Confirm || (_inputManager.IsMouseActive && currentMouseState.LeftButton == ButtonState.Pressed);

                if (skipPressed)
                {
                    _isPlinkingIn = false;
                    // Fast forward all animators to prevent late particles
                    foreach (var p in _allPlinks) p.Start(0, 0.001f);
                    if (_inputManager.IsMouseActive) _inputManager.ConsumeMouseClick();
                }
                else
                {
                    int centerX = Global.VIRTUAL_WIDTH / 2;
                    var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

                    _plinkTitle1.Update(gameTime, new Vector2(centerX, 14));
                    _plinkTitle2.Update(gameTime, new Vector2(centerX, 14 + secondaryFont.LineHeight + 2));
                    _plinkStats.Update(gameTime, new Vector2(centerX, BASE_CENTER_Y + 70));

                    for (int i = 0; i < 7; i++)
                    {
                        int offset = i - 3;
                        float visualOffset = offset + _carouselSlideOffset;
                        float xOffset = MathF.Sin(visualOffset * 0.5f) * 100f;
                        float xPos = centerX + xOffset;
                        float curveY = MathF.Pow(Math.Abs(visualOffset), 1.5f) * 2.0f;
                        float baseYPos = BASE_CENTER_Y - curveY;
                        _plinkCarousel[i].Update(gameTime, new Vector2(xPos, baseYPos));
                    }

                    // Buttons update their own plinks during Draw, but we still need to check if they are active
                    if (!_allPlinks.Any(p => p.IsActive))
                    {
                        _isPlinkingIn = false;
                    }
                }
            }
            else
            {
                // --- Normal Selection Logic ---
                int currentScroll = currentMouseState.ScrollWheelValue;
                int scrollDelta = currentScroll - _lastScrollWheelValue;
                _lastScrollWheelValue = currentScroll;
                _scrollAccumulator += scrollDelta;

                const int SCROLL_THRESHOLD = 120;
                if (_scrollAccumulator >= SCROLL_THRESHOLD)
                {
                    CycleCharacter(-1); // Scroll Up -> Left
                    _scrollAccumulator = 0;
                }
                else if (_scrollAccumulator <= -SCROLL_THRESHOLD)
                {
                    CycleCharacter(1); // Scroll Down -> Right
                    _scrollAccumulator = 0;
                }

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

            // --- Title ---
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, staticTransform);

            float titleY = 14f;

            float t1Scale = _isPlinkingIn ? _plinkTitle1.Scale : 1f;
            float t1Rot = _isPlinkingIn ? _plinkTitle1.Rotation : 0f;
            if (t1Scale > 0.01f)
            {
                Vector2 size1 = secondaryFont.MeasureString(INTRO_LINE_1);
                var pos1 = new Vector2((Global.VIRTUAL_WIDTH - size1.X) / 2, titleY - 2);
                TextAnimator.DrawTextWithEffect(spriteBatch, secondaryFont, INTRO_LINE_1, pos1, _global.Palette_DarkPale, TextEffectType.None, 0f, new Vector2(t1Scale), null, t1Rot);

                if (_isPlinkingIn && _plinkTitle1.FlashTint.HasValue)
                {
                    Rectangle bounds = new Rectangle((int)pos1.X, (int)pos1.Y, (int)size1.X, (int)size1.Y);
                    spriteBatch.DrawSnapped(_spriteManager.EmptySprite, bounds, _plinkTitle1.FlashTint.Value);
                }
            }

            float t2Scale = _isPlinkingIn ? _plinkTitle2.Scale : 1f;
            float t2Rot = _isPlinkingIn ? _plinkTitle2.Rotation : 0f;
            if (t2Scale > 0.01f)
            {
                Vector2 size2 = font.MeasureString(INTRO_LINE_2);
                var pos2 = new Vector2((Global.VIRTUAL_WIDTH - size2.X) / 2, titleY + secondaryFont.LineHeight + 2);
                TextAnimator.DrawTextWithEffect(spriteBatch, font, INTRO_LINE_2, pos2, _global.Palette_White, TextEffectType.RainbowWave, _titleWaveTimer, new Vector2(t2Scale), null, t2Rot);

                if (_isPlinkingIn && _plinkTitle2.FlashTint.HasValue)
                {
                    Rectangle bounds = new Rectangle((int)pos2.X, (int)pos2.Y, (int)size2.X, (int)size2.Y);
                    spriteBatch.DrawSnapped(_spriteManager.EmptySprite, bounds, _plinkTitle2.FlashTint.Value);
                }
            }

            spriteBatch.End();

            // --- Carousel ---
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
            if (_characterIds.Count > 0)
            {
                DrawCarousel(spriteBatch, font, tertiaryFont);
            }
            spriteBatch.End();

            // --- UI Elements ---
            // Left Arrow
            float leftY = 0f;
            Color leftColor = (_leftArrow.IsHovered || _leftArrow.IsSelected) ? _global.ButtonHoverColor : _global.GameTextColor;

            if (_leftArrowSimTimer > 0f)
            {
                float progress = 1f - (_leftArrowSimTimer / ARROW_SIM_DURATION);
                if (progress < 0.5f) { leftY = -1f; leftColor = _global.ButtonHoverColor; }
                else { leftY = 1f; leftColor = _global.Palette_Fruit; }
            }
            else if (_leftArrow.IsHovered && _inputManager.GetEffectiveMouseState().LeftButton == ButtonState.Pressed)
            {
                leftY = 1f;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateTranslation(0, leftY, 0) * staticTransform);
            _leftArrow.Draw(spriteBatch, font, gameTime, Matrix.Identity, false, null, null, leftColor);
            spriteBatch.End();

            // Right Arrow
            float rightY = 0f;
            Color rightColor = (_rightArrow.IsHovered || _rightArrow.IsSelected) ? _global.ButtonHoverColor : _global.GameTextColor;

            if (_rightArrowSimTimer > 0f)
            {
                float progress = 1f - (_rightArrowSimTimer / ARROW_SIM_DURATION);
                if (progress < 0.5f) { rightY = -1f; rightColor = _global.ButtonHoverColor; }
                else { rightY = 1f; rightColor = _global.Palette_Fruit; }
            }
            else if (_rightArrow.IsHovered && _inputManager.GetEffectiveMouseState().LeftButton == ButtonState.Pressed)
            {
                rightY = 1f;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Matrix.CreateTranslation(0, rightY, 0) * staticTransform);
            _rightArrow.Draw(spriteBatch, font, gameTime, Matrix.Identity, false, null, null, rightColor);
            spriteBatch.End();

            // Select Button
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, staticTransform);
            Color selectColor = (_selectButton.IsHovered || _selectButton.IsSelected) ? _global.ButtonHoverColor : _global.GameTextColor;
            _selectButton.Draw(spriteBatch, font, gameTime, Matrix.Identity, false, null, null, selectColor);
            spriteBatch.End();

            // --- Stats and Abilities ---
            float sScale = _isPlinkingIn ? _plinkStats.Scale : 1f;
            float sRot = _isPlinkingIn ? _plinkStats.Rotation : 0f;

            if (sScale > 0.01f)
            {
                Vector2 statsCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, BASE_CENTER_Y + 70);
                Matrix statsMatrix = Matrix.CreateTranslation(-statsCenter.X, -statsCenter.Y, 0) *
                                     Matrix.CreateScale(sScale) *
                                     Matrix.CreateRotationZ(sRot) *
                                     Matrix.CreateTranslation(statsCenter.X, statsCenter.Y, 0);

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, statsMatrix * staticTransform);
                DrawStatsAndAbilities(spriteBatch, secondaryFont, tertiaryFont);

                if (_isPlinkingIn && _plinkStats.FlashTint.HasValue)
                {
                    Rectangle flashRect = new Rectangle((int)statsCenter.X - 70, (int)statsCenter.Y - 30, 140, 60);
                    spriteBatch.DrawSnapped(_spriteManager.EmptySprite, flashRect, _plinkStats.FlashTint.Value);
                }

                spriteBatch.End();
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
        }

        private Color GetStatColor(int value)
        {
            if (value >= 8) return _global.StatColor_High;
            if (value >= 4) return _global.StatColor_Average;
            return _global.StatColor_Low;
        }

        private void DrawStatsAndAbilities(SpriteBatch spriteBatch, BitmapFont secondaryFont, BitmapFont tertiaryFont)
        {
            if (_characterIds.Count == 0) return;
            string charId = _characterIds[_focusedIndex];
            if (!BattleDataCache.PartyMembers.TryGetValue(charId, out var data)) return;

            int centerX = Global.VIRTUAL_WIDTH / 2;
            int startY = BASE_CENTER_Y + 38;
            int currentY = startY;

            // --- Stats Block ---
            string[] labels = { "STR", "INT", "TEN", "AGI" };
            int[] values = { data.Strength, data.Intelligence, data.Tenacity, data.Agility };

            int statBlockX = centerX - 30;

            float standardLabelWidth = secondaryFont.MeasureString("STR").Width;

            // 1. HP Row
            string hpLabel = "HP";
            float hpLabelWidth = secondaryFont.MeasureString(hpLabel).Width;

            float hpLabelX = statBlockX + (standardLabelWidth - hpLabelWidth);

            spriteBatch.DrawStringSnapped(secondaryFont, hpLabel, new Vector2(hpLabelX, currentY), _global.Palette_DarkestPale);

            Texture2D statBg = _spriteManager.InventoryStatBarEmpty;
            float barAreaWidth = (statBg != null) ? statBg.Width : 40f;
            float barStartX = statBlockX + 19;
            float barCenterX = barStartX + (barAreaWidth / 2f);

            string hpValue = data.MaxHP.ToString();
            Vector2 hpValueSize = secondaryFont.MeasureString(hpValue);

            int hpScore = 1;
            if (_globalMaxHP > _globalMinHP)
            {
                hpScore = 1 + (int)Math.Round((double)(data.MaxHP - _globalMinHP) * 9 / (_globalMaxHP - _globalMinHP));
            }
            else
            {
                hpScore = 5;
            }
            hpScore = Math.Clamp(hpScore, 1, 10);
            Color hpColor = GetStatColor(hpScore);

            spriteBatch.DrawStringSnapped(secondaryFont, hpValue, new Vector2(barCenterX - hpValueSize.X / 2f, currentY), hpColor);

            currentY += secondaryFont.LineHeight + 1;

            // 2. Stat Rows
            Texture2D statFull = _spriteManager.InventoryStatBarFull;

            for (int i = 0; i < labels.Length; i++)
            {
                spriteBatch.DrawStringSnapped(secondaryFont, labels[i], new Vector2(statBlockX, currentY), _global.Palette_DarkestPale);

                if (statBg != null)
                {
                    float pipX = statBlockX + 19;
                    float pipY = currentY + MathF.Ceiling((secondaryFont.LineHeight - statBg.Height) / 2f);
                    spriteBatch.DrawSnapped(statBg, new Vector2(pipX, pipY), Color.White);

                    if (statFull != null)
                    {
                        int val = values[i];
                        int basePoints = Math.Clamp(val, 0, 10);
                        if (basePoints > 0)
                        {
                            var srcBase = new Rectangle(0, 0, basePoints * 4, 3);
                            Color pipColor = GetStatColor(basePoints);
                            spriteBatch.DrawSnapped(statFull, new Vector2(pipX, pipY), srcBase, pipColor);
                        }
                    }
                }
                currentY += secondaryFont.LineHeight + 1;
            }

            currentY += 4;

            // --- Passive Ability Block ---
            string abilityLabel = "ABILITY";
            Vector2 abilityLabelSize = tertiaryFont.MeasureString(abilityLabel);
            spriteBatch.DrawStringSnapped(tertiaryFont, abilityLabel, new Vector2(centerX - abilityLabelSize.X / 2f, currentY), _global.Palette_DarkestPale);
            currentY += tertiaryFont.LineHeight + 2;

            if (_cachedAbilityInfo.HasValue)
            {
                var (name, desc) = _cachedAbilityInfo.Value;

                Vector2 nameSize = secondaryFont.MeasureString(name);
                spriteBatch.DrawStringSnapped(secondaryFont, name, new Vector2(centerX - nameSize.X / 2f, currentY), _global.Palette_LightPale);
                currentY += secondaryFont.LineHeight + 1;

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
                            spriteBatch.DrawStringSnapped(tertiaryFont, line, new Vector2(centerX - lineSize.X / 2f, localY), _global.Palette_Pale);
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
                        spriteBatch.DrawStringSnapped(tertiaryFont, line, new Vector2(centerX - lineSize.X / 2f, localY), _global.Palette_Pale);
                    }
                }
            }
        }

        private void DrawCarousel(SpriteBatch spriteBatch, BitmapFont font, BitmapFont tertiaryFont)
        {
            int centerX = Global.VIRTUAL_WIDTH / 2;
            int centerY = BASE_CENTER_Y;
            var sheet = _spriteManager.PlayerMasterSpriteSheet;
            var silhouette = _spriteManager.PlayerMasterSpriteSheetSilhouette;
            int count = _characterIds.Count;

            int[] drawOrder = { -3, 3, -2, 2, -1, 1, 0 };

            const float SPREAD_FACTOR = 0.5f;
            const float RADIUS = 100f;

            foreach (int offset in drawOrder)
            {
                int plinkIndex = offset + 3;
                var plink = _plinkCarousel[plinkIndex];

                float pScale = _isPlinkingIn ? plink.Scale : 1f;
                float pRot = _isPlinkingIn ? plink.Rotation : 0f;

                if (_isPlinkingIn && pScale < 0.01f) continue;

                int charIndex = (_focusedIndex + offset) % count;
                if (charIndex < 0) charIndex += count;

                string charId = _characterIds[charIndex];
                bool isCenter = (offset == 0);

                float finalOpacity = isCenter ? 1.0f : 0.6f;
                if (Math.Abs(offset) >= 2) finalOpacity = 0.3f;

                float visualOffset = offset + _carouselSlideOffset;

                float xOffset = MathF.Sin(visualOffset * SPREAD_FACTOR) * RADIUS;
                float xPos = centerX + xOffset;

                int spriteIndex = int.Parse(charId);

                PlayerSpriteType spriteType;
                if (Math.Abs(offset) >= 3) spriteType = PlayerSpriteType.Portrait5x5;
                else if (Math.Abs(offset) >= 1) spriteType = PlayerSpriteType.Portrait8x8;
                else spriteType = PlayerSpriteType.Normal;

                float curveY = MathF.Pow(Math.Abs(visualOffset), 1.5f) * 2.0f;
                float baseYPos = centerY - curveY;
                float headYPos = baseYPos;

                if (isCenter)
                {
                    float bob = MathF.Sin(_idleTimer * 4f);
                    headYPos += (bob > 0 ? -1f : 0f);
                    if (bob > 0) spriteType = PlayerSpriteType.Alt;
                }

                Vector2 origin = new Vector2(16, 16);
                Vector2 bodyPosition = new Vector2(MathF.Round(xPos), MathF.Round(baseYPos));
                Vector2 headPosition = new Vector2(MathF.Round(xPos), MathF.Round(headYPos));

                // Draw Body
                if (Math.Abs(offset) < 1)
                {
                    PlayerSpriteType bodyType = (spriteType == PlayerSpriteType.Alt) ? PlayerSpriteType.BodyAlt : PlayerSpriteType.BodyNormal;
                    var bodySourceRect = _spriteManager.GetPlayerSourceRect(spriteIndex, bodyType);

                    spriteBatch.Draw(sheet, bodyPosition, bodySourceRect, Color.White * finalOpacity, pRot, origin, pScale, SpriteEffects.None, 0f);

                    if (_isPlinkingIn && plink.FlashTint.HasValue && silhouette != null)
                    {
                        spriteBatch.Draw(silhouette, bodyPosition, bodySourceRect, plink.FlashTint.Value, pRot, origin, pScale, SpriteEffects.None, 0f);
                    }
                }

                // Draw Head
                var sourceRect = _spriteManager.GetPlayerSourceRect(spriteIndex, spriteType);
                spriteBatch.Draw(sheet, headPosition, sourceRect, Color.White * finalOpacity, pRot, origin, pScale, SpriteEffects.None, 0f);

                if (_isPlinkingIn && plink.FlashTint.HasValue && silhouette != null)
                {
                    spriteBatch.Draw(silhouette, headPosition, sourceRect, plink.FlashTint.Value, pRot, origin, pScale, SpriteEffects.None, 0f);
                }

                // Draw Text (Only for center, uses the same plink scale)
                if (isCenter && BattleDataCache.PartyMembers.TryGetValue(charId, out var data))
                {
                    string name = data.Name.ToUpper();
                    Vector2 nameSize = font.MeasureString(name);
                    Vector2 namePos = new Vector2(centerX - nameSize.X / 2, centerY + 26);
                    TextAnimator.DrawTextWithEffect(spriteBatch, font, name, namePos, _global.Palette_LightPale, TextEffectType.None, 0f, new Vector2(pScale), null, pRot);

                    string numberText = (spriteIndex + 1).ToString();
                    Vector2 numSize = tertiaryFont.MeasureString(numberText);
                    Vector2 numPos = new Vector2(centerX - numSize.X / 2, centerY + 20);
                    TextAnimator.DrawTextWithEffect(spriteBatch, tertiaryFont, numberText, numPos, _global.Palette_LightPale, TextEffectType.None, 0f, new Vector2(pScale), null, pRot);
                }
            }
        }
    }
}