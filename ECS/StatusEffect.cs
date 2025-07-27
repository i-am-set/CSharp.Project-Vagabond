namespace ProjectVagabond
{
    public abstract class StatusEffect
    {
        public string Name { get; protected set; }
        public string Source { get; protected set; }
        public float TickFrequency { get; protected set; }

        public abstract void OnApply(int targetId, ComponentStore componentStore, int amount);
        public abstract void OnTick(int targetId, ComponentStore componentStore, int amount);
        public abstract void OnRemove(int targetId, ComponentStore componentStore);
    }
}