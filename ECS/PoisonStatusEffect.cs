namespace ProjectVagabond
{
    public class PoisonStatusEffect : StatusEffect
    {
        public PoisonStatusEffect(string source)
        {
            Name = "Poison";
            Source = source;
            TickFrequency = 3.0f;
        }

        public override void OnApply(int targetId, ComponentStore componentStore)
        {
            var targetName = EntityNamer.GetName(targetId);
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{targetName} is poisoned!" });
        }

        public override void OnTick(int targetId, ComponentStore componentStore)
        {
            var healthComp = componentStore.GetComponent<HealthComponent>(targetId);
            if (healthComp != null)
            {
                healthComp.TakeDamage(1);
                var targetName = EntityNamer.GetName(targetId);
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{targetName} takes [green]1[/] damage from poison." });
            }
        }

        public override void OnRemove(int targetId, ComponentStore componentStore)
        {
            var targetName = EntityNamer.GetName(targetId);
            EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{targetName} is no longer poisoned." });
        }
    }
}