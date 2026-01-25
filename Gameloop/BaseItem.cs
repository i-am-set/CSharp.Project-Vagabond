using ProjectVagabond.Battle;

namespace ProjectVagabond.Items
{
    public enum ItemType { Relic } // Removed Weapon

    public class BaseItem
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Flavor { get; set; }
        public ItemType Type { get; set; }
        public string SpritePath { get; set; }
        public object OriginalData { get; set; }

        public static BaseItem FromRelic(RelicData data)
        {
            return new BaseItem
            {
                ID = data.RelicID,
                Name = data.RelicName,
                Description = data.Description,
                Flavor = data.Flavor,
                Type = ItemType.Relic,
                SpritePath = $"Sprites/Items/Relics/{data.RelicID}",
                OriginalData = data
            };
        }
    }
}