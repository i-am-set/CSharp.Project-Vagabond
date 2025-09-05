using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    /// <summary>
    /// A scene dedicated to managing and rendering a turn-based battle.
    /// </summary>
    public class BattleScene : GameScene
    {
        private BattleManager _battleManager;
        private int _enemyEntityId = -1;

        // UI Components
        private BattleLog _battleLog;
        private ActionMenu _actionMenu;

        private ComponentStore _componentStore;
        private SceneManager _sceneManager;

        // State Tracking
        private BattleManager.BattlePhase _previousBattlePhase;
        private bool _isBattleOver;
        private float _endOfBattleTimer;
        private const float END_OF_BATTLE_DELAY = 2.0f; // Seconds to wait before exiting

        // Layout Constants
        private const int DIVIDER_Y = 120;
        private const int LOG_AREA_WIDTH = 180;

        public BattleScene()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _sceneManager = ServiceLocator.Get<SceneManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            // The battle scene animation can encompass the whole screen.
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            base.Initialize();
            var logBounds = new Rectangle(10, DIVIDER_Y + 5, LOG_AREA_WIDTH, Global.VIRTUAL_HEIGHT - DIVIDER_Y - 10);
            _battleLog = new BattleLog(logBounds);
            _actionMenu = new ActionMenu();
        }

        public override void Enter()
        {
            base.Enter();

            _actionMenu.ResetAnimationState();

            _isBattleOver = false;
            _endOfBattleTimer = 0f;

            EventBus.Subscribe<GameEvents.BattleLogMessagePublished>(OnBattleLogMessage);
            _actionMenu.OnMoveSelected += OnPlayerMoveSelected;

            // For this debug implementation, we create combatants when the scene starts.
            var gameState = ServiceLocator.Get<GameState>();
            int playerEntityId = gameState.PlayerEntityId;

            // Spawn a temporary enemy for the battle
            _enemyEntityId = Spawner.Spawn("wanderer", new Vector2(-1, -1)); // Position doesn't matter

            if (_enemyEntityId == -1)
            {
                Debug.WriteLine("[BattleScene] [FATAL] Failed to spawn enemy for battle. Aborting.");
                _battleManager = null;
                return;
            }

            // Create the BattleCombatant objects from the entity IDs
            var playerCombatant = BattleCombatantFactory.CreateFromEntity(playerEntityId, "player_1");
            var enemyCombatant = BattleCombatantFactory.CreateFromEntity(_enemyEntityId, "enemy_1");

            if (playerCombatant == null || enemyCombatant == null)
            {
                Debug.WriteLine("[BattleScene] [FATAL] Failed to create one or more combatants from entities. Aborting.");
                _battleManager = null;
                return;
            }

            var playerParty = new List<BattleCombatant> { playerCombatant };
            var enemyParty = new List<BattleCombatant> { enemyCombatant };

            // Initialize the BattleManager with the created combatants
            _battleManager = new BattleManager(playerParty, enemyParty);
            _previousBattlePhase = _battleManager.CurrentPhase;
        }

        public override void Exit()
        {
            base.Exit();
            EventBus.Unsubscribe<GameEvents.BattleLogMessagePublished>(OnBattleLogMessage);
            _actionMenu.OnMoveSelected -= OnPlayerMoveSelected;

            // Clean up the temporary enemy entity when leaving the battle scene
            if (_enemyEntityId != -1)
            {
                var entityManager = ServiceLocator.Get<EntityManager>();
                var componentStore = ServiceLocator.Get<ComponentStore>();
                componentStore.EntityDestroyed(_enemyEntityId);
                entityManager.DestroyEntity(_enemyEntityId);
                _enemyEntityId = -1;
            }
        }

        private void OnBattleLogMessage(GameEvents.BattleLogMessagePublished e)
        {
            _battleLog.AddMessage(e.Message);
        }

        private void OnPlayerMoveSelected(MoveData move, BattleCombatant target)
        {
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
            if (player != null)
            {
                var action = new QueuedAction
                {
                    Actor = player,
                    Target = target,
                    ChosenMove = move,
                    Priority = move.Priority,
                    ActorAgility = player.Stats.Agility
                };
                _battleManager.SetPlayerAction(action);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_battleManager == null) return;

            // If the battle is over, handle the delay and transition back to the map.
            if (_isBattleOver)
            {
                _endOfBattleTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_endOfBattleTimer >= END_OF_BATTLE_DELAY)
                {
                    _sceneManager.ChangeScene(GameSceneState.TerminalMap);
                }
                return; // Stop further updates once the battle is won/lost
            }

            // The BattleManager drives the state of the battle.
            _battleManager.Update();

            // Check for phase transitions to manage UI state.
            var currentPhase = _battleManager.CurrentPhase;
            if (currentPhase != _previousBattlePhase)
            {
                // If we just entered the ActionSelection phase, show the menu.
                if (currentPhase == BattleManager.BattlePhase.ActionSelection)
                {
                    var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && !c.IsDefeated);
                    if (player != null)
                    {
                        _actionMenu.Show(player, _battleManager.AllCombatants.ToList());
                    }
                }
                else
                {
                    // If we left the ActionSelection phase, hide the menu.
                    _actionMenu.Hide();
                }
                _previousBattlePhase = currentPhase;
            }

            // Update the action menu if it's supposed to be visible.
            if (currentPhase == BattleManager.BattlePhase.ActionSelection)
            {
                _actionMenu.Update(Mouse.GetState());
            }

            // Check if the battle just ended in this frame.
            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.BattleOver)
            {
                _isBattleOver = true;
                _actionMenu.Hide();
                return;
            }

            // Placeholder for triggering animations during ActionResolution
            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.ActionResolution)
            {
                // Future: Check the action being resolved and trigger corresponding visual effects.
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_battleManager == null)
            {
                string errorText = "Battle failed to initialize.";
                Vector2 textSize = font.MeasureString(errorText);
                spriteBatch.DrawString(font, errorText, new Vector2((Global.VIRTUAL_WIDTH - textSize.X) / 2, (Global.VIRTUAL_HEIGHT - textSize.Y) / 2), Color.Red);
                return;
            }

            // --- Draw Combatant HUDs ---
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);
            var enemy = _battleManager.AllCombatants.FirstOrDefault(c => !c.IsPlayerControlled);

            if (player != null)
            {
                DrawCombatantHud(spriteBatch, font, player, new Vector2(50, 80));
            }
            if (enemy != null)
            {
                DrawCombatantHud(spriteBatch, font, enemy, new Vector2(Global.VIRTUAL_WIDTH - 100, 80));
            }

            // --- Draw UI Divider ---
            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, new Rectangle(0, DIVIDER_Y, Global.VIRTUAL_WIDTH, 1), Color.White);


            // --- Draw UI Panels ---
            _battleLog.Draw(spriteBatch, font);
            _actionMenu.Draw(spriteBatch, font, gameTime, transform);
        }

        private void DrawCombatantHud(SpriteBatch spriteBatch, BitmapFont font, BattleCombatant combatant, Vector2 position)
        {
            if (combatant.IsDefeated) return;

            // Draw HUD
            string name = combatant.Name;
            string hp = $"HP: {combatant.Stats.CurrentHP} / {combatant.Stats.MaxHP}";
            spriteBatch.DrawString(font, name, position + new Vector2(0, -20), Color.White);
            spriteBatch.DrawString(font, hp, position + new Vector2(0, -10), Color.White);
        }
    }
}