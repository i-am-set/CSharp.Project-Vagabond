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

        public void ToggleShowingAutoCompleteSuggestions(bool toggle)
        {
            _showingAutoCompleteSuggestions = toggle;
        }

        public void SetSelectedAutoCompleteSuggestionIndex(int index)
        {
            _selectedAutoCompleteSuggestionIndex = index;
        }

        public void UpdateAutoCompleteSuggestions(string currentInput)
        {
            _autoCompleteSuggestions.Clear();
            _selectedAutoCompleteSuggestionIndex = -1;
    
            if (string.IsNullOrEmpty(currentInput))
            {
                _showingAutoCompleteSuggestions = false;
                return;
            }
    
            var matches = Core.CurrentCommandProcessor.Commands.Keys
                .Where(cmd => cmd.StartsWith(currentInput.ToLower()))
                .OrderBy(cmd => cmd)
                .ToList();
    
            _autoCompleteSuggestions = matches;
            _showingAutoCompleteSuggestions = matches.Count > 0;
    
            if (_showingAutoCompleteSuggestions)
                _selectedAutoCompleteSuggestionIndex = 0;
        }
    }
}
