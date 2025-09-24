using Microsoft.Xna.Framework;
using ProjectVagabond.Battle;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A system responsible for handling changes to the player's passive abilities.
    /// It listens for events and modifies the player's state accordingly.
    /// </summary>
    public class AbilityLearningSystem : ISystem
    {
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;

        public AbilityLearningSystem()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            EventBus.Subscribe<GameEvents.PlayerAbilitySetChanged>(OnPlayerAbilitySetChanged);
        }

        private void OnPlayerAbilitySetChanged(GameEvents.PlayerAbilitySetChanged e)
        {
            var abilitiesComponent = _componentStore.GetComponent<PassiveAbilitiesComponent>(_gameState.PlayerEntityId);
            if (abilitiesComponent == null) return;

            switch (e.ChangeType)
            {
                case GameEvents.AbilitySetChangeType.Learn:
                    HandleLearnAbility(e.AbilityID, abilitiesComponent);
                    break;
                case GameEvents.AbilitySetChangeType.Forget:
                    HandleForgetAbility(e.AbilityID, abilitiesComponent);
                    break;
            }
        }

        private void HandleLearnAbility(string abilityId, PassiveAbilitiesComponent abilitiesComponent)
        {
            if (!BattleDataCache.Abilities.TryGetValue(abilityId, out var abilityData))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Ability '{abilityId}' does not exist." });
                return;
            }

            if (abilitiesComponent.AbilityIDs.Contains(abilityId))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Player already has the {abilityData.AbilityName} ability." });
                return;
            }

            abilitiesComponent.AbilityIDs.Add(abilityId);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_teal]Player learned the {abilityData.AbilityName} ability!" });
        }

        private void HandleForgetAbility(string abilityId, PassiveAbilitiesComponent abilitiesComponent)
        {
            if (!abilitiesComponent.AbilityIDs.Contains(abilityId))
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Player does not have an ability with ID '{abilityId}'." });
                return;
            }

            BattleDataCache.Abilities.TryGetValue(abilityId, out var abilityData);
            string abilityName = abilityData?.AbilityName ?? abilityId;

            abilitiesComponent.AbilityIDs.Remove(abilityId);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[palette_orange]Player forgot the {abilityName} ability." });
        }

        public void Update(GameTime gameTime)
        {
            // This system is purely event-driven.
        }
    }
}