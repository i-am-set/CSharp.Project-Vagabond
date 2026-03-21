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

        private int _randomCount = 6;

        // Intro Text
        private const string INTRO_LINE_1 = "SELECTING";
        private const string INTRO_LINE_2 = "COMBATANTS...";

        // --- Plink Animation State ---
        private bool _isPlinkingIn = true;
        private PlinkAnimator _plinkTitle1 = null!;
        private PlinkAnimator _plinkTitle2 = null!;
        private List<PlinkAnimator> _allPlinks = new List<PlinkAnimator>();

        private Queue<Action> _plinkQueue = new Queue<Action>();
        private float _plinkTimer = 0f;
        private const float PLINK_STAGGER = 0.05f;

        private float _titleWaveTimer = 0f;
        private float _idleTimer = 0f;

        // Slot Machine State
        private enum SlotState { WindUp, Spinning, Settling, Stopped }

        private const float SLOT_SHAKE_DURATION = 0.35f;
        private const float SLOT_SHAKE_MAGNITUDE = 8f;
        private const float SLOT_SHAKE_FREQUENCY = 60f;

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
            public float SettleDuration;
            public PlinkAnimator Plink = new PlinkAnimator();
            public bool HasHopped;
            public float ShakeTimer;
        }

        private enum IntroState { Spinning, Transitioning }
        private IntroState _state = IntroState.Spinning;
        private List<SlotColumn> _slots = new();
        private float _spinTimer = 0f;
        private int _slotsStopped = 0;
        private float _spinSoundTimer = 0f;
        private bool _hasPlayedFinishEffect = false;

        public NewGameIntroScene()
        {
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
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

            _selectedWizards.Clear();
            _titleWaveTimer = 0f;
            _idleTimer = 0f;
            _state = IntroState.Spinning;
            _slots.Clear();
            _hasPlayedFinishEffect = false;

            _isPlinkingIn = true;
            _allPlinks.Clear();
            _plinkQueue.Clear();

            _plinkTitle1 = new PlinkAnimator(); _allPlinks.Add(_plinkTitle1);
            _plinkTitle2 = new PlinkAnimator(); _allPlinks.Add(_plinkTitle2);

            foreach (var p in _allPlinks) p.Start(9999f, 0.25f);

            var randomActions = new List<Action>
            {
                () => _plinkTitle1.Start(0f, 0.25f),
                () => _plinkTitle2.Start(0f, 0.25f)
            };

            randomActions = randomActions.OrderBy(x => _random.Next()).ToList();

            foreach (var a in randomActions) _plinkQueue.Enqueue(a);

            _plinkTimer = 0f;

            // Setup slots immediately
            var shuffled = _characterIds.OrderBy(x => _random.Next()).ToList();
            for (int i = 0; i < _randomCount; i++)
            {
                float vIndex = _random.Next(0, _characterIds.Count);
                _slots.Add(new SlotColumn
                {
                    VirtualIndex = vIndex,
                    MaxSpeed = 30f + (float)_random.NextDouble() * 20f,
                    Speed = 0f,
                    TargetId = shuffled[i],
                    TargetIdIndex = _characterIds.IndexOf(shuffled[i]),
                    State = SlotState.WindUp,
                    StateTimer = 0f,
                    ShakeTimer = 0f
                });
            }

            _spinTimer = 0f;
            _slotsStopped = 0;
            _spinSoundTimer = 0f;
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
            _idleTimer += dt;

            foreach (var key in _selectedWizards.Keys.ToList())
            {
                var data = _selectedWizards[key];
                _selectedWizards[key] = (data.Timer + dt, data.TargetRotation);
            }

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

            if (_state == IntroState.Spinning)
            {
                _spinTimer += dt;

                if (_slotsStopped < _randomCount)
                {
                    _spinSoundTimer -= dt;
                    if (_spinSoundTimer <= 0f)
                    {
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_hover");

                        float speedUpProgress = Math.Clamp(_spinTimer / 0.5f, 0f, 1f);
                        _spinSoundTimer = MathHelper.Lerp(0.25f, 0.06f, Easing.EaseInQuad(speedUpProgress));
                    }
                }

                float baseSpinDuration = 2.5f;
                float stopInterval = 0.3f;

                int shouldBeStopping = 0;
                if (_spinTimer >= baseSpinDuration)
                {
                    shouldBeStopping = Math.Min(_randomCount, (int)((_spinTimer - baseSpinDuration) / stopInterval) + 1);
                }

                for (int i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];

                    if (slot.ShakeTimer > 0)
                    {
                        slot.ShakeTimer -= dt;
                    }

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

                            diff += _characterIds.Count;

                            slot.SettleDuration = 0.35f;
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
                        float ease = Easing.EaseOutBackBig(p);
                        slot.VirtualIndex = MathHelper.Lerp(slot.StartIndex, slot.AbsoluteTargetIndex, ease);

                        if (dt > 0) slot.Speed = (slot.VirtualIndex - prevIndex) / dt;

                        if (p >= 1f)
                        {
                            slot.VirtualIndex = slot.AbsoluteTargetIndex;
                            slot.Speed = 0f;
                            slot.State = SlotState.Stopped;
                            slot.ShakeTimer = SLOT_SHAKE_DURATION;
                            _slotsStopped++;

                            _hapticsManager.TriggerZoomPulse(_global.HapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                            slot.Plink.Start(0f, 0.3f);
                            _selectedWizards[slot.TargetId] = (0f, 0f);

                            var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();
                            audio.PlaySfx("sfx_slot_lock", 0.1f);

                            var emitter = _particleSystemManager.CreateEmitter(ParticleEffects.CreateUIPlink());
                            int slotWidth = (Global.VIRTUAL_WIDTH - 40) / _randomCount;
                            int startX = 20 + (i * slotWidth);
                            int centerX = startX + (slotWidth / 2);
                            emitter.Position = new Vector2(centerX, Global.VIRTUAL_HEIGHT / 2f + 10);
                            emitter.EmitBurst(15);

                            if (_slotsStopped == _randomCount)
                            {
                                _state = IntroState.Transitioning;
                                _spinTimer = 0f;
                            }
                        }
                    }
                }
            }
            else if (_state == IntroState.Transitioning)
            {
                _spinTimer += dt;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].ShakeTimer > 0) _slots[i].ShakeTimer -= dt;
                    _slots[i].Plink.Update(effectiveGameTime, Vector2.Zero);
                }

                if (!_hasPlayedFinishEffect && _spinTimer >= SLOT_SHAKE_DURATION)
                {
                    _hasPlayedFinishEffect = true;
                    _hapticsManager.TriggerShake(3.0f, 1.5f);
                    ServiceLocator.Get<Core>().TriggerFullscreenFlash(Color.White, 0.1f);
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlaySfx("sfx_slot_finish");
                }

                if (_spinTimer > 1.5f + SLOT_SHAKE_DURATION)
                {
                    StartGameActual();
                }
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            GameTime effectiveGameTime = _inputManager.GetEffectiveGameTime(gameTime, true);

            var core = ServiceLocator.Get<Core>();
            var secondaryFont = core.SecondaryFont;
            var tertiaryFont = core.TertiaryFont;

            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, null, null, null, transform);
            spriteBatch.Draw(_spriteManager.EmptySprite, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            float titleY = 14f;

            float t1Scale = _isPlinkingIn ? _plinkTitle1.Scale : 1f;
            float t1Rot = _isPlinkingIn ? _plinkTitle1.Rotation : 0f;
            if (t1Scale > 0.01f)
            {
                Vector2 size1 = secondaryFont.MeasureString(INTRO_LINE_1);
                var pos1 = new Vector2(MathF.Round((Global.VIRTUAL_WIDTH - size1.X) / 2f), MathF.Round(titleY - 2));
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
                var pos2 = new Vector2(MathF.Round((Global.VIRTUAL_WIDTH - size2.X) / 2f), MathF.Round(titleY + secondaryFont.LineHeight + 2));
                TextAnimator.DrawTextWithEffect(spriteBatch, font, INTRO_LINE_2, pos2, _global.Palette_White, TextEffectType.RainbowWave, _titleWaveTimer, new Vector2(t2Scale), null, t2Rot);

                if (_isPlinkingIn && _plinkTitle2.FlashTint.HasValue)
                {
                    Rectangle bounds = new Rectangle((int)pos2.X, (int)pos2.Y, (int)size2.X, (int)size2.Y);
                    spriteBatch.DrawSnapped(_spriteManager.EmptySprite, bounds, _plinkTitle2.FlashTint.Value);
                }
            }

            spriteBatch.End();

            int slotWidth = (Global.VIRTUAL_WIDTH - 40) / _randomCount;
            int startX = 20;
            int centerY = Global.VIRTUAL_HEIGHT / 2 + 10;
            int clipHeight = 100;
            int clipY = centerY - (clipHeight / 2);

            var pixel = ServiceLocator.Get<Texture2D>();

            var graphics = ServiceLocator.Get<GraphicsDeviceManager>();

            Vector2 topLeft = Vector2.Transform(new Vector2(startX, clipY), transform);
            Vector2 bottomRight = Vector2.Transform(new Vector2(startX + (slotWidth * _randomCount), clipY + clipHeight), transform);

            Rectangle screenScissor = new Rectangle(
                (int)Math.Min(topLeft.X, bottomRight.X),
                (int)Math.Min(topLeft.Y, bottomRight.Y),
                (int)Math.Abs(bottomRight.X - topLeft.X),
                (int)Math.Abs(bottomRight.Y - topLeft.Y)
            );

            var originalRasterizerState = new RasterizerState { ScissorTestEnable = true };
            graphics.GraphicsDevice.ScissorRectangle = screenScissor;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, originalRasterizerState, null, transform);

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];

                float slotShakeX = 0f;
                if (slot.ShakeTimer > 0)
                {
                    float progress = 1f - (slot.ShakeTimer / SLOT_SHAKE_DURATION);
                    float decay = 1f - Easing.EaseOutQuad(progress);
                    slotShakeX = MathF.Sin(slot.ShakeTimer * SLOT_SHAKE_FREQUENCY) * SLOT_SHAKE_MAGNITUDE * decay;
                }

                int slotCenterX = startX + (i * slotWidth) + (slotWidth / 2) + (int)MathF.Round(slotShakeX);

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
                    float drawYPos = yPos;

                    PlayerSpriteType spriteType = PlayerSpriteType.Normal;

                    float pScale = 1f;
                    float pRot = 0f;
                    if (slot.State == SlotState.Stopped && j == 0)
                    {
                        pScale = slot.Plink.IsActive ? slot.Plink.Scale : 1f;
                        pRot = slot.Plink.IsActive ? slot.Plink.Rotation : 0f;

                        float bob = MathF.Sin(_idleTimer * 6f);
                        if (bob > 0)
                        {
                            spriteType = PlayerSpriteType.Alt;
                            drawYPos -= 2f;
                        }
                    }

                    float speedStretch = 1f + (Math.Abs(slot.Speed) * 0.015f);
                    float blurAlpha = Math.Clamp(1f - (Math.Abs(slot.Speed) * 0.01f), 0.7f, 1f);
                    float squashX = 1f / (1f + (speedStretch - 1f) * 0.5f);
                    Vector2 pScaleVec = new Vector2(pScale * squashX, pScale * speedStretch);

                    var sourceRect = _spriteManager.GetPlayerSourceRect(spriteIndex, spriteType);
                    Vector2 origin = new Vector2(16, 16);

                    spriteBatch.Draw(_spriteManager.PlayerMasterSpriteSheet, new Vector2(slotCenterX, drawYPos), sourceRect, Color.White * blurAlpha, pRot, origin, pScaleVec, SpriteEffects.None, 0f);
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

            _particleSystemManager.Draw(spriteBatch, transform, 1); // Foreground particles (Plinks)

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
        }
    }
}