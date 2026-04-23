using Microsoft.Xna.Framework;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Scenes
{
    public class ScoundrelBoardController
    {
        public List<Card> Deck { get; } = new List<Card>();
        public List<Card> Room { get; } = new List<Card>();
        public List<Card> Discard { get; } = new List<Card>();
        public List<Card> SlainPile { get; } = new List<Card>();
        public List<Card> CardsToReturn { get; } = new List<Card>();

        public Card? WeaponSlot { get; set; }
        public Card? PocketSlot { get; set; }
        public Card? FocusedCard { get; set; }
        public Card FistCard { get; private set; }
        public Card SkipCard { get; private set; }

        public readonly Vector2 DeckPos = new Vector2(30, 40);
        public readonly Vector2 DiscardPos = new Vector2(30, 140);
        public readonly Vector2 WeaponPos = new Vector2(160, 140);
        public readonly Vector2 PocketPos = new Vector2(276, 160);
        public readonly Vector2[] RoomPositions = new Vector2[]
        {
            new Vector2(103, 80),
            new Vector2(141, 80),
            new Vector2(179, 80),
            new Vector2(217, 80)
        };

        private float _roomWaveTimer = 0f;
        private float _roomWaveInterval = 3f;
        private float _currentWaveTime = -1f;
        private Random _random = new Random();

        private readonly List<Card> _allCardsCache = new List<Card>(50);

        public ScoundrelBoardController()
        {
            FistCard = new Card(CardSuit.None, CardType.Outline, 0, 0)
            {
                IsFaceUp = true,
                Position = new Vector2(220, 140),
                TargetPosition = new Vector2(220, 140),
                ZIndex = 200
            };

            SkipCard = new Card(CardSuit.None, CardType.Outline, 0, 0)
            {
                IsFaceUp = true,
                ZIndex = 300
            };
        }

        public void Reset()
        {
            Deck.Clear();
            Room.Clear();
            Discard.Clear();
            SlainPile.Clear();
            CardsToReturn.Clear();
            WeaponSlot = null;
            PocketSlot = null;
            FocusedCard = null;
            SkipCard.Position = DeckPos;
            SkipCard.TargetPosition = DeckPos;

            _roomWaveTimer = 0f;
            _roomWaveInterval = 3f + (float)_random.NextDouble() * 2f;
            _currentWaveTime = -1f;
        }

        public void Update(float dt)
        {
            foreach (var card in Deck) card.Update(dt);
            foreach (var card in Room) card.Update(dt);
            foreach (var card in Discard) card.Update(dt);
            foreach (var card in SlainPile) card.Update(dt);
            foreach (var card in CardsToReturn) card.Update(dt);
            WeaponSlot?.Update(dt);
            PocketSlot?.Update(dt);
            FistCard.Update(dt);
            SkipCard.Update(dt);
        }

        public void UpdateWaves(float dt)
        {
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
        }

        public void ApplyWaveOffsets()
        {
            foreach (var c in Room)
            {
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

        public void EquipWeapon(Card weapon)
        {
            if (WeaponSlot != null)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=4;freq=200;slide=-100;atk=0.01;sus=0.05;dec=0.15;detune=0.03;lpf=1000;vol=0.2", 0.2f);

                for (int i = SlainPile.Count - 1; i >= 0; i--)
                {
                    MoveToDiscard(SlainPile[i], false);
                }
                SlainPile.Clear();

                MoveToDiscard(WeaponSlot, false);
                WeaponSlot = null;
            }

            Room.Remove(weapon);
            WeaponSlot = weapon;
            WeaponSlot.RoomSlotIndex = -1;
            WeaponSlot.IsHovered = false;
            WeaponSlot.TargetPosition = WeaponPos;
            WeaponSlot.ZIndex = 200;

            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=6;freq=1500;atk=0.01;sus=0.02;dec=0.1;hpf=800;vol=0.1|wave=2;freq=800;slide=400;atk=0.01;sus=0.05;dec=0.1;detune=0.02;vol=0.1", 0.2f);
        }

        public void MoveToDiscard(Card card, bool playSound = true)
        {
            Room.Remove(card);
            Discard.Add(card);
            card.RoomSlotIndex = -1;
            card.IsHovered = false;
            card.TargetPosition = DiscardPos + new Vector2(0, -Discard.Count * 0.5f);
            card.TargetScale = Vector2.One;
            card.TargetRotation = 0f;
            card.ZIndex = 50 + Discard.Count;

            if (playSound)
            {
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayRoutedSfx("proc:wave=3;freq=1200;slide=-800;atk=0.01;sus=0.01;dec=0.05;vol=0.08|wave=4;freq=600;slide=-200;atk=0.01;sus=0.02;dec=0.05;vol=0.12", 0.15f);
            }
        }

        public void MoveToSlainPile(Card card, GameMode mode)
        {
            Room.Remove(card);
            SlainPile.Add(card);
            card.RoomSlotIndex = -1;
            card.IsHovered = false;
            card.TargetRotation = MathHelper.PiOver2;
            card.TargetScale = Vector2.One;
            card.ZIndex = 150 + SlainPile.Count;
        }

        public List<Card> GetAllCards(bool includeFist, bool includeSkip)
        {
            _allCardsCache.Clear();
            _allCardsCache.AddRange(Deck);
            _allCardsCache.AddRange(Discard);
            _allCardsCache.AddRange(SlainPile);
            if (WeaponSlot != null) _allCardsCache.Add(WeaponSlot);
            if (PocketSlot != null) _allCardsCache.Add(PocketSlot);
            _allCardsCache.AddRange(Room);
            if (includeFist) _allCardsCache.Add(FistCard);
            if (includeSkip) _allCardsCache.Add(SkipCard);
            _allCardsCache.AddRange(CardsToReturn);
            return _allCardsCache;
        }
    }
}