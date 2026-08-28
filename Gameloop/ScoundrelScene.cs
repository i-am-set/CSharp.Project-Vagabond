using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public enum ScoundrelState { Intro, Dealing, Playing, Focused, ResolvingMonster, GameOver, Restarting, FloorCleared, CleaningUp, SweepingBoard, Reward, TreasureOpening, Roulette, RouletteFinished }

    public class ScoundrelScene : GameScene
    {
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly InputManager _inputManager;
        private readonly SceneManager _sceneManager;
        private readonly HapticsManager _hapticsManager;
        private readonly Core _core;
        private readonly Texture2D _pixel;

        private RunContext _runContext;

        private ScoundrelState _state;
        private ScoundrelBoardController _board;
        private ScoundrelUIController _ui;
        private ScoundrelCombatController _combat;

        private float _introTimer;
        private const float INTRO_DURATION = 1.0f;
        private int _cardsLanded = 0;
        private bool _introAligning = false;

        private float _dealTimer;
        private const float DEAL_INTERVAL = 0.15f;

        private float _previewFlashTimer = 0f;
        private Card? _lastHoveredCard = null;

        private MouseState _previousMouseState;
        private bool _uiInitialized = false;

        private bool _isPaused = false;

        private float _restartHoldTimer = 0f;
        private const float RESTART_HOLD_DURATION = 1.0f;
        private float _returnTimer = 0f;

        private float _floorClearedTimer = 0f;

        private float _tallyTimer = 0f;
        private int _tallyPhase = 0;

        private float _sweepTimer = 0f;

        // --- Roulette Tuning ---
        private const float ROULETTE_BASE_SPIN_DURATION = 10.0f;
        private const int ROULETTE_BELT_LENGTH = 300;

        private List<int> _rouletteBelt = new List<int>();
        private float _rouletteOffset = 0f;
        private float _rouletteTargetOffset = 0f;
        private float _rouletteSpinTimer = 0f;
        private float _rouletteSpinDuration = ROULETTE_BASE_SPIN_DURATION;

        private Card _activeTreasureCard;
        private float _treasureAnimTimer = 0f;
        private float _rouletteModalAlpha = 0f;
        private float _rouletteWinPlinkTimer = 0f;
        private float _roulettePhaseTimer = 0f;
        private bool _isRouletteFromReward = false;
        private float _rouletteNeedleRotation = 0f;
        private float _rouletteNeedleVelocity = 0f;

        private readonly List<Card> _selectableCardsCache = new List<Card>(50);
        private readonly List<Card> _unselectableCardsCache = new List<Card>(50);

        private readonly Random _random = new Random();

        private float _rewardTimer = 0f;
        private int _lastSecond = -1;
        private int _previousScore = 0;
        private float _timerPopTimer = 1f;

        private float _timeBonusTimer = 0f;
        private int _timeBonusAmount = 0;
        private ScoundrelState _timeBonusNextState;

        public ScoundrelScene()
        {
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _core = ServiceLocator.Get<Core>();
            _pixel = ServiceLocator.Get<Texture2D>();
        }

        public override Rectangle GetAnimatedBounds() => new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

        public override void Initialize()
        {
            base.Initialize();
            try { _runContext = ServiceLocator.Get<RunContext>(); }
            catch { _runContext = new RunContext(); ServiceLocator.Register(_runContext); }

            _board = new ScoundrelBoardController();
            _ui = new ScoundrelUIController(this);
            _combat = new ScoundrelCombatController();
        }

        public override void Enter()
        {
            base.Enter();

            if (!_uiInitialized)
            {
                _ui.Initialize();
                _uiInitialized = true;
            }

            var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();
            audio.MusicPitchOffset = 0f;
            audio.PlayMusic("music_battle", 1.0f);
            audio.SetCurrentMusicStemVolume(0, 1.0f);
            audio.SetCurrentMusicStemVolume(1, 0.0f);

            _isPaused = false;
            _ui.ConfirmationDialog.Hide();

            if (SaveManager.CurrentSave != null)
            {
                RestoreFromSave();
            }
            else
            {
                RestartGame();
            }

            _previousScore = _runContext.CurrentScore;
            _previousMouseState = _inputManager.GetEffectiveMouseState();
        }

        private void RestoreFromSave()
        {
            var data = SaveManager.CurrentSave;

            _runContext.Mode = data.Mode;
            _runContext.Floor = data.Floor;
            _runContext.CurrentScore = data.CurrentScore;
            _runContext.MaxHealth = data.MaxHealth;
            _runContext.Health = data.Health;
            _runContext.RelicSeed = data.RelicSeed;

            _combat.Reset(_runContext.Health, _runContext.RoomTimeLimit);
            _combat.Health = data.Health;
            _combat.TimeRemaining = data.TimeRemaining;
            _lastSecond = (int)Math.Ceiling(_combat.TimeRemaining);
            _combat.TotalCardsInFloor = data.TotalCardsInFloor;
            _combat.CardsResolvedThisRoom = data.CardsResolvedThisRoom;
            _combat.PotionsUsedThisRoom = data.PotionsUsedThisRoom;
            _combat.CanSkip = data.CanSkip;
            _combat.PocketLocked = data.PocketLocked;

            _board.Reset();

            foreach (var cd in data.Deck) _board.Deck.Add(CreateCardFromData(cd));
            foreach (var cd in data.Room) _board.Room.Add(CreateCardFromData(cd));
            foreach (var cd in data.Discard) _board.Discard.Add(CreateCardFromData(cd));
            foreach (var cd in data.SlainPile) _board.SlainPile.Add(CreateCardFromData(cd));
            if (data.WeaponSlot != null) _board.WeaponSlot = CreateCardFromData(data.WeaponSlot);
            if (data.PocketSlot != null) _board.PocketSlot = CreateCardFromData(data.PocketSlot);

            for (int i = 0; i < _board.Deck.Count; i++)
            {
                _board.Deck[i].Position = _board.DeckPos + new Vector2(0, -i * 0.25f);
                _board.Deck[i].TargetPosition = _board.Deck[i].Position;
                _board.Deck[i].ZIndex = i;
                _board.Deck[i].IsFaceUp = false;
                _board.Deck[i].Scale = Vector2.One;
                _board.Deck[i].TargetScale = Vector2.One;
            }

            foreach (var c in _board.Room)
            {
                c.Position = _board.RoomPositions[c.RoomSlotIndex];
                c.TargetPosition = c.Position;
                c.ZIndex = 100 + c.RoomSlotIndex;
                c.IsFaceUp = true;
                c.Scale = Vector2.One;
                c.TargetScale = Vector2.One;
            }

            for (int i = 0; i < _board.Discard.Count; i++)
            {
                _board.Discard[i].Position = _board.DiscardPos + new Vector2(0, -i * 0.25f);
                _board.Discard[i].TargetPosition = _board.Discard[i].Position;
                _board.Discard[i].ZIndex = 50 + i;
                _board.Discard[i].IsFaceUp = true;
                _board.Discard[i].Scale = Vector2.One;
                _board.Discard[i].TargetScale = Vector2.One;
            }

            if (_board.WeaponSlot != null)
            {
                _board.WeaponSlot.Position = _board.WeaponPos;
                _board.WeaponSlot.TargetPosition = _board.WeaponPos;
                _board.WeaponSlot.ZIndex = 200;
                _board.WeaponSlot.IsFaceUp = true;
                _board.WeaponSlot.Scale = Vector2.One;
                _board.WeaponSlot.TargetScale = Vector2.One;

                for (int i = 0; i < _board.SlainPile.Count; i++)
                {
                    if (_runContext.Mode == GameMode.Classic)
                    {
                        _board.SlainPile[i].Position = _board.WeaponPos + new Vector2(7, 0);
                        _board.SlainPile[i].TargetPosition = _board.SlainPile[i].Position;
                    }
                    else
                    {
                        if (i == 0) _board.SlainPile[i].Position = _board.WeaponPos + new Vector2(10, -7);
                        else if (i == 1) _board.SlainPile[i].Position = _board.WeaponPos + new Vector2(10, 7);
                        _board.SlainPile[i].TargetPosition = _board.SlainPile[i].Position;
                    }
                    _board.SlainPile[i].Rotation = MathHelper.PiOver2;
                    _board.SlainPile[i].TargetRotation = MathHelper.PiOver2;
                    _board.SlainPile[i].ZIndex = 150 + i;
                    _board.SlainPile[i].IsFaceUp = true;
                    _board.SlainPile[i].Scale = Vector2.One;
                    _board.SlainPile[i].TargetScale = Vector2.One;
                }
            }

            if (_board.PocketSlot != null)
            {
                _board.PocketSlot.Position = _board.PocketPos + (_combat.PocketLocked ? new Vector2(0, 20) : Vector2.Zero);
                _board.PocketSlot.TargetPosition = _board.PocketPos + (_combat.PocketLocked ? new Vector2(0, 20) : Vector2.Zero);
                _board.PocketSlot.ZIndex = 250;
                _board.PocketSlot.IsFaceUp = true;
                _board.PocketSlot.Scale = Vector2.One;
                _board.PocketSlot.TargetScale = Vector2.One;
            }

            _state = data.State;

            if (_state == ScoundrelState.Focused)
            {
                if (data.FocusedRoomSlotIndex >= 0)
                {
                    _board.FocusedCard = _board.Room.FirstOrDefault(c => c.RoomSlotIndex == data.FocusedRoomSlotIndex);
                    if (_board.FocusedCard != null)
                    {
                        _board.FocusedCard.Position = _board.RoomPositions[_board.FocusedCard.RoomSlotIndex] + new Vector2(0, -3);
                        _board.FocusedCard.TargetPosition = _board.FocusedCard.Position;
                        _board.FocusedCard.ZIndex = 500;
                    }
                }
            }
            else if (_state == ScoundrelState.ResolvingMonster)
            {
                var monster = _board.Room.FirstOrDefault(c => c.RoomSlotIndex == data.ResolvingMonsterRoomSlotIndex);
                if (monster != null)
                {
                    _combat.StartMonsterResolution(monster, data.ResolveDamage, data.ResolveWeaponUsed);
                    monster.ZIndex = 500;
                }
                else
                {
                    _state = ScoundrelState.Playing;
                }
            }
            else if (_state == ScoundrelState.Reward)
            {
                _rewardTimer = 0f;
                _board.RewardCards.Clear();

                var treasure = new Card(CardSuit.None, CardType.Treasure, 0, 0) { IsFaceUp = false };
                treasure.Position = new Vector2(Global.VIRTUAL_WIDTH / 2f - 40, Global.VIRTUAL_HEIGHT / 2f);
                treasure.TargetPosition = treasure.Position;

                var potion = new Card(CardSuit.Hearts, CardType.Potion, 14, 20) { IsFaceUp = false };
                potion.Position = new Vector2(Global.VIRTUAL_WIDTH / 2f + 40, Global.VIRTUAL_HEIGHT / 2f);
                potion.TargetPosition = potion.Position;

                _board.RewardCards.Add(treasure);
                _board.RewardCards.Add(potion);
            }

            _ui.DeckCountPlink.Start(0.5f, 0.3f);
            _ui.DiscardCountPlink.Start(0.6f, 0.3f);
            _ui.HealthPlink.Start(0.7f, 0.4f);

            _previousScore = _runContext.CurrentScore;
            SaveManager.CurrentSave = null;
        }

        private void SaveCurrentState()
        {
            if (_state == ScoundrelState.GameOver || _state == ScoundrelState.Restarting || _state == ScoundrelState.Intro || _state == ScoundrelState.CleaningUp || _state == ScoundrelState.SweepingBoard) return;

            if (_combat.Health <= 0)
            {
                SaveManager.DeleteSave();
                return;
            }

            var data = new ScoundrelSaveData
            {
                Mode = _runContext.Mode,
                Floor = _runContext.Floor,
                CurrentScore = _runContext.CurrentScore,
                MaxHealth = _runContext.MaxHealth,
                Health = _combat.Health,
                TimeRemaining = _combat.TimeRemaining,
                TotalCardsInFloor = _combat.TotalCardsInFloor,
                LastSlainValue = _combat.GetLastSlainValue(_board),
                CardsResolvedThisRoom = _combat.CardsResolvedThisRoom,
                PotionsUsedThisRoom = _combat.PotionsUsedThisRoom,
                CanSkip = _combat.CanSkip,
                PocketLocked = _combat.PocketLocked,
                State = _state,
                FocusedRoomSlotIndex = _board.FocusedCard?.RoomSlotIndex ?? -1,
                ResolvingMonsterRoomSlotIndex = _combat.ResolvingMonster?.RoomSlotIndex ?? -1,
                ResolveDamage = _combat.ResolveDamage,
                ResolveWeaponUsed = _combat.ResolveWeaponUsed,
                RelicSeed = _runContext.RelicSeed,
                WeaponSlot = _board.WeaponSlot != null ? CreateDataFromCard(_board.WeaponSlot) : null,
                PocketSlot = _board.PocketSlot != null ? CreateDataFromCard(_board.PocketSlot) : null
            };

            data.Deck = _board.Deck.Select(CreateDataFromCard).ToList();
            data.Room = _board.Room.Select(CreateDataFromCard).ToList();
            data.Discard = _board.Discard.Select(CreateDataFromCard).ToList();
            data.SlainPile = _board.SlainPile.Select(CreateDataFromCard).ToList();

            SaveManager.SaveGame(data);
        }

        private CardData CreateDataFromCard(Card c)
        {
            return new CardData
            {
                Suit = c.Suit,
                Type = c.Type,
                Rank = c.Rank,
                BaseValue = c.BaseValue,
                IsFaceUp = c.IsFaceUp,
                RoomSlotIndex = c.RoomSlotIndex
            };
        }

        private Card CreateCardFromData(CardData d)
        {
            return new Card(d.Suit, d.Type, d.Rank, d.BaseValue)
            {
                IsFaceUp = d.IsFaceUp,
                RoomSlotIndex = d.RoomSlotIndex
            };
        }

        private void RestartGame()
        {
            _combat.Reset(_runContext.Health, _runContext.RoomTimeLimit);
            _lastSecond = (int)Math.Ceiling(_combat.TimeRemaining);
            _ui.Reset();

            _previewFlashTimer = 0f;
            _lastHoveredCard = null;

            _rewardTimer = 0f;
            _timerPopTimer = 1f;

            _board.Reset();
            _board.Deck.AddRange(DeckGenerator.Generate(_runContext));
            _combat.TotalCardsInFloor = _board.Deck.Count;

            _state = ScoundrelState.Intro;
            _introTimer = 0f;
            _cardsLanded = 0;
            _restartHoldTimer = 0f;
            _introAligning = false;

            for (int i = 0; i < _board.Deck.Count; i++)
            {
                _board.Deck[i].Position = new Vector2(_board.DeckPos.X, -200 - i * 10);
                _board.Deck[i].TargetPosition = _board.Deck[i].Position;
                _board.Deck[i].Scale = Vector2.One;
                _board.Deck[i].TargetScale = Vector2.One;
                _board.Deck[i].ZIndex = i;
                _board.Deck[i].TargetRotation = (float)(_random.NextDouble() - 0.5) * 0.5f;
                _board.Deck[i].Rotation = _board.Deck[i].TargetRotation;
            }

            _ui.DeckCountPlink.Start(0.5f, 0.3f);
            _ui.DiscardCountPlink.Start(0.6f, 0.3f);
            _ui.HealthPlink.Start(0.7f, 0.4f);
        }

        public void ResetBoard()
        {
            SaveManager.DeleteSave();
            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().MusicPitchOffset = 0f;

            _runContext.Reset();
            _combat.Reset(_runContext.Health, _runContext.RoomTimeLimit);
            _lastSecond = (int)Math.Ceiling(_combat.TimeRemaining);
            _ui.Reset();

            _previewFlashTimer = 0f;
            _lastHoveredCard = null;

            _rewardTimer = 0f;
            _timerPopTimer = 1f;

            _board.CardsToReturn.Clear();
            _board.CardsToReturn.AddRange(_board.Room);
            _board.CardsToReturn.AddRange(_board.Discard);
            _board.CardsToReturn.AddRange(_board.SlainPile);
            if (_board.WeaponSlot != null) _board.CardsToReturn.Add(_board.WeaponSlot);
            if (_board.PocketSlot != null) _board.CardsToReturn.Add(_board.PocketSlot);
            if (_board.FocusedCard != null && !_board.CardsToReturn.Contains(_board.FocusedCard)) _board.CardsToReturn.Add(_board.FocusedCard);
            if (_combat.ResolvingMonster != null && !_board.CardsToReturn.Contains(_combat.ResolvingMonster)) _board.CardsToReturn.Add(_combat.ResolvingMonster);

            _board.Room.Clear();
            _board.Discard.Clear();
            _board.SlainPile.Clear();
            _board.WeaponSlot = null;
            _board.PocketSlot = null;
            _board.FocusedCard = null;

            foreach (var c in _board.CardsToReturn)
            {
                c.RoomSlotIndex = -1;
                c.IsHovered = false;
                c.IsFocused = false;
                c.IsSelectable = false;
                c.VisualYOffset = 0f;
                c.ShakeOffset = Vector2.Zero;
                c.FlashWhiteIntensity = 0f;
            }

            _state = ScoundrelState.Restarting;
            _returnTimer = 0f;
            _restartHoldTimer = 0f;

            _previousScore = _runContext.CurrentScore;
        }

        public override void Exit()
        {
            base.Exit();
            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().MusicPitchOffset = 0f;
            ServiceLocator.Get<GeometricBackgroundManager>().Hide();
            ServiceLocator.Get<ParticleSystemManager>().ClearAllEmitters();
            ServiceLocator.Get<GeometricBackgroundManager>().Reset();
            PoolManager.ClearAll();
        }

        public void TogglePause()
        {
            if (_state == ScoundrelState.GameOver) return;

            _isPaused = !_isPaused;
            var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();

            if (_isPaused)
            {
                audio.SetCurrentMusicStemVolume(0, 0.0f, fadeSpeed: 5f);
                audio.SetCurrentMusicStemVolume(1, 1.0f, fadeSpeed: 5f);

                if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse)
                {
                    _ui.PauseNavGroup.SelectFirst();
                }
                else
                {
                    _ui.PauseNavGroup.DeselectAll();
                }
            }
            else
            {
                audio.SetCurrentMusicStemVolume(0, 1.0f, fadeSpeed: 5f);
                audio.SetCurrentMusicStemVolume(1, 0.0f, fadeSpeed: 5f);
                _ui.ConfirmationDialog.Hide();
            }
        }

        public void ExitToMainMenu()
        {
            _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff);
        }

        public void ApplyRewardAndAdvance(Action rewardAction)
        {
            rewardAction?.Invoke();
            _runContext.Health = _combat.Health;

            if (_combat.Health <= 0)
            {
                SaveManager.DeleteSave();
                _state = ScoundrelState.GameOver;
                _combat.PlayDefeatSequence();
                _combat.CalculateTargetScore(_board, _runContext.MaxHealth, _runContext.CurrentScore);
                return;
            }

            _runContext.Floor++;
            PrepareNextFloor();
        }

        private void PrepareNextFloor()
        {
            _combat.Reset(_runContext.Health, _runContext.RoomTimeLimit);
            _lastSecond = (int)Math.Ceiling(_combat.TimeRemaining);
            _ui.Reset();

            _previewFlashTimer = 0f;
            _lastHoveredCard = null;

            _rewardTimer = 0f;

            _board.Reset();
            _board.Deck.AddRange(DeckGenerator.Generate(_runContext));
            _combat.TotalCardsInFloor = _board.Deck.Count;

            _state = ScoundrelState.Intro;
            _introTimer = 0f;
            _cardsLanded = 0;
            _restartHoldTimer = 0f;
            _introAligning = false;

            for (int i = 0; i < _board.Deck.Count; i++)
            {
                _board.Deck[i].Position = new Vector2(_board.DeckPos.X, -200 - i * 10);
                _board.Deck[i].TargetPosition = _board.Deck[i].Position;
                _board.Deck[i].Scale = Vector2.One;
                _board.Deck[i].TargetScale = Vector2.One;
                _board.Deck[i].ZIndex = i;
                _board.Deck[i].TargetRotation = (float)(_random.NextDouble() - 0.5) * 0.5f;
                _board.Deck[i].Rotation = _board.Deck[i].TargetRotation;
            }

            _ui.DeckCountPlink.Start(0.5f, 0.3f);
            _ui.DiscardCountPlink.Start(0.6f, 0.3f);
            _ui.HealthPlink.Start(0.7f, 0.4f);

            SaveCurrentState();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_runContext.CurrentScore > _previousScore)
            {
                int diff = _runContext.CurrentScore - _previousScore;
                _ui.ScorePlink.Start(0f, 0.5f);
                _ui.AddScoreFloatingText(diff);
                _previousScore = _runContext.CurrentScore;
            }

            if (_state == ScoundrelState.Playing || _state == ScoundrelState.Focused || _state == ScoundrelState.ResolvingMonster)
            {
                _combat.TimeRemaining -= dt;
                int currentSecond = (int)Math.Ceiling(_combat.TimeRemaining);
                if (currentSecond < _lastSecond)
                {
                    _lastSecond = currentSecond;
                    if (currentSecond >= 0)
                    {
                        _ui.TimerPlink.Start(0f, 0.2f);
                        if (currentSecond <= 5 && currentSecond > 0)
                        {
                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=0;freq=1200;atk=0.005;sus=0;dec=0.02;vol=0.05", 0.05f);
                        }
                        else if (currentSecond <= 10 && currentSecond > 5)
                        {
                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=0;freq=800;atk=0.005;sus=0;dec=0.02;vol=0.05", 0.05f);
                        }
                    }
                }

                if (_combat.TimeRemaining <= 0)
                {
                    _combat.TimeRemaining = 0;
                    CheckGameOver();
                }
            }

            var mouseState = _inputManager.GetEffectiveMouseState();
            bool justClicked = _inputManager.IsMouseClickAvailable() && mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            Vector2 mousePos = Core.TransformMouse(mouseState.Position);

            if (_inputManager.Back)
            {
                if (_ui.ConfirmationDialog.IsActive) _ui.ConfirmationDialog.Hide();
                else TogglePause();
            }

            if (_isPaused)
            {
                if (_ui.ConfirmationDialog.IsActive) _ui.ConfirmationDialog.Update(gameTime);
                else
                {
                    foreach (var btn in _ui.PauseButtons) btn.Update(mouseState);
                    if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse) _ui.PauseNavGroup.UpdateInput(_inputManager);
                }
                _previousMouseState = mouseState;
                return;
            }

            var currentKeyboardState = Keyboard.GetState();
            if (currentKeyboardState.IsKeyDown(Keys.R) && _state != ScoundrelState.GameOver && _state != ScoundrelState.Restarting)
            {
                _restartHoldTimer += dt;
                if (_restartHoldTimer >= RESTART_HOLD_DURATION)
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_confirm");
                    ResetBoard();
                }
            }
            else _restartHoldTimer = 0f;

            _ui.Update(dt, gameTime, _board.DeckPos, _board.DiscardPos, _board.Deck.Count, _board.Discard.Count);
            _board.UpdateWaves(dt);
            _combat.Update(dt);

            UpdateHoverStates(mousePos, dt);

            _board.Update(dt);

            if (_state == ScoundrelState.Reward)
            {
                _ui.HealthBarOpacity = Math.Max(0f, _ui.HealthBarOpacity - dt * 5f);
            }
            else
            {
                _ui.HealthBarOpacity = Math.Min(1f, _ui.HealthBarOpacity + dt * 5f);
            }

            _ui.TimerPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, 12f);
            _ui.TimerOverrideText = null;
            _ui.TimerColor = _global.Palette_LightPale;
            _ui.TimerOpacity = 1f;

            if (_timerPopTimer < 1f)
            {
                _timerPopTimer += dt * 1.5f;
                float p = Math.Clamp(_timerPopTimer, 0f, 1f);
                _ui.TimerScale = Easing.EaseOutBack(p);
            }
            else
            {
                _ui.TimerScale = 1f;
            }

            switch (_state)
            {
                case ScoundrelState.Intro: UpdateIntro(dt, justClicked); break;
                case ScoundrelState.Restarting: UpdateRestarting(dt); break;
                case ScoundrelState.Dealing: UpdateDealing(dt); break;
                case ScoundrelState.Playing: UpdatePlaying(justClicked, mousePos); break;
                case ScoundrelState.Focused: UpdateFocused(justClicked, mousePos); break;
                case ScoundrelState.ResolvingMonster: UpdateResolvingMonster(dt); break;
                case ScoundrelState.FloorCleared: UpdateFloorCleared(dt); break;
                case ScoundrelState.CleaningUp: UpdateCleaningUp(dt); break;
                case ScoundrelState.SweepingBoard: UpdateSweepingBoard(dt); break;
                case ScoundrelState.Reward: UpdateReward(dt, justClicked, mousePos, mouseState, gameTime); break;
                case ScoundrelState.GameOver: UpdateGameOver(dt, justClicked, mousePos, mouseState); break;
                case ScoundrelState.TreasureOpening: UpdateTreasureOpening(dt); break;
                case ScoundrelState.Roulette: UpdateRoulette(dt); break;
                case ScoundrelState.RouletteFinished: UpdateRouletteFinished(dt); break;
            }

            _previousMouseState = mouseState;
        }

        private void UpdateHoverStates(Vector2 mousePos, float dt)
        {
            bool canSkipNow = _state == ScoundrelState.Playing && _board.Room.Count == 4 && _combat.CardsResolvedThisRoom == 0 && _combat.CanSkip && _board.Deck.Count > 0;
            var allCards = _board.GetAllCards(_state == ScoundrelState.Focused, canSkipNow);

            foreach (var c in allCards)
            {
                c.IsSelectable = false;
                c.ExpandHitboxX = false;
                c.OutlineColor = null;
                c.ForceRenderAboveVeil = false;
                c.IsFocused = (c == _board.FocusedCard);
                if (_state != ScoundrelState.ResolvingMonster || c != _combat.ResolvingMonster) c.VisualYOffset = 0f;
            }

            if (_state == ScoundrelState.Playing || _state == ScoundrelState.ResolvingMonster || _state == ScoundrelState.TreasureOpening || _state == ScoundrelState.Roulette || _state == ScoundrelState.RouletteFinished)
            {
                foreach (var c in _board.Room) c.ForceRenderAboveVeil = true;
                if (_board.WeaponSlot != null) _board.WeaponSlot.ForceRenderAboveVeil = true;
                if (_board.PocketSlot != null && !_combat.PocketLocked) _board.PocketSlot.ForceRenderAboveVeil = true;
            }

            if (_state == ScoundrelState.Playing)
            {
                foreach (var c in _board.Room)
                {
                    c.IsSelectable = true;
                    c.ExpandHitboxX = true;
                }
                if (_board.PocketSlot != null && !_combat.PocketLocked)
                {
                    _board.PocketSlot.IsSelectable = true;
                    _board.PocketSlot.ExpandHitboxX = true;
                }
                _board.ApplyWaveOffsets();

                if (canSkipNow)
                {
                    _board.SkipCard.IsSelectable = true;
                    _board.SkipCard.ExpandHitboxX = true;
                    _board.SkipCard.TargetPosition = _board.Deck.Count > 0 ? _board.Deck.Last().TargetPosition : _board.DeckPos;
                }
            }
            else if (_state == ScoundrelState.Focused)
            {
                if (_board.FocusedCard != null) { _board.FocusedCard.IsSelectable = true; _board.FocusedCard.ForceRenderAboveVeil = true; }
                if (_board.WeaponSlot != null) { _board.WeaponSlot.IsSelectable = true; _board.WeaponSlot.ForceRenderAboveVeil = true; }
                _board.FistCard.IsSelectable = true;
                _board.FistCard.ForceRenderAboveVeil = true;
            }

            _selectableCardsCache.Clear();
            _unselectableCardsCache.Clear();

            foreach (var c in allCards)
            {
                if (c.IsSelectable || c.ForceRenderAboveVeil) _selectableCardsCache.Add(c);
                else _unselectableCardsCache.Add(c);
            }

            _selectableCardsCache.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
            _unselectableCardsCache.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));

            if (_state == ScoundrelState.Roulette || _state == ScoundrelState.TreasureOpening || _state == ScoundrelState.RouletteFinished) return;

            Card? newHovered = null;
            if ((_state == ScoundrelState.Playing || _state == ScoundrelState.Focused) && _inputManager.IsMouseClickAvailable())
            {
                for (int i = _selectableCardsCache.Count - 1; i >= 0; i--)
                {
                    if (_selectableCardsCache[i].IsSelectable && _selectableCardsCache[i].GetBounds().Contains(mousePos))
                    {
                        newHovered = _selectableCardsCache[i];
                        break;
                    }
                }
            }

            if (newHovered != _lastHoveredCard)
            {
                _previewFlashTimer = 0f;
                _lastHoveredCard = newHovered;
            }
            else if (newHovered != null) _previewFlashTimer += dt;

            foreach (var c in allCards)
            {
                if (c == newHovered && !c.IsHovered)
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_hover");
                }
                c.IsHovered = (c == newHovered);
            }

            if (newHovered != null && !newHovered.IsFocused) newHovered.OutlineColor = _global.Palette_Sun;

            if (_board.WeaponSlot != null)
            {
                _board.WeaponSlot.TargetPosition = _board.WeaponPos;
                _board.WeaponSlot.IsBeingReplaced = false;
            }

            if (_board.PocketSlot != null)
            {
                _board.PocketSlot.TargetPosition = _board.PocketPos + (_combat.PocketLocked ? new Vector2(0, 20) : Vector2.Zero);
            }

            if (_state == ScoundrelState.Playing && newHovered != null && newHovered.Type == CardType.Weapon && _board.WeaponSlot != null)
            {
                _board.WeaponSlot.TargetPosition = _board.WeaponPos + new Vector2(38, 0);
                _board.WeaponSlot.IsBeingReplaced = true;
            }

            if (_board.WeaponSlot != null)
            {
                for (int i = 0; i < _board.SlainPile.Count; i++)
                {
                    var slain = _board.SlainPile[i];
                    if (_runContext.Mode == GameMode.Classic)
                    {
                        slain.TargetPosition = _board.WeaponSlot.TargetPosition + new Vector2(7, 0);
                    }
                    else
                    {
                        if (i == 0) slain.TargetPosition = _board.WeaponSlot.TargetPosition + new Vector2(10, -7);
                        else if (i == 1) slain.TargetPosition = _board.WeaponSlot.TargetPosition + new Vector2(10, 7);
                    }
                }
            }

            if (_state == ScoundrelState.Playing && newHovered != null && newHovered.Type == CardType.Monster && _board.WeaponSlot != null)
            {
                bool canUse = _runContext.Mode == GameMode.Classic
                    ? newHovered.Value <= _combat.GetLastSlainValue(_board)
                    : _board.SlainPile.Count < 3;

                if (canUse)
                {
                    _board.WeaponSlot.OutlineColor = _global.Palette_Leaf;
                    _board.WeaponSlot.ForceRenderAboveVeil = true;
                }
                else
                {
                    _board.WeaponSlot.OutlineColor = null;
                }
            }

            if (!canSkipNow)
            {
                _board.SkipCard.TargetPosition = _board.Deck.Count > 0 ? _board.Deck.Last().TargetPosition : _board.DeckPos;
                _board.SkipCard.Position = _board.SkipCard.TargetPosition;
            }
        }

        private void UpdateIntro(float dt, bool justClicked)
        {
            _introTimer += dt;

            if (!_introAligning)
            {
                if (justClicked && _inputManager.IsMouseClickAvailable())
                {
                    _introTimer = INTRO_DURATION;
                    _inputManager.ConsumeMouseClick();
                }

                float progress = Math.Clamp(_introTimer / INTRO_DURATION, 0f, 1f);
                float easedProgress = Easing.EaseOutSine(progress);

                int targetLanded = (int)(easedProgress * _board.Deck.Count);

                while (_cardsLanded < targetLanded && _cardsLanded < _board.Deck.Count)
                {
                    _board.Deck[_cardsLanded].TargetPosition = _board.DeckPos + new Vector2(0, -_cardsLanded * 0.25f);
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_plink");
                    _cardsLanded++;
                }

                if (_introTimer >= INTRO_DURATION)
                {
                    for (int i = _cardsLanded; i < _board.Deck.Count; i++)
                    {
                        _board.Deck[i].TargetPosition = _board.DeckPos + new Vector2(0, -i * 0.25f);
                    }
                    _cardsLanded = _board.Deck.Count;

                    _introAligning = true;
                    _introTimer = 0f;
                }
            }
            else
            {
                if (_introTimer >= 0.25f && _board.Deck.Count > 0 && _board.Deck[0].TargetRotation != 0f)
                {
                    for (int i = 0; i < _board.Deck.Count; i++)
                    {
                        _board.Deck[i].TargetRotation = 0f;
                    }
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=5;freq=300;atk=0.02;sus=0.0;dec=0.15;lpf=1000;vol=0.1", 0.1f);
                }

                if (_introTimer >= 0.55f)
                {
                    _state = ScoundrelState.Dealing;
                    _dealTimer = 0f;
                }
            }
        }

        private void UpdateRestarting(float dt)
        {
            _returnTimer -= dt;
            if (_returnTimer <= 0f)
            {
                if (_board.CardsToReturn.Count > 0)
                {
                    var card = _board.CardsToReturn.Last();
                    _board.CardsToReturn.RemoveAt(_board.CardsToReturn.Count - 1);

                    card.IsFaceUp = false;
                    card.TargetPosition = _board.DeckPos + new Vector2(0, -_board.Deck.Count * 0.25f);
                    card.TargetScale = Vector2.One;
                    card.TargetRotation = 0f;
                    card.ZIndex = _board.Deck.Count;

                    _board.Deck.Add(card);

                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=900;atk=0.01;sus=0.0;dec=0.15;exp=1;vol=0.05", 0.1f);

                    _returnTimer = _board.CardsToReturn.Count > 4 ? 0.02f : 0.1f;
                }
                else
                {
                    _board.Deck.Clear();
                    _board.Deck.AddRange(DeckGenerator.Generate(_runContext));
                    _combat.TotalCardsInFloor = _board.Deck.Count;
                    for (int i = 0; i < _board.Deck.Count; i++)
                    {
                        _board.Deck[i].TargetPosition = _board.DeckPos + new Vector2(0, -i * 0.25f);
                        _board.Deck[i].Position = _board.Deck[i].TargetPosition;
                        _board.Deck[i].ZIndex = i;
                    }

                    _state = ScoundrelState.Dealing;
                    _dealTimer = DEAL_INTERVAL;
                }
            }
        }

        private void UpdateDealing(float dt)
        {
            _dealTimer -= dt;
            if (_dealTimer <= 0f && _board.Room.Count < 4 && _board.Deck.Count > 0)
            {
                var card = _board.Deck.Last();
                _board.Deck.RemoveAt(_board.Deck.Count - 1);
                _board.Room.Add(card);

                int slot = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (!_board.Room.Any(c => c.RoomSlotIndex == i))
                    {
                        slot = i;
                        break;
                    }
                }

                card.RoomSlotIndex = slot;
                card.TargetPosition = _board.RoomPositions[slot];
                card.ZIndex = 100 + slot;
                card.Flip();

                _dealTimer = DEAL_INTERVAL;
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=900;atk=0.01;sus=0.0;dec=0.15;exp=1;vol=0.05", 0.1f);
            }
            else if (_board.Room.Count == 4 || _board.Deck.Count == 0)
            {
                if (_state == ScoundrelState.Dealing)
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=400;atk=0.05;sus=0.1;dec=0.3;detune=0.01;delay=0.1;delfb=0.2;vol=0.15|wave=2;freq=600;atk=0.05;sus=0.1;dec=0.3;detune=0.01;vol=0.15", 0.15f);
                }
                _state = ScoundrelState.Playing;
                _combat.CardsResolvedThisRoom = 0;
                _combat.PotionsUsedThisRoom = 0;
                _combat.PocketLocked = false;
                _combat.TimeRemaining = _runContext.RoomTimeLimit;
                _lastSecond = (int)Math.Ceiling(_combat.TimeRemaining);
                SaveCurrentState();
            }
        }

        private void UpdatePlaying(bool justClicked, Vector2 mousePos)
        {
            var mouseState = _inputManager.GetEffectiveMouseState();
            bool justRightClicked = _inputManager.IsMouseClickAvailable() && mouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            if (justRightClicked && _lastHoveredCard != null && _board.PocketSlot == null && _runContext.Mode != GameMode.Classic)
            {
                if ((_lastHoveredCard.Type == CardType.Weapon || _lastHoveredCard.Type == CardType.Potion) && _board.Room.Contains(_lastHoveredCard))
                {
                    PocketRoomCard(_lastHoveredCard);
                    _inputManager.ConsumeMouseClick();
                }
            }
            else if (justClicked && _inputManager.IsMouseClickAvailable() && _lastHoveredCard != null)
            {
                if (_lastHoveredCard == _board.SkipCard)
                {
                    OnSkipClicked();
                    _inputManager.ConsumeMouseClick();
                }
                else if (_lastHoveredCard == _board.PocketSlot)
                {
                    UsePocketCard();
                    _inputManager.ConsumeMouseClick();
                }
                else
                {
                    ResolveCardClick(_lastHoveredCard, false);
                    _inputManager.ConsumeMouseClick();
                }
            }

            CheckGameOver();
        }

        private void PocketRoomCard(Card card)
        {
            _board.Room.Remove(card);
            _board.PocketSlot = card;

            _combat.PocketLocked = true;
            _combat.CanSkip = false;

            card.RoomSlotIndex = -1;
            card.IsHovered = false;
            card.TargetPosition = _board.PocketPos + new Vector2(0, 20);
            card.ZIndex = 250;
            card.TargetRotation = 0f;
            card.Scale = Vector2.One;

            if (_board.Deck.Count > 0)
            {
                var newCard = _board.Deck.Last();
                _board.Deck.RemoveAt(_board.Deck.Count - 1);
                _board.Room.Add(newCard);

                int slot = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (!_board.Room.Any(c => c.RoomSlotIndex == i)) { slot = i; break; }
                }

                newCard.RoomSlotIndex = slot;
                newCard.TargetPosition = _board.RoomPositions[slot];
                newCard.ZIndex = 100 + slot;
                newCard.Flip();

                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=900;atk=0.01;sus=0.0;dec=0.15;exp=1;vol=0.05", 0.1f);
            }

            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_click");
            SaveCurrentState();
        }

        private void UsePocketCard()
        {
            if (_board.PocketSlot == null) return;
            var card = _board.PocketSlot;
            _board.PocketSlot = null;
            ResolveCardClick(card, true);
            SaveCurrentState();
        }

        private void ResolveCardClick(Card card, bool isFromPocket = false)
        {
            if (card.Type == CardType.Potion)
            {
                int healAmount = _combat.PotionsUsedThisRoom == 0 ? card.Value : 0;
                if (healAmount > 0) card.FlashWhiteIntensity = 1f;
                _combat.ApplyHeal(healAmount, _runContext.MaxHealth, _ui);
                _combat.PotionsUsedThisRoom++;
                _runContext.CurrentScore += card.Value;
                _board.MoveToDiscard(card);
                OnCardResolved(isFromPocket);
            }
            else if (card.Type == CardType.Weapon)
            {
                _board.EquipWeapon(card, _runContext);
                OnCardResolved(isFromPocket);
            }
            else if (card.Type == CardType.Treasure)
            {
                _activeTreasureCard = card;
                _activeTreasureCard.ZIndex = 500;
                _activeTreasureCard.TargetPosition = _activeTreasureCard.Position + new Vector2(0, -30);
                _isRouletteFromReward = false;
                _state = ScoundrelState.TreasureOpening;
                _treasureAnimTimer = 0f;
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=880;atk=0.01;sus=0.05;dec=0.2;vol=0.15");
                SaveCurrentState();
            }
            else if (card.Type == CardType.Monster)
            {
                bool canUseWeapon = false;
                if (_board.WeaponSlot != null)
                {
                    canUseWeapon = _runContext.Mode == GameMode.Classic
                        ? card.Value <= _combat.GetLastSlainValue(_board)
                        : _board.SlainPile.Count < 3;
                }

                if (canUseWeapon)
                {
                    _board.FocusedCard = card;
                    _board.FocusedCard.TargetPosition = _board.RoomPositions[_board.FocusedCard.RoomSlotIndex] + new Vector2(0, -3);
                    _board.FocusedCard.ZIndex = 500;
                    _state = ScoundrelState.Focused;
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=6;freq=1500;atk=0.01;sus=0.02;dec=0.1;hpf=800;vol=0.1|wave=2;freq=1200;slide=-400;atk=0.01;sus=0.05;dec=0.1;detune=0.02;vol=0.1", 0.2f);
                    SaveCurrentState();
                }
                else
                {
                    if (_combat.Health - card.Value <= 0) SaveManager.DeleteSave();
                    _combat.StartMonsterResolution(card, card.Value, false);
                    _state = ScoundrelState.ResolvingMonster;
                    if (_board.FocusedCard != null)
                    {
                        _board.FocusedCard.ZIndex = 100 + _board.FocusedCard.RoomSlotIndex;
                        _board.FocusedCard = null;
                    }
                    if (SaveManager.HasSave()) SaveCurrentState();
                }
            }
        }

        private void UpdateTreasureOpening(float dt)
        {
            _treasureAnimTimer += dt;
            if (_treasureAnimTimer > 0.02f && _treasureAnimTimer - dt <= 0.02f)
            {
                _activeTreasureCard.TargetScale = new Vector2(1.5f, 1.5f);
                _activeTreasureCard.FlashWhiteIntensity = 1f;
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=1108.73;slide=200;atk=0.02;sus=0.05;dec=0.2;vol=0.15");
                ServiceLocator.Get<ParticleSystemManager>().CreateEmitter(ParticleEffects.CreateUIPlink()).EmitBurst(20);
            }
            if (_treasureAnimTimer > 0.12f && _treasureAnimTimer - dt <= 0.12f)
            {
                _activeTreasureCard.TargetScale = Vector2.One;
            }
            if (_treasureAnimTimer > 0.2f)
            {
                StartRoulette();
            }
        }

        private void StartRoulette()
        {
            _state = ScoundrelState.Roulette;
            _rouletteBelt.Clear();
            var rng = new Random(_runContext.RelicSeed);

            for (int i = 0; i < ROULETTE_BELT_LENGTH; i++)
            {
                int roll = rng.Next(100);
                int rarity = roll < 50 ? 0 : (roll < 80 ? 1 : (roll < 95 ? 2 : 3));
                _rouletteBelt.Add(rarity);
            }

            int winningIndex = rng.Next(120, 170);
            float itemTotalWidth = 50f;

            _rouletteTargetOffset = winningIndex * itemTotalWidth;

            _rouletteOffset = 0f;
            _rouletteSpinTimer = 0f;
            _rouletteSpinDuration = ROULETTE_BASE_SPIN_DURATION + (float)rng.NextDouble() * 1.5f;
            _rouletteModalAlpha = 0f;
            _roulettePhaseTimer = 0f;
            _rouletteWinPlinkTimer = 0f;
            _rouletteNeedleRotation = 0f;
            _rouletteNeedleVelocity = 0f;
        }

        private void UpdateRoulette(float dt)
        {
            _rouletteModalAlpha = Math.Min(1f, _rouletteModalAlpha + dt * 10f);

            float springForce = -_rouletteNeedleRotation * 150f;
            if (_rouletteNeedleRotation > 0.5f)
            {
                springForce -= (_rouletteNeedleRotation - 0.5f) * 20000f;
            }

            float damping = _rouletteNeedleVelocity > 0 ? 45f : 25f;
            float dampingForce = -_rouletteNeedleVelocity * damping;

            _rouletteNeedleVelocity += (springForce + dampingForce) * dt;
            _rouletteNeedleRotation += _rouletteNeedleVelocity * dt;

            if (_rouletteNeedleRotation < 0f)
            {
                _rouletteNeedleRotation = 0f;
                _rouletteNeedleVelocity = 0f;
            }

            if (_rouletteSpinTimer < _rouletteSpinDuration)
            {
                _rouletteSpinTimer += dt;
                float p = Math.Clamp(_rouletteSpinTimer / _rouletteSpinDuration, 0f, 1f);

                float ease = 1f - MathF.Pow(1f - p, 4f);

                float previousOffset = _rouletteOffset;
                _rouletteOffset = _rouletteTargetOffset * ease;

                int prevIndex = (int)((previousOffset + 20f) / 50f);
                int currIndex = (int)((_rouletteOffset + 20f) / 50f);
                if (currIndex > prevIndex && p < 1f)
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_hover", 0.4f);
                    _rouletteNeedleVelocity = 25f;
                }

                if (_rouletteSpinTimer >= _rouletteSpinDuration)
                {
                    _rouletteOffset = _rouletteTargetOffset;
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=1046.5;atk=0.01;sus=0.05;dec=0.3;vol=0.15|wave=2;freq=1567.98;atk=0.01;sus=0.05;dec=0.3;delay=0.05;vol=0.15");
                    _rouletteWinPlinkTimer = 0.2f;
                }
            }
            else
            {
                _roulettePhaseTimer += dt;
                if (_rouletteWinPlinkTimer > 0) _rouletteWinPlinkTimer -= dt;

                if (_roulettePhaseTimer > 0.8f)
                {
                    _state = ScoundrelState.RouletteFinished;
                    _roulettePhaseTimer = 0f;
                }
            }
        }

        private void UpdateRouletteFinished(float dt)
        {
            _rouletteModalAlpha = Math.Max(0f, _rouletteModalAlpha - dt * 15f);
            _roulettePhaseTimer += dt;

            if (_activeTreasureCard != null)
            {
                if (_roulettePhaseTimer < 0.05f)
                {
                    float p = _roulettePhaseTimer / 0.05f;
                    float ease = Easing.EaseOutCubic(p);
                    _activeTreasureCard.Scale = Vector2.Lerp(Vector2.One, new Vector2(1.2f, 1.2f), ease);
                    _activeTreasureCard.TargetScale = _activeTreasureCard.Scale;
                }
                else if (_roulettePhaseTimer < 0.15f)
                {
                    if (_roulettePhaseTimer - dt < 0.05f)
                    {
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=600;slide=-300;atk=0.01;sus=0.05;dec=0.15;vol=0.1");
                    }
                    float p = (_roulettePhaseTimer - 0.05f) / 0.1f;
                    float ease = Easing.EaseInBack(p);
                    _activeTreasureCard.Scale = Vector2.Lerp(new Vector2(1.2f, 1.2f), Vector2.Zero, ease);
                    _activeTreasureCard.TargetScale = _activeTreasureCard.Scale;
                }
                else
                {
                    _activeTreasureCard.Scale = Vector2.Zero;
                    _activeTreasureCard.TargetScale = Vector2.Zero;
                }
            }

            if (_rouletteModalAlpha <= 0f && _roulettePhaseTimer >= 0.15f)
            {
                _runContext.RelicSeed = new Random(_runContext.RelicSeed).Next();

                _board.ConsumeCard(_activeTreasureCard);
                _activeTreasureCard = null;

                if (_isRouletteFromReward)
                {
                    ApplyRewardAndAdvance(null);
                }
                else
                {
                    _combat.CardsResolvedThisRoom++;
                    _state = ScoundrelState.Playing;
                    OnCardResolved(false);
                }
            }
        }

        private void OnCardResolved(bool isFromPocket = false)
        {
            if (!isFromPocket) _combat.CardsResolvedThisRoom++;

            if (_combat.Health <= 0 || _combat.TimeRemaining <= 0)
            {
                CheckGameOver();
                return;
            }

            if (!HasMonsterInRoom() && !HasMonsterInDeck())
            {
                CheckGameOver();
            }
            else if (_combat.CardsResolvedThisRoom >= 3)
            {
                if (_board.Deck.Count > 0)
                {
                    int timeBonus = (int)Math.Ceiling(_combat.TimeRemaining);
                    if (timeBonus > 0)
                    {
                        _ui.AddFlyingTimer(timeBonus, () => {
                            _runContext.CurrentScore += timeBonus;
                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=1200;atk=0.01;sus=0.0;dec=0.15;exp=1;vol=0.1", 0.1f);
                        });
                        _timerPopTimer = 0f;
                    }

                    _state = ScoundrelState.Dealing;
                    _dealTimer = DEAL_INTERVAL;
                    _combat.CanSkip = true;
                }
                else
                {
                    _combat.PocketLocked = false;
                    _combat.CardsResolvedThisRoom = 0;
                    _combat.CanSkip = true;
                }
            }
            SaveCurrentState();
        }

        private void UpdateFocused(bool justClicked, Vector2 mousePos)
        {
            if (justClicked && _inputManager.IsMouseClickAvailable())
            {
                if (_lastHoveredCard == _board.FistCard)
                {
                    OnFistsClicked();
                    _inputManager.ConsumeMouseClick();
                }
                else if (_lastHoveredCard == _board.WeaponSlot)
                {
                    OnWeaponClicked();
                    _inputManager.ConsumeMouseClick();
                }
                else if (_lastHoveredCard == null)
                {
                    CancelFocus();
                    _inputManager.ConsumeMouseClick();
                }
            }
        }

        private void OnFistsClicked()
        {
            if (_board.FocusedCard != null)
            {
                if (_combat.Health - _board.FocusedCard.Value <= 0) SaveManager.DeleteSave();
                _combat.StartMonsterResolution(_board.FocusedCard, _board.FocusedCard.Value, false);
                _state = ScoundrelState.ResolvingMonster;
                _board.FocusedCard = null;
                if (SaveManager.HasSave()) SaveCurrentState();
            }
        }

        private void OnWeaponClicked()
        {
            if (_board.FocusedCard == null || _board.WeaponSlot == null) return;

            bool canUse = _runContext.Mode == GameMode.Classic
                ? _board.FocusedCard.Value <= _combat.GetLastSlainValue(_board)
                : _board.SlainPile.Count < 3;

            if (!canUse)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=4;freq=150;atk=0.01;sus=0.1;dec=0.1;detune=0.03;vol=0.15|wave=0;freq=150;atk=0.01;sus=0.1;dec=0.1;duty=0.2;vol=0.1", 0.2f);
                return;
            }

            int damage = Math.Max(0, _board.FocusedCard.Value - _board.WeaponSlot.Value);
            if (_combat.Health - damage <= 0) SaveManager.DeleteSave();

            _combat.StartMonsterResolution(_board.FocusedCard, damage, true);
            _state = ScoundrelState.ResolvingMonster;
            _board.FocusedCard = null;
            if (SaveManager.HasSave()) SaveCurrentState();
        }

        private void CancelFocus()
        {
            if (_board.FocusedCard != null)
            {
                _board.FocusedCard.TargetPosition = _board.RoomPositions[_board.FocusedCard.RoomSlotIndex];
                _board.FocusedCard.ZIndex = 100 + _board.FocusedCard.RoomSlotIndex;
                _board.FocusedCard = null;
            }
            _state = ScoundrelState.Playing;
            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_hover");
            SaveCurrentState();
        }

        private void UpdateResolvingMonster(float dt)
        {
            _combat.UpdateResolution(dt, _board, _ui, _core, _hapticsManager, _runContext, () => {
                _state = ScoundrelState.Playing;
                OnCardResolved(false);
            });
        }

        private void UpdateFloorCleared(float dt)
        {
            _floorClearedTimer -= dt;
            if (_floorClearedTimer <= 0)
            {
                _state = ScoundrelState.CleaningUp;
                _tallyPhase = 0;
                _tallyTimer = 0f;
            }
        }

        private void UpdateCleaningUp(float dt)
        {
            _tallyTimer += dt;

            if (_tallyPhase == 0)
            {
                if (_tallyTimer > 0.3f)
                {
                    Card card = null;
                    bool isFromPocket = false;

                    if (_board.Room.Count > 0)
                    {
                        card = _board.Room[0];
                    }
                    else if (_board.PocketSlot != null)
                    {
                        card = _board.PocketSlot;
                        isFromPocket = true;
                    }

                    if (card != null)
                    {
                        if (card.Type == CardType.Potion)
                        {
                            int actualHeal = Math.Min(card.Value, _runContext.MaxHealth - _combat.Health);
                            if (actualHeal > 0) card.FlashWhiteIntensity = 1f;

                            if (actualHeal > 0)
                            {
                                _combat.ApplyHeal(actualHeal, _runContext.MaxHealth, _ui);
                            }
                        }

                        var psm = ServiceLocator.Get<ParticleSystemManager>();
                        var emitter = psm.CreateEmitter(ParticleEffects.CreateUIPlink());
                        emitter.Position = card.Position;
                        emitter.EmitBurst(10);

                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_plink", 0.2f);
                        _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);

                        if (isFromPocket)
                        {
                            _board.PocketSlot = null;
                        }

                        _runContext.CurrentScore += card.Value;
                        _board.MoveToDiscard(card);
                        _tallyTimer = 0f;
                    }
                    else
                    {
                        _tallyPhase = 1;
                        _tallyTimer = 0f;
                    }
                }
            }
            else if (_tallyPhase == 1)
            {
                if (_board.Deck.Count > 0)
                {
                    if (_tallyTimer > 0.15f)
                    {
                        var card = _board.Deck.Last();
                        _board.Deck.RemoveAt(_board.Deck.Count - 1);
                        _board.Room.Add(card);

                        int slot = _board.Room.Count - 1;

                        card.RoomSlotIndex = slot;
                        card.TargetPosition = _board.RoomPositions[slot];
                        card.ZIndex = 100 + slot;
                        card.Flip();

                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=900;atk=0.01;sus=0.0;dec=0.15;exp=1;vol=0.05", 0.1f);
                        _tallyTimer = 0f;

                        if (_board.Room.Count == 4 || _board.Deck.Count == 0)
                        {
                            _tallyPhase = 0;
                        }
                    }
                }
                else
                {
                    _tallyPhase = 2;
                    _tallyTimer = 0f;
                }
            }
            else if (_tallyPhase == 2)
            {
                if (_tallyTimer > 0.5f)
                {
                    StartSweepingBoard();
                }
            }
        }

        private void StartSweepingBoard()
        {
            _state = ScoundrelState.SweepingBoard;
            _board.CardsToReturn.Clear();
            _board.CardsToReturn.AddRange(_board.Room);
            _board.CardsToReturn.AddRange(_board.Discard);
            _board.CardsToReturn.AddRange(_board.SlainPile);
            if (_board.WeaponSlot != null) _board.CardsToReturn.Add(_board.WeaponSlot);
            if (_board.PocketSlot != null) _board.CardsToReturn.Add(_board.PocketSlot);

            _board.Room.Clear();
            _board.Discard.Clear();
            _board.SlainPile.Clear();
            if (_board.WeaponSlot != null) _runContext.CurrentScore += _board.WeaponSlot.Value;
            _board.WeaponSlot = null;
            _board.PocketSlot = null;

            _sweepTimer = 0f;
        }

        private void UpdateSweepingBoard(float dt)
        {
            _sweepTimer -= dt;
            if (_sweepTimer <= 0f)
            {
                if (_board.CardsToReturn.Count > 0)
                {
                    var card = _board.CardsToReturn.Last();

                    if (card.IsFaceUp && !card.IsFlipping)
                    {
                        card.Flip();
                        _sweepTimer = 0.15f;
                    }
                    else if (!card.IsFlipping)
                    {
                        _board.CardsToReturn.RemoveAt(_board.CardsToReturn.Count - 1);

                        card.TargetPosition = _board.DeckPos + new Vector2(0, -_board.Deck.Count * 0.25f);
                        card.TargetRotation = (float)(_random.NextDouble() - 0.5) * 0.5f;
                        card.ZIndex = _board.Deck.Count;

                        _board.Deck.Add(card);

                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=5;freq=200;atk=0.02;sus=0.0;dec=0.1;lpf=800;vol=0.05", 0.1f);

                        _sweepTimer = _board.CardsToReturn.Count > 4 ? 0.02f : 0.1f;
                    }
                }
                else
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=880;atk=0.02;sus=0.05;dec=0.3;vol=0.1|wave=2;freq=1108.73;atk=0.02;sus=0.05;dec=0.3;delay=0.05;vol=0.1", 0.1f);
                    _state = ScoundrelState.Reward;
                    _rewardTimer = 0f;
                    _ui.RewardFadeTimer = 0f;

                    _board.RewardCards.Clear();

                    var treasure = new Card(CardSuit.None, CardType.Treasure, 0, 0) { IsFaceUp = false };
                    treasure.Position = new Vector2(Global.VIRTUAL_WIDTH / 2f - 40, -60);
                    treasure.TargetPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f - 40, Global.VIRTUAL_HEIGHT / 2f);

                    var potion = new Card(CardSuit.Hearts, CardType.Potion, 14, 20) { IsFaceUp = false };
                    potion.Position = new Vector2(Global.VIRTUAL_WIDTH / 2f + 40, -60);
                    potion.TargetPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f + 40, Global.VIRTUAL_HEIGHT / 2f);

                    _board.RewardCards.Add(treasure);
                    _board.RewardCards.Add(potion);

                    foreach (var c in _board.Deck) c.TargetRotation = 0f;

                    SaveCurrentState();
                }
            }
        }

        private void UpdateReward(float dt, bool justClicked, Vector2 mousePos, MouseState mouseState, GameTime gameTime)
        {
            _rewardTimer += dt;

            if (_rewardTimer > 0.5f && _board.RewardCards.Count > 0 && !_board.RewardCards[0].IsFaceUp && !_board.RewardCards[0].IsFlipping)
            {
                foreach (var card in _board.RewardCards) card.Flip();
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=900;atk=0.01;sus=0.0;dec=0.15;exp=1;vol=0.05", 0.1f);
            }

            Card hoveredCard = null;
            foreach (var card in _board.RewardCards)
            {
                card.IsHovered = card.GetBounds().Contains(mousePos);
                if (card.IsHovered) hoveredCard = card;
            }

            if (justClicked && hoveredCard != null && hoveredCard.IsFaceUp)
            {
                if (hoveredCard.Type == CardType.Potion)
                {
                    _combat.ApplyHeal(20, _runContext.MaxHealth, _ui);
                    ApplyRewardAndAdvance(null);
                }
                else if (hoveredCard.Type == CardType.Treasure)
                {
                    _activeTreasureCard = hoveredCard;
                    _activeTreasureCard.ZIndex = 500;
                    _activeTreasureCard.TargetPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f - 30);

                    foreach (var c in _board.RewardCards)
                    {
                        if (c != _activeTreasureCard) c.TargetPosition = new Vector2(c.Position.X, Global.VIRTUAL_HEIGHT + 100);
                    }

                    _isRouletteFromReward = true;
                    _state = ScoundrelState.TreasureOpening;
                    _treasureAnimTimer = 0f;
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=880;atk=0.01;sus=0.05;dec=0.2;vol=0.15");
                    SaveCurrentState();
                }
            }
        }

        private void UpdateGameOver(float dt, bool justClicked, Vector2 mousePos, MouseState mouseState)
        {
            _combat.UpdateScoreAnimation(dt);

            _ui.TryAgainButton.Update(mouseState);
            _ui.ExitButton.Update(mouseState);

            if (justClicked)
            {
                if (_ui.TryAgainButton.Bounds.Contains(mousePos)) _inputManager.ConsumeMouseClick();
                else if (_ui.ExitButton.Bounds.Contains(mousePos)) _inputManager.ConsumeMouseClick();
            }
        }

        private void UpdateScoringTimeBonus(float dt)
        {
            _timeBonusTimer += dt;

            float duration = 0.5f;
            float p = Math.Clamp(_timeBonusTimer / duration, 0f, 1f);
            float ease = p * p;

            Vector2 startPos = new Vector2(Global.VIRTUAL_WIDTH / 2f, 12f);

            var secFont = _core.SecondaryFont;
            var tertFont = _core.TertiaryFont;
            Vector2 endPos = new Vector2(Global.VIRTUAL_WIDTH - 10, 10 + tertFont.LineHeight + 2 + (secFont.LineHeight / 2f));

            _ui.TimerPosition = Vector2.Lerp(startPos, endPos, ease);
            _ui.TimerScale = MathHelper.Lerp(1f, 0.5f, p);
            _ui.TimerOpacity = MathHelper.Lerp(1f, 0f, p * p * p);

            if (_timeBonusTimer >= duration)
            {
                _runContext.CurrentScore += _timeBonusAmount;
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=1200;atk=0.01;sus=0.0;dec=0.15;exp=1;vol=0.1", 0.1f);

                _ui.TimerPosition = new Vector2(Global.VIRTUAL_WIDTH / 2f, 12f);
                _timerPopTimer = 0f;

                if (_timeBonusNextState == ScoundrelState.Dealing)
                {
                    _state = ScoundrelState.Dealing;
                    _dealTimer = DEAL_INTERVAL;
                }
                else if (_timeBonusNextState == ScoundrelState.FloorCleared)
                {
                    _state = ScoundrelState.FloorCleared;
                    _floorClearedTimer = 1.5f;
                    _ui.FloorClearedTextTimer = 0f;
                    SaveCurrentState();
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=523.25;atk=0.05;sus=0.1;dec=0.4;vol=0.1|wave=2;freq=659.25;atk=0.05;sus=0.1;dec=0.4;delay=0.1;vol=0.1|wave=2;freq=783.99;atk=0.05;sus=0.2;dec=0.6;delay=0.2;vol=0.1", 0.15f);
                }
                else if (_timeBonusNextState == ScoundrelState.GameOver)
                {
                    SaveManager.DeleteSave();
                    _state = ScoundrelState.GameOver;
                    _combat.PlayVictorySequence();
                    _combat.CalculateTargetScore(_board, _runContext.MaxHealth, _runContext.CurrentScore);
                }
            }
        }

        private void OnSkipClicked()
        {
            _combat.CanSkip = false;
            var skippedCards = new List<Card>(_board.Room);
            _board.Room.Clear();

            foreach (var card in skippedCards)
            {
                card.IsFaceUp = false;
                card.RoomSlotIndex = -1;
                card.IsHovered = false;
                _board.Deck.Insert(0, card);
            }

            for (int i = 0; i < _board.Deck.Count; i++)
            {
                _board.Deck[i].TargetPosition = _board.DeckPos + new Vector2(0, -i * 0.25f);
                _board.Deck[i].ZIndex = i;
            }

            _state = ScoundrelState.Dealing;
            _dealTimer = DEAL_INTERVAL;

            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=3;freq=1000;slide=-500;atk=0.02;sus=0.05;dec=0.15;vol=0.12|wave=4;freq=400;slide=-100;atk=0.02;sus=0.05;dec=0.15;vol=0.15", 0.2f);
            SaveCurrentState();
        }

        private bool HasMonsterInRoom()
        {
            foreach (var c in _board.Room)
            {
                if (c.Type == CardType.Monster) return true;
            }
            return false;
        }

        private bool HasMonsterInDeck()
        {
            foreach (var c in _board.Deck)
            {
                if (c.Type == CardType.Monster) return true;
            }
            return false;
        }

        private void CheckGameOver()
        {
            if (_state == ScoundrelState.GameOver || _state == ScoundrelState.FloorCleared) return;

            if (_combat.Health <= 0 || _combat.TimeRemaining <= 0)
            {
                SaveManager.DeleteSave();
                _state = ScoundrelState.GameOver;
                _combat.PlayDefeatSequence();
                _combat.CalculateTargetScore(_board, _runContext.MaxHealth, _runContext.CurrentScore);
            }
            else if (!HasMonsterInRoom() && !HasMonsterInDeck())
            {
                int timeBonus = (int)Math.Ceiling(_combat.TimeRemaining);

                if (_runContext.Mode == GameMode.Classic || _runContext.Floor >= _runContext.MaxFloors)
                {
                    if (timeBonus > 0) _runContext.CurrentScore += timeBonus;
                    SaveManager.DeleteSave();
                    _state = ScoundrelState.GameOver;
                    _combat.PlayVictorySequence();
                    _combat.CalculateTargetScore(_board, _runContext.MaxHealth, _runContext.CurrentScore);
                }
                else
                {
                    if (timeBonus > 0)
                    {
                        _ui.AddFlyingTimer(timeBonus, () => {
                            _runContext.CurrentScore += timeBonus;
                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=1200;atk=0.01;sus=0.0;dec=0.15;exp=1;vol=0.1", 0.1f);
                        });
                        _timerPopTimer = 0f;
                    }
                    _state = ScoundrelState.FloorCleared;
                    _floorClearedTimer = 1.5f;
                    _ui.FloorClearedTextTimer = 0f;
                    SaveCurrentState();
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=523.25;atk=0.05;sus=0.1;dec=0.4;vol=0.1|wave=2;freq=659.25;atk=0.05;sus=0.1;dec=0.4;delay=0.1;vol=0.1|wave=2;freq=783.99;atk=0.05;sus=0.2;dec=0.6;delay=0.2;vol=0.1", 0.15f);
                }
            }
        }

        private int GetPreviewHealth()
        {
            if (_lastHoveredCard == null || !_lastHoveredCard.IsFaceUp) return _combat.Health;

            if (_state == ScoundrelState.Focused)
            {
                if (_lastHoveredCard == _board.WeaponSlot) return _combat.Health - Math.Max(0, _board.FocusedCard!.Value - _board.WeaponSlot.Value);
                if (_lastHoveredCard == _board.FistCard) return _combat.Health - _board.FocusedCard!.Value;
            }
            else
            {
                if (_lastHoveredCard.Type == CardType.Monster)
                {
                    bool canUseWeapon = false;
                    if (_board.WeaponSlot != null)
                    {
                        canUseWeapon = _runContext.Mode == GameMode.Classic
                            ? _lastHoveredCard.Value <= _combat.GetLastSlainValue(_board)
                            : _board.SlainPile.Count < 3;
                    }

                    if (canUseWeapon)
                    {
                        return _combat.Health - Math.Max(0, _lastHoveredCard.Value - _board.WeaponSlot!.Value);
                    }
                    return _combat.Health - _lastHoveredCard.Value;
                }
                else if (_lastHoveredCard.Type == CardType.Potion)
                {
                    int baseHeal = _combat.PotionsUsedThisRoom == 0 ? _lastHoveredCard.Value : 0;
                    return Math.Min(_runContext.MaxHealth, _combat.Health + baseHeal);
                }
            }
            return _combat.Health;
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            bool canSkipNow = _state == ScoundrelState.Playing && _board.Room.Count == 4 && _combat.CardsResolvedThisRoom == 0 && _combat.CanSkip && _board.Deck.Count > 0;

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

            foreach (var card in _unselectableCardsCache)
            {
                if (_runContext.Mode == GameMode.Classic && _board.SlainPile.Contains(card))
                {
                    int index = _board.SlainPile.IndexOf(card);
                    if (index < _board.SlainPile.Count - 2) continue;
                    if (index == _board.SlainPile.Count - 2)
                    {
                        var lastCard = _board.SlainPile.Last();
                        if (Vector2.Distance(lastCard.Position, lastCard.TargetPosition) < 2f) continue;
                    }
                }

                card.Draw(spriteBatch, _spriteManager);
            }

            if (_runContext.Mode != GameMode.Classic)
            {
                if (_board.PocketSlot == null)
                {
                    Rectangle outlineSource = _spriteManager.ScoundrelCardRects[1, 0];
                    Vector2 origin = new Vector2(18f, 25f);
                    Vector2 drawPos = new Vector2(MathF.Round(_board.PocketPos.X), MathF.Round(_board.PocketPos.Y));
                    spriteBatch.DrawSnapped(_spriteManager.ScoundrelCardsSpriteSheet, drawPos, outlineSource, Color.White * 0.15f, 0f, origin, 1f, SpriteEffects.None, 0f);

                    var secFont = _core.SecondaryFont;
                    string pocketText = "POCKET";
                    Vector2 pSize = secFont.MeasureString(pocketText);
                    Vector2 pTextPos = new Vector2(
                        MathF.Round(_board.PocketPos.X - pSize.X / 2f),
                        MathF.Round(_board.PocketPos.Y - 33f - pSize.Y / 2f)
                    );
                    spriteBatch.DrawStringOutlinedSnapped(secFont, pocketText, pTextPos, _global.Palette_DarkPale * 0.5f, _global.Palette_Off * 0.5f);
                }

                if (_board.PocketSlot != null && _combat.PocketLocked)
                {
                    var secFont = _core.SecondaryFont;
                    string pocketText = "POCKET";
                    Vector2 pSize = secFont.MeasureString(pocketText);
                    Vector2 pTextPos = new Vector2(
                        MathF.Round(_board.PocketPos.X - pSize.X / 2f),
                        MathF.Round(_board.PocketSlot.Position.Y - 33f - pSize.Y / 2f)
                    );
                    spriteBatch.DrawStringOutlinedSnapped(secFont, pocketText, pTextPos, _global.Palette_LightPale, _global.Palette_Off);
                }
            }

            if (_state == ScoundrelState.Playing || _state == ScoundrelState.Focused || _state == ScoundrelState.ResolvingMonster)
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off * 0.4f);
            }

            bool isHoveringWeapon = (_state == ScoundrelState.Playing || _state == ScoundrelState.Focused) && _lastHoveredCard != null && _lastHoveredCard.Type == CardType.Weapon;
            bool showWeaponOutline = isHoveringWeapon || (_board.WeaponSlot != null && _board.WeaponSlot.IsBeingReplaced);

            if (showWeaponOutline)
            {
                Rectangle outlineSource = _spriteManager.ScoundrelCardRects[1, 0];
                Vector2 origin = new Vector2(18f, 25f);
                Vector2 drawPos = new Vector2(MathF.Round(_board.WeaponPos.X), MathF.Round(_board.WeaponPos.Y));
                spriteBatch.DrawSnapped(_spriteManager.ScoundrelCardsSpriteSheet, drawPos, outlineSource, Color.White * 0.5f, 0f, origin, 1f, SpriteEffects.None, 0f);
            }

            bool isHoveringPocketable = (_state == ScoundrelState.Playing) && _lastHoveredCard != null && _board.Room.Contains(_lastHoveredCard) && (_lastHoveredCard.Type == CardType.Weapon || _lastHoveredCard.Type == CardType.Potion) && _board.PocketSlot == null && _runContext.Mode != GameMode.Classic;

            if (_runContext.Mode != GameMode.Classic)
            {
                if (isHoveringPocketable)
                {
                    Rectangle outlineSource = _spriteManager.ScoundrelCardRects[1, 0];
                    Vector2 origin = new Vector2(18f, 25f);
                    Vector2 drawPos = new Vector2(MathF.Round(_board.PocketPos.X), MathF.Round(_board.PocketPos.Y));
                    spriteBatch.DrawSnapped(_spriteManager.ScoundrelCardsSpriteSheet, drawPos, outlineSource, Color.White * 0.5f, 0f, origin, 1f, SpriteEffects.None, 0f);

                    var secFont = _core.SecondaryFont;
                    string pocketText = "POCKET";
                    Vector2 pSize = secFont.MeasureString(pocketText);
                    Vector2 pTextPos = new Vector2(
                        MathF.Round(_board.PocketPos.X - pSize.X / 2f),
                        MathF.Round(_board.PocketPos.Y - 33f - pSize.Y / 2f)
                    );
                    spriteBatch.DrawStringOutlinedSnapped(secFont, pocketText, pTextPos, _global.Palette_LightPale, _global.Palette_Off);
                }
                else if (_board.PocketSlot != null && !_combat.PocketLocked && (_state == ScoundrelState.Playing || _state == ScoundrelState.Focused || _state == ScoundrelState.ResolvingMonster))
                {
                    var secFont = _core.SecondaryFont;
                    string pocketText = "POCKET";
                    Vector2 pSize = secFont.MeasureString(pocketText);
                    float visualOffset = (_board.PocketSlot.IsHovered && !_board.PocketSlot.IsFocused) ? -1f : 0f;
                    Color pColor = _board.PocketSlot.IsHovered ? _global.Palette_Sun : _global.Palette_LightPale;

                    Vector2 pTextPos = new Vector2(
                        MathF.Round(_board.PocketPos.X - pSize.X / 2f),
                        MathF.Round(_board.PocketSlot.Position.Y - 33f - pSize.Y / 2f + visualOffset)
                    );
                    spriteBatch.DrawStringOutlinedSnapped(secFont, pocketText, pTextPos, pColor, _global.Palette_Off);
                }
            }

            if (_board.WeaponSlot != null && _board.WeaponSlot.IsBeingReplaced)
            {
                var xIcon = _spriteManager.ShopXIcon;
                if (xIcon != null)
                {
                    Vector2 xPos = new Vector2(MathF.Round(_board.WeaponSlot.Position.X), MathF.Round(_board.WeaponSlot.Position.Y));
                    if (_board.WeaponSlot.IsHovered && !_board.WeaponSlot.IsFocused) xPos.Y -= 1f;
                    Vector2 xOrigin = new Vector2(xIcon.Width / 2f, xIcon.Height / 2f);
                    spriteBatch.DrawSnapped(xIcon, xPos, null, _global.Palette_Rust, _board.WeaponSlot.Rotation, xOrigin, _board.WeaponSlot.Scale, SpriteEffects.None, 0f);
                }
            }

            foreach (var card in _selectableCardsCache)
            {
                if (card == _activeTreasureCard) continue;
                card.Draw(spriteBatch, _spriteManager);
            }

            if (canSkipNow)
            {
                var secFont = _core.SecondaryFont;
                string skipText = "SKIP\nROOM";
                Vector2 tSize = secFont.MeasureString(skipText);
                Vector2 tPos = new Vector2(MathF.Round(_board.SkipCard.Position.X - tSize.X / 2f), MathF.Round(_board.SkipCard.Position.Y + _board.SkipCard.VisualYOffset - tSize.Y / 2f));
                if (_board.SkipCard.IsHovered && !_board.SkipCard.IsFocused) tPos.Y -= 1f;
                Color textColor = _board.SkipCard.IsHovered ? _global.Palette_Sun : _global.Palette_LightPale;
                spriteBatch.DrawStringOutlinedSnapped(secFont, skipText, tPos, textColor, _global.Palette_Off);
            }

            if (_state == ScoundrelState.Focused)
            {
                Rectangle fistOutlineSource = _spriteManager.ScoundrelCardRects[1, 0];
                Vector2 outlineOrigin = new Vector2(18f, 25f);
                Vector2 fDrawPos = new Vector2(MathF.Round(_board.FistCard.Position.X), MathF.Round(_board.FistCard.Position.Y + _board.FistCard.VisualYOffset));
                if (_board.FistCard.IsHovered && !_board.FistCard.IsFocused) fDrawPos.Y -= 1f;

                spriteBatch.DrawSnapped(_spriteManager.ScoundrelCardsSpriteSheet, fDrawPos, fistOutlineSource, Color.White * 0.5f, 0f, outlineOrigin, 1f, SpriteEffects.None, 0f);

                var secFont = _core.SecondaryFont;
                string fistText = "FISTS";
                Vector2 fSize = secFont.MeasureString(fistText);
                Vector2 textPos = new Vector2(MathF.Round(fDrawPos.X - fSize.X / 2f), MathF.Round(fDrawPos.Y - fSize.Y / 2f));
                Color fColor = _board.FistCard.IsHovered ? _global.Palette_Sun : _global.Palette_LightPale;
                spriteBatch.DrawStringOutlinedSnapped(secFont, fistText, textPos, fColor, _global.Palette_Off);
            }

            _ui.DrawRunningScore(spriteBatch, _runContext.CurrentScore);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, null, null, null, transform);

            foreach (var card in _unselectableCardsCache)
            {
                if (_runContext.Mode == GameMode.Classic && _board.SlainPile.Contains(card))
                {
                    int index = _board.SlainPile.IndexOf(card);
                    if (index < _board.SlainPile.Count - 2) continue;
                    if (index == _board.SlainPile.Count - 2)
                    {
                        var lastCard = _board.SlainPile.Last();
                        if (Vector2.Distance(lastCard.Position, lastCard.TargetPosition) < 2f) continue;
                    }
                }

                card.DrawFlash(spriteBatch, _spriteManager);
            }
            foreach (var card in _selectableCardsCache)
            {
                if (card == _activeTreasureCard) continue;
                card.DrawFlash(spriteBatch, _spriteManager);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            if (_state != ScoundrelState.Reward && !(_isRouletteFromReward && (_state == ScoundrelState.TreasureOpening || _state == ScoundrelState.Roulette || _state == ScoundrelState.RouletteFinished)))
            {
                _ui.DrawCounters(spriteBatch, _board.Deck.Count, _board.Discard.Count, _board.DeckPos, _board.DiscardPos);
            }

            _ui.DrawHoverIndicators(spriteBatch, _lastHoveredCard, _state, _board, _combat, _runContext.MaxHealth, _previewFlashTimer);
            _ui.DrawHealthBar(spriteBatch, _combat.Health, GetPreviewHealth(), _runContext.MaxHealth, _spriteManager);

            _ui.DrawRestartBar(spriteBatch, _restartHoldTimer, RESTART_HOLD_DURATION);

            _ui.DrawTimer(spriteBatch, _combat.TimeRemaining, gameTime);
            _ui.DrawFlyingTimers(spriteBatch);

            if (_state == ScoundrelState.FloorCleared || _state == ScoundrelState.CleaningUp || _state == ScoundrelState.SweepingBoard)
            {
                _ui.DrawFloorCleared(spriteBatch);
            }
            else if (_state == ScoundrelState.Reward || (_isRouletteFromReward && (_state == ScoundrelState.TreasureOpening || _state == ScoundrelState.Roulette || _state == ScoundrelState.RouletteFinished)))
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black * 0.8f);
                foreach (var c in _board.RewardCards)
                {
                    if (c == _activeTreasureCard) continue;
                    c.Draw(spriteBatch, _spriteManager);
                }

                foreach (var c in _board.Deck) c.Draw(spriteBatch, _spriteManager);
                foreach (var c in _board.Discard) c.Draw(spriteBatch, _spriteManager);
                _ui.DrawCounters(spriteBatch, _board.Deck.Count, _board.Discard.Count, _board.DeckPos, _board.DiscardPos);
            }
            else if (_state == ScoundrelState.GameOver)
            {
                _ui.DrawGameOver(spriteBatch, gameTime, transform, _combat.Health, _combat.TimeRemaining, _combat.DisplayScore);
            }

            if (_activeTreasureCard != null && (_state == ScoundrelState.TreasureOpening || _state == ScoundrelState.Roulette || _state == ScoundrelState.RouletteFinished))
            {
                _activeTreasureCard.Draw(spriteBatch, _spriteManager);

                if (_activeTreasureCard.FlashWhiteIntensity > 0f)
                {
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, null, null, null, transform);
                    _activeTreasureCard.DrawFlash(spriteBatch, _spriteManager);
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
                }
            }

            if (_state == ScoundrelState.Roulette || _state == ScoundrelState.RouletteFinished)
            {
                _ui.DrawRouletteModal(spriteBatch, _rouletteBelt, _rouletteOffset, _rouletteModalAlpha, _rouletteWinPlinkTimer, _rouletteNeedleRotation);
            }

            if (_isPaused)
            {
                _ui.DrawPauseMenu(spriteBatch, gameTime, transform);
            }

            _ui.DrawFloatingTexts(spriteBatch, gameTime);
        }
    }
}