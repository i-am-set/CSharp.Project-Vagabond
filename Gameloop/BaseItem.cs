using ProjectVagabond.Battle;

namespace ProjectVagabond.Items
{
    public enum ItemType { None } // Relic removed

    public class BaseItem
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Flavor { get; set; }
        public ItemType Type { get; set; }
        public string SpritePath { get; set; }
        public object OriginalData { get; set; }
    }
}