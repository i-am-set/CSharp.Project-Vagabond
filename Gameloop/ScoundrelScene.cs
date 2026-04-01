using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Particles;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectVagabond.Scenes
{
    public class ScoundrelScene : GameScene
    {
        private enum ScoundrelState { Dealing, Playing, Focused, ResolvingMonster, GameOver }

        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly InputManager _inputManager;
        private readonly SceneManager _sceneManager;
        private readonly HapticsManager _hapticsManager;
        private readonly Core _core;
        private readonly Texture2D _pixel;

        private ScoundrelState _state;
        private List<Card> _deck = new List<Card>();
        private List<Card> _room = new List<Card>();
        private List<Card> _discard = new List<Card>();
        private List<Card> _slainPile = new List<Card>();
        private Card _weaponSlot;
        private Card _focusedCard;
        private Card _fistCard;

        private int _health;
        private int _lastSlainValue;
        private int _cardsResolvedThisRoom;
        private int _potionsUsedThisRoom;
        private bool _canSkip;

        private float _dealTimer;
        private const float DEAL_INTERVAL = 0.15f;
        private float _skipShakeTimer = 0f;

        private float _roomWaveTimer = 0f;
        private float _roomWaveInterval = 3f;
        private float _currentWaveTime = -1f;

        private Button _skipButton;
        private Button _exitButton;

        private PlinkAnimator _healthPlink;
        private List<FloatingText> _floatingTexts = new List<FloatingText>();

        private float[] _heartFlashTimers = new float[10];
        private int[] _heartFlashFrames = new int[10];
        private const float HEART_FLASH_DURATION = 0.75f;
        private const float HEART_FLASH_BLINK_INTERVAL = 0.15f;
        private const float HEART_FLASH_BLINK_HALF = 0.075f;

        private float _previewFlashTimer = 0f;
        private Card _lastHoveredCard = null;

        private Vector2 _deckPos = new Vector2(30, 40);
        private Vector2 _discardPos = new Vector2(30, 140);
        private Vector2 _weaponPos = new Vector2(160, 140);
        private Vector2[] _roomPositions = new Vector2[]
        {
            new Vector2(103, 80),
            new Vector2(141, 80),
            new Vector2(179, 80),
            new Vector2(217, 80)
        };

        private Random _random = new Random();
        private MouseState _previousMouseState;
        private bool _uiInitialized = false;

        private int _displayScore;
        private int _targetScore;
        private float _scoreAnimTimer;
        private bool _scoreSlamPlayed;

        // Monster Resolution State
        private Card _resolvingMonster;
        private float _resolveTimer;
        private int _resolveDamage;
        private bool _resolveDamageApplied;
        private bool _resolveWeaponUsed;
        private float _resolveTargetRotation;

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

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        private void InitializeUI()
        {
            if (_uiInitialized) return;

            var secFont = _core.SecondaryFont;
            var defFont = _core.DefaultFont;

            Vector2 skipSize = defFont.MeasureString("SKIP ROOM");
            int skipW = (int)skipSize.X + 12;
            int skipH = (int)skipSize.Y + 6;
            _skipButton = new Button(new Rectangle(Global.VIRTUAL_WIDTH / 2 - skipW / 2, 18, skipW, skipH), "SKIP ROOM", font: defFont);
            _skipButton.OnClick += OnSkipClicked;

            _exitButton = new Button(new Rectangle(Global.VIRTUAL_WIDTH / 2 - 30, 120, 60, 15), "MAIN MENU", font: secFont) { DrawBorderOnHover = true };
            _exitButton.OnClick += () => { _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff); };

            _healthPlink = new PlinkAnimator { MaxScale = 1.5f, RestScale = 1.0f };

            _fistCard = new Card(CardSuit.None, CardType.Outline, 0, 0);
            _fistCard.IsFaceUp = true;
            _fistCard.Position = new Vector2(236, 140);
            _fistCard.TargetPosition = new Vector2(236, 140);
            _fistCard.ZIndex = 200;

            _uiInitialized = true;
        }

        public override void Enter()
        {
            base.Enter();
            InitializeUI();

            var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();
            audio.MusicPitchOffset = 0f;
            audio.PlayMusic("music_battle", 1.0f);
            audio.SetCurrentMusicStemVolume(0, 1.0f);
            audio.SetCurrentMusicStemVolume(1, 0.0f);

            _health = 20;
            _lastSlainValue = 99;
            _cardsResolvedThisRoom = 0;
            _potionsUsedThisRoom = 0;
            _canSkip = true;
            _floatingTexts.Clear();

            Array.Clear(_heartFlashTimers, 0, 10);
            Array.Clear(_heartFlashFrames, 0, 10);
            _previewFlashTimer = 0f;
            _lastHoveredCard = null;

            _displayScore = -208;
            _targetScore = 0;
            _scoreAnimTimer = 0f;
            _scoreSlamPlayed = false;
            _skipShakeTimer = 0f;

            _roomWaveTimer = 0f;
            _roomWaveInterval = 3f + (float)_random.NextDouble() * 2f;
            _currentWaveTime = -1f;

            _deck.Clear();
            _room.Clear();
            _discard.Clear();
            _slainPile.Clear();
            _weaponSlot = null;
            _focusedCard = null;
            _resolvingMonster = null;

            GenerateDeck();
            _state = ScoundrelState.Dealing;
            _dealTimer = 0f;
            _previousMouseState = _inputManager.GetEffectiveMouseState();
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

        private void GenerateDeck()
        {
            for (int i = 2; i <= 10; i++) _deck.Add(new Card(CardSuit.Hearts, CardType.Potion, i, i));
            for (int i = 2; i <= 10; i++) _deck.Add(new Card(CardSuit.Diamonds, CardType.Weapon, i, i));
            for (int i = 2; i <= 14; i++) _deck.Add(new Card(CardSuit.Spades, CardType.Monster, i, i));
            for (int i = 2; i <= 14; i++) _deck.Add(new Card(CardSuit.Clubs, CardType.Monster, i, i));

            _deck = _deck.OrderBy(x => _random.Next()).ToList();

            for (int i = 0; i < _deck.Count; i++)
            {
                _deck[i].Position = _deckPos + new Vector2(0, -i * 0.25f);
                _deck[i].TargetPosition = _deck[i].Position;
                _deck[i].ZIndex = i;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            var mouseState = _inputManager.GetEffectiveMouseState();
            bool justClicked = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            Vector2 mousePos = Core.TransformMouse(mouseState.Position);

            var secFont = _core.SecondaryFont;
            string hpText = $"HP: {_health}";
            Vector2 hpSize = secFont.MeasureString(hpText);
            Vector2 hpCenter = new Vector2(Global.VIRTUAL_WIDTH - 20 - hpSize.X / 2f, 20 + hpSize.Y / 2f);
            _healthPlink.Update(gameTime, hpCenter);

            if (_skipShakeTimer > 0) _skipShakeTimer -= dt;

            for (int i = 0; i < 10; i++)
            {
                if (_heartFlashTimers[i] > 0) _heartFlashTimers[i] -= dt;
            }

            _roomWaveTimer += dt;
            if (_roomWaveTimer >= _roomWaveInterval)
            {
                _roomWaveTimer = 0f;
                _roomWaveInterval = 3f + (float)_random.NextDouble() * 3f;
                _currentWaveTime = 0f;
            }

            if (_currentWaveTime >= 0f)
            {
                _currentWaveTime += dt;
                if (_currentWaveTime > 1.0f)
                {
                    _currentWaveTime = -1f;
                }
            }

            for (int i = _floatingTexts.Count - 1; i >= 0; i--)
            {
                _floatingTexts[i].Timer -= dt;
                _floatingTexts[i].LocalOffset.Y -= 10f * dt;
                if (_floatingTexts[i].Timer <= 0) _floatingTexts.RemoveAt(i);
            }

            var allCards = new List<Card>();
            allCards.AddRange(_deck);
            allCards.AddRange(_discard);
            allCards.AddRange(_slainPile);
            if (_weaponSlot != null) allCards.Add(_weaponSlot);
            allCards.AddRange(_room);
            if (_state == ScoundrelState.Focused) allCards.Add(_fistCard);

            foreach (var c in allCards)
            {
                c.IsSelectable = false;
                c.ExpandHitboxX = false;
                c.OutlineColor = null;
                c.ForceRenderAboveVeil = false;
                c.IsFocused = (c == _focusedCard);
                if (_state != ScoundrelState.ResolvingMonster || c != _resolvingMonster)
                {
                    c.VisualYOffset = 0f;
                }
            }

            if (_state == ScoundrelState.Playing)
            {
                foreach (var c in _room)
                {
                    c.IsSelectable = true;
                    c.ExpandHitboxX = true;

                    if (_currentWaveTime >= 0f && c.RoomSlotIndex >= 0 && c.RoomSlotIndex <= 3)
                    {
                        float slotTime = c.RoomSlotIndex * 0.1f;
                        float localTime = _currentWaveTime - slotTime;
                        if (localTime > 0f && localTime < 0.2f)
                        {
                            float progress = localTime / 0.2f;
                            c.VisualYOffset = -MathF.Sin(progress * MathHelper.Pi) * 1f;
                        }
                    }
                }
            }
            else if (_state == ScoundrelState.Focused)
            {
                if (_focusedCard != null) _focusedCard.IsSelectable = true;
                if (_weaponSlot != null) _weaponSlot.IsSelectable = true;
                _fistCard.IsSelectable = true;
            }

            Card newHovered = null;
            if ((_state == ScoundrelState.Playing || _state == ScoundrelState.Focused) && _inputManager.IsMouseClickAvailable())
            {
                newHovered = allCards.Where(c => c.IsSelectable).OrderByDescending(c => c.ZIndex).FirstOrDefault(c => c.GetBounds().Contains(mousePos));
            }

            if (newHovered != _lastHoveredCard)
            {
                _previewFlashTimer = 0f;
                _lastHoveredCard = newHovered;
            }
            else if (newHovered != null)
            {
                _previewFlashTimer += dt;
            }

            foreach (var c in allCards)
            {
                if (c == newHovered && !c.IsHovered)
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_hover");
                    _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                }
                c.IsHovered = (c == newHovered);
            }

            if (newHovered != null && !newHovered.IsFocused)
            {
                newHovered.OutlineColor = _global.Palette_Sun;
            }

            if (_weaponSlot != null)
            {
                _weaponSlot.TargetPosition = _weaponPos;
                _weaponSlot.IsBeingReplaced = false;
            }

            if (_state == ScoundrelState.Playing && newHovered != null && newHovered.Type == CardType.Weapon && _weaponSlot != null)
            {
                _weaponSlot.TargetPosition = _weaponPos + new Vector2(34, 0);
                _weaponSlot.IsBeingReplaced = true;
            }

            if (_weaponSlot != null)
            {
                foreach (var slain in _slainPile)
                {
                    slain.TargetPosition = _weaponSlot.TargetPosition + new Vector2(7, 0);
                }
            }

            if (_state == ScoundrelState.Playing && newHovered != null && newHovered.Type == CardType.Monster && _weaponSlot != null)
            {
                if (newHovered.Value < _lastSlainValue)
                {
                    _weaponSlot.OutlineColor = _global.Palette_Leaf;
                    _weaponSlot.ForceRenderAboveVeil = true;
                }
                else
                {
                    _weaponSlot.OutlineColor = null;
                    _weaponSlot.ForceRenderAboveVeil = false;
                }
            }

            foreach (var card in _deck) card.Update(dt);
            foreach (var card in _room) card.Update(dt);
            foreach (var card in _discard) card.Update(dt);
            foreach (var card in _slainPile) card.Update(dt);
            _weaponSlot?.Update(dt);
            if (_state == ScoundrelState.Focused) _fistCard.Update(dt);

            if (_state == ScoundrelState.Dealing)
            {
                _dealTimer -= dt;
                if (_dealTimer <= 0f && _room.Count < 4 && _deck.Count > 0)
                {
                    var card = _deck.Last();
                    _deck.RemoveAt(_deck.Count - 1);
                    _room.Add(card);

                    int slot = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        if (!_room.Any(c => c.RoomSlotIndex == i))
                        {
                            slot = i;
                            break;
                        }
                    }

                    card.RoomSlotIndex = slot;
                    card.TargetPosition = _roomPositions[slot];
                    card.ZIndex = 100 + slot;
                    card.Flip();

                    _dealTimer = DEAL_INTERVAL;
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=300;slide=3750;atk=0;sus=0;dec=0.08;exp=1;vol=0.05", 0.15f);
                }
                else if (_room.Count == 4 || _deck.Count == 0)
                {
                    if (_state == ScoundrelState.Dealing)
                    {
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=400;atk=0.05;sus=0.1;dec=0.3;detune=0.01;delay=0.1;delfb=0.2;vol=0.15|wave=2;freq=600;atk=0.05;sus=0.1;dec=0.3;detune=0.01;vol=0.15", 0.15f);
                        _skipShakeTimer = 0.6f;
                    }
                    _state = ScoundrelState.Playing;
                    _cardsResolvedThisRoom = 0;
                    _potionsUsedThisRoom = 0;
                }
            }
            else if (_state == ScoundrelState.Playing)
            {
                bool canSkipNow = _room.Count == 4 && _cardsResolvedThisRoom == 0 && _canSkip;
                _skipButton.IsEnabled = canSkipNow;
                _skipButton.Update(mouseState);

                if (justClicked && _skipButton.Bounds.Contains(mousePos) && canSkipNow)
                {
                    _inputManager.ConsumeMouseClick();
                }

                if (justClicked && _inputManager.IsMouseClickAvailable() && newHovered != null)
                {
                    ResolveCardClick(newHovered);
                    _inputManager.ConsumeMouseClick();
                }

                CheckGameOver();
            }
            else if (_state == ScoundrelState.Focused)
            {
                if (justClicked && _inputManager.IsMouseClickAvailable())
                {
                    if (newHovered == _fistCard)
                    {
                        OnFistsClicked();
                        _inputManager.ConsumeMouseClick();
                    }
                    else if (newHovered == _weaponSlot)
                    {
                        OnWeaponClicked();
                        _inputManager.ConsumeMouseClick();
                    }
                    else if (newHovered == null)
                    {
                        CancelFocus();
                        _inputManager.ConsumeMouseClick();
                    }
                }
            }
            else if (_state == ScoundrelState.ResolvingMonster)
            {
                _resolveTimer += dt;

                float lungeTime = 0.1f;
                float retreatTime = 0.25f;
                float totalTime = lungeTime + retreatTime;

                if (_resolveTimer < lungeTime)
                {
                    // Lunge with anticipation (pulls up slightly, then slams down)
                    float p = _resolveTimer / lungeTime;
                    _resolvingMonster.VisualYOffset = Easing.EaseInBack(p) * 24f;
                }
                else if (_resolveTimer < totalTime)
                {
                    // Apex / Impact
                    if (!_resolveDamageApplied)
                    {
                        if (_resolveDamage > 0) ApplyDamage(_resolveDamage);

                        if (_resolveWeaponUsed)
                        {
                            string sfxWeaponBlock = "proc:wave=0;freq=400;slide=-100;atk=0.01;sus=0.02;dec=0.2;duty=0.2;vol=0.15|wave=4;freq=100;atk=0.01;sus=0.02;dec=0.15;vol=0.2|wave=6;freq=500;atk=0.01;sus=0.01;dec=0.1;vol=0.1";
                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(sfxWeaponBlock, 0.2f);
                        }

                        string sfxPlayerDamage = "proc:wave=4;freq=120;slide=-40;atk=0.01;sus=0.05;dec=0.2;dist=0.8;lpf=600;vol=0.2|wave=5;freq=100;atk=0.01;sus=0.05;dec=0.2;vol=0.15";
                        string sfxMonsterDamage = "proc:wave=4;freq=60;slide=-20;atk=0.01;sus=0.05;dec=0.2;dist=0.8;lpf=300;vol=0.2|wave=5;freq=50;atk=0.01;sus=0.05;dec=0.2;vol=0.15";

                        float baseFreq = MathHelper.Lerp(700f, 250f, (_resolvingMonster.Value - 2f) / 12f);
                        float slideFreq = baseFreq * 0.6f;
                        string sfxMonsterDie = $"proc:wave=4;freq={baseFreq:F0};slide=-{slideFreq:F0};atk=0.02;sus=0.1;dec=0.35;detune=0.04;vibdepth=15;vibspeed=12;vol=0.15|wave=2;freq={baseFreq / 2:F0};slide=-{slideFreq / 2:F0};atk=0.02;sus=0.1;dec=0.35;vol=0.15";

                        if (_resolveDamage > 0)
                        {
                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(sfxPlayerDamage, 0.2f);
                        }

                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(sfxMonsterDamage, 0.2f);
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx(sfxMonsterDie, 0.2f);

                        _resolveDamageApplied = true;
                    }

                    float hitT = _resolveTimer - lungeTime;
                    float p = hitT / retreatTime;

                    // Retreat with springy overshoot
                    _resolvingMonster.VisualYOffset = MathHelper.Lerp(24f, 0f, Easing.EaseOutBack(p));

                    // Rotation Tween
                    float rotEase = Easing.EaseOutCubic(p);
                    _resolvingMonster.Rotation = _resolveTargetRotation * rotEase;
                    _resolvingMonster.TargetRotation = _resolvingMonster.Rotation;

                    // Violent random shake
                    float decay = 1f - Easing.EaseOutQuad(p);
                    float shakeX = (float)(_random.NextDouble() * 2 - 1) * 10f * decay;
                    float shakeY = (float)(_random.NextDouble() * 2 - 1) * 10f * decay;
                    _resolvingMonster.ShakeOffset = new Vector2(shakeX, shakeY);

                    // Flash white
                    _resolvingMonster.FlashWhiteIntensity = p < 0.15f ? 1f : (1f - (p - 0.15f) / 0.85f);
                }
                else
                {
                    // Finish
                    _resolvingMonster.ShakeOffset = Vector2.Zero;
                    _resolvingMonster.FlashWhiteIntensity = 0f;
                    _resolvingMonster.VisualYOffset = 0f;
                    _resolvingMonster.TargetRotation = _resolveTargetRotation;

                    if (_resolveWeaponUsed)
                    {
                        _lastSlainValue = _resolvingMonster.Value;
                        MoveToSlainPile(_resolvingMonster);

                        if (_resolvingMonster.Value == 2)
                        {
                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=6;freq=1000;atk=0.01;sus=0.05;dec=0.2;hpf=500;hpfsweep=-200;vol=0.15|wave=4;freq=300;slide=-100;atk=0.01;sus=0.02;dec=0.15;detune=0.04;vol=0.1", 0.2f);
                            MoveToDiscard(_weaponSlot, false);
                            _weaponSlot = null;
                            foreach (var c in _slainPile) MoveToDiscard(c, false);
                            _slainPile.Clear();
                        }
                    }
                    else
                    {
                        MoveToDiscard(_resolvingMonster);
                    }

                    _state = ScoundrelState.Playing;
                    OnCardResolved();
                }
            }
            else if (_state == ScoundrelState.GameOver)
            {
                if (_scoreAnimTimer < 3.0f)
                {
                    _scoreAnimTimer += dt;
                    float p = Math.Clamp(_scoreAnimTimer / 3.0f, 0f, 1f);
                    float ease = Easing.EaseOutQuint(p);

                    if (_health <= 0)
                    {
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().MusicPitchOffset = MathHelper.Lerp(0f, -1f, ease);
                    }

                    int newScore = (int)MathF.Round(MathHelper.Lerp(-208, _targetScore, ease));

                    if (newScore != _displayScore)
                    {
                        _displayScore = newScore;
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx($"proc:wave=2;freq={400 + p * 800};atk=0.01;sus=0;dec=0.05;detune=0.01;vol=0.12", 0.15f);
                    }

                    if (p >= 1f && !_scoreSlamPlayed)
                    {
                        _displayScore = _targetScore;
                        ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=4;freq=100;slide=-50;atk=0.01;sus=0.1;dec=0.4;detune=0.03;lpf=800;vol=0.3|wave=5;freq=200;atk=0.01;sus=0.1;dec=0.3;lpf=500;vol=0.25", 0.15f);
                        _scoreSlamPlayed = true;
                    }
                }

                _exitButton.Update(mouseState);
                if (justClicked && _exitButton.Bounds.Contains(mousePos))
                {
                    _inputManager.ConsumeMouseClick();
                }
            }

            _previousMouseState = mouseState;
        }

        private void StartMonsterResolution(Card monster, int damage, bool weaponUsed)
        {
            _state = ScoundrelState.ResolvingMonster;
            _resolvingMonster = monster;
            _resolveTimer = 0f;
            _resolveDamage = damage;
            _resolveDamageApplied = false;
            _resolveWeaponUsed = weaponUsed;

            float minRot = 12f * (MathF.PI / 180f);
            float maxRot = 30f * (MathF.PI / 180f);
            _resolveTargetRotation = (minRot + (float)_random.NextDouble() * (maxRot - minRot)) * (_random.Next(2) == 0 ? 1 : -1);

            if (_focusedCard != null)
            {
                _focusedCard.ZIndex = 100 + _focusedCard.RoomSlotIndex;
                _focusedCard = null;
            }
        }

        private void ResolveCardClick(Card card)
        {
            if (card.Type == CardType.Potion)
            {
                int healAmount = _potionsUsedThisRoom == 0 ? card.Value : 0;
                ApplyHeal(healAmount);
                _potionsUsedThisRoom++;
                MoveToDiscard(card);
                OnCardResolved();
            }
            else if (card.Type == CardType.Weapon)
            {
                EquipWeapon(card);
                OnCardResolved();
            }
            else if (card.Type == CardType.Monster)
            {
                if (_weaponSlot == null || card.Value >= _lastSlainValue)
                {
                    StartMonsterResolution(card, card.Value, false);
                }
                else
                {
                    _focusedCard = card;
                    _focusedCard.TargetPosition = _roomPositions[_focusedCard.RoomSlotIndex] + new Vector2(0, -3);
                    _focusedCard.ZIndex = 500;
                    _state = ScoundrelState.Focused;
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_click");
                }
            }
        }

        private void OnSkipClicked()
        {
            _canSkip = false;
            var skippedCards = _room.ToList();
            _room.Clear();

            foreach (var card in skippedCards)
            {
                card.IsFaceUp = false;
                card.RoomSlotIndex = -1;
                card.IsHovered = false;
                _deck.Insert(0, card);
            }

            for (int i = 0; i < _deck.Count; i++)
            {
                _deck[i].TargetPosition = _deckPos + new Vector2(0, -i * 0.25f);
                _deck[i].ZIndex = i;
            }

            _state = ScoundrelState.Dealing;
            _dealTimer = DEAL_INTERVAL;

            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=3;freq=1000;slide=-500;atk=0.02;sus=0.05;dec=0.15;vol=0.12|wave=4;freq=400;slide=-100;atk=0.02;sus=0.05;dec=0.15;vol=0.15", 0.2f);
        }

        private void OnFistsClicked()
        {
            StartMonsterResolution(_focusedCard, _focusedCard.Value, false);
        }

        private void OnWeaponClicked()
        {
            if (_focusedCard.Value >= _lastSlainValue)
            {
                _hapticsManager.TriggerShake(5f, 0.2f);
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=4;freq=150;atk=0.01;sus=0.1;dec=0.1;detune=0.03;vol=0.15|wave=0;freq=150;atk=0.01;sus=0.1;dec=0.1;duty=0.2;vol=0.1", 0.2f);
                return;
            }

            int damage = Math.Max(0, _focusedCard.Value - _weaponSlot.Value);
            StartMonsterResolution(_focusedCard, damage, true);
        }

        private void CancelFocus()
        {
            _focusedCard.TargetPosition = _roomPositions[_focusedCard.RoomSlotIndex];
            _focusedCard.ZIndex = 100 + _focusedCard.RoomSlotIndex;
            _focusedCard = null;
            _state = ScoundrelState.Playing;
            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_hover");
        }

        private void OnCardResolved()
        {
            _cardsResolvedThisRoom++;
            _canSkip = true;

            if (_cardsResolvedThisRoom >= 3 && _deck.Count > 0)
            {
                _state = ScoundrelState.Dealing;
                _dealTimer = DEAL_INTERVAL;
            }
        }

        private void ApplyDamage(int amount)
        {
            _health -= amount;
            _healthPlink.Start(0f, 0.3f);
            _hapticsManager.TriggerShake(amount * 1f, 0.1f);

            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=1;freq=200;slide=-100;atk=0.01;sus=0.1;dec=0.2;dist=0.5;vol=0.15", 0.2f);

            _floatingTexts.Add(new FloatingText { Number = amount, IsHealing = false, Timer = 1.0f, LocalOffset = new Vector2(20, 150) });
        }

        private async void PlayHealFull()
        {
            var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();
            audio.PlayRoutedSfx("proc:wave=2;freq=400;atk=0.02;sus=0.05;dec=0.15;detune=0.01;delay=0.05;delfb=0.15;vol=0.15", 0.15f);
            await Task.Delay(100);
            audio.PlayRoutedSfx("proc:wave=2;freq=600;atk=0.02;sus=0.05;dec=0.15;detune=0.01;delay=0.05;delfb=0.15;vol=0.15", 0.15f);
            await Task.Delay(200);
            audio.PlayRoutedSfx("proc:wave=2;freq=800;atk=0.02;sus=0.05;dec=0.25;detune=0.01;delay=0.05;delfb=0.15;vol=0.15", 0.15f);
        }

        private async void PlayHealPartial()
        {
            var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();
            audio.PlayRoutedSfx("proc:wave=2;freq=400;atk=0.02;sus=0.05;dec=0.15;detune=0.01;vol=0.15", 0.15f);
            await Task.Delay(100);
            audio.PlayRoutedSfx("proc:wave=2;freq=600;atk=0.02;sus=0.05;dec=0.2;detune=0.01;vol=0.15", 0.15f);
        }

        private async void PlayDefeatSequence()
        {
            var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();
            audio.PlayRoutedSfx("proc:wave=4;freq=300;atk=0.05;sus=0.1;dec=0.3;detune=0.02;lpf=2000;vol=0.2", 0.15f);
            await Task.Delay(300);
            audio.PlayRoutedSfx("proc:wave=4;freq=250;atk=0.05;sus=0.1;dec=0.3;detune=0.02;lpf=2000;vol=0.2", 0.15f);
            await Task.Delay(600);
            audio.PlayRoutedSfx("proc:wave=4;freq=200;atk=0.05;sus=0.2;dec=0.6;detune=0.02;lpf=2000;vol=0.2", 0.15f);
        }

        private async void PlayVictorySequence()
        {
            var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();
            audio.PlayRoutedSfx("proc:wave=0;freq=400;atk=0.02;sus=0.1;dec=0.2;detune=0.01;lpf=3000;vol=0.15", 0.15f);
            await Task.Delay(200);
            audio.PlayRoutedSfx("proc:wave=0;freq=500;atk=0.02;sus=0.1;dec=0.2;detune=0.01;lpf=3000;vol=0.15", 0.15f);
            await Task.Delay(400);
            audio.PlayRoutedSfx("proc:wave=0;freq=600;atk=0.02;sus=0.1;dec=0.2;detune=0.01;lpf=3000;vol=0.15", 0.15f);
            await Task.Delay(600);
            audio.PlayRoutedSfx("proc:wave=0;freq=800;atk=0.02;sus=0.2;dec=0.6;detune=0.01;lpf=3000;vol=0.15", 0.15f);
        }

        private void ApplyHeal(int amount)
        {
            int actualHeal = Math.Min(amount, 20 - _health);
            _health += actualHeal;
            _healthPlink.Start(0f, 0.3f);

            if (actualHeal == 0)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=2;freq=300;slide=-50;atk=0.02;sus=0.05;dec=0.15;detune=0.01;vol=0.15", 0.15f);
            }
            else if (_health == 20)
            {
                PlayHealFull();
            }
            else
            {
                PlayHealPartial();
            }

            if (actualHeal > 0)
            {
                _floatingTexts.Add(new FloatingText { Number = actualHeal, IsHealing = true, Timer = 1.0f, LocalOffset = new Vector2(20, 150) });
            }
        }

        private void EquipWeapon(Card weapon)
        {
            if (_weaponSlot != null)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=4;freq=200;slide=-100;atk=0.01;sus=0.05;dec=0.15;detune=0.03;lpf=1000;vol=0.2", 0.2f);
                MoveToDiscard(_weaponSlot, false);
                foreach (var c in _slainPile) MoveToDiscard(c, false);
                _slainPile.Clear();
            }

            _room.Remove(weapon);
            _weaponSlot = weapon;
            _weaponSlot.RoomSlotIndex = -1;
            _weaponSlot.IsHovered = false;
            _weaponSlot.TargetPosition = _weaponPos;
            _weaponSlot.ZIndex = 200;
            _lastSlainValue = 99;

            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=6;freq=1500;atk=0.01;sus=0.02;dec=0.1;hpf=800;vol=0.1|wave=2;freq=800;slide=400;atk=0.01;sus=0.05;dec=0.1;detune=0.02;vol=0.1", 0.2f);
        }

        private void MoveToDiscard(Card card, bool playSound = true)
        {
            _room.Remove(card);
            _discard.Add(card);
            card.RoomSlotIndex = -1;
            card.IsHovered = false;
            card.TargetPosition = _discardPos + new Vector2(0, -_discard.Count * 0.25f);
            card.TargetScale = Vector2.One;
            card.TargetRotation = 0f;
            card.ZIndex = 50 + _discard.Count;

            if (playSound)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=3;freq=1200;slide=-800;atk=0.01;sus=0.01;dec=0.05;vol=0.08|wave=4;freq=600;slide=-200;atk=0.01;sus=0.02;dec=0.05;vol=0.12", 0.15f);
            }
        }

        private void MoveToSlainPile(Card card)
        {
            _room.Remove(card);
            _slainPile.Add(card);
            card.RoomSlotIndex = -1;
            card.IsHovered = false;
            card.TargetRotation = MathHelper.PiOver2;
            card.TargetScale = Vector2.One;
            card.ZIndex = 150 + _slainPile.Count;
        }

        private void CheckGameOver()
        {
            if (_health <= 0)
            {
                _state = ScoundrelState.GameOver;
                PlayDefeatSequence();
                CalculateTargetScore();
            }
            else if (_deck.Count == 0 && !_room.Any(c => c.Type == CardType.Monster))
            {
                _state = ScoundrelState.GameOver;
                PlayVictorySequence();
                CalculateTargetScore();
            }
        }

        private void CalculateTargetScore()
        {
            _targetScore = _health;
            if (_health <= 0)
            {
                int remainingMonsters = _deck.Concat(_room).Where(c => c.Type == CardType.Monster).Sum(c => c.Value);
                _targetScore = _health - remainingMonsters;
            }
            else if (_health == 20)
            {
                var bestPotion = _room.Where(c => c.Type == CardType.Potion).OrderByDescending(c => c.Value).FirstOrDefault();
                if (bestPotion != null) _targetScore += bestPotion.Value;
            }
            _displayScore = -208;
            _scoreAnimTimer = 0f;
            _scoreSlamPlayed = false;
        }

        private void DrawHoverText(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 pos, Color color)
        {
            Vector2 size = font.MeasureString(text);
            Vector2 startX = new Vector2(MathF.Round(pos.X - size.X / 2f), MathF.Round(pos.Y - size.Y / 2f));
            spriteBatch.DrawStringOutlinedSnapped(font, text, startX, color, _global.Palette_Off);
        }

        private void DrawMonsterDamageText(SpriteBatch spriteBatch, BitmapFont defFont, BitmapFont tertFont, Card monsterCard, bool showWeaponDamage)
        {
            Vector2 hoverPos = monsterCard.Position + new Vector2(0, -32);
            string dmgText = $"-{monsterCard.Value}";
            Vector2 dmgSize = defFont.MeasureString(dmgText);

            if (showWeaponDamage && _weaponSlot != null)
            {
                int wDmg = Math.Max(0, monsterCard.Value - _weaponSlot.Value);
                string wText = $"(-{wDmg})";
                Vector2 wSize = tertFont.MeasureString(wText);

                float totalW = dmgSize.X + 2 + wSize.X;
                Vector2 startX = new Vector2(MathF.Round(hoverPos.X - totalW / 2f), MathF.Round(hoverPos.Y - dmgSize.Y / 2f));

                spriteBatch.DrawStringOutlinedSnapped(defFont, dmgText, startX, _global.Palette_Rust, _global.Palette_Off);

                Color wColor = wDmg == 0 ? _global.Palette_DarkSun : _global.Palette_Rust;
                Vector2 wPos = new Vector2(startX.X + dmgSize.X + 2, startX.Y + (dmgSize.Y - wSize.Y) / 2f);
                spriteBatch.DrawStringOutlinedSnapped(tertFont, wText, wPos, wColor, _global.Palette_Off);
            }
            else
            {
                Vector2 startX = new Vector2(MathF.Round(hoverPos.X - dmgSize.X / 2f), MathF.Round(hoverPos.Y - dmgSize.Y / 2f));
                spriteBatch.DrawStringOutlinedSnapped(defFont, dmgText, startX, _global.Palette_Rust, _global.Palette_Off);
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var secFont = _core.SecondaryFont;
            var defFont = _core.DefaultFont;
            var tertFont = _core.TertiaryFont;

            bool canSkipNow = _room.Count == 4 && _cardsResolvedThisRoom == 0 && _canSkip;

            void DrawSkipButton(bool isEnabled)
            {
                float hopY = _skipButton.HoverAnimator.CurrentOffset;
                float shakeX = 0f;
                if (_skipShakeTimer > 0)
                {
                    float progress = _skipShakeTimer / 0.6f;
                    shakeX = MathF.Sin(_skipShakeTimer * 50f) * 5f * progress;
                }

                Rectangle b = _skipButton.Bounds;
                b.Y += (int)hopY;
                b.X += (int)MathF.Round(shakeX);

                Color bgColor = isEnabled ? (_skipButton.IsHovered ? _global.Palette_Sun : _global.Palette_DarkSun) : _global.Palette_DarkGray;
                Color textColor = isEnabled ? _global.Palette_Off : _global.Palette_Gray;

                spriteBatch.Draw(_pixel, new Rectangle(b.X + 1, b.Y, b.Width - 2, b.Height), bgColor);
                spriteBatch.Draw(_pixel, new Rectangle(b.X, b.Y + 1, 1, b.Height - 2), bgColor);
                spriteBatch.Draw(_pixel, new Rectangle(b.Right - 1, b.Y + 1, 1, b.Height - 2), bgColor);

                Vector2 tSize = defFont.MeasureString("SKIP ROOM");
                Vector2 tPos = new Vector2(MathF.Round(b.Center.X - tSize.X / 2f), MathF.Round(b.Center.Y - tSize.Y / 2f));
                spriteBatch.DrawStringSnapped(defFont, "SKIP ROOM", tPos, textColor);
            }

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

            if (_weaponSlot != null && _weaponSlot.IsBeingReplaced)
            {
                Rectangle outlineSource = _spriteManager.ScoundrelCardRects[1, 0];
                Vector2 origin = new Vector2(18f, 25f);
                Vector2 drawPos = new Vector2(MathF.Round(_weaponPos.X), MathF.Round(_weaponPos.Y));
                spriteBatch.DrawSnapped(_spriteManager.ScoundrelCardsSpriteSheet, drawPos, outlineSource, Color.White * 0.5f, 0f, origin, 1f, SpriteEffects.None, 0f);
            }

            var allCards = new List<Card>();
            allCards.AddRange(_deck);
            allCards.AddRange(_discard);
            allCards.AddRange(_slainPile);
            if (_weaponSlot != null) allCards.Add(_weaponSlot);
            allCards.AddRange(_room);
            if (_state == ScoundrelState.Focused) allCards.Add(_fistCard);

            var unselectable = allCards.Where(c => !c.IsSelectable && !c.ForceRenderAboveVeil).OrderBy(c => c.ZIndex).ToList();
            var selectable = allCards.Where(c => c.IsSelectable || c.ForceRenderAboveVeil).OrderBy(c => c.ZIndex).ToList();

            if (_state == ScoundrelState.Focused)
            {
                foreach (var card in unselectable) card.Draw(spriteBatch, _spriteManager);
            }

            if (_state == ScoundrelState.Playing && !canSkipNow)
            {
                DrawSkipButton(false);
            }

            if (_state == ScoundrelState.Focused || (_state == ScoundrelState.Playing && !canSkipNow))
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off * 0.4f);
            }

            if (_state != ScoundrelState.Focused)
            {
                foreach (var card in unselectable) card.Draw(spriteBatch, _spriteManager);
            }

            if (_weaponSlot != null && _weaponSlot.IsBeingReplaced)
            {
                var xIcon = _spriteManager.ShopXIcon;
                if (xIcon != null)
                {
                    Vector2 xPos = new Vector2(MathF.Round(_weaponSlot.Position.X), MathF.Round(_weaponSlot.Position.Y));
                    if (_weaponSlot.IsHovered && !_weaponSlot.IsFocused) xPos.Y -= 1f;
                    Vector2 xOrigin = new Vector2(xIcon.Width / 2f, xIcon.Height / 2f);
                    spriteBatch.DrawSnapped(xIcon, xPos, null, _global.Palette_Rust, _weaponSlot.Rotation, xOrigin, _weaponSlot.Scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off * 0.4f);

            foreach (var card in selectable) card.Draw(spriteBatch, _spriteManager);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, null, null, null, transform);
            foreach (var card in allCards) card.DrawFlash(spriteBatch, _spriteManager);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            // Draw Deck/Discard Counters
            if (_deck.Count > 0)
            {
                string deckText = _deck.Count.ToString();
                Vector2 deckSize = secFont.MeasureString(deckText);
                spriteBatch.DrawStringOutlinedSnapped(secFont, deckText, _deckPos + new Vector2(-deckSize.X / 2f, 32), _global.Palette_DarkestPale, _global.Palette_Off);
            }

            if (_discard.Count > 0)
            {
                string discardText = _discard.Count.ToString();
                Vector2 discardSize = secFont.MeasureString(discardText);
                spriteBatch.DrawStringOutlinedSnapped(secFont, discardText, _discardPos + new Vector2(-discardSize.X / 2f, 32), _global.Palette_DarkestPale, _global.Palette_Off);
            }

            // Draw Hover Indicators
            int previewHealth = _health;
            var hoveredCard = allCards.FirstOrDefault(c => c.IsHovered && c.IsSelectable);
            if (hoveredCard != null && hoveredCard.IsFaceUp)
            {
                if (_state == ScoundrelState.Focused)
                {
                    if (hoveredCard == _weaponSlot)
                    {
                        int wDmg = Math.Max(0, _focusedCard.Value - _weaponSlot.Value);
                        string wText = $"-{wDmg}";
                        Color wColor = wDmg == 0 ? _global.Palette_DarkSun : _global.Palette_Rust;
                        DrawHoverText(spriteBatch, defFont, wText, hoveredCard.Position + new Vector2(0, -32), wColor);
                        previewHealth = _health - wDmg;
                    }
                    else if (hoveredCard == _fistCard)
                    {
                        string fText = $"-{_focusedCard.Value}";
                        DrawHoverText(spriteBatch, defFont, fText, hoveredCard.Position + new Vector2(0, -32), _global.Palette_Rust);
                        previewHealth = _health - _focusedCard.Value;
                    }
                }
                else
                {
                    if (hoveredCard.Type == CardType.Monster)
                    {
                        bool canUseWeapon = _weaponSlot != null && hoveredCard.Value < _lastSlainValue;
                        DrawMonsterDamageText(spriteBatch, defFont, tertFont, hoveredCard, canUseWeapon);

                        if (canUseWeapon)
                        {
                            bool showFistDamage = (_previewFlashTimer % 1.2f) < 0.6f;
                            if (showFistDamage)
                            {
                                previewHealth = _health - hoveredCard.Value;
                            }
                            else
                            {
                                int wDmg = Math.Max(0, hoveredCard.Value - _weaponSlot.Value);
                                previewHealth = _health - wDmg;
                            }
                        }
                        else
                        {
                            previewHealth = _health - hoveredCard.Value;
                        }
                    }
                    else if (hoveredCard.Type == CardType.Potion)
                    {
                        int baseHeal = _potionsUsedThisRoom == 0 ? hoveredCard.Value : 0;
                        int actualHeal = Math.Min(baseHeal, 20 - _health);
                        string healText = $"+{actualHeal}";
                        Color hColor = actualHeal == 0 ? _global.Palette_DarkSun : _global.Palette_Leaf;
                        DrawHoverText(spriteBatch, defFont, healText, hoveredCard.Position + new Vector2(0, -32), hColor);
                        previewHealth = Math.Min(20, _health + baseHeal);
                    }
                    else if (hoveredCard.Type == CardType.Weapon)
                    {
                        DrawHoverText(spriteBatch, defFont, "EQUIP", hoveredCard.Position + new Vector2(0, -32), _global.Palette_DarkSun);
                    }
                }
            }

            if (_state == ScoundrelState.Focused)
            {
                Vector2 fSize = secFont.MeasureString("FIST");
                Vector2 fPos = _fistCard.Position + (_fistCard.IsHovered ? new Vector2(0, -1) : Vector2.Zero);
                spriteBatch.DrawStringOutlinedSnapped(secFont, "FIST", fPos - fSize / 2f, _global.Palette_Sun, _global.Palette_Off);
            }

            var heartSheet = _spriteManager.HealthHearts7x6SpriteSheet;

            if (heartSheet != null)
            {
                int maxHearts = 10;
                int heartWidth = 7;
                int heartHeight = 6;
                int spacing = 1;
                int totalWidth = maxHearts * heartWidth + (maxHearts - 1) * spacing;

                Vector2 barCenter = new Vector2(Global.VIRTUAL_WIDTH - 10 - totalWidth / 2f, 15f);
                float hpScale = _healthPlink.IsActive ? _healthPlink.Scale : 1f;

                for (int i = 0; i < maxHearts; i++)
                {
                    int currentHeartVal = Math.Clamp(_health - i * 2, 0, 2);
                    int previewHeartVal = Math.Clamp(previewHealth - i * 2, 0, 2);

                    int frameIndex = 2;
                    if (currentHeartVal == 2) frameIndex = 0;
                    else if (currentHeartVal == 1) frameIndex = 1;

                    if (_heartFlashTimers[i] > 0)
                    {
                        bool isFlashFrame = (_heartFlashTimers[i] % HEART_FLASH_BLINK_INTERVAL) > HEART_FLASH_BLINK_HALF;
                        if (isFlashFrame) frameIndex = _heartFlashFrames[i];
                    }
                    else if (currentHeartVal != previewHeartVal)
                    {
                        if ((currentHeartVal == 2 && previewHeartVal == 0) || (currentHeartVal == 0 && previewHeartVal == 2)) frameIndex = 3;
                        else if ((currentHeartVal == 2 && previewHeartVal == 1) || (currentHeartVal == 1 && previewHeartVal == 2)) frameIndex = 4;
                        else if ((currentHeartVal == 1 && previewHeartVal == 0) || (currentHeartVal == 0 && previewHeartVal == 1)) frameIndex = 5;
                        else frameIndex = 3;
                    }

                    Rectangle sourceRect = new Rectangle(frameIndex * heartWidth, 0, heartWidth, heartHeight);

                    Vector2 offset = new Vector2(i * (heartWidth + spacing) + (heartWidth / 2f), heartHeight / 2f) - new Vector2(totalWidth / 2f, heartHeight / 2f);
                    Vector2 finalPos = barCenter + offset * hpScale;
                    Vector2 origin = new Vector2(heartWidth / 2f, heartHeight / 2f);

                    spriteBatch.DrawSnapped(heartSheet, finalPos + new Vector2(-1, 0), sourceRect, _global.Palette_Off, 0f, origin, hpScale, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(heartSheet, finalPos + new Vector2(1, 0), sourceRect, _global.Palette_Off, 0f, origin, hpScale, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(heartSheet, finalPos + new Vector2(0, -1), sourceRect, _global.Palette_Off, 0f, origin, hpScale, SpriteEffects.None, 0f);
                    spriteBatch.DrawSnapped(heartSheet, finalPos + new Vector2(0, 1), sourceRect, _global.Palette_Off, 0f, origin, hpScale, SpriteEffects.None, 0f);

                    spriteBatch.DrawSnapped(heartSheet, finalPos, sourceRect, Color.White, 0f, origin, hpScale, SpriteEffects.None, 0f);
                }

                string hpLabel = "HP ";
                string currentHpText = _health.ToString();
                string maxHpText = "/20";

                Color valColor;
                if (_health >= 14) valColor = _global.Palette_Leaf;
                else if (_health >= 7) valColor = _global.Palette_Fruit;
                else valColor = _global.Palette_Rust;

                Color currentHpTextColor = valColor;

                if (previewHealth != _health)
                {
                    currentHpText = previewHealth.ToString();
                    currentHpTextColor = _global.Palette_Sun;
                }

                Vector2 hpLabelSize = tertFont.MeasureString(hpLabel);
                Vector2 currentHpSize = defFont.MeasureString(currentHpText);
                Vector2 maxHpSize = tertFont.MeasureString(maxHpText);

                float totalTextWidth = hpLabelSize.X + currentHpSize.X + maxHpSize.X;
                float textStartX = MathF.Round(barCenter.X - totalTextWidth / 2f);
                float textY = MathF.Round(barCenter.Y + heartHeight / 2f + 4f);

                float baselineY = MathF.Round(textY + currentHpSize.Y);

                Vector2 pos1 = new Vector2(MathF.Round(textStartX), MathF.Round(baselineY - hpLabelSize.Y));
                Vector2 pos2 = new Vector2(MathF.Round(textStartX + hpLabelSize.X), MathF.Round(textY) + 1);
                Vector2 pos3 = new Vector2(MathF.Round(textStartX + hpLabelSize.X + currentHpSize.X), MathF.Round(baselineY - maxHpSize.Y));

                spriteBatch.DrawStringOutlinedSnapped(tertFont, hpLabel, pos1, _global.Palette_DarkestPale, _global.Palette_Off);
                spriteBatch.DrawStringOutlinedSnapped(defFont, currentHpText, pos2, currentHpTextColor, _global.Palette_Off);
                spriteBatch.DrawStringOutlinedSnapped(tertFont, maxHpText, pos3, valColor, _global.Palette_Off);
            }

            foreach (var ft in _floatingTexts)
            {
                Color c = ft.IsHealing ? _global.Palette_Leaf : _global.Palette_Rust;
                spriteBatch.DrawStringOutlinedSnapped(secFont, (ft.IsHealing ? "+" : "-") + ft.Number, ft.LocalOffset, c, _global.Palette_Off);
            }

            if (_state == ScoundrelState.Playing && canSkipNow)
            {
                DrawSkipButton(true);
            }

            if (_state == ScoundrelState.GameOver)
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black * 0.8f);

                string result = _health > 0 ? "VICTORY" : "DEFEAT";
                Color resColor = _health > 0 ? _global.Palette_Sun : _global.Palette_Rust;

                Vector2 rSize = _core.DefaultFont.MeasureString(result);
                spriteBatch.DrawStringOutlinedSnapped(_core.DefaultFont, result, new Vector2(Global.VIRTUAL_WIDTH / 2f - rSize.X / 2f, 60), resColor, _global.Palette_Off);

                string scoreText = $"SCORE: {_displayScore}";
                Vector2 sSize = secFont.MeasureString(scoreText);
                spriteBatch.DrawStringOutlinedSnapped(secFont, scoreText, new Vector2(Global.VIRTUAL_WIDTH / 2f - sSize.X / 2f, 90), _global.Palette_LightPale, _global.Palette_Off);

                _exitButton.Draw(spriteBatch, secFont, gameTime, transform);
            }
        }
    }
}