using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectVagabond
{
    public class TerminalRenderer
    {
        private List<string> _inputHistory = new List<string>();
        private List<ColoredLine> _wrappedHistory = new List<ColoredLine>();
        private int _scrollOffset = 0;
        private int _nextLineNumber = 1;

        public int ScrollOffset => _scrollOffset;
        public List<ColoredLine> WrappedHistory => _wrappedHistory;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void SetScrollOffset(int index)
        {
            _scrollOffset = index;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void AddToHistory(string message, Color? baseColor = null)
        {
            _inputHistory.Add(message);
            
            var coloredLine = ParseColoredText(message, baseColor); // Parse colored message
            coloredLine.LineNumber = _nextLineNumber++;

            var wrappedLines = WrapColoredText(coloredLine, GetTerminalWidthInChars()); // Wrap the colored line
            foreach (var line in wrappedLines)
            {
                _wrappedHistory.Add(line);
            }

            while (_wrappedHistory.Count > Global.MAX_HISTORY_LINES) // Limit total wrapped lines
            {
                _wrappedHistory.RemoveAt(0);
            }

            if (_inputHistory.Count > 50)
            {
                _inputHistory.RemoveAt(0);
            }
        }

        public void AddOutputToHistory(string output)
        {
            AddToHistory(output, Global.Instance.OutputTextColor);
        }

        public void ClearHistory()
        {
            _inputHistory.Clear();
            _wrappedHistory.Clear();
            _scrollOffset = 0;
            _nextLineNumber = 1;
        }


        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void DrawTerminal()
        {
            SpriteBatch _spriteBatch = Global.Instance.CurrentSpriteBatch;
            SpriteFont _defaultFont = Global.Instance.DefaultFont;

            int terminalX = 400;
            int terminalY = 50;
            int terminalWidth = Global.DEFAULT_TERMINAL_WIDTH;
            int terminalHeight = 600;

            // Create pixel texture for drawing rectangles //
            var pixel = new Texture2D(Core.Instance.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            // Draw terminal background //
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, terminalHeight + 30), Global.Instance.TerminalBg);

            // Draw terminal border with thicker lines (2 pixels thick) //
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, 2), Global.Instance.palette_White); // Top
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY + terminalHeight + 3, terminalWidth + 10, 2), Global.Instance.palette_White); // Bottom
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, 2, terminalHeight + 30), Global.Instance.palette_White); // Left
            _spriteBatch.Draw(pixel, new Rectangle(terminalX + terminalWidth + 3, terminalY - 25, 2, terminalHeight + 30), Global.Instance.palette_White); // Right

            // Draw terminal title //
            _spriteBatch.DrawString(_defaultFont, "Terminal Output", new Vector2(terminalX, terminalY - 20), Global.Instance.TextColor);

            // Draw wrapped command history //
            int maxVisibleLines = (terminalHeight - 40) / Global.TERMINAL_LINE_SPACING; // Reduced from 80 to 40
            int totalLines = _wrappedHistory.Count;
            int startIndex = Math.Max(0, totalLines - maxVisibleLines - _scrollOffset);
            int endIndex = Math.Min(totalLines, startIndex + maxVisibleLines);

            for (int i = startIndex; i < endIndex; i++)
            {
                int lineIndex = i - startIndex;
                float x = terminalX;
                float y = terminalY + lineIndex * Global.TERMINAL_LINE_SPACING;

                foreach (var segment in _wrappedHistory[i].Segments)
                {
                    _spriteBatch.DrawString(_defaultFont, segment.Text, new Vector2(x, y), segment.Color);
                    x += _defaultFont.MeasureString(segment.Text).X;
                }
    
                if (_wrappedHistory[i].LineNumber > 0) // Only show numbers for actual content lines
                {
                    string lineNumText = _wrappedHistory[i].LineNumber.ToString();
                    float lineNumX = terminalX + 710; // Position outside terminal, to the right
                    _spriteBatch.DrawString(_defaultFont, lineNumText, new Vector2(lineNumX, y), Global.Instance.palette_DarkGray);
                }
            }

            // Draw scroll indicator only when there's content that can be scrolled //
            bool canScrollUp = _scrollOffset > 0;
            bool canScrollDown = _wrappedHistory.Count > maxVisibleLines;

            if (canScrollUp || canScrollDown)
            {
                string scrollIndicator = "";
                if (_scrollOffset > 0)
                {
                    scrollIndicator = $"^ Scrolled up {_scrollOffset} lines";
                }
    
                int scrollY = terminalY + (endIndex - startIndex) * Global.TERMINAL_LINE_SPACING + 5;
                _spriteBatch.DrawString(_defaultFont, scrollIndicator, new Vector2(terminalX, scrollY), Color.Gold);
            }

            // Draw separator line above input with thicker line (2 pixels thick) //
            int inputLineY = terminalY + terminalHeight - 20;
            int separatorY = inputLineY - 5;
            _spriteBatch.Draw(pixel, new Rectangle(terminalX-5, separatorY, 710, 2), Global.Instance.palette_White);

            // Draw the input line //
            string inputDisplay = $"> {Core.CurrentInputHandler.CurrentInput}_";
            string wrappedInput = WrapText(inputDisplay, GetTerminalWidthInChars());
            _spriteBatch.DrawString(_defaultFont, wrappedInput, new Vector2(terminalX, inputLineY), Color.Khaki);

            // Draw autocomplete suggestions //
            if (Core.CurrentAutoCompleteManager.ShowingAutoCompleteSuggestions && Core.CurrentAutoCompleteManager.AutoCompleteSuggestions.Count > 0)
            {
                int suggestionY = inputLineY - 20;
                int visibleSuggestions = Math.Min(Core.CurrentAutoCompleteManager.AutoCompleteSuggestions.Count, 5);
    
                // Calculate background dimensions //
                int maxSuggestionWidth = 0;
                for (int i = 0; i < visibleSuggestions; i++)
                {
                    string prefix = (i == Core.CurrentAutoCompleteManager.SelectedAutoCompleteSuggestionIndex) ? " >" : "  ";
                    string fullText = prefix + Core.CurrentAutoCompleteManager.AutoCompleteSuggestions[i];
                    int textWidth = (int)_defaultFont.MeasureString(fullText).X;
                    maxSuggestionWidth = Math.Max(maxSuggestionWidth, textWidth);
                }
    
                // Draw background rectangle //
                int backgroundHeight = visibleSuggestions * Global.FONT_SIZE;
                int backgroundY = suggestionY - (visibleSuggestions - 1) * Global.FONT_SIZE;
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, maxSuggestionWidth + 4, backgroundHeight), Global.Instance.palette_Black);
    
                // Draw border around suggestions //
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, maxSuggestionWidth + 4, 1), Global.Instance.palette_LightGray); // Top
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY + backgroundHeight, maxSuggestionWidth + 4, 1), Global.Instance.palette_LightGray); // Bottom
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, 1, backgroundHeight), Global.Instance.palette_LightGray); // Left
                _spriteBatch.Draw(pixel, new Rectangle(terminalX + maxSuggestionWidth + 4, backgroundY, 1, backgroundHeight), Global.Instance.palette_LightGray); // Right
    
                // Draw suggestions //
                for (int i = 0; i < visibleSuggestions; i++)
                {
                    Color suggestionColor = (i == Core.CurrentAutoCompleteManager.SelectedAutoCompleteSuggestionIndex) ? Color.Khaki : Global.Instance.palette_LightGray;
                    string prefix = (i == Core.CurrentAutoCompleteManager.SelectedAutoCompleteSuggestionIndex) ? " >" : "  ";
                    _spriteBatch.DrawString(_defaultFont, prefix + Core.CurrentAutoCompleteManager.AutoCompleteSuggestions[i], 
                        new Vector2(terminalX + 2, suggestionY - i * Global.FONT_SIZE), suggestionColor);
                }
            }

            // Draw status line OUTSIDE terminal (below it) //
            int statusY = terminalY + terminalHeight + 15;
            string statusText = $"Path: {Core.CurrentGameState.PendingPathPreview.Count} steps";
            if (Core.CurrentGameState.IsExecutingPath)
            {
                statusText += $" | Executing: {Core.CurrentGameState.CurrentPathIndex + 1}/{Core.CurrentGameState.PendingPathPreview.Count}";
            }
            string wrappedStatus = WrapText(statusText, GetTerminalWidthInChars());
            _spriteBatch.DrawString(_defaultFont, wrappedStatus, new Vector2(terminalX, statusY), Global.Instance.palette_LightGray);

            // Draw prompt line OUTSIDE terminal (below status) //
            int promptY = statusY + (wrappedStatus.Split('\n').Length * Global.TERMINAL_LINE_SPACING) + 10;
            string promptText = GetPromptText();
            if (!string.IsNullOrEmpty(promptText))
            {
                var coloredPrompt = ParseColoredText(promptText, Color.Khaki);
                var wrappedPromptLines = WrapColoredText(coloredPrompt, GetTerminalWidthInChars());
    
                for (int i = 0; i < wrappedPromptLines.Count; i++)
                {
                    float x = terminalX;
                    float y = promptY + i * Global.PROMPT_LINE_SPACING;
        
                    foreach (var segment in wrappedPromptLines[i].Segments)
                    {
                        _spriteBatch.DrawString(_defaultFont, segment.Text, new Vector2(x, y), segment.Color);
                        x += _defaultFont.MeasureString(segment.Text).X;
                    }
                }
            }
        }

        private string GetPromptText()
        {
            if (Core.CurrentGameState.IsFreeMoveMode)
            {
                return "[skyblue]Free moving...\n[deepskyblue]Use [royalblue](W/A/S/D)[deepskyblue] to queue moves.\nPress [royalblue]ENTER[deepskyblue] to confirm, [royalblue]ESC[deepskyblue] to cancel: ";
            }
            else if (Core.CurrentGameState.PendingPathPreview.Count > 0 && !Core.CurrentGameState.IsExecutingPath)
            {
                return $"[khaki]Previewing path...\n[gold]Pending [orange]{Core.CurrentGameState.PendingPathPreview.Count}[gold] queued movements...\nPress [orange]ENTER[gold] to confirm, [orange]ESC[gold] to cancel: ";
                
            }
            return "";
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        private ColoredLine ParseColoredText(string text, Color? baseColor = null)
        {
            var line = new ColoredLine();
            var currentColor = baseColor ?? Global.Instance.TextColor;
            var currentText = "";

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    if (currentText.Length > 0)
                    {
                        line.Segments.Add(new ColoredText(currentText, currentColor));
                        currentText = "";
                    }

                    int closeIndex = text.IndexOf(']', i);
                    if (closeIndex != -1)
                    {
                        string colorTag = text.Substring(i + 1, closeIndex - i - 1);
                        i = closeIndex;

                        if (colorTag == "/")
                        {
                            currentColor = Global.Instance.TextColor;
                        }
                        else if (colorTag == "/o")
                        {
                            currentColor = Global.Instance.OutputTextColor;
                        }
                        else
                        {
                            currentColor = ParseColor(colorTag);
                        }
                    }
                    else
                    {
                        currentText += text[i];
                    }
                }
                else
                {
                    currentText += text[i];
                }
            }

            if (currentText.Length > 0)
            {
                line.Segments.Add(new ColoredText(currentText, currentColor));
            }

            return line;
        }

        private Color ParseColor(string colorName)
        {
            switch (colorName.ToLower())
            {
                case "palette_black": return Global.Instance.palette_Black;
                case "palette_darkgray": return Global.Instance.palette_DarkGray;
                case "palette_gray": return Global.Instance.palette_Gray;
                case "palette_lightgray": return Global.Instance.palette_LightGray;
                case "palette_white": return Global.Instance.palette_White;
                case "palette_teal": return Global.Instance.palette_Teal;
                case "palette_lightblue": return Global.Instance.palette_LightBlue;
                case "palette_darkblue": return Global.Instance.palette_DarkBlue;
                case "palette_darkgreen": return Global.Instance.palette_DarkGreen;
                case "palette_lightgreen": return Global.Instance.palette_LightGreen;
                case "palette_lightyellow": return Global.Instance.palette_LightYellow;
                case "palette_yellow": return Global.Instance.palette_Yellow;
                case "palette_orange": return Global.Instance.palette_Orange;
                case "palette_red": return Global.Instance.palette_Red;
                case "palette_darkpurple": return Global.Instance.palette_DarkPurple;
                case "palette_lightpurple": return Global.Instance.palette_LightPurple;
                case "palette_pink": return Global.Instance.palette_Pink;
                case "palette_brightwhite": return Global.Instance.palette_BrightWhite;

                case "khaki": return Color.Khaki;
                case "red": return Color.Red;
                case "green": return Color.Green;
                case "blue": return Color.Blue;
                case "yellow": return Color.Yellow;
                case "cyan": return Color.Cyan;
                case "magenta": return Color.Magenta;
                case "white": return Color.White;
                case "orange": return Color.Orange;
                case "gray":
                case "grey": return Color.Gray;
            }
    
            
            try // If not found in predefined colors, use reflection to find XNA color
            {
                var colorProperty = typeof(Color).GetProperty(colorName, 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);
        
                if (colorProperty != null && colorProperty.PropertyType == typeof(Color))
                {
                    return (Color)colorProperty.GetValue(null);
                }
            }
            catch
            {
                // If reflection fails, fall back to default
            }
    
            return Global.Instance.TextColor;
        }

        private List<ColoredLine> WrapColoredText(ColoredLine line, int maxWidth)
        {
            var wrappedLines = new List<ColoredLine>();
    
            var processedSegments = new List<ColoredText>();
    
            foreach (var segment in line.Segments)
            {
                if (segment.Text.Contains('\n'))
                {
                    string[] lines = segment.Text.Split('\n'); // Split segment by newlines
                    for (int i = 0; i < lines.Length; i++)
                    {
                        processedSegments.Add(new ColoredText(lines[i], segment.Color));
                
                        if (i < lines.Length - 1) // Add a special marker for line breaks (except for the last line)
                        {
                            processedSegments.Add(new ColoredText("\n", segment.Color));
                        }
                    }
                }
                else
                {
                    processedSegments.Add(segment);
                }
            }
    
            var currentLine = new ColoredLine { LineNumber = line.LineNumber }; // Now process the segments, creating new lines when we encounter \n markers
            var currentWords = new List<string>();
            var currentColors = new List<Color>();
            int currentLineWidth = 0;

            foreach (var segment in processedSegments)
            {
                if (segment.Text == "\n")
                {
                    if (currentWords.Count > 0 || wrappedLines.Count == 0) // Finish current line and start new one
                    {
                        var finishedLine = CombineColoredSegments(currentWords, currentColors);
                        finishedLine.LineNumber = wrappedLines.Count == 0 ? line.LineNumber : 0;
                        wrappedLines.Add(finishedLine);
                
                        currentWords.Clear();
                        currentColors.Clear();
                        currentLineWidth = 0;
                    }
            
                    currentLine = new ColoredLine { LineNumber = line.LineNumber }; // Start new line
                    continue;
                }
        
                var words = segment.Text.Split(' ', StringSplitOptions.None); // Process normal text segment

                for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
                {
                    string word = words[wordIndex];
                    bool needsSpace = currentWords.Count > 0 && wordIndex > 0;

                    if (wordIndex == 0 && segment.Text.StartsWith(" "))
                    {
                        int leadingSpaces = 0;
                        for (int j = 0; j < segment.Text.Length && segment.Text[j] == ' '; j++)
                        {
                            leadingSpaces++;
                        }

                        if (leadingSpaces > 0 && currentWords.Count == 0)
                        {
                            currentWords.Add(new string(' ', leadingSpaces));
                            currentColors.Add(segment.Color);
                            currentLineWidth += leadingSpaces;
                        }
                    }

                    int wordWidth = word.Length + (needsSpace ? 1 : 0);

                    if (currentLineWidth + wordWidth <= maxWidth || currentLineWidth == 0)
                    {
                        if (needsSpace)
                        {
                            currentWords.Add(" ");
                            currentColors.Add(segment.Color);
                            currentLineWidth += 1;
                        }

                        if (word.Length > 0)
                        {
                            currentWords.Add(word);
                            currentColors.Add(segment.Color);
                            currentLineWidth += word.Length;
                        }
                    }
                    else
                    {
                        if (currentWords.Count > 0)
                        {
                            var combinedLine = CombineColoredSegments(currentWords, currentColors);
                            combinedLine.LineNumber = line.LineNumber;
                            wrappedLines.Add(combinedLine);

                            currentWords.Clear();
                            currentColors.Clear();
                            currentLineWidth = 0;
                        }

                        if (word.Length > maxWidth)
                        {
                            for (int i = 0; i < word.Length; i += maxWidth)
                            {
                                int remainingChars = word.Length - i;
                                int charsToTake = Math.Min(maxWidth, remainingChars);
                                string wordPart = word.Substring(i, charsToTake);

                                var longWordLine = new ColoredLine { LineNumber = line.LineNumber };
                                longWordLine.Segments.Add(new ColoredText(wordPart, segment.Color));
                                wrappedLines.Add(longWordLine);
                            }
                        }
                        else
                        {
                            currentWords.Add(word);
                            currentColors.Add(segment.Color);
                            currentLineWidth = word.Length;
                        }
                    }
                }
            }

            if (currentWords.Count > 0)
            {
                var finalLine = CombineColoredSegments(currentWords, currentColors);
                finalLine.LineNumber = wrappedLines.Count == 0 ? line.LineNumber : 0;
                wrappedLines.Add(finalLine);
            }

            if (wrappedLines.Count == 0)
            {
                wrappedLines.Add(new ColoredLine { LineNumber = line.LineNumber });
            }

            return wrappedLines;
        }

        private ColoredLine CombineColoredSegments(List<string> words, List<Color> colors)
        {
            var line = new ColoredLine();
    
            if (words.Count == 0)
                return line;
    
            var currentText = new StringBuilder();
            Color currentColor = colors[0];
    
            for (int i = 0; i < words.Count; i++)
            {
                if (i > 0 && colors[i] != currentColor)
                {
                    if (currentText.Length > 0)
                    {
                        line.Segments.Add(new ColoredText(currentText.ToString(), currentColor));
                        currentText.Clear();
                    }
                    currentColor = colors[i];
                }
        
                currentText.Append(words[i]);
            }
    
            if (currentText.Length > 0)
            {
                line.Segments.Add(new ColoredText(currentText.ToString(), currentColor));
            }
    
            return line;
        }

        private string WrapText(string text, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var finalLines = new List<string>();
    
            string[] existingLines = text.Split('\n'); // First split by existing newlines
    
            foreach (string line in existingLines)
            {
                if (line.Length <= maxCharsPerLine)
                {
                    finalLines.Add(line); // Line doesn't need wrapping
                }
                else
                {
                    var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries); // Line needs wrapping
                    var currentLine = new StringBuilder();

                    foreach (string word in words)
                    {
                        string testLine = currentLine.Length > 0 ? currentLine + " " + word : word;
        
                        if (testLine.Length <= maxCharsPerLine)
                        {
                            if (currentLine.Length > 0)
                                currentLine.Append(' ');
                            currentLine.Append(word);
                        }
                        else
                        {
                            if (currentLine.Length > 0)
                            {
                                finalLines.Add(currentLine.ToString());
                                currentLine.Clear();
                            }
            
                            if (word.Length > maxCharsPerLine)
                            {
                                for (int i = 0; i < word.Length; i += maxCharsPerLine)
                                {
                                    int remainingChars = word.Length - i;
                                    int charsToTake = Math.Min(maxCharsPerLine, remainingChars);
                                    finalLines.Add(word.Substring(i, charsToTake));
                                }
                            }
                            else
                            {
                                currentLine.Append(word);
                            }
                        }
                    }

                    if (currentLine.Length > 0)
                    {
                        finalLines.Add(currentLine.ToString());
                    }
                }
            }

            return string.Join("\n", finalLines);
        }

        private int GetTerminalWidthInChars()
        {
            int terminalWidth = Global.DEFAULT_TERMINAL_WIDTH; // Your terminal pixel width
            float charWidth = Global.Instance.DefaultFont.MeasureString("W").X; // Use a wide character for measurement
            return (int)(terminalWidth / charWidth) - 2; // Subtract 2 for padding
        }
    }
}
