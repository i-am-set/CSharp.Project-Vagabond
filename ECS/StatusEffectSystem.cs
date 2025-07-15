using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class StatusEffectSystem
    {
        private readonly ComponentStore _componentStore;

        public StatusEffectSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        public void ProcessTimePassed(int secondsPassed)
        {
            if (secondsPassed <= 0) return;

            var entitiesWithEffects = _componentStore.GetAllEntitiesWithComponent<ActiveStatusEffectComponent>().ToList();
            var effectsToRemove = new List<(int entityId, ActiveStatusEffect effect)>();

            foreach (var entityId in entitiesWithEffects)
            {
                var statusEffectComp = _componentStore.GetComponent<ActiveStatusEffectComponent>(entityId);
                if (statusEffectComp == null || !statusEffectComp.ActiveEffects.Any()) continue;

                foreach (var effect in statusEffectComp.ActiveEffects)
                {
                    effect.Duration -= secondsPassed;
                    if (effect.Duration <= 0)
                    {
                        effectsToRemove.Add((entityId, effect));
                        continue;
                    }

                    if (effect.BaseEffect.TickFrequency > 0)
                    {
                        effect.TimeSinceLastTick += secondsPassed;
                        while (effect.TimeSinceLastTick >= effect.BaseEffect.TickFrequency)
                        {
                            effect.BaseEffect.OnTick(entityId, _componentStore);
                            effect.TimeSinceLastTick -= effect.BaseEffect.TickFrequency;
                        }
                    }
                }
            }

            foreach (var (entityId, effect) in effectsToRemove)
            {
                var statusEffectComp = _componentStore.GetComponent<ActiveStatusEffectComponent>(entityId);
                if (statusEffectComp != null && statusEffectComp.ActiveEffects.Remove(effect))
                {
                    effect.BaseEffect.OnRemove(entityId, _componentStore);
                }
            }
        }

        public void ApplyEffect(int targetId, StatusEffect effect, float duration, int sourceId)
        {
            var statusEffectComp = _componentStore.GetComponent<ActiveStatusEffectComponent>(targetId);
            if (statusEffectComp == null)
            {
                statusEffectComp = new ActiveStatusEffectComponent();
                _componentStore.AddComponent(targetId, statusEffectComp);
            }

            var activeEffect = new ActiveStatusEffect(effect, duration, sourceId);
            statusEffectComp.ActiveEffects.Add(activeEffect);
            effect.OnApply(targetId, _componentStore);
        }

        public StatusEffect CreateEffectFromName(string name, string source)
        {
            switch (name.ToLower())
            {
                case "poison":
                    return new PoisonStatusEffect(source);
                case "weakness":
                    return new WeaknessStatusEffect(source);
                default:
                    return null;
            }
        }
    }
}