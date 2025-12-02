#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Battle.UI
{
    public enum BattleUIState { Default, Targeting, ItemTargeting }
    public enum BattleSubMenuState { None, ActionRoot, ActionMoves, Item }
    public class HoverHighlightState { public MoveData? CurrentMove; public List<BattleCombatant> Targets = new List<BattleCombatant>(); public float Timer = 0f; public const float SingleTargetFlashOnDuration = 0.4f; public const float SingleTargetFlashOffDuration = 0.2f; public const float MultiTargetFlashOnDuration = 0.4f; public const float MultiTargetFlashOffDuration = 0.4f; }
    public class BattleUIManager
    {
        public event Action<MoveData, MoveEntry, BattleCombatant>? OnMoveSelected;
        public event Action<ConsumableItemData, BattleCombatant>? OnItemSelected;
        public event Action? OnFleeRequested;

        private readonly BattleNarrator _battleNarrator;
        private readonly ActionMenu _actionMenu;
        private readonly ItemMenu _itemMenu;
        private readonly Button _itemTargetingBackButton;
        private readonly Global _global;

        public BattleUIState UIState { get; private set; } = BattleUIState.Default;
        public BattleSubMenuState SubMenuState { get; private set; } = BattleSubMenuState.None;
        public MoveData? MoveForTargeting { get; private set; }
        public MoveEntry? SpellForTargeting => _actionMenu.SelectedSpellbookEntry;
        public ConsumableItemData? ItemForTargeting { get; private set; }
        public TargetType? TargetTypeForSelection =>
            UIState == BattleUIState.Targeting ? MoveForTargeting?.Target :
            UIState == BattleUIState.ItemTargeting ? ItemForTargeting?.Target :
            null;
        public MoveData? HoveredMove => _actionMenu.HoveredMove;


        private float _itemTargetingTextAnimTimer = 0f;
        private readonly Queue<Action> _narrationQueue = new Queue<Action>();
        public readonly HoverHighlightState HoverHighlightState = new HoverHighlightState();
        public float SharedPulseTimer { get; private set; } = 0f;

        public bool IsBusy => _battleNarrator.IsBusy || _narrationQueue.Any();
        public bool IsWaitingForInput => _battleNarrator.IsWaitingForInput;

        private bool _isPromptVisible;
        private readonly List<Texture2D> _promptTextures = new List<Texture2D>();
        private int _promptTextureIndex;
        private float _promptCycleTimer;
        private const float PROMPT_CYCLE_INTERVAL = 0.5f;
        private Button? _lastHoveredButton;

        // Debug Bounds
        private Rectangle _narratorBounds;
        private Rectangle _controlPromptBounds;

        public BattleUIManager()
        {
            _global = ServiceLocator.Get<Global>();
            const int narratorWidth = 314;
            const int narratorHeight = 50;
            int narratorX = (Global.VIRTUAL_WIDTH - narratorWidth) / 2;
            const int narratorY = 123;
            _narratorBounds = new Rectangle(narratorX, narratorY, narratorWidth, narratorHeight);

            _battleNarrator = new BattleNarrator(_narratorBounds);
            _actionMenu = new ActionMenu();
            _itemMenu = new ItemMenu();

            // Initialize with correct style
            _itemTargetingBackButton = new Button(Rectangle.Empty, "BACK", enableHoverSway: false) { CustomDefaultTextColor = _global.Palette_Gray };
            _itemTargetingBackButton.OnClick += () =>
            {
                UIState = BattleUIState.Default;
                OnItemMenuRequested();
            };

            _actionMenu.OnMoveSelected += HandlePlayerMoveSelected;
            _actionMenu.OnItemMenuRequested += OnItemMenuRequested;
            _actionMenu.OnMovesMenuOpened += () => SubMenuState = BattleSubMenuState.ActionMoves;
            _actionMenu.OnMainMenuOpened += () => SubMenuState = BattleSubMenuState.ActionRoot;
            _actionMenu.OnFleeRequested += () => OnFleeRequested?.Invoke();
            _itemMenu.OnBack += OnItemMenuBack;
            _itemMenu.OnItemConfirmed += HandlePlayerItemSelected;
            _itemMenu.OnItemTargetingRequested += OnItemTargetingRequested;
        }

        public void Reset()
        {
            _actionMenu.ResetAnimationState();
            _itemTargetingBackButton.ResetAnimationState();
            UIState = BattleUIState.Default;
            SubMenuState = BattleSubMenuState.None;
            _narrationQueue.Clear();
        }

        public void ForceClearNarration()
        {
            _narrationQueue.Clear();
            _battleNarrator.ForceClear();
        }

        public void ShowActionMenu(BattleCombatant player, List<BattleCombatant> allCombatants)
        {
            SubMenuState = BattleSubMenuState.ActionRoot;
            _actionMenu.Show(player, allCombatants);
            _itemMenu.Hide();
        }

        public void HideAllMenus()
        {
            UIState = BattleUIState.Default;
            SubMenuState = BattleSubMenuState.None;
            _actionMenu.Hide();
            _actionMenu.SetState(ActionMenu.MenuState.Main);
            _itemMenu.Hide();
        }

        public void ShowNarration(string message)
        {
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            _narrationQueue.Enqueue(() => _battleNarrator.Show(message, secondaryFont));
        }

        public void GoBack()
        {
            if (UIState == BattleUIState.ItemTargeting)
            {
                UIState = BattleUIState.Default;
                OnItemMenuRequested();
            }
            else if (SubMenuState == BattleSubMenuState.Item)
            {
                OnItemMenuBack();
            }
            else
            {
                _actionMenu.GoBack();
            }
        }

        public void Update(GameTime gameTime, MouseState currentMouseState, KeyboardState currentKeyboardState)
        {
            SharedPulseTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            _battleNarrator.Update(gameTime);
            UpdateHoverHighlights(gameTime);
            UpdateControlPrompt(gameTime);

            _actionMenu.Update(currentMouseState, gameTime);

            if (SubMenuState == BattleSubMenuState.Item)
            {
                _itemMenu.Update(currentMouseState, gameTime);
            }

            if (UIState == BattleUIState.ItemTargeting)
            {
                _itemTargetingTextAnimTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                _itemTargetingBackButton.Update(currentMouseState);
            }

            if (_actionMenu.CurrentMenuState == ActionMenu.MenuState.Targeting)
            {
                UIState = BattleUIState.Targeting;
                MoveForTargeting = _actionMenu.SelectedMove;
            }
            else if (UIState != BattleUIState.ItemTargeting)
            {
                UIState = BattleUIState.Default;
            }

            if (!_battleNarrator.IsBusy && _narrationQueue.Any())
            {
                var nextStep = _narrationQueue.Dequeue();
                nextStep.Invoke();
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (SubMenuState == BattleSubMenuState.Item)
            {
                _itemMenu.Draw(spriteBatch, font, gameTime, transform);
            }
            else
            {
                _actionMenu.Draw(spriteBatch, font, gameTime, transform);
            }

            if (UIState == BattleUIState.ItemTargeting)
            {
                DrawItemTargetingOverlay(spriteBatch, font, gameTime, transform);
            }

            _battleNarrator.Draw(spriteBatch, ServiceLocator.Get<Core>().SecondaryFont, gameTime);

            DrawControlPrompt(spriteBatch);

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                var pixel = ServiceLocator.Get<Texture2D>();

                // Narrator
                spriteBatch.DrawSnapped(pixel, _narratorBounds, Color.Purple * 0.5f);

                // Control Prompt
                if (_isPromptVisible && _promptTextures.Any())
                {
                    spriteBatch.DrawSnapped(pixel, _controlPromptBounds, Color.Purple * 0.5f);
                }
            }
        }

        private void UpdateControlPrompt(GameTime gameTime)
        {
            if (IsBusy)
            {
                _isPromptVisible = false;
                _lastHoveredButton = null;
                return;
            }

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            Button? currentHoveredButton = null;

            if (SubMenuState == BattleSubMenuState.ActionRoot || SubMenuState == BattleSubMenuState.ActionMoves)
            {
                currentHoveredButton = _actionMenu.HoveredButton;
            }
            else if (SubMenuState == BattleSubMenuState.Item)
            {
                currentHoveredButton = _itemMenu.HoveredButton;
            }

            _isPromptVisible = true;

            if (currentHoveredButton == null)
            {
                if (_lastHoveredButton != null)
                {
                    _promptTextures.Clear();
                    // Use MousePromptBlank instead of MousePromptDisabled to avoid darkening
                    _promptTextures.Add(spriteManager.MousePromptBlank);
                    _promptTextureIndex = 0;
                    _lastHoveredButton = null;
                }
                else if (!_promptTextures.Any())
                {
                    // Use MousePromptBlank instead of MousePromptDisabled to avoid darkening
                    _promptTextures.Add(spriteManager.MousePromptBlank);
                }
                return;
            }

            if (currentHoveredButton != _lastHoveredButton)
            {
                _lastHoveredButton = currentHoveredButton;
                _promptTextures.Clear();
                _promptCycleTimer = 0f;
                _promptTextureIndex = 0;

                if (currentHoveredButton.HasLeftClickAction)
                {
                    _promptTextures.Add(spriteManager.MousePromptLeftClick);
                }
                if (currentHoveredButton.HasRightClickAction)
                {
                    _promptTextures.Add(spriteManager.MousePromptRightClick);
                }
            }

            if (_promptTextures.Count > 1)
            {
                _promptCycleTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_promptCycleTimer >= PROMPT_CYCLE_INTERVAL)
                {
                    _promptCycleTimer -= PROMPT_CYCLE_INTERVAL;
                    _promptTextureIndex = (_promptTextureIndex + 1) % _promptTextures.Count;
                }
            }
            else
            {
                _promptTextureIndex = 0;
            }
        }

        private void DrawControlPrompt(SpriteBatch spriteBatch)
        {
            if (!_isPromptVisible || !_promptTextures.Any())
            {
                return;
            }

            var textureToDraw = _promptTextures[_promptTextureIndex];
            const int padding = 2;
            var position = new Vector2(
                Global.VIRTUAL_WIDTH - textureToDraw.Width - padding,
                Global.VIRTUAL_HEIGHT - textureToDraw.Height - padding
            );

            // Update debug bounds
            _controlPromptBounds = new Rectangle((int)position.X, (int)position.Y, textureToDraw.Width, textureToDraw.Height);

            var global = ServiceLocator.Get<Global>();
            var outlineColor = global.Palette_Black;

            // Draw Outline
            spriteBatch.DrawSnapped(textureToDraw, position + new Vector2(-1, 0), outlineColor);
            spriteBatch.DrawSnapped(textureToDraw, position + new Vector2(1, 0), outlineColor);
            spriteBatch.DrawSnapped(textureToDraw, position + new Vector2(0, -1), outlineColor);
            spriteBatch.DrawSnapped(textureToDraw, position + new Vector2(0, 1), outlineColor);

            spriteBatch.DrawSnapped(textureToDraw, position, Color.White);
        }

        private void DrawItemTargetingOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            // Draw Background
            const int dividerY = 123;
            var bgRect = new Rectangle(0, dividerY, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - dividerY);
            spriteBatch.DrawSnapped(pixel, bgRect, _global.Palette_Black);

            // Draw Border
            spriteBatch.DrawSnapped(spriteManager.BattleBorderTarget, Vector2.Zero, Color.White);

            const int backButtonPadding = 8;
            const int backButtonHeight = 15; // Match ItemMenu
            const int backButtonTopMargin = 1;
            const int horizontalPadding = 10;
            const int verticalPadding = 2;
            int availableWidth = Global.VIRTUAL_WIDTH - (horizontalPadding * 2);
            int availableHeight = Global.VIRTUAL_HEIGHT - dividerY - (verticalPadding * 2);
            int gridAreaHeight = availableHeight - backButtonHeight - backButtonTopMargin;
            int gridStartY = dividerY + verticalPadding + 4;

            string text = "CHOOSE A TARGET";
            Vector2 textSize = font.MeasureString(text);

            float animX = MathF.Sin(_itemTargetingTextAnimTimer * 4f) * 1f;
            float animY = MathF.Sin(_itemTargetingTextAnimTimer * 8f) * 1f;
            Vector2 animOffset = new Vector2(animX, animY);

            Vector2 textPos = new Vector2(
                horizontalPadding + (availableWidth - textSize.X) / 2,
                gridStartY + (gridAreaHeight - textSize.Y) / 2
            ) + animOffset;
            spriteBatch.DrawStringSnapped(font, text, textPos, Color.Red);

            // Ensure button size matches ItemMenu
            var backSize = (_itemTargetingBackButton.Font ?? secondaryFont).MeasureString(_itemTargetingBackButton.Text);
            int backWidth = (int)backSize.Width + 16;

            // Position: Y=165, Centered + 1px
            _itemTargetingBackButton.Bounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - backWidth) / 2 + 1,
                165,
                backWidth,
                backButtonHeight
            );

            // Ensure font is set if not already
            if (_itemTargetingBackButton.Font == null) _itemTargetingBackButton.Font = secondaryFont;

            _itemTargetingBackButton.Draw(spriteBatch, font, gameTime, transform);
        }

        private void HandlePlayerMoveSelected(MoveData move, MoveEntry entry, BattleCombatant target)
        {
            OnMoveSelected?.Invoke(move, entry, target);
        }

        private void HandlePlayerItemSelected(ConsumableItemData item)
        {
            OnItemSelected?.Invoke(item, null);
        }

        private void OnItemMenuRequested()
        {
            SubMenuState = BattleSubMenuState.Item;
            _actionMenu.Hide();
            _itemMenu.Show(ServiceLocator.Get<BattleManager>().AllCombatants.ToList());
        }

        private void OnItemTargetingRequested(ConsumableItemData item)
        {
            UIState = BattleUIState.ItemTargeting;
            ItemForTargeting = item;
            _itemMenu.Hide();
            _itemTargetingTextAnimTimer = 0f;
        }

        private void OnItemMenuBack()
        {
            SubMenuState = BattleSubMenuState.ActionRoot;
            _itemMenu.Hide();
            var player = ServiceLocator.Get<BattleManager>().AllCombatants.FirstOrDefault(c => c.IsPlayerControlled && !c.IsDefeated);
            if (player != null)
            {
                _actionMenu.Show(player, ServiceLocator.Get<BattleManager>().AllCombatants.ToList());
            }
        }

        private void UpdateHoverHighlights(GameTime gameTime)
        {
            HoverHighlightState.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            var hoveredMove = _actionMenu.HoveredMove;
            var allCombatants = ServiceLocator.Get<BattleManager>().AllCombatants;

            if (hoveredMove != HoverHighlightState.CurrentMove)
            {
                HoverHighlightState.CurrentMove = hoveredMove;
                HoverHighlightState.Targets.Clear();

                if (hoveredMove != null)
                {
                    var player = allCombatants.First(c => c.IsPlayerControlled);
                    var enemies = allCombatants.Where(c => !c.IsPlayerControlled && !c.IsDefeated).ToList();
                    var all = allCombatants.Where(c => !c.IsDefeated).ToList();

                    switch (hoveredMove.Target)
                    {
                        case TargetType.Single: HoverHighlightState.Targets.AddRange(enemies); break;
                        case TargetType.Every: HoverHighlightState.Targets.AddRange(enemies); break;
                        case TargetType.Self: HoverHighlightState.Targets.Add(player); break;
                        case TargetType.SingleAll: HoverHighlightState.Targets.AddRange(all); break;
                        case TargetType.EveryAll: HoverHighlightState.Targets.AddRange(all); break;
                    }
                }
            }
        }
    }
}