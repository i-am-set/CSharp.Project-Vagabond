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

        // NOTE: The time-based processing of status effects has been removed with the WorldClockManager.
        // A new turn-based system (e.g., subscribing to a PlayerActionExecuted event) would be
        // needed to make status effects tick down over turns instead of time.

        private void RemoveExpiredEffects(List<(int entityId, ActiveStatusEffect effect)> effectsToRemove)
        {
            foreach (var (entityId, effect) in effectsToRemove)
            {
                var statusEffectComp = _componentStore.GetComponent<ActiveStatusEffectComponent>(entityId);
                if (statusEffectComp != null && statusEffectComp.ActiveEffects.Remove(effect))
                {
                    effect.BaseEffect.OnRemove(entityId, _componentStore);
                }
            }
        }

        public void ApplyEffect(int targetId, StatusEffect effect, float durationInRounds, int sourceId, int amount)
        {
            var statusEffectComp = _componentStore.GetComponent<ActiveStatusEffectComponent>(targetId);
            if (statusEffectComp == null)
            {
                statusEffectComp = new ActiveStatusEffectComponent();
                _componentStore.AddComponent(targetId, statusEffectComp);
            }

            // Check if an effect of the same type already exists.
            var existingEffect = statusEffectComp.ActiveEffects.FirstOrDefault(e => e.BaseEffect.Name == effect.Name);

            if (existingEffect != null)
            {
                // If it exists, refresh its duration and stack the amount.
                existingEffect.Duration += durationInRounds;
                existingEffect.Amount += amount;
                var targetName = EntityNamer.GetName(targetId);
            }
            else
            {
                // If it's a new effect, apply it normally.
                var activeEffect = new ActiveStatusEffect(effect, durationInRounds, sourceId, amount);
                statusEffectComp.ActiveEffects.Add(activeEffect);
                effect.OnApply(targetId, _componentStore, amount);
            }
        }

        public StatusEffect CreateEffectFromName(string name, string source)
        {
            switch (name.ToLower())
            {
                case "weakness":
                    return new WeaknessStatusEffect(source);
                default:
                    return null;
            }
        }
    }
}