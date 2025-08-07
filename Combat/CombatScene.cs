using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ProjectVagabond.Scenes
{
    public class CombatScene : GameScene
    {
        private CombatManager _combatManager;
        private HandRenderer _leftHandRenderer;
        private HandRenderer _rightHandRenderer;
        private ActionMenu _leftActionMenu;
        private ActionMenu _rightActionMenu;
        private CombatInputHandler _inputHandler;
        private Texture2D _enemyTexture;
        private AnimationManager _animationManager;

        public override bool UsesLetterboxing => false;

        public override void Initialize()
        {
            base.Initialize();

            // For now, use default speeds. This would later be fetched from player stats.
            _combatManager = new CombatManager(leftHandSpeed: 10f, rightHandSpeed: 8f);

            _leftHandRenderer = new HandRenderer(_combatManager.LeftHand);
            _rightHandRenderer = new HandRenderer(_combatManager.RightHand);

            _leftActionMenu = new ActionMenu(HandType.Left);
            _rightActionMenu = new ActionMenu(HandType.Right);

            _inputHandler = new CombatInputHandler(_combatManager, _leftHandRenderer, _rightHandRenderer, _leftActionMenu, _rightActionMenu);
            _animationManager = ServiceLocator.Get<AnimationManager>();
        }

        public override void Enter()
        {
            base.Enter();
            EventBus.Subscribe<GameEvents.UIThemeOrResolutionChanged>(OnResolutionChanged);

            var gameState = ServiceLocator.Get<GameState>();
            var combatants = new List<int> { gameState.PlayerEntityId, -1 }; // Player and a dummy enemy
            _combatManager.StartCombat(combatants);

            // Populate menus and load textures here, as Enter() is called after Core.LoadContent()
            var actionManager = ServiceLocator.Get<ActionManager>();
            var allActions = actionManager.GetAllActions();
            _leftActionMenu.SetActions(allActions);
            _rightActionMenu.SetActions(allActions);

            _enemyTexture = ServiceLocator.Get<SpriteManager>().EnemySprite;
            _leftHandRenderer.LoadContent();
            _rightHandRenderer.LoadContent();

            RecalculateLayouts();

            _leftHandRenderer.EnterScene();
            _rightHandRenderer.EnterScene();
            _leftActionMenu.EnterScene();
            _rightActionMenu.EnterScene();
            _inputHandler.Reset();

            _animationManager.Register("LeftHandSway", _leftHandRenderer.SwayAnimation);
            _animationManager.Register("RightHandSway", _rightHandRenderer.SwayAnimation);
        }

        public override void Exit()
        {
            base.Exit();
            EventBus.Unsubscribe<GameEvents.UIThemeOrResolutionChanged>(OnResolutionChanged);
            _animationManager.Unregister("LeftHandSway");
            _animationManager.Unregister("RightHandSway");
        }

        private void OnResolutionChanged(GameEvents.UIThemeOrResolutionChanged e)
        {
            RecalculateLayouts();
        }

        private void RecalculateLayouts()
        {
            // This method is now the single source of truth for triggering layout updates
            // in response to resolution changes or scene entry.
            _leftHandRenderer.RecalculateLayout();
            _rightHandRenderer.RecalculateLayout();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_combatManager.CurrentState == PlayerTurnState.Resolving)
            {
                ResolveCurrentTurn();
                return; // Skip other updates for this frame
            }

            if (IsInputBlocked) return;

            _inputHandler.Update(gameTime);

            _leftHandRenderer.Update(gameTime, _combatManager, _inputHandler);
            _rightHandRenderer.Update(gameTime, _combatManager, _inputHandler);
            _leftActionMenu.Update(gameTime, _inputHandler, _combatManager);
            _rightActionMenu.Update(gameTime, _inputHandler, _combatManager);
        }

        private void ResolveCurrentTurn()
        {
            var playerActions = _combatManager.GeneratePlayerActions();
            // In a full game, we would gather actions from enemies as well.
            var allActions = new List<CombatAction>(playerActions);

            var resolvedOrder = TurnResolver.ResolveTurnOrder(allActions);

            // For this prototype, we just log the result and reset.
            LogResolvedOrder(resolvedOrder);

            // Reset for the next turn.
            _combatManager.ResetTurn();
            _inputHandler.Reset();
        }

        private void LogResolvedOrder(List<CombatAction> resolvedOrder)
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- TURN RESOLVED ---");
            for (int i = 0; i < resolvedOrder.Count; i++)
            {
                var action = resolvedOrder[i];
                sb.Append($"{i + 1}. Caster: {action.CasterEntityId}, ");
                sb.Append($"Action: '{action.ActionData.Name}', ");
                sb.Append($"Priority: {action.ActionData.Priority}, ");
                sb.Append($"Speed: {action.EffectiveCastSpeed:F1}");
                sb.AppendLine();
            }
            sb.AppendLine("---------------------");
            Debug.WriteLine(sb.ToString());
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // Draw placeholder enemy
            if (_enemyTexture != null)
            {
                var enemyPos = new Vector2(
                    (Global.VIRTUAL_WIDTH - _enemyTexture.Width) / 2f,
                    20
                );
                spriteBatch.Draw(_enemyTexture, enemyPos, Color.White);
            }

            // Draw the hands first so they are in the background
            _leftHandRenderer.Draw(spriteBatch, font, gameTime);
            _rightHandRenderer.Draw(spriteBatch, font, gameTime);

            // Draw the action menus on top, with the focused menu last (on top)
            var focusedHand = _inputHandler.FocusedHand;
            var unfocusedMenu = (focusedHand == HandType.Left) ? _rightActionMenu : _leftActionMenu;
            var focusedMenu = (focusedHand == HandType.Left) ? _leftActionMenu : _rightActionMenu;

            unfocusedMenu.Draw(spriteBatch, font, gameTime);
            focusedMenu.Draw(spriteBatch, font, gameTime);

            // Draw the "CAST" prompt if in the confirmation state
            if (_combatManager.CurrentState == PlayerTurnState.Confirming)
            {
                string castText = "CAST";
                Vector2 textSize = font.MeasureString(castText);
                Vector2 position = new Vector2(
                    (Global.VIRTUAL_WIDTH - textSize.X) / 2,
                    Global.VIRTUAL_HEIGHT - 100
                );
                spriteBatch.DrawString(font, castText, position, Color.Yellow);
            }
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }
    }
}