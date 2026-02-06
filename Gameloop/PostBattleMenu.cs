using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class PostBattleMenu
    {
        private bool _isVisible;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly Global _global;
        private BitmapFont _font;
        private readonly Texture2D _pixelTexture;

        // Layout constants
        private const int PANEL_WIDTH = 240;
        private const int PANEL_HEIGHT = 80;
        private const int BUTTON_WIDTH = 70;
        private const int BUTTON_HEIGHT = 24;
        private const int BUTTON_SPACING = 10;

        public event Action OnComplete;

        public PostBattleMenu()
        {
            _global = ServiceLocator.Get<Global>();
            // Font is loaded in LoadContent, so we cannot retrieve it here during Initialize.
            // It will be retrieved lazily.
            _pixelTexture = ServiceLocator.Get<Texture2D>();
            InitializeButtons();
        }

        private void InitializeButtons()
        {
            int startX = (Global.VIRTUAL_WIDTH - PANEL_WIDTH) / 2;
            int startY = (Global.VIRTUAL_HEIGHT - PANEL_HEIGHT) / 2;

            // Center the buttons horizontally within the panel
            int totalButtonWidth = (BUTTON_WIDTH * 3) + (BUTTON_SPACING * 2);
            int buttonStartX = (Global.VIRTUAL_WIDTH - totalButtonWidth) / 2;
            int buttonY = startY + (PANEL_HEIGHT - BUTTON_HEIGHT) / 2 + 10; // Slightly offset down for title space

            // REST Button
            var restBtn = new Button(
                new Rectangle(buttonStartX, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT),
                "REST",
                "RestParty",
                font: null // Will be set lazily
            );
            restBtn.OnClick = OnRestClicked;
            _buttons.Add(restBtn);

            // RECRUIT Button
            var recruitBtn = new Button(
                new Rectangle(buttonStartX + BUTTON_WIDTH + BUTTON_SPACING, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT),
                "RECRUIT",
                "RecruitMember",
                font: null // Will be set lazily
            );
            recruitBtn.OnClick = OnRecruitClicked;
            _buttons.Add(recruitBtn);

            // SKIP Button
            var skipBtn = new Button(
                new Rectangle(buttonStartX + (BUTTON_WIDTH + BUTTON_SPACING) * 2, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT),
                "SKIP",
                "SkipPostBattle",
                font: null // Will be set lazily
            );
            skipBtn.OnClick = OnSkipClicked;
            _buttons.Add(skipBtn);
        }

        private void EnsureFontLoaded()
        {
            if (_font == null)
            {
                _font = ServiceLocator.Get<Core>().SecondaryFont;
                if (_font != null)
                {
                    foreach (var btn in _buttons)
                    {
                        btn.Font = _font;
                    }
                }
            }
        }

        public void Show()
        {
            _isVisible = true;
            EnsureFontLoaded();

            // Update Recruit button state based on party size and available candidates
            var gameState = ServiceLocator.Get<GameState>();
            var recruitBtn = _buttons.Find(b => b.Text == "RECRUIT");

            if (recruitBtn != null)
            {
                var candidates = GetRecruitableMemberIds(gameState.PlayerState);
                bool isPartyFull = gameState.PlayerState.Party.Count >= 4;
                bool hasCandidates = candidates.Count > 0;

                recruitBtn.IsEnabled = !isPartyFull && hasCandidates;
            }
        }

        public void Hide()
        {
            _isVisible = false;
        }

        public void Update(GameTime gameTime, MouseState mouseState)
        {
            if (!_isVisible) return;

            foreach (var btn in _buttons)
            {
                btn.Update(mouseState);
            }
        }

        public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
        {
            if (!_isVisible) return;
            EnsureFontLoaded();

            // Draw Panel Background
            int x = (Global.VIRTUAL_WIDTH - PANEL_WIDTH) / 2;
            int y = (Global.VIRTUAL_HEIGHT - PANEL_HEIGHT) / 2;
            var rect = new Rectangle(x, y, PANEL_WIDTH, PANEL_HEIGHT);

            // Background
            spriteBatch.DrawSnapped(_pixelTexture, rect, _global.Palette_Black);

            // Border
            DrawBorder(spriteBatch, rect, _global.Palette_LightGray, 1);

            // Title
            string title = "VICTORY - CHOOSE ACTION";
            if (_font != null)
            {
                Vector2 titleSize = _font.MeasureString(title);
                Vector2 titlePos = new Vector2(
                    x + (PANEL_WIDTH - titleSize.X) / 2,
                    y + 8
                );
                spriteBatch.DrawStringSnapped(_font, title, titlePos, _global.Palette_Sun);
            }

            // Buttons
            foreach (var btn in _buttons)
            {
                // Pass _font explicitly in case Button.Font is still null for some reason, 
                // though EnsureFontLoaded should have handled it.
                btn.Draw(spriteBatch, _font, gameTime, Matrix.Identity);
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            // Top
            spriteBatch.DrawSnapped(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            spriteBatch.DrawSnapped(_pixelTexture, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.DrawSnapped(_pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            spriteBatch.DrawSnapped(_pixelTexture, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        private void OnRestClicked()
        {
            var playerState = ServiceLocator.Get<GameState>().PlayerState;
            foreach (var member in playerState.Party)
            {
                member.CurrentHP = member.MaxHP;
            }
            Complete();
        }

        private void OnRecruitClicked()
        {
            var gameState = ServiceLocator.Get<GameState>();
            var candidates = GetRecruitableMemberIds(gameState.PlayerState);

            if (candidates.Count > 0 && gameState.PlayerState.Party.Count < 4)
            {
                var rng = new Random();
                string selectedId = candidates[rng.Next(candidates.Count)];

                var newMember = PartyMemberFactory.CreateMember(selectedId);
                if (newMember != null)
                {
                    gameState.PlayerState.AddPartyMember(newMember);
                }
            }
            Complete();
        }

        private void OnSkipClicked()
        {
            Complete();
        }

        private void Complete()
        {
            Hide();
            OnComplete?.Invoke();
        }

        private List<string> GetRecruitableMemberIds(PlayerState playerState)
        {
            // Filter out any ID that is in PastMemberIds (which includes current party members)
            return BattleDataCache.PartyMembers.Keys
                .Where(id => !playerState.PastMemberIds.Contains(id))
                .ToList();
        }
    }
}