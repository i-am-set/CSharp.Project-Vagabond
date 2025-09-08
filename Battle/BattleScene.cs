using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
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
        private BattleNarrator _battleNarrator;
        private ActionMenu _actionMenu;

        private ComponentStore _componentStore;
        private SceneManager _sceneManager;

        // State Tracking
        private BattleManager.BattlePhase _previousBattlePhase;
        private bool _isBattleOver;
        private float _endOfBattleTimer;
        private const float END_OF_BATTLE_DELAY = 2.0f; // Seconds to wait before exiting

        // UI State
        private enum BattleUIState { Default, Targeting }
        private BattleUIState _uiState = BattleUIState.Default;
        private MoveData _moveForTargeting;
        private List<TargetInfo> _currentTargets = new List<TargetInfo>();
        private int _hoveredTargetIndex = -1;
        private struct TargetInfo { public BattleCombatant Combatant; public Rectangle Bounds; }

        // Layout Constants
        private const int DIVIDER_Y = 120;

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
            var narratorBounds = new Rectangle(0, DIVIDER_Y, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - DIVIDER_Y);
            _battleNarrator = new BattleNarrator(narratorBounds);
            _actionMenu = new ActionMenu();
        }

        public override void Enter()
        {
            base.Enter();

            _actionMenu.ResetAnimationState();
            _uiState = BattleUIState.Default;

            _isBattleOver = false;
            _endOfBattleTimer = 0f;

            EventBus.Subscribe<GameEvents.BattleActionResolved>(OnBattleActionResolved);
            _actionMenu.OnMoveSelected += OnPlayerMoveSelected;
            _actionMenu.OnTargetingInitiated += OnTargetingInitiated;
            _actionMenu.OnTargetingCancelled += OnTargetingCancelled;

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
            EventBus.Unsubscribe<GameEvents.BattleActionResolved>(OnBattleActionResolved);
            _actionMenu.OnMoveSelected -= OnPlayerMoveSelected;
            _actionMenu.OnTargetingInitiated -= OnTargetingInitiated;
            _actionMenu.OnTargetingCancelled -= OnTargetingCancelled;

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

        private void OnBattleActionResolved(GameEvents.BattleActionResolved e)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            _battleNarrator.Show(e.NarrationMessage, secondaryFont);
        }

        private void OnTargetingInitiated(MoveData move)
        {
            _uiState = BattleUIState.Targeting;
            _moveForTargeting = move;
        }

        private void OnTargetingCancelled()
        {
            _uiState = BattleUIState.Default;
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
            if (_battleManager == null)
            {
                base.Update(gameTime);
                return;
            }

            // If the battle is over, handle the delay and transition back to the map.
            if (_isBattleOver)
            {
                _endOfBattleTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_endOfBattleTimer >= END_OF_BATTLE_DELAY)
                {
                    _sceneManager.ChangeScene(GameSceneState.TerminalMap);
                }
                base.Update(gameTime);
                return; // Stop further updates once the battle is won/lost
            }

            var currentKeyboardState = Keyboard.GetState();
            var currentMouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            // Update UI elements that should always be active
            _battleNarrator.Update(gameTime);
            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.ActionSelection)
            {
                _actionMenu.Update(currentMouseState);
                if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
                {
                    _actionMenu.GoBack();
                }
            }

            if (_uiState == BattleUIState.Targeting)
            {
                _hoveredTargetIndex = -1;
                for (int i = 0; i < _currentTargets.Count; i++)
                {
                    if (_currentTargets[i].Bounds.Contains(virtualMousePos))
                    {
                        _hoveredTargetIndex = i;
                        break;
                    }
                }

                if (UIInputManager.CanProcessMouseClick() && currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
                {
                    if (_hoveredTargetIndex != -1)
                    {
                        var selectedTarget = _currentTargets[_hoveredTargetIndex].Combatant;
                        OnPlayerMoveSelected(_moveForTargeting, selectedTarget);
                        _uiState = BattleUIState.Default;
                        UIInputManager.ConsumeMouseClick();
                    }
                }
            }

            // The BattleScene controls the flow of the BattleManager based on the narrator's state.
            _battleManager.CanAdvance = !_battleNarrator.IsBusy;
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

            // Check if the battle just ended in this frame.
            if (_battleManager.CurrentPhase == BattleManager.BattlePhase.BattleOver && !_isBattleOver)
            {
                _isBattleOver = true;
                _actionMenu.Hide();
            }

            // CRITICAL: Call base.Update() at the end to update previous input states for the next frame.
            base.Update(gameTime);
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (_battleManager == null)
            {
                string errorText = "Battle failed to initialize.";
                Vector2 textSize = font.MeasureString(errorText);
                spriteBatch.DrawStringSnapped(font, errorText, new Vector2((Global.VIRTUAL_WIDTH - textSize.X) / 2, (Global.VIRTUAL_HEIGHT - textSize.Y) / 2), Color.Red);
                return;
            }

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            // --- Draw Combatant HUDs ---
            _currentTargets.Clear();
            var enemies = _battleManager.AllCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();
            var player = _battleManager.AllCombatants.FirstOrDefault(c => c.IsPlayerControlled);

            if (player != null)
            {
                DrawCombatantHud(spriteBatch, font, secondaryFont, player, new Vector2(50, 80));
            }
            if (enemies.Any())
            {
                // This is a simple layout for now. A more robust system would handle more enemies.
                DrawCombatantHud(spriteBatch, font, secondaryFont, enemies[0], new Vector2(Global.VIRTUAL_WIDTH - 100, 80));
                _currentTargets.Add(new TargetInfo
                {
                    Combatant = enemies[0],
                    Bounds = GetCombatantNameBounds(enemies[0], new Vector2(Global.VIRTUAL_WIDTH - 100, 80), font)
                });
            }

            // --- Draw Targeting UI ---
            if (_uiState == BattleUIState.Targeting)
            {
                for (int i = 0; i < _currentTargets.Count; i++)
                {
                    Color boxColor = i == _hoveredTargetIndex ? Color.Red : Color.Yellow;
                    var bounds = _currentTargets[i].Bounds;
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Right, bounds.Top), boxColor);
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Bottom), new Vector2(bounds.Right, bounds.Bottom), boxColor);
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Left, bounds.Top), new Vector2(bounds.Left, bounds.Bottom), boxColor);
                    spriteBatch.DrawLineSnapped(new Vector2(bounds.Right, bounds.Top), new Vector2(bounds.Right, bounds.Bottom), boxColor);
                }
            }

            // --- Draw UI Divider ---
            spriteBatch.DrawSnapped(pixel, new Rectangle(0, DIVIDER_Y, Global.VIRTUAL_WIDTH, 1), Color.White);


            // --- Draw UI Panels ---
            _actionMenu.Draw(spriteBatch, font, gameTime, transform);
            _battleNarrator.Draw(spriteBatch, secondaryFont);
        }

        private Rectangle GetCombatantNameBounds(BattleCombatant combatant, Vector2 position, BitmapFont nameFont)
        {
            Vector2 nameSize = nameFont.MeasureString(combatant.Name);
            Vector2 namePos = position + new Vector2(0, -20);
            return new Rectangle((int)namePos.X - 2, (int)namePos.Y - 2, (int)nameSize.X + 4, (int)nameSize.Y + 4);
        }

        private void DrawCombatantHud(SpriteBatch spriteBatch, BitmapFont nameFont, BitmapFont statsFont, BattleCombatant combatant, Vector2 position)
        {
            if (combatant.IsDefeated) return;

            // Draw Name
            spriteBatch.DrawStringSnapped(nameFont, combatant.Name, position + new Vector2(0, -20), Color.White);

            // Draw HP line with mixed colors
            var global = ServiceLocator.Get<Global>();
            Color labelColor = global.Palette_LightGray;
            Color numberColor = Color.White;
            Vector2 hpPosition = position + new Vector2(0, -10);

            string hpLabel = "HP: ";
            string currentHp = combatant.Stats.CurrentHP.ToString();
            string separator = "/";
            string maxHp = combatant.Stats.MaxHP.ToString();

            // Draw "HP: "
            spriteBatch.DrawStringSnapped(statsFont, hpLabel, hpPosition, labelColor);
            float currentX = hpPosition.X + statsFont.MeasureString(hpLabel).Width;

            // Draw Current HP
            spriteBatch.DrawStringSnapped(statsFont, currentHp, new Vector2(currentX, hpPosition.Y), numberColor);
            currentX += statsFont.MeasureString(currentHp).Width;

            // Draw "/"
            spriteBatch.DrawStringSnapped(statsFont, separator, new Vector2(currentX, hpPosition.Y), labelColor);
            currentX += statsFont.MeasureString(separator).Width;

            // Draw Max HP
            spriteBatch.DrawStringSnapped(statsFont, maxHp, new Vector2(currentX, hpPosition.Y), numberColor);
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}