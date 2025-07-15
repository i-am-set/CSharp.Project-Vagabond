namespace ProjectVagabond
{
    public class WeaknessStatusEffect : StatusEffect
    {
        public WeaknessStatusEffect(string source)
        {
            Name = "Weakness";
            Source = source;
            TickFrequency = 0; // No ticking behavior
        }

        public override void OnApply(int targetId, ComponentStore componentStore)
        {
            // No immediate effect on apply
        }

        public override void OnTick(int targetId, ComponentStore componentStore)
        {
            // No ticking behavior
        }

        public override void OnRemove(int targetId, ComponentStore componentStore)
        {
            // No effect on removal
        }
    }
}