using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Battle.UI
{
    public class SwitchMenu
    {
        public event Action<BattleCombatant>? OnMemberSelected;
        public event Action? OnBack;

        private bool _isVisible;
        public bool IsVisible => _isVisible;

        private List<Button> _buttons = new List<Button>();
        private Button _backButton;
        private readonly Global _global;
        private int _activeSlotIndex = -1;

        private const int PANEL_WIDTH = 140;
        private const int MOVE_BTN_HEIGHT = 9;
        private const int ACTION_BTN_HEIGHT = 8;
        private const int HITBOX_PADDING = 1;

        public bool IsForced { get; set; } = false;

        public SwitchMenu()
        {
            _global = ServiceLocator.Get<Global>();
        }

        public void Show(int slotIndex, List<BattleCombatant> allCombatants, List<BattleCombatant> reservedMembers)
        {
            _isVisible = true;
            _activeSlotIndex = slotIndex;
            InitializeButtons(allCombatants, reservedMembers);
        }

        public void Hide()
        {
            _isVisible = false;
            IsForced = false;
            _activeSlotIndex = -1;
        }

        private void InitializeButtons(List<BattleCombatant> allCombatants, List<BattleCombatant> reservedMembers)
        {
            _buttons.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;
            var global = ServiceLocator.Get<Global>();

            var battleManager = ServiceLocator.Get<BattleManager>();
            var activePlayers = battleManager.AllCombatants.Where(c => c.IsPlayerControlled && c.IsActiveOnField).ToList();
            bool isCentered = activePlayers.Count == 1;

            var area = BattleLayout.GetActionMenuArea(_activeSlotIndex);
            int panelHeight = 49;
            int startX = area.Center.X - (PANEL_WIDTH / 2);
            int startY = area.Center.Y - (panelHeight / 2);

            int currentY = startY;
            var gameState = ServiceLocator.Get<GameState>();
            var partyList = gameState.PlayerState.Party;

            for (int i = 0; i < 4; i++)
            {
                string label = "---";
                bool enabled = false;
                BattleCombatant? member = null;

                if (i < partyList.Count)
                {
                    var partyMember = partyList[i];
                    member = allCombatants.FirstOrDefault(c => c.IsPlayerControlled && c.Name == partyMember.Name);

                    if (member != null)
                    {
                        label = member.Name.ToUpper();
                        bool isActive = member.IsActiveOnField;
                        bool isDefeated = member.IsDefeated;
                        bool isReserved = reservedMembers != null && reservedMembers.Contains(member);

                        if (!isActive && !isDefeated && !isReserved)
                        {
                            enabled = true;
                        }
                    }
                }

                var btn = new TextOverImageButton(
                    new Rectangle(startX, currentY, PANEL_WIDTH, MOVE_BTN_HEIGHT),
                    label,
                    null,
                    font: secondaryFont,
                    enableHoverSway: false, // FIX: Disabled hover sway to prevent text moving up
                    iconTexture: null
                )
                {
                    CustomDefaultTextColor = enabled ? global.GameTextColor : global.Palette_DarkShadow,
                    CustomHoverTextColor = global.ButtonHoverColor,
                    IsEnabled = enabled,
                    AlignLeft = false,
                    ContentXOffset = 0,
                    TextRenderOffset = Vector2.Zero // FIX: Moved down 1px (was 0, -1)
                };

                if (member != null && enabled)
                {
                    var targetMember = member;
                    btn.OnClick += () => OnMemberSelected?.Invoke(targetMember);
                }

                _buttons.Add(btn);
                currentY += MOVE_BTN_HEIGHT + HITBOX_PADDING;
            }

            int backWidth = 50;
            int backX = startX + (PANEL_WIDTH - backWidth) / 2;

            _backButton = new Button(
                new Rectangle(backX, currentY, backWidth, ACTION_BTN_HEIGHT),
                "BACK",
                font: tertiaryFont
            )
            {
                CustomDefaultTextColor = global.GameTextColor,
                CustomHoverTextColor = global.ButtonHoverColor,
                IsEnabled = !IsForced
            };

            _backButton.OnClick += () =>
            {
                if (!IsForced) OnBack?.Invoke();
            };
        }

        public void Update(MouseState currentMouseState)
        {
            if (!_isVisible) return;

            foreach (var button in _buttons)
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
            var global = ServiceLocator.Get<Global>();

            for (int i = 0; i < _buttons.Count; i++)
            {
                var btn = _buttons[i];
                var rect = btn.Bounds;
                var visualRect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
                visualRect.Height -= HITBOX_PADDING;

                Color bgColor;
                if (!btn.IsEnabled) bgColor = global.Palette_Black;
                else if (btn.IsHovered) bgColor = global.Palette_Rust;
                else bgColor = global.Palette_DarkestPale;

                DrawBeveledBackground(spriteBatch, pixel, visualRect, bgColor);
                btn.Draw(spriteBatch, btn.Font, gameTime, Matrix.Identity);
            }

            if (!IsForced)
            {
                var rect = _backButton.Bounds;
                var visualRect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
                visualRect.Height -= HITBOX_PADDING;

                Color bgColor = _backButton.IsHovered ? global.Palette_Rust : global.Palette_DarkShadow;
                DrawBeveledBackground(spriteBatch, pixel, visualRect, bgColor);
                _backButton.Draw(spriteBatch, _backButton.Font, gameTime, Matrix.Identity);
            }
        }

        private void DrawBeveledBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
        {
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Y, rect.Width - 4, 1), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 2, rect.Bottom - 1, rect.Width - 4, 1), color);
            spriteBatch.DrawSnapped(pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2), color);
        }
    }
}
