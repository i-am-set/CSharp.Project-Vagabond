using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    public class RadioGroup
    {
        public List<ToggleButton> Buttons { get; } = new List<ToggleButton>();
        private int _selectedIndex = -1;

        public event Action<ToggleButton> OnSelectionChanged;

        public RadioGroup(int defaultIndex = 0)
        {
            _selectedIndex = defaultIndex;
        }

        public void AddButton(ToggleButton button)
        {
            int buttonIndex = Buttons.Count;
            button.OnClick += () => SetSelectedIndex(buttonIndex);
            Buttons.Add(button);

            if (buttonIndex == _selectedIndex)
            {
                button.IsSelected = true;
            }
        }

        public void SetSelectedIndex(int index)
        {
            if (index < 0 || index >= Buttons.Count || index == _selectedIndex)
            {
                return;
            }

            if (_selectedIndex != -1)
            {
                Buttons[_selectedIndex].IsSelected = false;
            }

            _selectedIndex = index;
            Buttons[_selectedIndex].IsSelected = true;

            OnSelectionChanged?.Invoke(Buttons[_selectedIndex]);
        }

        public void CycleNext()
        {
            if (Buttons.Count == 0) return;

            int nextIndex = (_selectedIndex + 1) % Buttons.Count;
            SetSelectedIndex(nextIndex);
        }

        public ToggleButton GetSelectedButton()
        {
            if (_selectedIndex >= 0 && _selectedIndex < Buttons.Count)
            {
                return Buttons[_selectedIndex];
            }
            return null;
        }

        public void Update(MouseState currentMouseState)
        {
            foreach (var button in Buttons)
            {
                button.Update(currentMouseState);
            }
        }

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            foreach (var button in Buttons)
            {
                button.Draw(spriteBatch, font, gameTime);
            }
        }
    }
}