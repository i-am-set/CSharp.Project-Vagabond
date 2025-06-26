using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public class InputHandler
    {
        private string _currentInput = "";
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private HashSet<Keys> _processedKeys = new HashSet<Keys>();
        private bool _controlPressed = false;
        private bool _shiftPressed = false;
        private float _backspaceTimer = 0f;
        private float _backspaceInitialDelay = 0.3f;
        private bool _backspaceHeld = false;
        private int _cursorPosition;
        private string _clipboard = "";
        private List<string> _commandHistory = new List<string>();
        private int _commandHistoryIndex = -1;
        private string _currentEditingCommand = "";

        public string CurrentInput => _currentInput;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void SetCurrentInput(String input) => _currentInput = input;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void HandleInput(GameTime gameTime)
        {
            KeyboardState currentKeyboardState = Keyboard.GetState();
            MouseState currentMouseState = Mouse.GetState();
            Keys[] pressedKeys = currentKeyboardState.GetPressedKeys();

            _controlPressed = currentKeyboardState.IsKeyDown(Keys.LeftControl) || currentKeyboardState.IsKeyDown(Keys.RightControl);
            _shiftPressed = currentKeyboardState.IsKeyDown(Keys.LeftShift) || currentKeyboardState.IsKeyDown(Keys.RightShift);

            if (currentMouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
            {
                int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                int scrollLines = scrollDelta > 0 ? 3 : -3;

                int maxVisibleLines = Core.CurrentTerminalRenderer.GetMaxVisibleLines();
                int currentOffset = Core.CurrentTerminalRenderer.ScrollOffset;
                int maxOffset = Math.Max(0, Core.CurrentTerminalRenderer.WrappedHistory.Count - maxVisibleLines);

                if (scrollDelta > 0) // scrolling up
                {
                    Core.CurrentTerminalRenderer.SetScrollOffset(Math.Min(currentOffset + Math.Abs(scrollLines), maxOffset));
                }
                else // scrolling down
                {
                    Core.CurrentTerminalRenderer.SetScrollOffset(Math.Max(currentOffset - Math.Abs(scrollLines), 0));
                }
            }

            if (!_previousKeyboardState.IsKeyDown(Keys.Escape) && currentKeyboardState.IsKeyDown(Keys.Escape))
            {
                if (Core.CurrentGameState.IsExecutingPath)
                {
                    Core.CurrentGameState.CancelPathExecution();
                }
                else if (Core.CurrentGameState.IsFreeMoveMode)
                {
                    Core.CurrentGameState.ToggleIsFreeMoveMode(false);
                    _processedKeys.Clear();
                }
                else if (Core.CurrentGameState.PendingActions.Count > 0)
                {
                    Core.CurrentGameState.CancelPendingActions();
                    Core.CurrentTerminalRenderer.AddOutputToHistory("Pending actions cleared.");
                }
            }

            if (!_previousKeyboardState.IsKeyDown(Keys.M) && currentKeyboardState.IsKeyDown(Keys.M))
            {
                Core.CurrentGameState.ToggleMapView();
            }

            if (Core.CurrentGameState.IsFreeMoveMode)
            {
                Vector2 moveDir = Vector2.Zero;
                if (currentKeyboardState.IsKeyDown(Keys.W) || currentKeyboardState.IsKeyDown(Keys.Up)) moveDir.Y--;
                if (currentKeyboardState.IsKeyDown(Keys.S) || currentKeyboardState.IsKeyDown(Keys.Down)) moveDir.Y++;
                if (currentKeyboardState.IsKeyDown(Keys.A) || currentKeyboardState.IsKeyDown(Keys.Left)) moveDir.X--;
                if (currentKeyboardState.IsKeyDown(Keys.D) || currentKeyboardState.IsKeyDown(Keys.Right)) moveDir.X++;

                bool newMoveKeyPressed = false;
                Keys[] moveKeys = { Keys.W, Keys.A, Keys.S, Keys.D, Keys.Up, Keys.Down, Keys.Left, Keys.Right };
                foreach (var key in moveKeys)
                {
                    if (currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key))
                    {
                        newMoveKeyPressed = true;
                        break;
                    }
                }

                if (newMoveKeyPressed && moveDir != Vector2.Zero)
                {
                    string[] args = { "move", "1" };
                    if (_shiftPressed)
                    {
                        Core.CurrentGameState.QueueRunMovement(moveDir, args);
                    }
                    else
                    {
                        Core.CurrentGameState.QueueWalkMovement(moveDir, args);
                    }
                }
                else if (!_previousKeyboardState.IsKeyDown(Keys.Enter) && currentKeyboardState.IsKeyDown(Keys.Enter))
                {
                    if (Core.CurrentGameState.PendingActions.Count > 0 && !Core.CurrentGameState.IsExecutingPath)
                    {
                        Core.CurrentGameState.ToggleExecutingPath(true);
                        Core.CurrentGameState.SetCurrentPathIndex(0);
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"Executing queue of[undo] {Core.CurrentGameState.PendingActions.Count}[gray] action(s)...");
                    }
                    else if (Core.CurrentGameState.IsExecutingPath)
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory("Already executing an action queue.");
                    }
                    else
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory("No actions queued.");
                    }
                }
            }
            else
            {
                foreach (Keys key in pressedKeys)
                {
                    if (!_previousKeyboardState.IsKeyDown(key))
                    {
                        if (key == Keys.Enter)
                        {
                            Core.CurrentAutoCompleteManager.ToggleShowingAutoCompleteSuggestions(false);
                            if (string.IsNullOrEmpty(_currentInput.Trim()) && Core.CurrentGameState.PendingActions.Count > 0 && !Core.CurrentGameState.IsExecutingPath)
                            {
                                Core.CurrentGameState.ToggleExecutingPath(true);
                                Core.CurrentGameState.SetCurrentPathIndex(0);
                                Core.CurrentTerminalRenderer.AddOutputToHistory($"Executing queue with {Core.CurrentGameState.PendingActions.Count} actions...");
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(_currentInput.Trim()))
                                {
                                    _commandHistory.Add(_currentInput.Trim());
                                    if (_commandHistory.Count > 50)
                                    {
                                        _commandHistory.RemoveAt(0);
                                    }
                                }
                                ProcessCommand(_currentInput.Trim().ToLower());

                                _commandHistoryIndex = -1;
                                _currentEditingCommand = "";
                            }
                            _currentInput = "";
                            _cursorPosition = 0;
                        }
                        else if (key == Keys.Tab)
                        {
                            if (Core.CurrentAutoCompleteManager.ShowingAutoCompleteSuggestions && Core.CurrentAutoCompleteManager.SelectedAutoCompleteSuggestionIndex >= 0)
                            {
                                _currentInput = Core.CurrentAutoCompleteManager.AutoCompleteSuggestions[Core.CurrentAutoCompleteManager.SelectedAutoCompleteSuggestionIndex];
                                _cursorPosition = _currentInput.Length;
                                Core.CurrentAutoCompleteManager.ToggleShowingAutoCompleteSuggestions(false);
                            }
                        }
                        else if (key == Keys.Up && Core.CurrentAutoCompleteManager.ShowingAutoCompleteSuggestions)
                        {
                            Core.CurrentAutoCompleteManager.SetSelectedAutoCompleteSuggestionIndex(Math.Min(Core.CurrentAutoCompleteManager.AutoCompleteSuggestions.Count - 1, Core.CurrentAutoCompleteManager.SelectedAutoCompleteSuggestionIndex + 1));
                        }
                        else if (key == Keys.Down && Core.CurrentAutoCompleteManager.ShowingAutoCompleteSuggestions)
                        {
                            Core.CurrentAutoCompleteManager.SetSelectedAutoCompleteSuggestionIndex(Math.Max(0, Core.CurrentAutoCompleteManager.SelectedAutoCompleteSuggestionIndex - 1));
                        }
                        else if (key == Keys.Up)
                        {
                            NavigateCommandHistory(1);
                        }
                        else if (key == Keys.Down)
                        {
                            NavigateCommandHistory(-1);
                        }
                        else if (_controlPressed)
                        {
                            HandleControlCommands(key);
                        }
                        else if (_shiftPressed)
                        {
                            HandleShiftCommands(key);
                        }
                        else if (key == Keys.Back)
                        {
                            HandleBackspace();
                            Core.CurrentAutoCompleteManager.UpdateAutoCompleteSuggestions(_currentInput);
                            _backspaceHeld = true;
                            _backspaceTimer = 0f;
                            _backspaceInitialDelay = 0.3f;
                        }
                        else if (key == Keys.Delete)
                        {
                            if (_cursorPosition < _currentInput.Length)
                            {
                                _currentInput = _currentInput.Remove(_cursorPosition, 1);
                            }
                        }
                        else if (key == Keys.Home)
                        {
                            _cursorPosition = 0;
                        }
                        else if (key == Keys.End)
                        {
                            _cursorPosition = _currentInput.Length;
                        }
                        else if (key == Keys.Space)
                        {
                            _currentInput += " ";
                            _cursorPosition++;
                        }
                        else if (key == Keys.PageUp)
                        {
                            int maxVisibleLines = Core.CurrentTerminalRenderer.GetMaxVisibleLines(); // <-- FIX
                            Core.CurrentTerminalRenderer.SetScrollOffset(Math.Min(Core.CurrentTerminalRenderer.ScrollOffset + 5, Math.Max(0, Core.CurrentTerminalRenderer.WrappedHistory.Count - maxVisibleLines)));
                        }
                        else if (key == Keys.PageDown)
                        {
                            Core.CurrentTerminalRenderer.SetScrollOffset(Math.Max(Core.CurrentTerminalRenderer.ScrollOffset - 5, 0));
                        }
                        else
                        {
                            HandleCharacterInput(key);
                        }
                    }
                }

                if (_backspaceHeld && currentKeyboardState.IsKeyDown(Keys.Back)) // Handle held backspace with acceleration
                {
                    _backspaceTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_backspaceTimer >= _backspaceInitialDelay)
                    {
                        HandleBackspace();
                        _backspaceTimer = 0f;
                        _backspaceInitialDelay = Math.Max(Global.MIN_BACKSPACE_DELAY, _backspaceInitialDelay * Global.BACKSPACE_ACCELERATION);
                    }
                }
                else if (_backspaceHeld)
                {
                    _backspaceHeld = false;
                }
            }

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;
        }

        private void HandleControlCommands(Keys key)
        {
            switch (key)
            {
                case Keys.X: // Cut
                    if (!string.IsNullOrEmpty(_currentInput))
                    {
                        _clipboard = _currentInput;
                        _currentInput = "";
                        _cursorPosition = 0;
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"Cut text to clipboard: '{_clipboard}'   (CTRL + X)");
                    }
                    break;

                case Keys.V: // Paste
                    if (!string.IsNullOrEmpty(_clipboard))
                    {
                        _currentInput += _clipboard;
                        _cursorPosition = _currentInput.Length;
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"Pasted from clipboard: '{_clipboard}'   (CTRL + V)");
                    }
                    break;

                case Keys.A: // Clear
                    if (!string.IsNullOrEmpty(_currentInput))
                    {
                        _currentInput = "";
                        _cursorPosition = 0;
                        Core.CurrentTerminalRenderer.AddOutputToHistory("Input cleared   (CTRL + A)");
                    }
                    break;

                case Keys.U: // Scroll up (CTRL + U)
                    {
                        int maxVisibleLines = Core.CurrentTerminalRenderer.GetMaxVisibleLines(); // <-- FIX
                        Core.CurrentTerminalRenderer.SetScrollOffset(Math.Min(Core.CurrentTerminalRenderer.ScrollOffset + 5, Math.Max(0, Core.CurrentTerminalRenderer.WrappedHistory.Count - maxVisibleLines)));
                    }
                    break;

                case Keys.D: // Scroll down (CTRL + D)
                    {
                        Core.CurrentTerminalRenderer.SetScrollOffset(Math.Max(Core.CurrentTerminalRenderer.ScrollOffset - 5, 0));
                    }
                    break;
            }
        }

        private void HandleShiftCommands(Keys key)
        {
            switch (key)
            {
                case Keys.Up:
                    {
                        // filler
                    }
                    break;
            }
        }

        private void HandleBackspace()
        {
            if (_currentInput.Length > 0)
            {
                _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
                _cursorPosition = Math.Max(0, _cursorPosition - 1);
            }
        }

        private void HandleCharacterInput(Keys key)
        {
            string keyString = key.ToString();
            if (keyString.Length == 1)
            {
                _currentInput += keyString.ToLower();
                _cursorPosition++;
            }
            else if (keyString.StartsWith("D") && keyString.Length == 2)
            {
                _currentInput += keyString.Substring(1);
                _cursorPosition++;
            }

            Core.CurrentAutoCompleteManager.UpdateAutoCompleteSuggestions(_currentInput);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private void NavigateCommandHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;

            if (_commandHistoryIndex == -1) // If we're not currently browsing history, save what the user was typing
            {
                _currentEditingCommand = _currentInput;
            }

            int newIndex = _commandHistoryIndex + direction; // Calculate new index

            if (newIndex < -1)
            {
                return; // Already at the beginning, don't go further
            }
            else if (newIndex >= _commandHistory.Count)
            {
                return; // Already at the end, don't go further
            }
            else if (newIndex == -1)
            {
                _currentInput = _currentEditingCommand; // Back to current editing (what user was typing before browsing history)
                _commandHistoryIndex = -1;
            }
            else
            {
                _commandHistoryIndex = newIndex; // Navigate to specific history entry
                _currentInput = _commandHistory[_commandHistory.Count - 1 - _commandHistoryIndex];
            }

            _cursorPosition = _currentInput.Length;
        }

        private void ProcessCommand(string input)
        {
            if (string.IsNullOrEmpty(input)) return;

            Core.CurrentTerminalRenderer.AddToHistory($"> {input}");

            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string commandName = parts[0];

            if (Core.CurrentCommandProcessor.Commands.TryGetValue(commandName, out var command))
            {
                command.Action(parts);
            }
            else
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory($"Unknown command: '{commandName}'. Type 'help' for available commands.");
            }

            Core.CurrentTerminalRenderer.SetScrollOffset(0);
        }
    }
}