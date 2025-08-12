using Microsoft.Xna.Framework;
using ProjectVagabond.Scenes;
using System.Diagnostics;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// The final state of combat. Handles rewards, cleanup, and transitioning out of the scene.
    /// </summary>
    public class CombatEndState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            Debug.WriteLine("--- Combat End ---");
            Debug.WriteLine("  ... Combat has ended. Transitioning back to map.");
            // TODO: Display victory/defeat screen, grant rewards, etc.

            var sceneManager = ServiceLocator.Get<SceneManager>();
            sceneManager.ChangeScene(GameSceneState.TerminalMap);
        }

        public void OnExit(CombatManager combatManager)
        {
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
        }
    }
}