#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Animations;
using ProjectVagabond.Battle;
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
        private readonly Random _random = new Random();

        private List<string> _characterIds = new();
        private Dictionary<string, (float Timer, float TargetRotation)> _selectedWizards = new();

        // UI Elements
        private Button _btnMode1v1 = null!;
        private Button _btnMode4ffa = null!;
        private Button _btnMode6ffa = null!;
        private Button _btnMode8ffa = null!;
        private Button _startButton = null!;
        private readonly NavigationGroup _navigationGroup;

        private int _randomCount = 6;

        // Intro Text
        private const string INTRO_LINE_1 = "CHOOSE A";
        private const string INTRO_LINE_2 = "GAME MODE";

        // --- Plink Animation State ---
        private bool _isPlinkingIn = true;
        private PlinkAnimator _plinkTitle1 = null!;
        private PlinkAnimator _plinkTitle2 = null!;
        private List<PlinkAnimator> _allPlinks = new List<PlinkAnimator>();

        private Queue<Action> _plinkQueue = new Queue<Action>();
        private float _plinkTimer = 0f;
        private const float PLINK_STAGGER = 0.05f;

        private float _titleWaveTimer = 0f;

        // Slot Machine State
        private enum SlotState { WindUp, Spinning, Settling, Stopped }

        private class SlotColumn
        {
            public float VirtualIndex;
            public float Speed;
            public float MaxSpeed;
            public int TargetIdIndex;
            public float StartIndex;
            public float AbsoluteTargetIndex;
            public string TargetId = "";
            public SlotState State;
            public float StateTimer;
            public int LastPassedIndex;
            public float SettleDuration;
            public PlinkAnimator Plink = new PlinkAnimator();
        }

        private enum IntroState { SelectingMode, Spinning, Transitioning }
        private IntroState _state = IntroState.SelectingMode;
        private List<SlotColumn> _slots = new();
        private float _spinTimer = 0f;
        private int _slotsStopped = 0;

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

            _selectedWizards.Clear();
            _titleWaveTimer = 0f;
            _state = IntroState.SelectingMode;
            _slots.Clear();

            _navigationGroup.DeselectAll();

            _isPlinkingIn = true;
            _allPlinks.Clear();
            _plinkQueue.Clear();

            _plinkTitle1 = new PlinkAnimator(); _allPlinks.Add(_plinkTitle1);
            _plinkTitle2 = new PlinkAnimator(); _allPlinks.Add(_plinkTitle2);

            foreach (var p in _allPlinks) p.Start(9999f, 0.25f);

            _btnMode1v1.SetHiddenForEntrance();
            _btnMode4ffa.SetHiddenForEntrance();
            _btnMode6ffa.SetHiddenForEntrance();
            _btnMode8ffa.SetHiddenForEntrance();
            _startButton.SetHiddenForEntrance();

            _allPlinks.Add(_btnMode1v1.Plink);
            _allPlinks.Add(_btnMode4ffa.Plink);
            _allPlinks.Add(_btnMode6ffa.Plink);
            _allPlinks.Add(_btnMode8ffa.Plink);
            _allPlinks.Add(_startButton.Plink);

            var randomActions = new List<Action>
            {
                () => _plinkTitle1.Start(0f, 0.25f),
                () => _plinkTitle2.Start(0f, 0.25f)
            };

            randomActions = randomActions.OrderBy(x => _random.Next()).ToList();

            foreach (var a in randomActions) _plinkQueue.Enqueue(a);

            _plinkQueue.Enqueue(() => _btnMode1v1.PlayEntrance(0f));
            _plinkQueue.Enqueue(() => _btnMode4ffa.PlayEntrance(0f));
            _plinkQueue.Enqueue(() => _btnMode6ffa.PlayEntrance(0f));
            _plinkQueue.Enqueue(() => _btnMode8ffa.PlayEntrance(0f));
            _plinkQueue.Enqueue(() => _startButton.PlayEntrance(0f));

            _plinkTimer = 0f;
        }

        private void InitializeData()
        {
            _characterIds = GameDataCache.WizardCats.Keys.ToList();
            _characterIds.Sort((a, b) =>
            {
                if (int.TryParse(a, out int idA) && int.TryParse(b, out int idB))
                    return idA.CompareTo(idB);
                return string.Compare(a, b, StringComparison.Ordinal);
            });
        }

        private void InitializeUI()
        {
            _navigationGroup.Clear();
            var core = ServiceLocator.Get<Core>();
            var secondaryFont = core.SecondaryFont;

            int centerX = Global.VIRTUAL_WIDTH / 2;

            int btnW = 50;
            int btnH = 16;
            int gridStartX = centerX - 51;
            int gridStartY = 80;
            int gridSpacingX = 52;
            int gridSpacingY = 18;

            _btnMode1v1 = new Button(new Rectangle(gridStartX, gridStartY, btnW, btnH), "1v1", font: secondaryFont)
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                CustomDefaultTextColor = _global.Palette_Off,
                CustomHoverTextColor = _global.Palette_Off,
                CustomSelectedTextColor = _global.Palette_Off,
                CustomDisabledTextColor = _global.Palette_Off
            };
            _btnMode1v1.OnClick += () => SetMode(2);
            _navigationGroup.Add(_btnMode1v1);

            _btnMode4ffa = new Button(new Rectangle(gridStartX + gridSpacingX, gridStartY, btnW, btnH), "4 FFA", font: secondaryFont)
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                CustomDefaultTextColor = _global.Palette_Off,
                CustomHoverTextColor = _global.Palette_Off,
                CustomSelectedTextColor = _global.Palette_Off,
                CustomDisabledTextColor = _global.Palette_Off
            };
            _btnMode4ffa.OnClick += () => SetMode(4);
            _navigationGroup.Add(_btnMode4ffa);

            _btnMode6ffa = new Button(new Rectangle(gridStartX, gridStartY + gridSpacingY, btnW, btnH), "6 FFA", font: secondaryFont)
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                CustomDefaultTextColor = _global.Palette_Off,
                CustomHoverTextColor = _global.Palette_Off,
                CustomSelectedTextColor = _global.Palette_Off,
                CustomDisabledTextColor = _global.Palette_Off
            };
            _btnMode6ffa.OnClick += () => SetMode(6);
            _navigationGroup.Add(_btnMode6ffa);

            _btnMode8ffa = new Button(new Rectangle(gridStartX + gridSpacingX, gridStartY + gridSpacingY, btnW, btnH), "8 FFA", font: secondaryFont)
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop,
                CustomDefaultTextColor = _global.Palette_Off,
                CustomHoverTextColor = _global.Palette_Off,
                CustomSelectedTextColor = _global.Palette_Off,
                CustomDisabledTextColor = _global.Palette_Off
            };
            _btnMode8ffa.OnClick += () => SetMode(8);
            _navigationGroup.Add(_btnMode8ffa);

            string startText = "START";
            Vector2 startSize = core.DefaultFont.MeasureString(startText);
            int startW = (int)startSize.X + 10;
            int startH = (int)startSize.Y + 6;
            int startButtonY = 146;

            _startButton = new Button(
                new Rectangle(Global.VIRTUAL_WIDTH / 2 - startW / 2, startButtonY, startW, startH),
                startText,
                font: core.DefaultFont
            )
            {
                TriggerHapticOnHover = true,
                HoverAnimation = HoverAnimationType.Hop
            };
            _startButton.OnClick += StartGame;
            _navigationGroup.Add(_startButton);
        }

        private void SetMode(int count)
        {
            if (_isPlinkingIn || _state != IntroState.SelectingMode) return;
            if (_randomCount != count)
            {
                _randomCount = count;
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

                if (count == 2) _btnMode1v1.Plink.Start(0f, 0.15f);
                else if (count == 4) _btnMode4ffa.Plink.Start(0f, 0.15f);
                else if (count == 6) _btnMode6ffa.Plink.Start(0f, 0.15f);
                else if (count == 8) _btnMode8ffa.Plink.Start(0f, 0.15f);
            }
        }

        private void StartGame()
        {
            if (_isPlinkingIn || _transitionManager.IsTransitioning || _state != IntroState.SelectingMode) return;
            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);

            _state = IntroState.Spinning;
            _slots.Clear();
            var shuffled = _characterIds.OrderBy(x => _random.Next()).ToList();
            _selectedWizards.Clear();

            for (int i = 0; i < _randomCount; i++)
            {
                _slots.Add(new SlotColumn
                {
                    VirtualIndex = _random.Next(0, _characterIds.Count),
                    MaxSpeed = 30f + (float)_random.NextDouble() * 20f,
                    Speed = 0f,
                    TargetId = shuffled[i],
                    TargetIdIndex = _characterIds.IndexOf(shuffled[i]),
                    State = SlotState.WindUp,
                    StateTimer = 0f,
                    LastPassedIndex = -1
                });
            }

            _spinTimer = 0f;
            _slotsStopped = 0;

            _btnMode1v1.SetHiddenForEntrance();
            _btnMode4ffa.SetHiddenForEntrance();
            _btnMode6ffa.SetHiddenForEntrance();
            _btnMode8ffa.SetHiddenForEntrance();
            _startButton.SetHiddenForEntrance();
        }

        private void StartGameActual()
        {
            var core = ServiceLocator.Get<Core>();
            var gameState = ServiceLocator.Get<GameState>();

            var loadingTasks = new List<LoadingTask>
            {
                new GenericTask("Initializing arena...", () =>
                {
                    gameState.InitializeWorld(_selectedWizards.Keys.ToList());
                })
            };

            core.SetGameLoaded(true);

            var transitionOut = _transitionManager.GetRandomTransition();
            var transitionIn = _transitionManager.GetRandomTransition();
            _sceneManager.ChangeScene(GameSceneState.Arena, transitionOut, transitionIn, 0f, loadingTasks);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);
            float dt = (float)effectiveGameTime.ElapsedGameTime.TotalSeconds;

            if (_transitionManager.IsTransitioning) return;

            _titleWaveTimer += dt;

            foreach (var key in _selectedWizards.Keys.ToList())
            {
                var data = _selectedWizards[key];
                _selectedWizards[key] = (data.Timer + dt, data.TargetRotation);
            }

            var currentMouseState = _inputManager.GetEffectiveMouseState();

            if (_isPlinkingIn)
            {
                _plinkTimer -= dt;
                while (_plinkTimer <= 0f && _plinkQueue.Count > 0)
                {
                    _plinkQueue.Dequeue().Invoke();
                    _plinkTimer += PLINK_STAGGER;
                }

                int centerX = Global.VIRTUAL_WIDTH / 2;
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

                _plinkTitle1.Update(effectiveGameTime, new Vector2(centerX, 14));
                _plinkTitle2.Update(effectiveGameTime, new Vector2(centerX, 14 + secondaryFont.LineHeight + 2));

                if (_plinkQueue.Count == 0 && !_allPlinks.Any(p => p.IsActive))
                {
                    _isPlinkingIn = false;
                }
            }
            else
            {
                if (_state == IntroState.SelectingMode)
                {
                    _btnMode1v1.HoverAnimation = _randomCount == 2 ? HoverAnimationType.None : HoverAnimationType.Hop;
                    _btnMode4ffa.HoverAnimation = _randomCount == 4 ? HoverAnimationType.None : HoverAnimationType.Hop;
                    _btnMode6ffa.HoverAnimation = _randomCount == 6 ? HoverAnimationType.None : HoverAnimationType.Hop;
                    _btnMode8ffa.HoverAnimation = _randomCount == 8 ? HoverAnimationType.None : HoverAnimationType.Hop;

                    _btnMode1v1.Update(currentMouseState);
                    _btnMode4ffa.Update(currentMouseState);
                    _btnMode6ffa.Update(currentMouseState);
                    _btnMode8ffa.Update(currentMouseState);
                    _startButton.Update(currentMouseState);

                    if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
                    {
                        _navigationGroup.DeselectAll();
                    }
                    else
                    {
                        if (!_btnMode1v1.IsSelected && !_btnMode4ffa.IsSelected && !_btnMode6ffa.IsSelected && !_btnMode8ffa.IsSelected && !_startButton.IsSelected)
                        {
                            _navigationGroup.Select(0);
                        }
                        _navigationGroup.UpdateInput(_inputManager);
                    }

                    if (_inputManager.Back)
                    {
                        _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff);
                    }
                }
                else if (_state == IntroState.Spinning)
                {
                    _spinTimer += dt;
                    float stopInterval = 0.8f; // Increased to keep total time similar despite faster settle
                    int shouldBeStopping = Math.Min(_randomCount, (int)(_spinTimer / stopInterval));

                    for (int i = 0; i < _slots.Count; i++)
                    {
                        var slot = _slots[i];
                        if (slot.State == SlotState.Stopped)
                        {
                            slot.Plink.Update(effectiveGameTime, Vector2.Zero);
                            continue;
                        }

                        slot.StateTimer += dt;

                        if (slot.State == SlotState.WindUp)
                        {
                            float windUpDuration = 0.3f;
                            float p = slot.StateTimer / windUpDuration;
                            slot.Speed = MathHelper.Lerp(0, -15f, Easing.EaseInQuad(p));
                            slot.VirtualIndex += slot.Speed * dt;

                            if (slot.StateTimer >= windUpDuration)
                            {
                                slot.State = SlotState.Spinning;
                                slot.StateTimer = 0f;
                            }
                        }
                        else if (slot.State == SlotState.Spinning)
                        {
                            slot.Speed = MathHelper.Lerp(slot.Speed, slot.MaxSpeed, dt * 4f);
                            slot.VirtualIndex += slot.Speed * dt;

                            if (i < shouldBeStopping)
                            {
                                float currentMod = slot.VirtualIndex % _characterIds.Count;
                                if (currentMod < 0) currentMod += _characterIds.Count;

                                float diff = slot.TargetIdIndex - currentMod;
                                if (diff < 0) diff += _characterIds.Count;

                                float baseSettleDuration = 1.1f;
                                float derivative = 4.70158f; // Derivative of EaseOutBack at t=0
                                float targetDistance = slot.Speed * baseSettleDuration / derivative;

                                // Find the optimal number of rotations to match the current speed smoothly
                                float diff1 = diff;
                                while (diff1 < targetDistance) diff1 += _characterIds.Count;
                                float diff2 = diff1 - _characterIds.Count;

                                if (diff2 > 0 && Math.Abs(diff2 - targetDistance) < Math.Abs(diff1 - targetDistance))
                                {
                                    diff = diff2;
                                }
                                else
                                {
                                    diff = diff1;
                                }

                                // Adjust the settle duration slightly to make the math perfectly continuous
                                slot.SettleDuration = diff * derivative / slot.Speed;
                                slot.SettleDuration = Math.Clamp(slot.SettleDuration, 0.7f, 1.5f);

                                slot.StartIndex = slot.VirtualIndex;
                                slot.AbsoluteTargetIndex = slot.VirtualIndex + diff;
                                slot.State = SlotState.Settling;
                                slot.StateTimer = 0f;
                            }
                        }
                        else if (slot.State == SlotState.Settling)
                        {
                            float p = Math.Clamp(slot.StateTimer / slot.SettleDuration, 0f, 1f);

                            float prevIndex = slot.VirtualIndex;
                            float ease = Easing.EaseOutBack(p); // Changed to a tighter, snappier spring
                            slot.VirtualIndex = MathHelper.Lerp(slot.StartIndex, slot.AbsoluteTargetIndex, ease);

                            if (dt > 0) slot.Speed = (slot.VirtualIndex - prevIndex) / dt;

                            if (p >= 1f)
                            {
                                slot.VirtualIndex = slot.AbsoluteTargetIndex;
                                slot.Speed = 0f;
                                slot.State = SlotState.Stopped;
                                _slotsStopped++;

                                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength * 2f);
                                slot.Plink.Start(0f, 0.3f);
                                _selectedWizards[slot.TargetId] = (0f, 0f);

                                var emitter = _particleSystemManager.CreateEmitter(ParticleEffects.CreateUIPlink());
                                int slotWidth = (Global.VIRTUAL_WIDTH - 40) / _randomCount;
                                int startX = 20 + (i * slotWidth);
                                int centerX = startX + (slotWidth / 2);
                                emitter.Position = new Vector2(centerX, Global.VIRTUAL_HEIGHT / 2f + 10);
                                emitter.EmitBurst(15);
                            }
                        }

                        int currentIndex = (int)MathF.Floor(slot.VirtualIndex);
                        if (currentIndex != slot.LastPassedIndex && slot.State != SlotState.Stopped)
                        {
                            slot.LastPassedIndex = currentIndex;
                            if (Math.Abs(slot.Speed) > 5f)
                            {
                                _hapticsManager.TriggerUICompoundShake(0.1f);
                            }
                        }
                    }

                    if (_slotsStopped == _randomCount)
                    {
                        _state = IntroState.Transitioning;
                        _spinTimer = 0f;
                    }
                }
                else if (_state == IntroState.Transitioning)
                {
                    _spinTimer += dt;
                    for (int i = 0; i < _slots.Count; i++)
                    {
                        _slots[i].Plink.Update(effectiveGameTime, Vector2.Zero);
                    }

                    if (_spinTimer > 1.5f)
                    {
                        StartGameActual();
                    }
                }
            }
        }

        private void DrawModeButtonBackground(SpriteBatch spriteBatch, Button btn, int modeValue, Texture2D pixel)
        {
            float scale = btn.Plink.IsActive ? btn.Plink.Scale : 1f;
            float rotation = btn.Plink.IsActive ? btn.Plink.Rotation : 0f;
            if (scale < 0.01f) return;

            float yOffset = 0f;
            Color bgColor;

            if (_randomCount == modeValue)
            {
                yOffset = 0f;
                bgColor = _global.Palette_Sun;
            }
            else
            {
                yOffset = btn.HoverAnimator.CurrentOffset;
                if (btn.IsPressed) bgColor = _global.Palette_Fruit;
                else if (btn.IsHovered) bgColor = _global.ButtonHoverColor;
                else bgColor = _global.Palette_DarkestPale;
            }

            Vector2 center = new Vector2(btn.Bounds.Center.X, btn.Bounds.Center.Y + yOffset);

            int w = btn.Bounds.Width;
            int h = btn.Bounds.Height;

            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), bgColor, rotation, new Vector2(0.5f, 0.5f), new Vector2(w - 4, h) * scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), bgColor, rotation, new Vector2(0.5f, 0.5f), new Vector2(w - 2, h - 2) * scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), bgColor, rotation, new Vector2(0.5f, 0.5f), new Vector2(w, h - 4) * scale, SpriteEffects.None, 0f);
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);

            var core = ServiceLocator.Get<Core>();
            var secondaryFont = core.SecondaryFont;
            var tertiaryFont = core.TertiaryFont;
            Matrix staticTransform = Matrix.CreateScale(core.FinalScale, core.FinalScale, 1.0f) *
                                     Matrix.CreateTranslation(core.FinalRenderRectangle.X, core.FinalRenderRectangle.Y, 0);

            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, null, null, null, staticTransform);
            spriteBatch.Draw(_spriteManager.EmptySprite, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, staticTransform);

            float titleY = 14f;

            string line1 = _state == IntroState.SelectingMode ? INTRO_LINE_1 : (_state == IntroState.Spinning ? "SELECTING" : "GET");
            string line2 = _state == IntroState.SelectingMode ? INTRO_LINE_2 : (_state == IntroState.Spinning ? "COMBATANTS..." : "READY!");

            float t1Scale = _isPlinkingIn ? _plinkTitle1.Scale : 1f;
            float t1Rot = _isPlinkingIn ? _plinkTitle1.Rotation : 0f;
            if (t1Scale > 0.01f)
            {
                Vector2 size1 = secondaryFont.MeasureString(line1);
                var pos1 = new Vector2(MathF.Round((Global.VIRTUAL_WIDTH - size1.X) / 2f), MathF.Round(titleY - 2));
                TextAnimator.DrawTextWithEffect(spriteBatch, secondaryFont, line1, pos1, _global.Palette_DarkPale, TextEffectType.None, 0f, new Vector2(t1Scale), null, t1Rot);

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
                Vector2 size2 = font.MeasureString(line2);
                var pos2 = new Vector2(MathF.Round((Global.VIRTUAL_WIDTH - size2.X) / 2f), MathF.Round(titleY + secondaryFont.LineHeight + 2));
                TextAnimator.DrawTextWithEffect(spriteBatch, font, line2, pos2, _global.Palette_White, TextEffectType.RainbowWave, _titleWaveTimer, new Vector2(t2Scale), null, t2Rot);

                if (_isPlinkingIn && _plinkTitle2.FlashTint.HasValue)
                {
                    Rectangle bounds = new Rectangle((int)pos2.X, (int)pos2.Y, (int)size2.X, (int)size2.Y);
                    spriteBatch.DrawSnapped(_spriteManager.EmptySprite, bounds, _plinkTitle2.FlashTint.Value);
                }
            }

            if (_state == IntroState.SelectingMode)
            {
                var pixel = ServiceLocator.Get<Texture2D>();
                DrawModeButtonBackground(spriteBatch, _btnMode1v1, 2, pixel);
                DrawModeButtonBackground(spriteBatch, _btnMode4ffa, 4, pixel);
                DrawModeButtonBackground(spriteBatch, _btnMode6ffa, 6, pixel);
                DrawModeButtonBackground(spriteBatch, _btnMode8ffa, 8, pixel);

                _btnMode1v1.Draw(spriteBatch, secondaryFont, effectiveGameTime, Matrix.Identity);
                _btnMode4ffa.Draw(spriteBatch, secondaryFont, effectiveGameTime, Matrix.Identity);
                _btnMode6ffa.Draw(spriteBatch, secondaryFont, effectiveGameTime, Matrix.Identity);
                _btnMode8ffa.Draw(spriteBatch, secondaryFont, effectiveGameTime, Matrix.Identity);

                Color startColor = (_startButton.IsHovered || _startButton.IsSelected) ? _global.ButtonHoverColor : _global.GameTextColor;
                _startButton.Draw(spriteBatch, core.DefaultFont, effectiveGameTime, Matrix.Identity, false, null, null, startColor);
            }
            else
            {
                spriteBatch.End();

                int slotWidth = (Global.VIRTUAL_WIDTH - 40) / _randomCount;
                int startX = 20;
                int centerY = Global.VIRTUAL_HEIGHT / 2 + 10;
                int clipHeight = 100;
                int clipY = centerY - (clipHeight / 2);

                var pixel = ServiceLocator.Get<Texture2D>();

                var graphics = ServiceLocator.Get<GraphicsDeviceManager>();
                Rectangle screenScissor = new Rectangle(
                    (int)(startX * core.FinalScale) + core.FinalRenderRectangle.X,
                    (int)(clipY * core.FinalScale) + core.FinalRenderRectangle.Y,
                    (int)((slotWidth * _randomCount) * core.FinalScale),
                    (int)(clipHeight * core.FinalScale)
                );

                var originalRasterizerState = new RasterizerState { ScissorTestEnable = true };
                graphics.GraphicsDevice.ScissorRectangle = screenScissor;

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, originalRasterizerState, null, staticTransform);

                for (int i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];
                    int slotCenterX = startX + (i * slotWidth) + (slotWidth / 2);

                    float vIndex = slot.VirtualIndex;
                    int centerCatIdx = ((int)MathF.Floor(vIndex) % _characterIds.Count + _characterIds.Count) % _characterIds.Count;
                    float offset = vIndex - MathF.Floor(vIndex);

                    for (int j = -2; j <= 2; j++)
                    {
                        int catIdx = (centerCatIdx + j) % _characterIds.Count;
                        if (catIdx < 0) catIdx += _characterIds.Count;

                        string charId = _characterIds[catIdx];
                        int spriteIndex = int.Parse(charId);

                        float yPos = centerY + ((j - offset) * 40f);

                        float pScale = 1f;
                        float pRot = 0f;
                        if (slot.State == SlotState.Stopped && j == 0)
                        {
                            pScale = slot.Plink.IsActive ? slot.Plink.Scale : 1f;
                            pRot = slot.Plink.IsActive ? slot.Plink.Rotation : 0f;
                        }

                        float speedStretch = 1f + (Math.Abs(slot.Speed) * 0.015f);
                        float blurAlpha = Math.Clamp(1f - (Math.Abs(slot.Speed) * 0.01f), 0.4f, 1f);
                        Vector2 pScaleVec = new Vector2(pScale, pScale * speedStretch);

                        var sourceRect = _spriteManager.GetPlayerSourceRect(spriteIndex, PlayerSpriteType.Normal);
                        Vector2 origin = new Vector2(16, 16);

                        spriteBatch.Draw(_spriteManager.PlayerMasterSpriteSheet, new Vector2(slotCenterX, yPos), sourceRect, Color.White * blurAlpha, pRot, origin, pScaleVec, SpriteEffects.None, 0f);
                    }
                }

                int shadowHeight = 24;
                for (int y = 0; y < shadowHeight; y++)
                {
                    float alpha = 1f - ((float)y / shadowHeight);
                    alpha = Easing.EaseOutQuad(alpha) * 0.85f;

                    spriteBatch.Draw(pixel, new Rectangle(startX, clipY + y, slotWidth * _randomCount, 1), Color.Black * alpha);
                    spriteBatch.Draw(pixel, new Rectangle(startX, clipY + clipHeight - 1 - y, slotWidth * _randomCount, 1), Color.Black * alpha);
                }

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, staticTransform);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
        }
    }
}
