using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle.UI
{
    public class SwitchMenu
    {
        public event Action<BattleCombatant>? OnMemberSelected;
        public event Action? OnBack;

        private bool _isVisible;
        private List<BattleCombatant> _benchMembers = new List<BattleCombatant>();
        private List<Button> _memberButtons = new List<Button>();
        private Button _backButton;
        private readonly Global _global;

        // Layout Constants
        private const int BUTTON_WIDTH = 140;
        private const int BUTTON_HEIGHT = 15;
        private const int BUTTON_SPACING = 2;
        private const int MENU_WIDTH = 160;
        private const int MENU_HEIGHT = 100; // Approximate

        /// <summary>
        /// If true, the menu is in "Forced Switch" mode (e.g. Disengage), and the Back button is disabled.
        /// </summary>
        public bool IsForced { get; set; } = false;

        public SwitchMenu()
        {
            _global = ServiceLocator.Get<Global>();
            _backButton = new Button(Rectangle.Empty, "BACK", enableHoverSway: false) { CustomDefaultTextColor = _global.Palette_DarkShadow };
            _backButton.OnClick += () =>
            {
                if (!IsForced) OnBack?.Invoke();
            };
        }

        public void Show(List<BattleCombatant> allCombatants, List<BattleCombatant> excludedMembers = null)
        {
            _isVisible = true;
            _benchMembers = allCombatants
                .Where(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2)
                .ToList();

            if (excludedMembers != null)
            {
                _benchMembers = _benchMembers.Except(excludedMembers).ToList();
            }

            InitializeButtons();
        }

        public void Hide()
        {
            _isVisible = false;
            IsForced = false; // Reset forced state on hide
        }

        private void InitializeButtons()
        {
            _memberButtons.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            int startY = 128; // Align with ActionMenu area
            int centerX = Global.VIRTUAL_WIDTH / 2;

            for (int i = 0; i < _benchMembers.Count; i++)
            {
                var member = _benchMembers[i];
                string text = $"{member.Name.ToUpper()}  HP:{member.Stats.CurrentHP}/{member.Stats.MaxHP}";

                var button = new Button(
                    new Rectangle(centerX - BUTTON_WIDTH / 2, startY + i * (BUTTON_HEIGHT + BUTTON_SPACING), BUTTON_WIDTH, BUTTON_HEIGHT),
                    text,
                    font: secondaryFont,
                    enableHoverSway: false
                );

                button.OnClick += () => OnMemberSelected?.Invoke(member);
                _memberButtons.Add(button);
            }

            // Position Back Button below list
            int backY = startY + (_benchMembers.Count * (BUTTON_HEIGHT + BUTTON_SPACING)) + 5;
            // Ensure it doesn't go off screen, clamp to standard back button position
            backY = Math.Max(backY, 165);

            var backSize = secondaryFont.MeasureString("BACK");
            int backWidth = (int)backSize.Width + 16;
            _backButton.Bounds = new Rectangle(centerX - backWidth / 2, backY, backWidth, 15);
            _backButton.Font = secondaryFont;
            _backButton.IsEnabled = !IsForced; // Disable if forced
        }

        public void Update(MouseState currentMouseState)
        {
            if (!_isVisible) return;

            foreach (var button in _memberButtons)
            {
                button.Update(currentMouseState);
            }

            if (!IsForced)
            {
                _backButton.Update(currentMouseState);
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!_isVisible) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            // Draw Background (Opaque Black)
            const int dividerY = 123;
            var bgRect = new Rectangle(0, dividerY, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - dividerY);
            spriteBatch.DrawSnapped(pixel, bgRect, _global.Palette_Black);

            // Draw Border
            if (spriteManager.BattleBorderSwitch != null)
            {
                spriteBatch.DrawSnapped(spriteManager.BattleBorderSwitch, Vector2.Zero, Color.White);
            }

            // Draw Title
            string title = IsForced ? "CHOOSE REPLACEMENT" : "SWITCH MEMBER";
            var titleSize = secondaryFont.MeasureString(title);
            var titlePos = new Vector2((Global.VIRTUAL_WIDTH - titleSize.Width) / 2, dividerY + 4);
            spriteBatch.DrawStringSnapped(secondaryFont, title, titlePos, _global.Palette_Blue);

            // Draw Buttons
            foreach (var button in _memberButtons)
            {
                button.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }

            if (!IsForced)
            {
                _backButton.Draw(spriteBatch, font, gameTime, Matrix.Identity);
            }
        }
    }
}
﻿