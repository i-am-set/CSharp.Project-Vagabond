using System.Collections.Generic;
using ProjectVagabond.Scenes;

namespace ProjectVagabond
{
    public class CardData
    {
        public CardSuit Suit { get; set; }
        public CardType Type { get; set; }
        public int Rank { get; set; }
        public int BaseValue { get; set; }
        public int Modifier { get; set; }
        public bool IsFaceUp { get; set; }
        public int RoomSlotIndex { get; set; }
    }

    public class ScoundrelSaveData
    {
        public GameMode Mode { get; set; }
        public int Floor { get; set; }
        public int MaxHealth { get; set; }
        public int Health { get; set; }
        public int Gold { get; set; }
        public float FloorTimer { get; set; }
        public int TotalCardsInFloor { get; set; }
        public int LastSlainValue { get; set; }
        public int CardsResolvedThisRoom { get; set; }
        public int PotionsUsedThisRoom { get; set; }
        public bool CanSkip { get; set; }
        public ScoundrelState State { get; set; }
        public int FocusedRoomSlotIndex { get; set; }
        public int ResolvingMonsterRoomSlotIndex { get; set; }
        public int ResolveDamage { get; set; }
        public bool ResolveWeaponUsed { get; set; }

        public Dictionary<string, int> CardModifiers { get; set; } = new Dictionary<string, int>();
        public List<CardData> ExtraCards { get; set; } = new List<CardData>();

        public List<CardData> Deck { get; set; } = new List<CardData>();
        public List<CardData> Room { get; set; } = new List<CardData>();
        public List<CardData> Discard { get; set; } = new List<CardData>();
        public List<CardData> SlainPile { get; set; } = new List<CardData>();
        public CardData WeaponSlot { get; set; }
        public CardData PocketSlot { get; set; }
    }
}