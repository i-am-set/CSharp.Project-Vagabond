using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class AutoCompleteManager
    {
        private CommandProcessor _commandProcessor; // Lazy loaded

        private List<string> _autoCompleteSuggestions = new List<string>();
        private int _selectedAutoCompleteSuggestionIndex = -1;
        private bool _showingAutoCompleteSuggestions = false;

        public List<string> AutoCompleteSuggestions => _autoCompleteSuggestions;
        public int SelectedAutoCompleteSuggestionIndex => _selectedAutoCompleteSuggestionIndex;
        public bool ShowingAutoCompleteSuggestions => _showingAutoCompleteSuggestions;

        public AutoCompleteManager() { }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void HideSuggestions()
        {
            _showingAutoCompleteSuggestions = false;
            _selectedAutoCompleteSuggestionIndex = -1;
            _autoCompleteSuggestions.Clear();
        }

        public void CycleSelection(int direction)
        {
            if (!_showingAutoCompleteSuggestions || !_autoCompleteSuggestions.Any()) return;

            // Invert direction to match visual layout (up key increases index)
            _selectedAutoCompleteSuggestionIndex -= direction;

            // Wrap around logic
            if (_selectedAutoCompleteSuggestionIndex < 0)
            {
                _selectedAutoCompleteSuggestionIndex = _autoCompleteSuggestions.Count - 1;
            }
            if (_selectedAutoCompleteSuggestionIndex >= _autoCompleteSuggestions.Count)
            {
                _selectedAutoCompleteSuggestionIndex = 0;
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void UpdateAutoCompleteSuggestions(string currentInput)
        {
            _commandProcessor ??= ServiceLocator.Get<CommandProcessor>(); // Lazyload the CommandProcessor.

            _autoCompleteSuggestions.Clear();
            _selectedAutoCompleteSuggestionIndex = -1;

            if (string.IsNullOrEmpty(currentInput))
            {
                _showingAutoCompleteSuggestions = false;
                return;
            }

            string[] parts = currentInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                _showingAutoCompleteSuggestions = false;
                return;
            }

            bool trailingSpace = currentInput.EndsWith(" ");
            string commandName = parts[0].ToLower();
            bool isCompleteCommand = _commandProcessor.Commands.ContainsKey(commandName);

            if (isCompleteCommand && (parts.Length > 1 || trailingSpace || currentInput.Length == commandName.Length))
            {
                var command = _commandProcessor.Commands[commandName];
                string[] typedArgs = parts.Skip(1).ToArray();
                string partialArg = "";

                if (!trailingSpace && typedArgs.Length > 0)
                {
                    partialArg = typedArgs.Last().ToLower();
                    typedArgs = typedArgs.Take(typedArgs.Length - 1).ToArray();
                }

                var argSuggestions = command.SuggestArguments(typedArgs);

                var filteredSuggestions = argSuggestions
                    .Where(s => s.ToLower().StartsWith(partialArg))
                    .OrderBy(s => s)
                    .ToList();

                string prefix = (commandName + " " + string.Join(" ", typedArgs)).Trim();

                _autoCompleteSuggestions = filteredSuggestions
                    .Select(s => $"{prefix} {s}")
                    .ToList();
            }
            else
            {
                var matches = _commandProcessor.Commands.Keys
                    .Where(cmd => cmd.StartsWith(currentInput.ToLower()))
                    .OrderBy(cmd => cmd)
                    .ToList();

                _autoCompleteSuggestions = matches;
            }

            _showingAutoCompleteSuggestions = _autoCompleteSuggestions.Any();
            if (_showingAutoCompleteSuggestions)
                _selectedAutoCompleteSuggestionIndex = 0;
        }
    }
}