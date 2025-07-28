namespace ProjectVagabond
{
    public class PoisonStatusEffect : StatusEffect
    {
        public PoisonStatusEffect(string source)
        {
            Name = "Poison";
            Source = source;
        }

        public override void OnApply(int targetId, ComponentStore componentStore, int amount)
        {
            var targetName = EntityNamer.GetName(targetId);
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{targetName} is poisoned for {amount} turns!" });
        }

        public override void OnTick(int targetId, ComponentStore componentStore, int amount)
        {
            var healthComp = componentStore.GetComponent<HealthComponent>(targetId);
            if (healthComp != null)
            {
                // The 'amount' passed to OnTick is now the remaining duration of the effect.
                int poisonDamage = amount;
                healthComp.TakeDamage(poisonDamage);
                var targetName = EntityNamer.GetName(targetId);
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{targetName} takes [green]{poisonDamage}[/] damage from poison." });
            }
        }

        public override void OnRemove(int targetId, ComponentStore componentStore)
        {
            var targetName = EntityNamer.GetName(targetId);
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{targetName} is no longer poisoned." });
        }
    }
}