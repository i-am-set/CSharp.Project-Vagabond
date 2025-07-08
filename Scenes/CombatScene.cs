using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ProjectVagabond.Scenes
{
    public class CombatScene : GameScene
    {
        private CombatLogPanel _combatLogPanel;
        private EnemyDisplayPanel _enemyDisplayPanel;

        public CombatScene()
        {
            // Define the area for the combat log panel on the right
            int logWidth = 450;
            int logHeight = 280;
            int logX = Global.VIRTUAL_WIDTH - logWidth - 20;
            int logY = 20;
            _combatLogPanel = new CombatLogPanel(new Rectangle(logX, logY, logWidth, logHeight));

            // Define the area for the enemy display panel on the left
            int enemyPanelWidth = 450;
            int enemyPanelHeight = 280;
            int enemyPanelX = 20;
            int enemyPanelY = 20;
            _enemyDisplayPanel = new EnemyDisplayPanel(new Rectangle(enemyPanelX, enemyPanelY, enemyPanelWidth, enemyPanelHeight));
        }

        public override void Update(GameTime gameTime)
        {
            // For now, allow escaping combat for testing purposes
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Core.CurrentGameState.EndCombat(); // This will be implemented later, but good to have the call
                Core.CurrentSceneManager.ChangeScene(GameSceneState.TerminalMap);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = Global.Instance.CurrentSpriteBatch;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Draw background or combat environment here in the future

            // Draw the UI panels
            _combatLogPanel.Draw(spriteBatch);
            _enemyDisplayPanel.Draw(spriteBatch);

            spriteBatch.End();
        }
    }
}