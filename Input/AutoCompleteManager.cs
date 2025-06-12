using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    public class AutoCompleteManager
    {
        private List<string> _autoCompleteSuggestions = new List<string>();
        private int _selectedAutoCompleteSuggestionIndex = -1;
        private bool _showingAutoCompleteSuggestions = false;

        public List<string> AutoCompleteSuggestions => _autoCompleteSuggestions;
        public int SelectedAutoCompleteSuggestionIndex => _selectedAutoCompleteSuggestionIndex;
        public bool ShowingAutoCompleteSuggestions => _showingAutoCompleteSuggestions;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void ToggleShowingAutoCompleteSuggestions(bool toggle)
        {
            _showingAutoCompleteSuggestions = toggle;
        }

        public void SetSelectedAutoCompleteSuggestionIndex(int index)
        {
            _selectedAutoCompleteSuggestionIndex = index;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void UpdateAutoCompleteSuggestions(string currentInput)
        {
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
            bool isCompleteCommand = Core.CurrentCommandProcessor.Commands.ContainsKey(commandName);

            // We suggest ARGUMENTS if:
            // 1. The first word is a complete command AND...
            // 2. ...the user has typed more than one word (e.g., "run u")
            // 3. ...OR there is a space after the command (e.g., "run ")
            // 4. ...OR the input is an EXACT match for the command name (e.g., "run")
            if (isCompleteCommand && (parts.Length > 1 || trailingSpace || currentInput.Length == commandName.Length))
            {
                // User is typing arguments for a known command.
                var command = Core.CurrentCommandProcessor.Commands[commandName];
                string[] typedArgs = parts.Skip(1).ToArray();
                string partialArg = "";

                // If there's no space at the end, the last part is a partial argument.
                if (!trailingSpace && typedArgs.Length > 0)
                {
                    partialArg = typedArgs.Last().ToLower();
                    typedArgs = typedArgs.Take(typedArgs.Length - 1).ToArray();
                }

                // Get suggestions for the next argument based on the arguments already fully typed.
                var argSuggestions = command.SuggestArguments(typedArgs);

                // Filter suggestions based on the partial argument.
                var filteredSuggestions = argSuggestions
                    .Where(s => s.ToLower().StartsWith(partialArg))
                    .OrderBy(s => s)
                    .ToList();

                // Construct the full command strings for the suggestions list.
                string prefix = (commandName + " " + string.Join(" ", typedArgs)).Trim();

                _autoCompleteSuggestions = filteredSuggestions
                    .Select(s => $"{prefix} {s}")
                    .ToList();
            }
            else
            {
                // User is still typing the command name itself (e.g., "ru" for "run").
                var matches = Core.CurrentCommandProcessor.Commands.Keys
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