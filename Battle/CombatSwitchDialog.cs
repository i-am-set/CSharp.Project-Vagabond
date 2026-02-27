using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.UI
{
    public class CombatSwitchDialog : Dialog
    {
        public event Action<BattleCombatant> OnMemberSelected;

        private List<BattleCombatant> _benchMembers = new List<BattleCombatant>();
        private List<Button> _memberButtons = new List<Button>();
        private int _selectedIndex = 0;
        private int? _targetSlotIndex = null;

        private const int DIALOG_WIDTH = 180;
        private const int BUTTON_HEIGHT = 15;
        private const int BUTTON_SPACING = 2;
        private const int PADDING = 10;

        public bool IsMandatory { get; set; } = false;

        public CombatSwitchDialog(GameScene scene) : base(scene) { }

        public void Show(List<BattleCombatant> allCombatants, int? slotIndex = null)
        {
            IsActive = true;
            _targetSlotIndex = slotIndex;
            _benchMembers = allCombatants
                .Where(c => c.IsPlayerControlled && !c.IsDefeated && c.BattleSlot >= 2)
                .ToList();

            InitializeButtons();

            _selectedIndex = 0;
            if (_memberButtons.Any())
            {
                var firstBtn = _memberButtons[0];
                Point screenPos = Core.TransformVirtualToScreen(firstBtn.Bounds.Center);
                Mouse.SetPosition(screenPos.X, screenPos.Y);
            }
        }

        private void InitializeButtons()
        {
            _memberButtons.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            int totalContentHeight = (_benchMembers.Count * (BUTTON_HEIGHT + BUTTON_SPACING)) + 20;
            int dialogHeight = totalContentHeight + (PADDING * 2);

            _dialogBounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - DIALOG_WIDTH) / 2,
                (Global.VIRTUAL_HEIGHT - dialogHeight) / 2,
                DIALOG_WIDTH,
                dialogHeight
            );

            int startY = _dialogBounds.Y + PADDING + 20;
            int buttonWidth = DIALOG_WIDTH - (PADDING * 2);
            int buttonX = _dialogBounds.X + PADDING;

            for (int i = 0; i < _benchMembers.Count; i++)
            {
                var member = _benchMembers[i];
                string text = $"{member.Name.ToUpper()}  HP:{member.Stats.CurrentHP}/{member.Stats.MaxHP}";

                var button = new Button(
                    new Rectangle(buttonX, startY + i * (BUTTON_HEIGHT + BUTTON_SPACING), buttonWidth, BUTTON_HEIGHT),
                    text,
                    font: secondaryFont,
                    enableHoverSway: false
                );

                button.OnClick += () =>
                {
                    OnMemberSelected?.Invoke(member);
                    Hide();
                };

                _memberButtons.Add(button);
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            var currentMouseState = Mouse.GetState();
            var inputManager = ServiceLocator.Get<InputManager>();

            for (int i = 0; i < _memberButtons.Count; i++)
            {
                _memberButtons[i].Update(currentMouseState);
                if (_memberButtons[i].IsHovered)
                {
                    _selectedIndex = i;
                }
            }

            if (inputManager.NavigateUp)
            {
                _selectedIndex = (_selectedIndex - 1 + _memberButtons.Count) % _memberButtons.Count;
                SnapMouseToSelection();
            }
            else if (inputManager.NavigateDown)
            {
                _selectedIndex = (_selectedIndex + 1) % _memberButtons.Count;
                SnapMouseToSelection();
            }

            if (inputManager.Confirm)
            {
                if (_selectedIndex >= 0 && _selectedIndex < _memberButtons.Count)
                {
                    _memberButtons[_selectedIndex].TriggerClick();
                }
            }

            if (!IsMandatory && inputManager.Back)
            {
                Hide();
            }

            _previousMouseState = currentMouseState;
        }

        private void SnapMouseToSelection()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _memberButtons.Count)
            {
                var btn = _memberButtons[_selectedIndex];
                Point screenPos = Core.TransformVirtualToScreen(btn.Bounds.Center);
                Mouse.SetPosition(screenPos.X, screenPos.Y);
            }
        }

        public override void DrawContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (!IsActive) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            spriteBatch.DrawSnapped(pixel, _dialogBounds, _global.Palette_Black);
            DrawRectangleBorder(spriteBatch, pixel, _dialogBounds, 1, _global.Palette_Sun);

            string title = IsMandatory ? "REINFORCEMENT NEEDED" : "CHOOSE REPLACEMENT";
            if (IsMandatory && _targetSlotIndex.HasValue)
            {
                title += $" (SLOT {_targetSlotIndex.Value + 1})";
            }

            var titleSize = secondaryFont.MeasureString(title);
            var titlePos = new Vector2(
                _dialogBounds.Center.X - titleSize.Width / 2,
                _dialogBounds.Y + PADDING
            );
            spriteBatch.DrawStringSnapped(secondaryFont, title, titlePos, _global.Palette_Sky);

            foreach (var button in _memberButtons)
            {
                button.Draw(spriteBatch, font, gameTime, transform);
            }
        }
    }
}