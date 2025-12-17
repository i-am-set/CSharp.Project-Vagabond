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

        // --- TUNING: Logic ---
        private const float HEAL_PERCENT_REST = 0.75f;
        private const float HEAL_PERCENT_TRAIN = 0.0f;
        private const float HEAL_PERCENT_SEARCH = 0.0f;
        private const float HEAL_PERCENT_GUARD = 0.0f;

        private const float GUARD_HEAL_MULTIPLIER = 2.0f;

        // Search Tuning
        private const int SEARCH_CHANCE_UNGUARDED = 50;
        private const int SEARCH_CHANCE_GUARDED = 90;

        // Train Tuning
        private const int TRAIN_AMOUNT_UNGUARDED = 1;
        private const int TRAIN_AMOUNT_GUARDED_MAJOR = 2;
        private const int TRAIN_AMOUNT_GUARDED_MINOR = 1;

        // --- TUNING: Colors ---
        private readonly Color COLOR_DESC_REST_NORMAL;
        private readonly Color COLOR_DESC_REST_GUARDED;
        private readonly Color COLOR_DESC_TRAIN_NORMAL;
        private readonly Color COLOR_DESC_TRAIN_GUARDED;
        private readonly Color COLOR_DESC_SEARCH_NORMAL;
        private readonly Color COLOR_DESC_SEARCH_GUARDED;
        private readonly Color COLOR_DESC_GUARD;

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

            // Initialize Tunable Colors
            COLOR_DESC_REST_NORMAL = _global.Palette_LightGreen;
            COLOR_DESC_REST_GUARDED = Color.Lime;
            COLOR_DESC_TRAIN_NORMAL = _global.Palette_LightPurple;
            COLOR_DESC_TRAIN_GUARDED = Color.Magenta;
            COLOR_DESC_SEARCH_NORMAL = _global.Palette_LightBlue;
            COLOR_DESC_SEARCH_GUARDED = Color.Aqua;
            COLOR_DESC_GUARD = _global.Palette_DarkGray;

            _confirmationDialog = new ConfirmationDialog(parentScene);

            _confirmButton = new Button(Rectangle.Empty, "CONFIRM", font: _core.SecondaryFont)
            {
                CustomDefaultTextColor = _global.Palette_BrightWhite,
                CustomHoverTextColor = _global.Palette_Red,
                UseScreenCoordinates = true
            };
            // Direct execution for Confirm, no dialog
            _confirmButton.OnClick += ExecuteRest;

            // Skip button uses Tertiary font
            _skipButton = new Button(Rectangle.Empty, "SKIP", font: _core.TertiaryFont)
            {
                CustomDefaultTextColor = _global.Palette_LightGray,
                CustomHoverTextColor = _global.Palette_Red,
                UseScreenCoordinates = true
            };
            // Skip still requires confirmation
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

            int margin = 3;
            int buttonY = screenBottom - BUTTON_HEIGHT - margin;

            // Confirm Button (Centered)
            var font = _core.SecondaryFont;
            var confirmSize = font.MeasureString("CONFIRM");
            int confirmWidth = (int)confirmSize.Width + 16;
            _confirmButton.Bounds = new Rectangle((Global.VIRTUAL_WIDTH - confirmWidth) / 2, buttonY, confirmWidth, BUTTON_HEIGHT);

            // Skip Button (Bottom Right)
            var skipFont = _core.TertiaryFont; // Using Tertiary Font
            var skipSize = skipFont.MeasureString("SKIP");
            int skipWidth = (int)skipSize.Width + 16;
            _skipButton.Bounds = new Rectangle(Global.VIRTUAL_WIDTH - skipWidth - 10, buttonY, skipWidth, BUTTON_HEIGHT);

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
            int startY = panelRect.Bottom - (4 * (buttonHeight + spacing)) - 5 - 9;
            int centerX = panelRect.Center.X;

            // Helper to create toggle buttons
            void AddBtn(string text, RestAction action)
            {
                var btn = new ToggleButton(
                    new Rectangle(centerX - buttonWidth / 2, startY, buttonWidth, buttonHeight),
                    text,
                    font: _core.SecondaryFont,
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
            _hapticsManager.TriggerHop(3f, 0.15f);
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
            var allRelics = BattleDataCache.Relics.Keys.ToList();

            // Process Each Member
            for (int i = 0; i < _gameState.PlayerState.Party.Count; i++)
            {
                var member = _gameState.PlayerState.Party[i];
                var action = _selectedActions[i];

                // Calculate multiplier for this member
                // Guarding members do not benefit from the guard multiplier (they are the source)
                float multiplier = (guardActive && action != RestAction.Guard) ? GUARD_HEAL_MULTIPLIER : 1.0f;

                switch (action)
                {
                    case RestAction.Rest:
                        {
                            int healAmount = (int)(member.MaxHP * HEAL_PERCENT_REST * multiplier);
                            int oldHP = member.CurrentHP;
                            member.CurrentHP = Math.Min(member.MaxHP, member.CurrentHP + healAmount);
                            int healed = member.CurrentHP - oldHP;
                            if (healed > 0) summary.AppendLine($"{member.Name} rested: +{healed} HP.");
                            else summary.AppendLine($"{member.Name} rested.");
                            break;
                        }

                    case RestAction.Train:
                        {
                            string[] stats = { "Strength", "Intelligence", "Tenacity", "Agility" };

                            if (guardActive)
                            {
                                // Guarded: +2 to one, +1 to another
                                int idx1 = _rng.Next(4);
                                int idx2;
                                do { idx2 = _rng.Next(4); } while (idx2 == idx1);

                                ApplyStatBoost(member, stats[idx1], TRAIN_AMOUNT_GUARDED_MAJOR);
                                ApplyStatBoost(member, stats[idx2], TRAIN_AMOUNT_GUARDED_MINOR);
                                summary.AppendLine($"{member.Name} trained (Guarded): +{TRAIN_AMOUNT_GUARDED_MAJOR} {stats[idx1].Substring(0, 3)}, +{TRAIN_AMOUNT_GUARDED_MINOR} {stats[idx2].Substring(0, 3)}.");
                            }
                            else
                            {
                                // Unguarded: +1 to one
                                int idx1 = _rng.Next(4);
                                ApplyStatBoost(member, stats[idx1], TRAIN_AMOUNT_UNGUARDED);
                                summary.AppendLine($"{member.Name} trained: +{TRAIN_AMOUNT_UNGUARDED} {stats[idx1].Substring(0, 3)}.");
                            }
                            break;
                        }

                    case RestAction.Search:
                        {
                            int chance = guardActive ? SEARCH_CHANCE_GUARDED : SEARCH_CHANCE_UNGUARDED;

                            if (_rng.Next(0, 100) < chance)
                            {
                                if (allRelics.Any())
                                {
                                    string relicId = allRelics[_rng.Next(allRelics.Count)];
                                    var relic = BattleDataCache.Relics[relicId];
                                    _gameState.PlayerState.AddRelic(relicId);
                                    summary.AppendLine($"[palette_teal]{member.Name} found Relic: {relic.RelicName}![/]");
                                }
                                else
                                {
                                    summary.AppendLine($"{member.Name} searched but found nothing (Empty DB).");
                                }
                            }
                            else
                            {
                                summary.AppendLine($"{member.Name} searched but found nothing.");
                            }
                            break;
                        }

                    case RestAction.Guard:
                        {
                            summary.AppendLine($"{member.Name} stood guard.");
                            break;
                        }
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
            var tertiaryFont = _core.TertiaryFont;

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
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.X) / 2, WORLD_Y_OFFSET + 10);
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_BrightWhite);

            // Check if anyone is guarding to calculate potential heal multiplier
            bool guardActive = _selectedActions.Values.Any(a => a == RestAction.Guard);

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

                    // Removed bob logic (bobOffset = 0)
                    var destRect = new Rectangle(centerX - 16, currentY, 32, 32);
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

                    // --- NEW: Draw Action Description ---
                    if (isOccupied && _selectedActions.TryGetValue(i, out var action))
                    {
                        string descText = "";
                        Color descColor = _global.Palette_White;

                        // Determine multiplier (Guard doesn't buff itself)
                        float multiplier = (guardActive && action != RestAction.Guard) ? GUARD_HEAL_MULTIPLIER : 1.0f;

                        switch (action)
                        {
                            case RestAction.Rest:
                                int finalPercent = (int)(HEAL_PERCENT_REST * multiplier * 100);
                                descText = $"+{finalPercent}% HP";
                                if (guardActive)
                                {
                                    descColor = COLOR_DESC_REST_GUARDED;
                                }
                                else
                                {
                                    descColor = COLOR_DESC_REST_NORMAL;
                                }
                                break;

                            case RestAction.Train:
                                if (guardActive)
                                {
                                    descText = $"+{TRAIN_AMOUNT_GUARDED_MAJOR} STAT\n+{TRAIN_AMOUNT_GUARDED_MINOR} STAT";
                                    descColor = COLOR_DESC_TRAIN_GUARDED;
                                }
                                else
                                {
                                    descText = $"+{TRAIN_AMOUNT_UNGUARDED} STAT";
                                    descColor = COLOR_DESC_TRAIN_NORMAL;
                                }
                                break;

                            case RestAction.Search:
                                int chance = guardActive ? SEARCH_CHANCE_GUARDED : SEARCH_CHANCE_UNGUARDED;
                                descText = $"{chance}% RELIC";
                                descColor = guardActive ? COLOR_DESC_SEARCH_GUARDED : COLOR_DESC_SEARCH_NORMAL;
                                break;

                            case RestAction.Guard:
                                descText = "+MODIFIER";
                                descColor = COLOR_DESC_GUARD;
                                break;
                        }

                        // Split by newline
                        var lines = descText.Split('\n');

                        // --- VERTICAL CENTERING LOGIC ---
                        // Calculate the area between the HP text and the buttons
                        // Button layout constants from CreateActionButtonsForMember:
                        // buttonHeight = 10, spacing = 1, 4 buttons.
                        // startY = panelRect.Bottom - (4 * 11) - 5 - 9;
                        float buttonsTopY = bounds.Bottom - (4 * 11) - 14;

                        float textTopBoundary = hpTextY + secondaryFont.LineHeight + 2; // +2 padding from HP text
                        float availableHeight = buttonsTopY - textTopBoundary;

                        // Calculate total text height
                        // LineHeight + 1 pixel spacing per line
                        float totalTextHeight = lines.Length * (secondaryFont.LineHeight + 1) - 1;

                        // Center it
                        float startDescY = textTopBoundary + (availableHeight - totalTextHeight) / 2f;

                        // Clamp to ensure it doesn't overlap if space is too tight
                        if (startDescY < textTopBoundary) startDescY = textTopBoundary;

                        float descY = startDescY;

                        foreach (var line in lines)
                        {
                            var lineSize = secondaryFont.MeasureString(line);
                            float lineX = centerX - (lineSize.Width / 2f);

                            // --- ANIMATION: Bob Logic ---
                            float bobOffset = 0f;
                            // Only bob if it's a positive effect (Rest or Train/Search)
                            // Guard is static
                            if (action != RestAction.Guard)
                            {
                                float speed = 5f;
                                // Sine wave 0..1
                                float t = (MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * speed) + 1f) * 0.5f;
                                // Round to 0 or 1
                                float val = MathF.Round(t);
                                // Invert to move UP
                                bobOffset = -val;
                            }

                            // IMPORTANT: Round the base Y first, then add the integer bob offset.
                            // This prevents sub-pixel centering from eating the 1-pixel animation.
                            float finalY = MathF.Round(descY) + bobOffset;

                            spriteBatch.DrawStringSnapped(secondaryFont, line, new Vector2(lineX, finalY), descColor);
                            descY += secondaryFont.LineHeight + 1; // Spacing between lines
                        }
                    }

                    currentY += 8 + (int)valSize.Height + 4 - 3;
                }
            }

            // Draw Action Buttons
            foreach (var btn in _actionButtons)
            {
                btn.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            }

            _confirmButton.Draw(spriteBatch, secondaryFont, gameTime, Matrix.Identity);
            _skipButton.Draw(spriteBatch, tertiaryFont, gameTime, Matrix.Identity); // Use Tertiary Font

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