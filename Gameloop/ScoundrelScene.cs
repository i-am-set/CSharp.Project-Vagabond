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
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public enum ScoundrelState { Intro, Dealing, Playing, Focused, ResolvingMonster, GameOver, Restarting, FloorCleared, TallyingPotions, SweepingBoard, Shop }

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
        private int _tallyIndex = 0;
        private List<Card> _tallyPotions = new List<Card>();

        private float _sweepTimer = 0f;

        private readonly List<Card> _selectableCardsCache = new List<Card>(50);
        private readonly List<Card> _unselectableCardsCache = new List<Card>(50);

        private readonly Random _random = new Random();

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

            _previousMouseState = _inputManager.GetEffectiveMouseState();
        }

        private void RestoreFromSave()
        {
            var data = SaveManager.CurrentSave;

            _runContext.Mode = data.Mode;
            _runContext.Floor = data.Floor;
            _runContext.MaxHealth = data.MaxHealth;
            _runContext.Health = data.Health;
            _runContext.Gold = data.Gold;

            _combat.Reset(_runContext.Health);
            _combat.Health = data.Health;
            _combat.FloorTimer = data.FloorTimer;
            _combat.TotalCardsInFloor = data.TotalCardsInFloor;
            _combat.LastSlainValue = data.LastSlainValue;
            _combat.CardsResolvedThisRoom = data.CardsResolvedThisRoom;
            _combat.PotionsUsedThisRoom = data.PotionsUsedThisRoom;
            _combat.CanSkip = data.CanSkip;

            _board.Reset();

            foreach (var cd in data.Deck) _board.Deck.Add(CreateCardFromData(cd));
            foreach (var cd in data.Room) _board.Room.Add(CreateCardFromData(cd));
            foreach (var cd in data.Discard) _board.Discard.Add(CreateCardFromData(cd));
            foreach (var cd in data.SlainPile) _board.SlainPile.Add(CreateCardFromData(cd));
            if (data.WeaponSlot != null) _board.WeaponSlot = CreateCardFromData(data.WeaponSlot);

            for (int i = 0; i < _board.Deck.Count; i++)
            {
                _board.Deck[i].Position = _board.DeckPos + new Vector2(0, -i * 0.5f);
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
                _board.Discard[i].Position = _board.DiscardPos + new Vector2(0, -i * 0.5f);
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
                    _board.SlainPile[i].Position = _board.WeaponPos + new Vector2(7, 0);
                    _board.SlainPile[i].TargetPosition = _board.SlainPile[i].Position;
                    _board.SlainPile[i].Rotation = MathHelper.PiOver2;
                    _board.SlainPile[i].TargetRotation = MathHelper.PiOver2;
                    _board.SlainPile[i].ZIndex = 150 + i;
                    _board.SlainPile[i].IsFaceUp = true;
                    _board.SlainPile[i].Scale = Vector2.One;
                    _board.SlainPile[i].TargetScale = Vector2.One;
                }
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
            else if (_state == ScoundrelState.Shop)
            {
                _ui.GenerateShop(this, _runContext, _combat);
            }

            _ui.DeckCountPlink.Start(0.5f, 0.3f);
            _ui.DiscardCountPlink.Start(0.6f, 0.3f);
            _ui.HealthPlink.Start(0.7f, 0.4f);

            SaveManager.CurrentSave = null;
        }

        private void SaveCurrentState()
        {
            if (_state == ScoundrelState.GameOver || _state == ScoundrelState.Restarting || _state == ScoundrelState.Intro || _state == ScoundrelState.TallyingPotions || _state == ScoundrelState.SweepingBoard) return;

            if (_combat.Health <= 0)
            {
                SaveManager.DeleteSave();
                return;
            }

            var data = new ScoundrelSaveData
            {
                Mode = _runContext.Mode,
                Floor = _runContext.Floor,
                MaxHealth = _runContext.MaxHealth,
                Health = _combat.Health,
                Gold = _runContext.Gold,
                FloorTimer = _combat.FloorTimer,
                TotalCardsInFloor = _combat.TotalCardsInFloor,
                LastSlainValue = _combat.LastSlainValue,
                CardsResolvedThisRoom = _combat.CardsResolvedThisRoom,
                PotionsUsedThisRoom = _combat.PotionsUsedThisRoom,
                CanSkip = _combat.CanSkip,
                State = _state,
                FocusedRoomSlotIndex = _board.FocusedCard?.RoomSlotIndex ?? -1,
                ResolvingMonsterRoomSlotIndex = _combat.ResolvingMonster?.RoomSlotIndex ?? -1,
                ResolveDamage = _combat.ResolveDamage,
                ResolveWeaponUsed = _combat.ResolveWeaponUsed,
                WeaponSlot = _board.WeaponSlot != null ? CreateDataFromCard(_board.WeaponSlot) : null
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
                Value = c.Value,
                IsFaceUp = c.IsFaceUp,
                RoomSlotIndex = c.RoomSlotIndex
            };
        }

        private Card CreateCardFromData(CardData d)
        {
            return new Card(d.Suit, d.Type, d.Rank, d.Value)
            {
                IsFaceUp = d.IsFaceUp,
                RoomSlotIndex = d.RoomSlotIndex
            };
        }

        private void RestartGame()
        {
            _combat.Reset(_runContext.Health);
            _ui.Reset();

            _previewFlashTimer = 0f;
            _lastHoveredCard = null;

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
            _combat.Reset(_runContext.Health);
            _ui.Reset();

            _previewFlashTimer = 0f;
            _lastHoveredCard = null;

            _board.CardsToReturn.Clear();
            _board.CardsToReturn.AddRange(_board.Room);
            _board.CardsToReturn.AddRange(_board.Discard);
            _board.CardsToReturn.AddRange(_board.SlainPile);
            if (_board.WeaponSlot != null) _board.CardsToReturn.Add(_board.WeaponSlot);
            if (_board.FocusedCard != null && !_board.CardsToReturn.Contains(_board.FocusedCard)) _board.CardsToReturn.Add(_board.FocusedCard);
            if (_combat.ResolvingMonster != null && !_board.CardsToReturn.Contains(_combat.ResolvingMonster)) _board.CardsToReturn.Add(_combat.ResolvingMonster);

            _board.Room.Clear();
            _board.Discard.Clear();
            _board.SlainPile.Clear();
            _board.WeaponSlot = null;
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
                _combat.CalculateTargetScore(_board, _runContext.MaxHealth);
                return;
            }

            _runContext.Floor++;
            PrepareNextFloor();
        }

        private void PrepareNextFloor()
        {
            _combat.Reset(_runContext.Health);
            _ui.Reset();

            _previewFlashTimer = 0f;
            _lastHoveredCard = null;

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

            if (_state == ScoundrelState.Playing || _state == ScoundrelState.Focused || _state == ScoundrelState.ResolvingMonster)
            {
                _combat.FloorTimer += dt;
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

            _ui.Update(dt, gameTime, _board.DeckPos, _board.DiscardPos);
            _board.UpdateWaves(dt);
            _combat.Update(dt);

            UpdateHoverStates(mousePos, dt);

            _board.Update(dt);

            switch (_state)
            {
                case ScoundrelState.Intro: UpdateIntro(dt, justClicked); break;
                case ScoundrelState.Restarting: UpdateRestarting(dt); break;
                case ScoundrelState.Dealing: UpdateDealing(dt); break;
                case ScoundrelState.Playing: UpdatePlaying(justClicked, mousePos); break;
                case ScoundrelState.Focused: UpdateFocused(justClicked, mousePos); break;
                case ScoundrelState.ResolvingMonster: UpdateResolvingMonster(dt); break;
                case ScoundrelState.FloorCleared: UpdateFloorCleared(dt); break;
                case ScoundrelState.TallyingPotions: UpdateTallyingPotions(dt); break;
                case ScoundrelState.SweepingBoard: UpdateSweepingBoard(dt); break;
                case ScoundrelState.Shop: UpdateShop(mouseState); break;
                case ScoundrelState.GameOver: UpdateGameOver(dt, justClicked, mousePos, mouseState); break;
            }

            _previousMouseState = mouseState;
        }

        private void UpdateHoverStates(Vector2 mousePos, float dt)
        {
            bool canSkipNow = _state == ScoundrelState.Playing && _board.Room.Count == 4 && _combat.CardsResolvedThisRoom == 0 && _combat.CanSkip;
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

            if (_state == ScoundrelState.Playing)
            {
                foreach (var c in _board.Room)
                {
                    c.IsSelectable = true;
                    c.ExpandHitboxX = true;
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
                if (_board.FocusedCard != null) _board.FocusedCard.IsSelectable = true;
                if (_board.WeaponSlot != null) _board.WeaponSlot.IsSelectable = true;
                _board.FistCard.IsSelectable = true;
            }
            else if (_state == ScoundrelState.TallyingPotions)
            {
                foreach (var p in _tallyPotions) p.ForceRenderAboveVeil = true;
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
                    _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                }
                c.IsHovered = (c == newHovered);
            }

            if (newHovered != null && !newHovered.IsFocused) newHovered.OutlineColor = _global.Palette_Sun;

            if (_board.WeaponSlot != null)
            {
                _board.WeaponSlot.TargetPosition = _board.WeaponPos;
                _board.WeaponSlot.IsBeingReplaced = false;
            }

            if (_state == ScoundrelState.Playing && newHovered != null && newHovered.Type == CardType.Weapon && _board.WeaponSlot != null)
            {
                _board.WeaponSlot.TargetPosition = _board.WeaponPos + new Vector2(38, 0);
                _board.WeaponSlot.IsBeingReplaced = true;
            }

            if (_board.WeaponSlot != null)
            {
                foreach (var slain in _board.SlainPile) slain.TargetPosition = _board.WeaponSlot.TargetPosition + new Vector2(7, 0);
            }

            if (_state == ScoundrelState.Playing && newHovered != null && newHovered.Type == CardType.Monster && _board.WeaponSlot != null)
            {
                if (newHovered.Value <= _combat.LastSlainValue)
                {
                    _board.WeaponSlot.OutlineColor = _global.Palette_Leaf;
                    _board.WeaponSlot.ForceRenderAboveVeil = true;
                }
                else
                {
                    _board.WeaponSlot.OutlineColor = null;
                    _board.WeaponSlot.ForceRenderAboveVeil = false;
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
                    _board.Deck[_cardsLanded].TargetPosition = _board.DeckPos + new Vector2(0, -_cardsLanded * 0.5f);
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_plink");
                    _cardsLanded++;
                }

                if (_introTimer >= INTRO_DURATION)
                {
                    for (int i = _cardsLanded; i < _board.Deck.Count; i++)
                    {
                        _board.Deck[i].TargetPosition = _board.DeckPos + new Vector2(0, -i * 0.5f);
                    }
                    _cardsLanded = _board.Deck.Count;

                    _introAligning = true;
                    _introTimer = 0f;

                    for (int i = 0; i < _board.Deck.Count; i++)
                    {
                        _board.Deck[i].TargetRotation = 0f;
                    }
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=5;freq=300;atk=0.02;sus=0.0;dec=0.15;lpf=1000;vol=0.1", 0.1f);
                }
            }
            else
            {
                if (_introTimer >= 0.3f)
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
                    card.TargetPosition = _board.DeckPos + new Vector2(0, -_board.Deck.Count * 0.5f);
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
                        _board.Deck[i].TargetPosition = _board.DeckPos + new Vector2(0, -i * 0.5f);
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
                SaveCurrentState();
            }
        }

        private void UpdatePlaying(bool justClicked, Vector2 mousePos)
        {
            if (justClicked && _inputManager.IsMouseClickAvailable() && _lastHoveredCard != null)
            {
                if (_lastHoveredCard == _board.SkipCard)
                {
                    OnSkipClicked();
                    _inputManager.ConsumeMouseClick();
                }
                else
                {
                    ResolveCardClick(_lastHoveredCard);
                    _inputManager.ConsumeMouseClick();
                }
            }

            CheckGameOver();
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

        private void UpdateResolvingMonster(float dt)
        {
            _combat.UpdateResolution(dt, _board, _ui, _core, _hapticsManager, _runContext, () => {
                _state = ScoundrelState.Playing;
                OnCardResolved();
            });
        }

        private void UpdateFloorCleared(float dt)
        {
            _floorClearedTimer -= dt;
            if (_floorClearedTimer <= 0)
            {
                _tallyPotions.Clear();
                _tallyPotions.AddRange(_board.Room.Where(c => c.Type == CardType.Potion));
                _tallyPotions.AddRange(_board.Deck.Where(c => c.Type == CardType.Potion));

                if (_tallyPotions.Count > 0)
                {
                    _state = ScoundrelState.TallyingPotions;
                    _tallyPhase = 0;
                    _tallyTimer = 0f;
                    _tallyIndex = 0;

                    float startX = (Global.VIRTUAL_WIDTH / 2f) - ((_tallyPotions.Count - 1) * 20f / 2f);
                    for (int i = 0; i < _tallyPotions.Count; i++)
                    {
                        var p = _tallyPotions[i];
                        p.TargetPosition = new Vector2(startX + i * 20f, Global.VIRTUAL_HEIGHT / 2f);
                        p.IsFaceUp = true;
                        p.ZIndex = 600 + i;
                        p.RoomSlotIndex = -1;
                    }
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=300;slide=100;atk=0.05;sus=0.05;dec=0.2;vol=0.08", 0.15f);
                }
                else
                {
                    StartSweepingBoard();
                }
            }
        }

        private void UpdateTallyingPotions(float dt)
        {
            _tallyTimer += dt;
            if (_tallyPhase == 0 && _tallyTimer > 0.5f)
            {
                _tallyPhase = 1;
                _tallyTimer = 0f;
            }
            else if (_tallyPhase == 1)
            {
                if (_tallyIndex < _tallyPotions.Count)
                {
                    if (_tallyTimer > 0.15f)
                    {
                        _tallyTimer = 0f;
                        var p = _tallyPotions[_tallyIndex];
                        p.TargetScale = Vector2.Zero;

                        int goldVal = p.Value / 2;
                        if (goldVal > 0)
                        {
                            _runContext.Gold += goldVal;
                            _ui.AddFloatingText(goldVal, false, true, p.Position);
                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=1600;slide=200;atk=0.01;sus=0.05;dec=0.2;vol=0.05", 0.1f);
                        }

                        var psm = ServiceLocator.Get<ParticleSystemManager>();
                        var emitter = psm.CreateEmitter(ParticleEffects.CreateUIPlink());
                        emitter.Position = p.Position;
                        emitter.EmitBurst(10);

                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_plink", 0.2f);
                        _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);

                        _tallyIndex++;
                    }
                }
                else if (_tallyTimer > 0.5f)
                {
                    foreach (var p in _tallyPotions)
                    {
                        _board.Room.Remove(p);
                        _board.Deck.Remove(p);
                    }
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

            _board.Room.Clear();
            _board.Discard.Clear();
            _board.SlainPile.Clear();
            _board.WeaponSlot = null;

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

                        card.TargetPosition = _board.DeckPos + new Vector2(0, -_board.Deck.Count * 0.5f);
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
                    _state = ScoundrelState.Shop;
                    _ui.ShopFadeTimer = 0f;
                    _ui.GenerateShop(this, _runContext, _combat);
                    SaveCurrentState();
                }
            }
        }

        private void UpdateShop(MouseState mouseState)
        {
            foreach (var btn in _ui.ShopButtons) btn.Update(mouseState);
            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse) _ui.ShopNavGroup.UpdateInput(_inputManager);
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

        private void ResolveCardClick(Card card)
        {
            if (card.Type == CardType.Potion)
            {
                int healAmount = _combat.PotionsUsedThisRoom == 0 ? card.Value : 0;
                _combat.ApplyHeal(healAmount, _runContext.MaxHealth, _ui);
                _combat.PotionsUsedThisRoom++;
                _board.MoveToDiscard(card);
                OnCardResolved();
            }
            else if (card.Type == CardType.Weapon)
            {
                _board.EquipWeapon(card);
                _combat.LastSlainValue = 99;
                OnCardResolved();
            }
            else if (card.Type == CardType.Monster)
            {
                bool canUseWeapon = _board.WeaponSlot != null && card.Value <= _combat.LastSlainValue;

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
                _board.Deck[i].TargetPosition = _board.DeckPos + new Vector2(0, -i * 0.5f);
                _board.Deck[i].ZIndex = i;
            }

            _state = ScoundrelState.Dealing;
            _dealTimer = DEAL_INTERVAL;

            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=3;freq=1000;slide=-500;atk=0.02;sus=0.05;dec=0.15;vol=0.12|wave=4;freq=400;slide=-100;atk=0.02;sus=0.05;dec=0.15;vol=0.15", 0.2f);
            SaveCurrentState();
        }

        private void OnFistsClicked()
        {
            if (_board.FocusedCard != null)
            {
                if (_combat.Health - _board.FocusedCard.Value <= 0) SaveManager.DeleteSave();
                _combat.StartMonsterResolution(_board.FocusedCard, _board.FocusedCard.Value, false);
                _state = ScoundrelState.ResolvingMonster;
                if (SaveManager.HasSave()) SaveCurrentState();
            }
        }

        private void OnWeaponClicked()
        {
            if (_board.FocusedCard == null || _board.WeaponSlot == null) return;

            if (_board.FocusedCard.Value > _combat.LastSlainValue)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=4;freq=150;atk=0.01;sus=0.1;dec=0.1;detune=0.03;vol=0.15|wave=0;freq=150;atk=0.01;sus=0.1;dec=0.1;duty=0.2;vol=0.1", 0.2f);
                return;
            }

            int damage = Math.Max(0, _board.FocusedCard.Value - _board.WeaponSlot.Value);
            if (_combat.Health - damage <= 0) SaveManager.DeleteSave();

            _combat.StartMonsterResolution(_board.FocusedCard, damage, true);
            _state = ScoundrelState.ResolvingMonster;
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

        private void OnCardResolved()
        {
            _combat.CardsResolvedThisRoom++;
            _combat.CanSkip = true;

            if (_combat.CardsResolvedThisRoom >= 3 && _board.Deck.Count > 0)
            {
                _state = ScoundrelState.Dealing;
                _dealTimer = DEAL_INTERVAL;
            }
            SaveCurrentState();
        }

        private void CheckGameOver()
        {
            if (_combat.Health <= 0)
            {
                SaveManager.DeleteSave();
                _state = ScoundrelState.GameOver;
                _combat.PlayDefeatSequence();
                _combat.CalculateTargetScore(_board, _runContext.MaxHealth);
            }
            else if (_board.Deck.Count == 0 && !HasMonsterInRoom())
            {
                if (_runContext.Mode == GameMode.Classic || _runContext.Floor >= _runContext.MaxFloors)
                {
                    SaveManager.DeleteSave();
                    _state = ScoundrelState.GameOver;
                    _combat.PlayVictorySequence();
                    _combat.CalculateTargetScore(_board, _runContext.MaxHealth);
                }
                else
                {
                    _combat.CalculateSpeedGold(_runContext, _ui);
                    _state = ScoundrelState.FloorCleared;
                    _floorClearedTimer = 1.5f;
                    _ui.FloorClearedTextTimer = 0f;
                    SaveCurrentState();
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=523.25;atk=0.05;sus=0.1;dec=0.4;vol=0.1|wave=2;freq=659.25;atk=0.05;sus=0.1;dec=0.4;delay=0.1;vol=0.1|wave=2;freq=783.99;atk=0.05;sus=0.2;dec=0.6;delay=0.2;vol=0.1", 0.15f);
                }
            }
        }

        private bool HasMonsterInRoom()
        {
            foreach (var c in _board.Room)
            {
                if (c.Type == CardType.Monster) return true;
            }
            return false;
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
                    bool canUseWeapon = _board.WeaponSlot != null && _lastHoveredCard.Value <= _combat.LastSlainValue;
                    if (canUseWeapon)
                    {
                        bool showWeaponDamage = (_previewFlashTimer % 1.5f) < 1.0f;
                        if (showWeaponDamage) return _combat.Health - Math.Max(0, _lastHoveredCard.Value - _board.WeaponSlot!.Value);
                        return _combat.Health - _lastHoveredCard.Value;
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
            bool canSkipNow = _state == ScoundrelState.Playing && _board.Room.Count == 4 && _combat.CardsResolvedThisRoom == 0 && _combat.CanSkip;

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

            bool isHoveringWeapon = (_state == ScoundrelState.Playing || _state == ScoundrelState.Focused) && _lastHoveredCard != null && _lastHoveredCard.Type == CardType.Weapon;
            bool showWeaponOutline = isHoveringWeapon || (_board.WeaponSlot != null && _board.WeaponSlot.IsBeingReplaced);

            if (showWeaponOutline)
            {
                Rectangle outlineSource = _spriteManager.ScoundrelCardRects[1, 0];
                Vector2 origin = new Vector2(18f, 25f);
                Vector2 drawPos = new Vector2(MathF.Round(_board.WeaponPos.X), MathF.Round(_board.WeaponPos.Y));
                spriteBatch.DrawSnapped(_spriteManager.ScoundrelCardsSpriteSheet, drawPos, outlineSource, Color.White * 0.5f, 0f, origin, 1f, SpriteEffects.None, 0f);
            }

            if (_state == ScoundrelState.Focused)
            {
                foreach (var card in _unselectableCardsCache) card.Draw(spriteBatch, _spriteManager);
            }

            if (_state == ScoundrelState.Focused || (_state == ScoundrelState.Playing && !canSkipNow))
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off * 0.4f);
            }

            if (_state != ScoundrelState.Focused)
            {
                foreach (var card in _unselectableCardsCache) card.Draw(spriteBatch, _spriteManager);
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

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off * 0.4f);

            foreach (var card in _selectableCardsCache) card.Draw(spriteBatch, _spriteManager);

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

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, null, null, null, transform);

            foreach (var card in _unselectableCardsCache) card.DrawFlash(spriteBatch, _spriteManager);
            foreach (var card in _selectableCardsCache) card.DrawFlash(spriteBatch, _spriteManager);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            _ui.DrawTimer(spriteBatch, _combat.FloorTimer);
            _ui.DrawCounters(spriteBatch, _board.Deck.Count, _board.Discard.Count, _board.DeckPos, _board.DiscardPos);
            _ui.DrawHoverIndicators(spriteBatch, _lastHoveredCard, _state, _board, _combat, _runContext.MaxHealth, _previewFlashTimer);
            _ui.DrawHealthBar(spriteBatch, _combat.Health, GetPreviewHealth(), _runContext.MaxHealth, _spriteManager);
            _ui.DrawGold(spriteBatch, _runContext.Gold);
            _ui.DrawFloatingTexts(spriteBatch);

            _ui.DrawRestartBar(spriteBatch, _restartHoldTimer, RESTART_HOLD_DURATION);

            if (_state == ScoundrelState.FloorCleared || _state == ScoundrelState.TallyingPotions || _state == ScoundrelState.SweepingBoard)
            {
                _ui.DrawFloorCleared(spriteBatch);
            }
            else if (_state == ScoundrelState.Shop)
            {
                _ui.DrawShop(spriteBatch, gameTime, transform);
            }
            else if (_state == ScoundrelState.GameOver)
            {
                _ui.DrawGameOver(spriteBatch, gameTime, transform, _combat.Health, _combat.DisplayScore);
            }

            if (_isPaused)
            {
                _ui.DrawPauseMenu(spriteBatch, gameTime, transform);
            }
        }
    }
}