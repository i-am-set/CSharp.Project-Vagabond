using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.Scenes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class InputHandler
    {
        // Injected Dependencies
        private readonly GameState _gameState;
        private readonly PlayerInputSystem _playerInputSystem;
        private readonly ClockRenderer _clockRenderer;
        private readonly Global _global;
        private AutoCompleteManager _autoCompleteManager; // Lazyloaded
        private CommandProcessor _commandProcessor; // Lazyloaded
        private TerminalRenderer _terminalRenderer; // Lazyloaded

        // Input State
        private string _currentInput = "";
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
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
        public bool IsTerminalInputActive { get; private set; } = false;

        public InputHandler()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _playerInputSystem = ServiceLocator.Get<PlayerInputSystem>();
            _clockRenderer = ServiceLocator.Get<ClockRenderer>();
            _global = ServiceLocator.Get<Global>();
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void SetCurrentInput(String input) => _currentInput = input;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void HandleInput(GameTime gameTime, Rectangle terminalBounds)
        {
            // Lazyload dependencies to break initialization cycles
            _terminalRenderer ??= ServiceLocator.Get<TerminalRenderer>();
            _autoCompleteManager ??= ServiceLocator.Get<AutoCompleteManager>();
            _commandProcessor ??= ServiceLocator.Get<CommandProcessor>();

            KeyboardState currentKeyboardState = Keyboard.GetState();
            MouseState currentMouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            // If the game is paused, only process the unpause command and nothing else.
            if (_gameState.IsPaused)
            {
                if (_gameState.IsExecutingActions && !_previousKeyboardState.IsKeyDown(Keys.Space) && currentKeyboardState.IsKeyDown(Keys.Space))
                {
                    _gameState.TogglePause();
                }
                _previousKeyboardState = currentKeyboardState;
                _previousMouseState = currentMouseState;
                return;
            }

            // --- Handle Terminal Activation/Deactivation ---
            if (!_gameState.IsInCombat)
            {
                // Toggle with '~' key
                if (currentKeyboardState.IsKeyDown(Keys.OemTilde) && !_previousKeyboardState.IsKeyDown(Keys.OemTilde))
                {
                    IsTerminalInputActive = !IsTerminalInputActive;
                    if (!IsTerminalInputActive)
                    {
                        _currentInput = "";
                        _autoCompleteManager.ToggleShowingAutoCompleteSuggestions(false);
                    }
                    else
                    {
                        _terminalRenderer.ResetCaratBlink();
                    }
                }

                if (IsTerminalInputActive)
                {
                    // Deactivate with Escape key
                    if (currentKeyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
                    {
                        IsTerminalInputActive = false;
                        _currentInput = "";
                        _autoCompleteManager.ToggleShowingAutoCompleteSuggestions(false);
                    }
                    // Deactivate by clicking outside the terminal
                    else if (currentMouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
                    {
                        if (!terminalBounds.Contains(virtualMousePos))
                        {
                            IsTerminalInputActive = false;
                            _currentInput = "";
                            _autoCompleteManager.ToggleShowingAutoCompleteSuggestions(false);
                        }
                    }
                }
            }
            else
            {
                // Force terminal to be inactive during combat
                IsTerminalInputActive = false;
            }


            // --- Process Other Inputs ---
            Keys[] pressedKeys = currentKeyboardState.GetPressedKeys();

            _controlPressed = currentKeyboardState.IsKeyDown(Keys.LeftControl) || currentKeyboardState.IsKeyDown(Keys.RightControl);
            _shiftPressed = currentKeyboardState.IsKeyDown(Keys.LeftShift) || currentKeyboardState.IsKeyDown(Keys.RightShift);

            HandleScrolling(currentMouseState);

            if (!IsTerminalInputActive)
            {
                HandleGlobalHotkeys(currentKeyboardState);
                if (_gameState.IsFreeMoveMode)
                {
                    HandleFreeMoveInput(currentKeyboardState);
                }
                else if (_gameState.IsExecutingActions)
                {
                    HandleExecutingActionsInput(currentKeyboardState);
                }
            }
            else // Terminal input is active
            {
                HandleTerminalInput(gameTime, currentKeyboardState, pressedKeys);
            }

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;
        }

        private void HandleScrolling(MouseState currentMouseState)
        {
            if (currentMouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
            {
                int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                int scrollLines = scrollDelta > 0 ? 3 : -3;

                if (_gameState.IsInCombat)
                {
                    int currentOffset = _terminalRenderer.CombatScrollOffset;
                    int maxOffset = Math.Max(0, _terminalRenderer.WrappedHistory.Count - _terminalRenderer.GetMaxVisibleLines());
                    if (scrollDelta > 0) // scrolling up
                    {
                        _terminalRenderer.CombatScrollOffset = Math.Min(currentOffset + Math.Abs(scrollLines), maxOffset);
                    }
                    else // scrolling down
                    {
                        _terminalRenderer.CombatScrollOffset = Math.Max(currentOffset - Math.Abs(scrollLines), 0);
                    }
                }
                else
                {
                    int currentOffset = _terminalRenderer.ScrollOffset;
                    int maxOffset = Math.Max(0, _terminalRenderer.WrappedHistory.Count - _terminalRenderer.GetMaxVisibleLines());
                    if (scrollDelta > 0) // scrolling up
                    {
                        _terminalRenderer.ScrollOffset = Math.Min(currentOffset + Math.Abs(scrollLines), maxOffset);
                    }
                    else // scrolling down
                    {
                        _terminalRenderer.ScrollOffset = Math.Max(currentOffset - Math.Abs(scrollLines), 0);
                    }
                }
            }
        }

        private void HandleGlobalHotkeys(KeyboardState currentKeyboardState)
        {
            if (!_previousKeyboardState.IsKeyDown(Keys.Escape) && currentKeyboardState.IsKeyDown(Keys.Escape))
            {
                if (_gameState.IsExecutingActions)
                {
                    _gameState.CancelExecutingActions();
                }
                else if (_gameState.IsFreeMoveMode)
                {
                    _gameState.ToggleIsFreeMoveMode(false);
                }
                else if (_gameState.PendingActions.Count > 0)
                {
                    _playerInputSystem.CancelPendingActions(_gameState);
                }
            }

            if (_gameState.IsExecutingActions && !_previousKeyboardState.IsKeyDown(Keys.Space) && currentKeyboardState.IsKeyDown(Keys.Space))
            {
                _gameState.TogglePause();
            }
        }

        private void HandleFreeMoveInput(KeyboardState currentKeyboardState)
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
                    _playerInputSystem.QueueRunMovement(_gameState, moveDir, args);
                }
                else
                {
                    _playerInputSystem.QueueJogMovement(_gameState, moveDir, args);
                }
            }
            else if (!_previousKeyboardState.IsKeyDown(Keys.Enter) && currentKeyboardState.IsKeyDown(Keys.Enter))
            {
                if (_gameState.PendingActions.Count > 0 && !_gameState.IsExecutingActions)
                {
                    _gameState.ToggleExecutingActions(true);
                }
                else if (_gameState.IsExecutingActions)
                {
                    _gameState.ToggleExecutingActions(false);
                }
                else
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "No actions queued." });
                }
            }
        }

        private void HandleTerminalInput(GameTime gameTime, KeyboardState currentKeyboardState, Keys[] pressedKeys)
        {
            foreach (Keys key in pressedKeys)
            {
                if (!_previousKeyboardState.IsKeyDown(key))
                {
                    if (key == Keys.Enter)
                    {
                        _autoCompleteManager.ToggleShowingAutoCompleteSuggestions(false);
                        if (string.IsNullOrEmpty(_currentInput.Trim()) && _gameState.PendingActions.Count > 0)
                        {
                            if (!_gameState.IsExecutingActions)
                            {
                                _gameState.ToggleExecutingActions(true);
                            }
                            else
                            {
                                _gameState.ToggleExecutingActions(false);
                            }
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
                        _terminalRenderer.ResetCaratBlink();
                    }
                    else if (key == Keys.Tab)
                    {
                        if (_autoCompleteManager.ShowingAutoCompleteSuggestions && _autoCompleteManager.SelectedAutoCompleteSuggestionIndex >= 0)
                        {
                            _currentInput = _autoCompleteManager.AutoCompleteSuggestions[_autoCompleteManager.SelectedAutoCompleteSuggestionIndex];
                            _cursorPosition = _currentInput.Length;
                            _autoCompleteManager.ToggleShowingAutoCompleteSuggestions(false);
                            _terminalRenderer.ResetCaratBlink();
                        }
                        else
                        {
                            _clockRenderer.TimeScaleGroup.CycleNext();
                        }
                    }
                    else if (key == Keys.Up && _autoCompleteManager.ShowingAutoCompleteSuggestions)
                    {
                        _autoCompleteManager.SetSelectedAutoCompleteSuggestionIndex(Math.Min(_autoCompleteManager.AutoCompleteSuggestions.Count - 1, _autoCompleteManager.SelectedAutoCompleteSuggestionIndex + 1));
                    }
                    else if (key == Keys.Down && _autoCompleteManager.ShowingAutoCompleteSuggestions)
                    {
                        _autoCompleteManager.SetSelectedAutoCompleteSuggestionIndex(Math.Max(0, _autoCompleteManager.SelectedAutoCompleteSuggestionIndex - 1));
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
                    else if (key == Keys.Back)
                    {
                        HandleBackspace();
                        _autoCompleteManager.UpdateAutoCompleteSuggestions(_currentInput);
                        _backspaceHeld = true;
                        _backspaceTimer = 0f;
                        _backspaceInitialDelay = 0.3f;
                    }
                    else if (key == Keys.Delete)
                    {
                        if (_cursorPosition < _currentInput.Length)
                        {
                            _currentInput = _currentInput.Remove(_cursorPosition, 1);
                            _terminalRenderer.ResetCaratBlink();
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
                    else if (key == Keys.Space && !string.IsNullOrEmpty(_currentInput))
                    {
                        _currentInput += " ";
                        _cursorPosition++;
                        _terminalRenderer.ResetCaratBlink();
                    }
                    else if (key == Keys.PageUp)
                    {
                        int maxVisibleLines = _terminalRenderer.GetMaxVisibleLines();
                        _terminalRenderer.ScrollOffset = Math.Min(_terminalRenderer.ScrollOffset + 5, Math.Max(0, _terminalRenderer.WrappedHistory.Count - maxVisibleLines));
                    }
                    else if (key == Keys.PageDown)
                    {
                        _terminalRenderer.ScrollOffset = Math.Max(_terminalRenderer.ScrollOffset - 5, 0);
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
                    // CORRECTED LINE: Access const members via the class name, not an instance.
                    _backspaceInitialDelay = Math.Max(Global.MIN_BACKSPACE_DELAY, _backspaceInitialDelay * (1 - Global.BACKSPACE_ACCELERATION));
                }
            }
            else if (_backspaceHeld)
            {
                _backspaceHeld = false;
            }
        }

        private void HandleExecutingActionsInput(KeyboardState currentKeyboardState)
        {
            foreach (Keys key in currentKeyboardState.GetPressedKeys())
            {
                if (!_previousKeyboardState.IsKeyDown(key))
                {
                    if (key == Keys.Tab)
                    {
                        _clockRenderer.TimeScaleGroup.CycleNext();
                    }
                }
            }
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
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Cut text to clipboard: '{_clipboard}'   (CTRL + X)" });
                        _terminalRenderer.ResetCaratBlink();
                    }
                    break;

                case Keys.V: // Paste
                    if (!string.IsNullOrEmpty(_clipboard))
                    {
                        _currentInput += _clipboard;
                        _cursorPosition = _currentInput.Length;
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Pasted from clipboard: '{_clipboard}'   (CTRL + V)" });
                        _terminalRenderer.ResetCaratBlink();
                    }
                    break;

                case Keys.A: // Clear
                    if (!string.IsNullOrEmpty(_currentInput))
                    {
                        _currentInput = "";
                        _cursorPosition = 0;
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Input cleared   (CTRL + A)" });
                        _terminalRenderer.ResetCaratBlink();
                    }
                    break;

                case Keys.U: // Scroll up (CTRL + U)
                    {
                        int maxVisibleLines = _terminalRenderer.GetMaxVisibleLines();
                        _terminalRenderer.ScrollOffset = Math.Min(_terminalRenderer.ScrollOffset + 5, Math.Max(0, _terminalRenderer.WrappedHistory.Count - maxVisibleLines));
                    }
                    break;

                case Keys.D: // Scroll down (CTRL + D)
                    {
                        _terminalRenderer.ScrollOffset = Math.Max(_terminalRenderer.ScrollOffset - 5, 0);
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
                _terminalRenderer.ResetCaratBlink();
            }
        }

        private void HandleCharacterInput(Keys key)
        {
            string keyString = key.ToString();
            if (keyString.Length == 1)
            {
                _currentInput += _shiftPressed ? keyString.ToUpper() : keyString.ToLower();
                _cursorPosition++;
            }
            else if (keyString.StartsWith("D") && keyString.Length == 2 && char.IsDigit(keyString[1]))
            {
                _currentInput += keyString.Substring(1);
                _cursorPosition++;
            }

            _terminalRenderer.ResetCaratBlink();
            _autoCompleteManager.UpdateAutoCompleteSuggestions(_currentInput);
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
            _terminalRenderer.ResetCaratBlink();
        }

        private void ProcessCommand(string input)
        {
            if (string.IsNullOrEmpty(input)) return;

            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"> {input}" });

            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string commandName = parts[0];

            if (_commandProcessor.Commands.TryGetValue(commandName, out var command))
            {
                command.Action(parts);
            }
            else
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Unknown command: '{commandName}'. Type 'help' for available commands." });
            }

            _terminalRenderer.ScrollOffset = 0;
        }
    }
}