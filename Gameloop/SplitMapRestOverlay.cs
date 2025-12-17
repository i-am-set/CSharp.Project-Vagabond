#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProjectVagabond.UI
{
    public class SplitMapRestOverlay
    {
        public bool IsOpen { get; private set; } = false;
        public event Action<string>? OnRestCompleted; // Returns summary text
        public event Action? OnLeaveRequested; // For Skip

        private readonly Global _global;
        private readonly SpriteManager _spriteManager;
        private readonly Core _core;
        private readonly GameState _gameState;
        private readonly HapticsManager _hapticsManager;

        private Button _confirmButton;
        private Button _skipButton;
        private ConfirmationDialog _confirmationDialog;

        // Layout Constants
        private const float WORLD_Y_OFFSET = 600f;
        private const int BUTTON_HEIGHT = 15;

        // Slot Layout
        private readonly Rectangle[] _partyMemberPanelAreas = new Rectangle[4];
        private const int PANEL_WIDTH = 76;
        private const int PANEL_HEIGHT = 132;

        // Action Buttons
        private enum RestAction { Rest, Train, Search, Guard }
        private readonly Dictionary<int, RestAction> _selectedActions = new Dictionary<int, RestAction>();
        private readonly List<Button> _actionButtons = new List<Button>();

        // Tuning
        private const float REST_HEAL_PERCENT = 0.75f;
        private const float GUARD_HEAL_MULTIPLIER = 2.0f;
        private const int SEARCH_CHANCE_1 = 30;
        private const int SEARCH_CHANCE_2 = 55;
        private const int SEARCH_CHANCE_3 = 75;
        private const int SEARCH_CHANCE_4 = 90;

        // Animation
        private int _portraitBgFrameIndex = 0;
        private float _portraitBgTimer;
        private float _portraitBgDuration;
        private static readonly Random _rng = new Random();

        public SplitMapRestOverlay(GameScene parentScene)
        {
            _core = ServiceLocator.Get<Core>();
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();

            _confirmationDialog = new ConfirmationDialog(parentScene);

            _confirmButton = new Button(Rectangle.Empty, "CONFIRM", font: _core.SecondaryFont)
            {
                CustomDefaultTextColor = _global.Palette_LightGreen,
                CustomHoverTextColor = Color.Lime,
                UseScreenCoordinates = true
            };
            _confirmButton.OnClick += RequestConfirmRest;

            _skipButton = new Button(Rectangle.Empty, "SKIP", font: _core.SecondaryFont)
            {
                CustomDefaultTextColor = _global.Palette_Red,
                CustomHoverTextColor = Color.Red,
                UseScreenCoordinates = true
            };
            _skipButton.OnClick += RequestSkipRest;
        }

        public void Show()
        {
            IsOpen = true;
            InitializeActions();
            RebuildLayout();
        }

        public void Hide()
        {
            IsOpen = false;
            _confirmationDialog.Hide();
        }

        private void InitializeActions()
        {
            _selectedActions.Clear();
            int partyCount = _gameState.PlayerState.Party.Count;
            for (int i = 0; i < partyCount; i++)
            {
                _selectedActions[i] = RestAction.Rest; // Default to Rest
            }
        }

        private void RebuildLayout()
        {
            int centerX = Global.VIRTUAL_WIDTH / 2;
            int screenBottom = (int)WORLD_Y_OFFSET + Global.VIRTUAL_HEIGHT;
            int margin = 10;
            int buttonY = screenBottom - BUTTON_HEIGHT - margin;

            // Confirm Button
            var font = _core.SecondaryFont;
            var confirmSize = font.MeasureString("CONFIRM");
            int confirmWidth = (int)confirmSize.Width + 16;
            _confirmButton.Bounds = new Rectangle(centerX - confirmWidth - 5, buttonY, confirmWidth, BUTTON_HEIGHT);

            // Skip Button
            var skipSize = font.MeasureString("SKIP");
            int skipWidth = (int)skipSize.Width + 16;
            _skipButton.Bounds = new Rectangle(centerX + 5, buttonY, skipWidth, BUTTON_HEIGHT);

            // Panel Areas - Always 4 slots centered
            int totalPanelWidth = (4 * PANEL_WIDTH);
            int startX = (Global.VIRTUAL_WIDTH - totalPanelWidth) / 2;

            _actionButtons.Clear();

            for (int i = 0; i < 4; i++)
            {
                _partyMemberPanelAreas[i] = new Rectangle(
                    startX + (i * PANEL_WIDTH),
                    (int)WORLD_Y_OFFSET + 40, // Push down a bit
                    PANEL_WIDTH,
                    PANEL_HEIGHT
                );

                // Only create buttons for occupied slots
                if (i < _gameState.PlayerState.Party.Count)
                {
                    CreateActionButtonsForMember(i, _partyMemberPanelAreas[i]);
                }
            }
        }

        private void CreateActionButtonsForMember(int memberIndex, Rectangle panelRect)
        {
            int buttonWidth = 50;
            int buttonHeight = 10;
            int spacing = 1;
            // Anchor to bottom of panel
            // Moved up by 16 pixels as requested (-16)
            int startY = panelRect.Bottom - (4 * (buttonHeight + spacing)) - 5 - 16;
            int centerX = panelRect.Center.X;

            // Helper to create toggle buttons
            void AddBtn(string text, RestAction action)
            {
                var btn = new ToggleButton(
                    new Rectangle(centerX - buttonWidth / 2, startY, buttonWidth, buttonHeight),
                    text,
                    font: _core.SecondaryFont, // Changed to SecondaryFont
                    customToggledTextColor: _global.Palette_Yellow,
                    customDefaultTextColor: _global.Palette_Gray
                )
                {
                    UseScreenCoordinates = true
                };

                // Guard Logic: If party size is 1, disable the Guard button but still show it.
                if (action == RestAction.Guard && _gameState.PlayerState.Party.Count <= 1)
                {
                    btn.IsEnabled = false;
                }

                btn.OnClick += () => SetAction(memberIndex, action);
                _actionButtons.Add(btn);
                startY += buttonHeight + spacing;
            }

            AddBtn("REST", RestAction.Rest);
            AddBtn("TRAIN", RestAction.Train);
            AddBtn("SEARCH", RestAction.Search);
            AddBtn("GUARD", RestAction.Guard);
        }

        private void SetAction(int memberIndex, RestAction action)
        {
            if (action == RestAction.Guard)
            {
                // Exclusive Logic: If setting Guard, unguard everyone else
                foreach (var key in _selectedActions.Keys.ToList())
                {
                    if (_selectedActions[key] == RestAction.Guard)
                    {
                        _selectedActions[key] = RestAction.Rest;
                    }
                }
            }
            else if (_selectedActions[memberIndex] == RestAction.Guard)
            {
                // If we were guarding and switched off, that's fine.
            }

            _selectedActions[memberIndex] = action;
            _hapticsManager.TriggerHop(1f, 0.05f);
        }

        private void RequestConfirmRest()
        {
            _confirmationDialog.Show(
                "Confirm rest actions?",
                new List<Tuple<string, Action>>
                {
                    Tuple.Create("YES", new Action(() => { ExecuteRest(); _confirmationDialog.Hide(); })),
                    Tuple.Create("[gray]NO", new Action(() => _confirmationDialog.Hide()))
                }
            );
        }

        private void RequestSkipRest()
        {
            _confirmationDialog.Show(
                "Skip resting entirely?",
                new List<Tuple<string, Action>>
                {
                    Tuple.Create("[red]SKIP", new Action(() => { OnLeaveRequested?.Invoke(); _confirmationDialog.Hide(); })),
                    Tuple.Create("CANCEL", new Action(() => _confirmationDialog.Hide()))
                }
            );
        }

        private void ExecuteRest()
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Rest Complete!");

            bool guardActive = _selectedActions.Values.Any(a => a == RestAction.Guard);
            int searchers = _selectedActions.Values.Count(a => a == RestAction.Search);

            // 1. Handle Search
            if (searchers > 0)
            {
                int chance = searchers switch { 1 => SEARCH_CHANCE_1, 2 => SEARCH_CHANCE_2, 3 => SEARCH_CHANCE_3, 4 => SEARCH_CHANCE_4, _ => 0 };
                int roll = _rng.Next(0, 100);
                if (roll < chance)
                {
                    // Find a random relic
                    var allRelics = BattleDataCache.Relics.Keys.ToList();
                    if (allRelics.Any())
                    {
                        string relicId = allRelics[_rng.Next(allRelics.Count)];
                        var relic = BattleDataCache.Relics[relicId];
                        _gameState.PlayerState.AddRelic(relicId);
                        summary.AppendLine($"[palette_teal]Found Relic: {relic.RelicName}![/]");
                    }
                }
                else
                {
                    summary.AppendLine("[palette_gray]Search yielded nothing.[/]");
                }
            }

            // 2. Process Each Member
            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                var member = _gameState.PlayerState.Party[i];
                var action = _selectedActions[i];

                switch (action)
                {
                    case RestAction.Rest:
                        float multiplier = guardActive ? GUARD_HEAL_MULTIPLIER : 1.0f;
                        int healAmount = (int)(member.MaxHP * REST_HEAL_PERCENT * multiplier);
                        int oldHP = member.CurrentHP;
                        member.CurrentHP = Math.Min(member.MaxHP, member.CurrentHP + healAmount);
                        int healed = member.CurrentHP - oldHP;
                        if (healed > 0) summary.AppendLine($"{member.Name} rested: +{healed} HP.");
                        else summary.AppendLine($"{member.Name} rested.");
                        break;

                    case RestAction.Train:
                        // Pick 2 distinct stats
                        string[] stats = { "Strength", "Intelligence", "Tenacity", "Agility" };
                        int idx1 = _rng.Next(4);
                        int idx2;
                        do { idx2 = _rng.Next(4); } while (idx2 == idx1);

                        ApplyStatBoost(member, stats[idx1], 2);
                        ApplyStatBoost(member, stats[idx2], 1);
                        summary.AppendLine($"{member.Name} trained: +2 {stats[idx1].Substring(0, 3)}, +1 {stats[idx2].Substring(0, 3)}.");
                        break;

                    case RestAction.Guard:
                        summary.AppendLine($"{member.Name} stood guard.");
                        break;

                    case RestAction.Search:
                        // Already handled globally, just log action
                        summary.AppendLine($"{member.Name} searched the area.");
                        break;
                }
            }

            OnRestCompleted?.Invoke(summary.ToString());
        }

        private void ApplyStatBoost(PartyMember member, string stat, int amount)
        {
            switch (stat)
            {
                case "Strength": member.Strength += amount; break;
                case "Intelligence": member.Intelligence += amount; break;
                case "Tenacity": member.Tenacity += amount; break;
                case "Agility": member.Agility += amount; break;
            }
        }

        public void Update(GameTime gameTime, MouseState mouseState, Matrix cameraTransform)
        {
            if (!IsOpen) return;

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Update(gameTime);
                return; // Block other input
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _portraitBgTimer += dt;
            if (_portraitBgTimer >= _portraitBgDuration)
            {
                _portraitBgTimer = 0f;
                _portraitBgDuration = (float)(_rng.NextDouble() * (8.0 - 2.0) + 2.0);
                var frames = _spriteManager.InventorySlotLargeSourceRects;
                if (frames != null && frames.Length > 0) _portraitBgFrameIndex = _rng.Next(frames.Length);
            }

            // Transform mouse to world space
            var virtualMousePos = Core.TransformMouse(mouseState.Position);
            var mouseInWorldSpace = Vector2.Transform(virtualMousePos, Matrix.Invert(cameraTransform));

            // Fake mouse state for world space buttons
            var worldMouseState = new MouseState((int)mouseInWorldSpace.X, (int)mouseInWorldSpace.Y, mouseState.ScrollWheelValue, mouseState.LeftButton, mouseState.MiddleButton, mouseState.RightButton, mouseState.XButton1, mouseState.XButton2);

            // Update Action Buttons
            for (int i = 0; i < _actionButtons.Count; i++)
            {
                var btn = _actionButtons[i];
                btn.Update(worldMouseState);
            }

            // Sync Toggle States
            int btnIndex = 0;
            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                // Rest
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Rest;
                // Train
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Train;
                // Search
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Search;
                // Guard (Conditional)
                if (btnIndex < _actionButtons.Count) ((ToggleButton)_actionButtons[btnIndex++]).IsSelected = _selectedActions[i] == RestAction.Guard;
            }

            _confirmButton.Update(worldMouseState);
            _skipButton.Update(worldMouseState);
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsOpen) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = _core.SecondaryFont;
            var defaultFont = ServiceLocator.Get<BitmapFont>();

            // Draw Background
            var bgRect = new Rectangle(0, (int)WORLD_Y_OFFSET, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
            spriteBatch.DrawSnapped(pixel, bgRect, _global.GameBg);

            // Draw Border
            if (_spriteManager.RestBorderMain != null)
            {
                spriteBatch.DrawSnapped(_spriteManager.RestBorderMain, new Vector2(0, WORLD_Y_OFFSET), Color.White);
            }

            // Title
            string title = "REST";
            var titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.Width) / 2, WORLD_Y_OFFSET + 10);
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_BrightWhite);

            // Draw Party Panels
            for (int i = 0; i < 4; i++)
            {
                var bounds = _partyMemberPanelAreas[i];
                bool isOccupied = i < _gameState.PlayerState.Party.Count;
                var member = isOccupied ? _gameState.PlayerState.Party[i] : null;

                int centerX = bounds.Center.X;
                int currentY = bounds.Y + 4;

                // 1. Name (Calculated here, drawn later to be on top)
                string name = isOccupied ? member!.Name.ToUpper() : "EMPTY";
                Color nameColor = isOccupied ? _global.Palette_BrightWhite : _global.Palette_DarkGray;

                var nameSize = defaultFont.MeasureString(name);
                Vector2 namePos = new Vector2(centerX - nameSize.Width / 2, currentY);

                // Advance Y for background drawing
                currentY += (int)nameSize.Height - 2;

                // 2. Portrait Background
                if (_spriteManager.InventorySlotLargeSourceRects != null && _spriteManager.InventorySlotLargeSourceRects.Length > 0)
                {
                    var largeFrame = _spriteManager.InventorySlotLargeSourceRects[_portraitBgFrameIndex];
                    Vector2 bgPos = new Vector2(centerX, currentY + 16);
                    Vector2 origin = new Vector2(largeFrame.Width / 2f, largeFrame.Height / 2f);
                    spriteBatch.DrawSnapped(_spriteManager.InventorySlotIdleLargeSpriteSheet, bgPos, largeFrame, Color.White, 0f, origin, 1.0f, SpriteEffects.None, 0f);
                }

                // 3. Portrait
                if (isOccupied && _spriteManager.PlayerPortraitsSpriteSheet != null && _spriteManager.PlayerPortraitSourceRects.Count > 0)
                {
                    int portraitIndex = Math.Clamp(member!.PortraitIndex, 0, _spriteManager.PlayerPortraitSourceRects.Count - 1);
                    var sourceRect = _spriteManager.PlayerPortraitSourceRects[portraitIndex];

                    // Animation Logic: Toggle between Main and Alt sprite
                    float animSpeed = 1f;
                    int frame = (int)(gameTime.TotalGameTime.TotalSeconds * animSpeed) % 2;
                    Texture2D textureToDraw = frame == 0 ? _spriteManager.PlayerPortraitsSpriteSheet : _spriteManager.PlayerPortraitsAltSpriteSheet;

                    // Bob Logic: Move up 1 pixel when using Alt sprite
                    float bobOffset = frame == 1 ? -1f : 0f;

                    var destRect = new Rectangle(centerX - 16, (int)(currentY + bobOffset), 32, 32);
                    spriteBatch.DrawSnapped(textureToDraw, destRect, sourceRect, Color.White);
                }

                // Draw Name NOW (On top of background/shadow)
                spriteBatch.DrawStringSnapped(defaultFont, name, namePos, nameColor);

                currentY += 32 + 2 - 6;

                // 4. Health Bar
                if (_spriteManager.InventoryPlayerHealthBarEmpty != null)
                {
                    int barX = centerX - (_spriteManager.InventoryPlayerHealthBarEmpty.Width / 2);
                    spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarEmpty, new Vector2(barX, currentY), Color.White);

                    if (isOccupied && _spriteManager.InventoryPlayerHealthBarFull != null)
                    {
                        float hpPercent = (float)member!.CurrentHP / Math.Max(1, member.MaxHP);
                        int visibleWidth = (int)(_spriteManager.InventoryPlayerHealthBarFull.Width * hpPercent);
                        var srcRect = new Rectangle(0, 0, visibleWidth, _spriteManager.InventoryPlayerHealthBarFull.Height);
                        spriteBatch.DrawSnapped(_spriteManager.InventoryPlayerHealthBarFull, new Vector2(barX + 1, currentY), srcRect, Color.White);
                    }

                    string hpValText = isOccupied ? $"{member!.CurrentHP}/{member.MaxHP}" : "0/0";
                    string hpSuffix = " HP";

                    var valSize = secondaryFont.MeasureString(hpValText);
                    var suffixSize = secondaryFont.MeasureString(hpSuffix);

                    float hpTextX = centerX - ((valSize.Width + suffixSize.Width) / 2f);
                    float hpTextY = currentY + 7;

                    Color hpValColor = isOccupied ? _global.Palette_BrightWhite : _global.Palette_DarkGray;
                    spriteBatch.DrawStringSnapped(secondaryFont, hpValText, new Vector2(hpTextX, hpTextY), hpValColor);
                    spriteBatch.DrawStringSnapped(secondaryFont, hpSuffix, new Vector2(hpTextX + valSize.Width, hpTextY), _global.Palette_Gray);
                    currentY += 8 + (int)valSize.Height + 4 - 3;
                }

                // 5. Stats (REMOVED)
            }

            // Draw Action Buttons
            foreach (var btn in _actionButtons)
            {
                btn.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            }

            _confirmButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            _skipButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                foreach (var rect in _partyMemberPanelAreas)
                {
                    spriteBatch.DrawSnapped(pixel, rect, Color.Blue * 0.2f);
                }
                foreach (var btn in _actionButtons)
                {
                    spriteBatch.DrawSnapped(pixel, btn.Bounds, Color.Green * 0.5f);
                }
            }
        }

        public void DrawDialogOverlay(SpriteBatch spriteBatch)
        {
            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.DrawOverlay(spriteBatch);
            }
        }

        public void DrawDialogContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_confirmationDialog.IsActive)
            {
                // Draw in screen space (Matrix.Identity)
                _confirmationDialog.DrawContent(spriteBatch, font, gameTime, Matrix.Identity);
            }
        }
    }
}