using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class ScoundrelScene : GameScene
    {
        private enum ScoundrelState { Dealing, Playing, Focused, GameOver }

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

        private Button _skipButton;
        private Button _exitButton;

        private PlinkAnimator _healthPlink;
        private List<FloatingText> _floatingTexts = new List<FloatingText>();

        private Vector2 _deckPos = new Vector2(30, 40);
        private Vector2 _discardPos = new Vector2(290, 40);
        private Vector2 _weaponPos = new Vector2(180, 140);
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
            _skipButton = new Button(new Rectangle(Global.VIRTUAL_WIDTH / 2 - skipW / 2, 5, skipW, skipH), "SKIP ROOM", font: defFont);
            _skipButton.OnClick += OnSkipClicked;

            _exitButton = new Button(new Rectangle(Global.VIRTUAL_WIDTH / 2 - 30, 120, 60, 15), "MAIN MENU", font: secFont) { DrawBorderOnHover = true };
            _exitButton.OnClick += () => { _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.FadeOff, TransitionType.FadeOff); };

            _healthPlink = new PlinkAnimator { MaxScale = 1.5f, RestScale = 1.0f };

            _fistCard = new Card(CardSuit.None, CardType.Outline, 0, 0);
            _fistCard.IsFaceUp = true;
            _fistCard.Position = new Vector2(140, 140);
            _fistCard.TargetPosition = new Vector2(140, 140);
            _fistCard.ZIndex = 200;

            _uiInitialized = true;
        }

        public override void Enter()
        {
            base.Enter();
            InitializeUI();

            _health = 20;
            _lastSlainValue = 99;
            _cardsResolvedThisRoom = 0;
            _potionsUsedThisRoom = 0;
            _canSkip = true;
            _floatingTexts.Clear();

            _deck.Clear();
            _room.Clear();
            _discard.Clear();
            _slainPile.Clear();
            _weaponSlot = null;
            _focusedCard = null;

            GenerateDeck();
            _state = ScoundrelState.Dealing;
            _dealTimer = 0f;
            _previousMouseState = _inputManager.GetEffectiveMouseState();
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

            _healthPlink.Update(gameTime, new Vector2(20, 160));

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

            foreach (var c in allCards) c.IsSelectable = false;

            if (_state == ScoundrelState.Playing)
            {
                foreach (var c in _room) c.IsSelectable = true;
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

            foreach (var c in allCards)
            {
                if (c == newHovered && !c.IsHovered)
                {
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_hover");
                    _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                }
                c.IsHovered = (c == newHovered);
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
                    ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_hover");
                }
                else if (_room.Count == 4 || _deck.Count == 0)
                {
                    _state = ScoundrelState.Playing;
                    _cardsResolvedThisRoom = 0;
                    _potionsUsedThisRoom = 0;
                }
            }
            else if (_state == ScoundrelState.Playing)
            {
                if (_room.Count == 4 && _cardsResolvedThisRoom == 0 && _canSkip)
                {
                    _skipButton.Update(mouseState);
                    if (justClicked && _skipButton.Bounds.Contains(mousePos))
                    {
                        _inputManager.ConsumeMouseClick();
                    }
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
            else if (_state == ScoundrelState.GameOver)
            {
                _exitButton.Update(mouseState);
                if (justClicked && _exitButton.Bounds.Contains(mousePos))
                {
                    _inputManager.ConsumeMouseClick();
                }
            }

            _previousMouseState = mouseState;
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
                    ApplyDamage(card.Value);
                    MoveToDiscard(card);
                    OnCardResolved();
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
            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_confirm");
        }

        private void OnFistsClicked()
        {
            ApplyDamage(_focusedCard.Value);
            MoveToDiscard(_focusedCard);
            ClearFocusAndResolve();
        }

        private void OnWeaponClicked()
        {
            if (_focusedCard.Value >= _lastSlainValue)
            {
                _hapticsManager.TriggerShake(5f, 0.2f);
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_alert");
                return;
            }

            int damage = Math.Max(0, _focusedCard.Value - _weaponSlot.Value);
            if (damage > 0) ApplyDamage(damage);

            _lastSlainValue = _focusedCard.Value;
            MoveToSlainPile(_focusedCard);

            if (_focusedCard.Value == 2)
            {
                MoveToDiscard(_weaponSlot);
                _weaponSlot = null;
                foreach (var c in _slainPile) MoveToDiscard(c);
                _slainPile.Clear();
            }

            ClearFocusAndResolve();
        }

        private void ClearFocusAndResolve()
        {
            _focusedCard = null;
            _state = ScoundrelState.Playing;
            OnCardResolved();
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
            _hapticsManager.TriggerShake(amount * 2f, 0.2f);
            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_alert");

            _floatingTexts.Add(new FloatingText { Number = amount, IsHealing = false, Timer = 1.0f, LocalOffset = new Vector2(20, 150) });
        }

        private void ApplyHeal(int amount)
        {
            int actualHeal = Math.Min(amount, 20 - _health);
            _health += actualHeal;
            _healthPlink.Start(0f, 0.3f);
            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_confirm");

            if (actualHeal > 0)
            {
                _floatingTexts.Add(new FloatingText { Number = actualHeal, IsHealing = true, Timer = 1.0f, LocalOffset = new Vector2(20, 150) });
            }
        }

        private void EquipWeapon(Card weapon)
        {
            if (_weaponSlot != null)
            {
                MoveToDiscard(_weaponSlot);
                foreach (var c in _slainPile) MoveToDiscard(c);
                _slainPile.Clear();
            }

            _room.Remove(weapon);
            _weaponSlot = weapon;
            _weaponSlot.RoomSlotIndex = -1;
            _weaponSlot.IsHovered = false;
            _weaponSlot.TargetPosition = _weaponPos;
            _weaponSlot.ZIndex = 200;
            _lastSlainValue = 99;
            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_confirm");
        }

        private void MoveToDiscard(Card card)
        {
            _room.Remove(card);
            _discard.Add(card);
            card.RoomSlotIndex = -1;
            card.IsHovered = false;
            card.TargetPosition = _discardPos + new Vector2(0, -_discard.Count * 0.25f);
            card.TargetScale = Vector2.One;
            card.TargetRotation = 0f;
            card.ZIndex = 50 + _discard.Count;
        }

        private void MoveToSlainPile(Card card)
        {
            _room.Remove(card);
            _slainPile.Add(card);
            card.RoomSlotIndex = -1;
            card.IsHovered = false;
            card.TargetPosition = _weaponPos + new Vector2(10 + _slainPile.Count * 5, 0);
            card.TargetRotation = MathHelper.PiOver2;
            card.TargetScale = Vector2.One;
            card.ZIndex = 150 + _slainPile.Count;
        }

        private void CheckGameOver()
        {
            if (_health <= 0)
            {
                _state = ScoundrelState.GameOver;
            }
            else if (_deck.Count == 0 && !_room.Any(c => c.Type == CardType.Monster))
            {
                _state = ScoundrelState.GameOver;
            }
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
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

            var allCards = new List<Card>();
            allCards.AddRange(_deck);
            allCards.AddRange(_discard);
            allCards.AddRange(_slainPile);
            if (_weaponSlot != null) allCards.Add(_weaponSlot);
            allCards.AddRange(_room);
            if (_state == ScoundrelState.Focused) allCards.Add(_fistCard);

            var unselectable = allCards.Where(c => !c.IsSelectable).OrderBy(c => c.ZIndex);
            var selectable = allCards.Where(c => c.IsSelectable).OrderBy(c => c.ZIndex);

            foreach (var card in unselectable) card.Draw(spriteBatch, _spriteManager);

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Off * 0.25f);

            foreach (var card in selectable) card.Draw(spriteBatch, _spriteManager);

            var secFont = _core.SecondaryFont;
            var defFont = _core.DefaultFont;
            var tertFont = _core.TertiaryFont;

            // Draw Deck/Discard Counters
            string deckText = _deck.Count.ToString();
            Vector2 deckSize = secFont.MeasureString(deckText);
            spriteBatch.DrawStringOutlinedSnapped(secFont, deckText, _deckPos + new Vector2(-deckSize.X / 2f, 32), _global.Palette_DarkestPale, _global.Palette_Off);

            string discardText = _discard.Count.ToString();
            Vector2 discardSize = secFont.MeasureString(discardText);
            spriteBatch.DrawStringOutlinedSnapped(secFont, discardText, _discardPos + new Vector2(-discardSize.X / 2f, 32), _global.Palette_DarkestPale, _global.Palette_Off);

            // Draw Hover Indicators
            if (_state == ScoundrelState.Focused && _focusedCard != null)
            {
                DrawMonsterDamageText(spriteBatch, defFont, tertFont, _focusedCard, true);
            }

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
                    }
                    else if (hoveredCard == _fistCard)
                    {
                        string fText = $"-{_focusedCard.Value}";
                        DrawHoverText(spriteBatch, defFont, fText, hoveredCard.Position + new Vector2(0, -32), _global.Palette_Rust);
                    }
                }
                else
                {
                    if (hoveredCard.Type == CardType.Monster)
                    {
                        DrawMonsterDamageText(spriteBatch, defFont, tertFont, hoveredCard, _weaponSlot != null && hoveredCard.Value < _lastSlainValue);
                    }
                    else if (hoveredCard.Type == CardType.Potion)
                    {
                        int baseHeal = _potionsUsedThisRoom == 0 ? hoveredCard.Value : 0;
                        int actualHeal = Math.Min(baseHeal, 20 - _health);
                        string healText = $"+{actualHeal}";
                        Color hColor = actualHeal == 0 ? _global.Palette_DarkSun : _global.Palette_Leaf;
                        DrawHoverText(spriteBatch, defFont, healText, hoveredCard.Position + new Vector2(0, -32), hColor);
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

            float hpScale = _healthPlink.IsActive ? _healthPlink.Scale : 1f;
            Color hpColor = _health > 5 ? _global.Palette_Leaf : _global.Palette_Rust;
            spriteBatch.DrawStringOutlinedSnapped(secFont, $"HP: {_health}", new Vector2(20, 160), hpColor, _global.Palette_Off, 0f, Vector2.Zero, hpScale, SpriteEffects.None, 0f);

            foreach (var ft in _floatingTexts)
            {
                Color c = ft.IsHealing ? _global.Palette_Leaf : _global.Palette_Rust;
                spriteBatch.DrawStringOutlinedSnapped(secFont, (ft.IsHealing ? "+" : "-") + ft.Number, ft.LocalOffset, c, _global.Palette_Off);
            }

            if (_state == ScoundrelState.Playing && _room.Count == 4 && _cardsResolvedThisRoom == 0 && _canSkip)
            {
                float hopY = _skipButton.HoverAnimator.CurrentOffset;
                Rectangle b = _skipButton.Bounds;
                b.Y += (int)hopY;

                Color bgColor = _skipButton.IsHovered ? _global.Palette_Sun : _global.Palette_DarkPale;

                spriteBatch.Draw(_pixel, new Rectangle(b.X + 1, b.Y, b.Width - 2, b.Height), bgColor);
                spriteBatch.Draw(_pixel, new Rectangle(b.X, b.Y + 1, 1, b.Height - 2), bgColor);
                spriteBatch.Draw(_pixel, new Rectangle(b.Right - 1, b.Y + 1, 1, b.Height - 2), bgColor);

                Vector2 tSize = defFont.MeasureString("SKIP ROOM");
                Vector2 tPos = new Vector2(MathF.Round(b.Center.X - tSize.X / 2f), MathF.Round(b.Center.Y - tSize.Y / 2f));
                spriteBatch.DrawStringSnapped(defFont, "SKIP ROOM", tPos, _global.Palette_Off);
            }

            if (_state == ScoundrelState.GameOver)
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black * 0.8f);

                string result = _health > 0 ? "VICTORY" : "DEFEAT";
                Color resColor = _health > 0 ? _global.Palette_Sun : _global.Palette_Rust;

                int score = _health;
                if (_health <= 0)
                {
                    int remainingMonsters = _deck.Concat(_room).Where(c => c.Type == CardType.Monster).Sum(c => c.Value);
                    score = _health - remainingMonsters;
                }
                else if (_health == 20)
                {
                    var bestPotion = _room.Where(c => c.Type == CardType.Potion).OrderByDescending(c => c.Value).FirstOrDefault();
                    if (bestPotion != null) score += bestPotion.Value;
                }

                Vector2 rSize = _core.DefaultFont.MeasureString(result);
                spriteBatch.DrawStringOutlinedSnapped(_core.DefaultFont, result, new Vector2(Global.VIRTUAL_WIDTH / 2f - rSize.X / 2f, 60), resColor, _global.Palette_Off);

                string scoreText = $"SCORE: {score}";
                Vector2 sSize = secFont.MeasureString(scoreText);
                spriteBatch.DrawStringOutlinedSnapped(secFont, scoreText, new Vector2(Global.VIRTUAL_WIDTH / 2f - sSize.X / 2f, 90), _global.Palette_LightPale, _global.Palette_Off);

                _exitButton.Draw(spriteBatch, secFont, gameTime, transform);
            }
        }
    }
}