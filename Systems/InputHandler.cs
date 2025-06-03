using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectVagabond
{
    public class InputHandler
    {
        private KeyboardState _previousKeyboardState;
        private KeyboardState _currentKeyboardState;
        private string _currentInput = "";
        private bool _hasNewCommand = false;
        private string _completedCommand = "";

        public string CurrentInput => _currentInput;
        public bool HasNewCommand => _hasNewCommand;

        public void Update()
        {
            _previousKeyboardState = _currentKeyboardState;
            _currentKeyboardState = Keyboard.GetState();

            // Handle text input
            var pressedKeys = _currentKeyboardState.GetPressedKeys()
                .Where(key => !_previousKeyboardState.IsKeyDown(key));

            foreach (var key in pressedKeys)
            {
                if (key == Keys.Enter)
                {
                    if (!string.IsNullOrWhiteSpace(_currentInput))
                    {
                        _completedCommand = _currentInput;
                        _hasNewCommand = true;
                        _currentInput = "";
                    } else
                    {
                        _completedCommand = "";
                        _hasNewCommand = true;
                        _currentInput = "";
                    }
                }
                else if (key == Keys.Back)
                {
                    if (_currentInput.Length > 0)
                        _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
                }
                else if (key == Keys.Space)
                {
                    _currentInput += " ";
                }
                else
                {
                    // Convert key to character
                    string keyString = key.ToString();
                    if (keyString.Length == 1)
                    {
                        char c = keyString[0];
                        if (char.IsLetter(c))
                        {
                            if (_currentKeyboardState.IsKeyDown(Keys.LeftShift) || 
                                _currentKeyboardState.IsKeyDown(Keys.RightShift))
                                c = char.ToUpper(c);
                            else
                                c = char.ToLower(c);
                            _currentInput += c;
                        }
                    }
                    else if (keyString.StartsWith("D") && keyString.Length == 2 && char.IsDigit(keyString[1]))
                    {
                        _currentInput += keyString[1];
                    }
                }
            }
        }

        public string GetCommand()
        {
            return _completedCommand;
        }

        public void ClearCommand()
        {
            _hasNewCommand = false;
            _completedCommand = "";
        }
    }
}
