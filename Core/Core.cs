using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

// TODO: Change the grid to 5x5 images instead of 8x8
namespace ProjectVagabond
{
    public class Core : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;

        // Map settings
        private const int VIEW_SIZE = 32; // Size of visible area
        private const int CELL_SIZE = 10; // Size of each grid cell in pixels
        private const int FONT_SIZE = 12;
        private const int TERMINAL_LINE_SPACING = 12;
        private const int PROMPT_LINE_SPACING = 16;
        private const float NOISE_SCALE = 0.2f; // Scale factor for noise generation
        private const int DEFAULT_TERMINAL_WIDTH = 700;

        // Game state
        private Vector2 _playerWorldPos;
        private FastNoiseLite _noise;

        // Input system
        private string _currentInput = "";
        private List<string> _inputHistory = new List<string>();
        private List<ColoredLine> _wrappedHistory = new List<ColoredLine>();
        private KeyboardState _previousKeyboardState;
        private List<Vector2> _pendingPath = new List<Vector2>(); // Path preview
        private float _moveTimer = 0f;
        private const float MOVE_DELAY = 0.2f; // Seconds between moves
        private bool _isExecutingPath = false;
        private int _currentPathIndex = 0;
        private bool _isFreeMoveMode = false;
        private HashSet<Keys> _processedKeys = new HashSet<Keys>();
        private int _scrollOffset = 0;
        private const int MAX_HISTORY_LINES = 200;
        private const int TERMINAL_HEIGHT = 600;
        private string _clipboard = "";
        private bool _controlPressed = false;
        private float _backspaceTimer = 0f;
        private float _backspaceDelay = 0.3f; // Initial delay
        private const float MIN_BACKSPACE_DELAY = 0.02f; // Fastest backspace speed
        private const float BACKSPACE_ACCELERATION = 0.95f; // How quickly backspace accelerates
        private bool _backspaceHeld = false;
        private int _cursorPosition; // For future cursor support
        private List<string> _commandHistory = new List<string>();
        private int _commandHistoryIndex = -1; // -1 means no history selection
        private string _currentEditingCommand = ""; // Stores what user was typing before browsing history
        private int _nextLineNumber = 1;
        private List<string> _autoCompleteSuggestions = new List<string>();
        private int _selectedSuggestionIndex = -1;
        private bool _showingSuggestions = false;

        // Command system
        private Dictionary<string, Action<string[]>> _commands;

        // UI Colors
        private Color _waterColor = Color.CornflowerBlue;
        private Color _flatlandColor = Color.DarkGray;
        private Color _hillColor = Color.White;
        private Color _mountainColor = Color.White;
        private Color _playerColor = Color.Red;
        private Color _pathColor = Color.Yellow;
        private Color _pathEndColor = Color.Orange;
        private Color _terminalBg = Color.Black;
        private Color _terminalTextColor = Color.White;

        // Noise Amounts
        private const float waterLevel = 0.3f;
        private const float flatlandsLevel = 0.6f;
        private const float hillsLevel = 0.7f;
        private const float mountainsLevel = 0.8f;

        // Grid element structure for sprite support
        public struct GridElement
        {
            public Texture2D Texture;
            public Color Color;
            public Vector2 Position;

            public GridElement(Texture2D texture, Color color, Vector2 position)
            {
                Texture = texture;
                Color = color;
                Position = position;
            }
        }

        // Sprite textures
        private Texture2D _waterSprite;
        private Texture2D _flatlandSprite;
        private Texture2D _hillSprite;
        private Texture2D _mountainSprite;
        private Texture2D _peakSprite;
        private Texture2D _playerSprite;
        private Texture2D _pathSprite;
        private Texture2D _pathEndSprite;

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set window size to accommodate UI
            _graphics.PreferredBackBufferWidth = 1200;
            _graphics.PreferredBackBufferHeight = 800;
        }

        protected override void Initialize()
        {
            _playerWorldPos = new Vector2(0, 0); // Start at world origin

            // Initialize noise generator
            _noise = new FastNoiseLite();
            _noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            _noise.SetSeed(RandomNumberGenerator.GetInt32(0, 99999));
            _noise.SetFrequency(NOISE_SCALE);

            InitializeCommands();

            base.Initialize();
        }

        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Action<string[]>>();

            _commands["help"] = (args) =>
            {
                AddOutputToHistory("Available commands:");
                AddOutputToHistory(" help - Show this help message");
                AddOutputToHistory(" look - Look around current area");
                AddOutputToHistory(" up/down/left/right <count> - Queue movement");
                AddOutputToHistory(" submit - Execute queued path");
                AddOutputToHistory(" clear - Clear pending path");
                AddOutputToHistory(" pos - Show current position");
            };

            _commands["look"] = (args) =>
            {
                int x = (int)_playerWorldPos.X;
                int y = (int)_playerWorldPos.Y;
                float noise = GetNoiseAt(x, y);
                string terrain = GetTerrainDescription(noise);
                AddOutputToHistory($"You are standing on {terrain}.");
                AddOutputToHistory($"Position: ({x}, {y})");
                AddOutputToHistory($"Terrain value: {noise:F2}");
            };

            _commands["up"] = (args) => QueueMovement(new Vector2(0, -1), args);
            _commands["down"] = (args) => QueueMovement(new Vector2(0, 1), args);
            _commands["left"] = (args) => QueueMovement(new Vector2(-1, 0), args);
            _commands["right"] = (args) => QueueMovement(new Vector2(1, 0), args);

            //_commands["submit"] = (args) => {
            //    if (_pendingPath.Count > 0 && !_isExecutingPath)
            //    {
            //        _isExecutingPath = true;
            //        _currentPathIndex = 0;
            //        AddOutputToHistory($"Executing path with {_pendingPath.Count} steps...");
            //    }
            //    else if (_isExecutingPath)
            //    {
            //        AddOutputToHistory("Already executing a path.");
            //    }
            //    else
            //    {
            //        AddOutputToHistory("No path queued.");
            //    }
            //};

            _commands["clear"] = (args) =>
            {
                if (!_isExecutingPath)
                {
                    _pendingPath.Clear();
                    AddOutputToHistory("Pending path cleared.");
                }
                else
                {
                    AddOutputToHistory("Cannot clear path while executing.");
                }
            };

            _commands["pos"] = (args) =>
            {
                AddOutputToHistory($"Current position: ({(int)_playerWorldPos.X}, {(int)_playerWorldPos.Y})");
                AddOutputToHistory($"Pending path steps: {_pendingPath.Count}");
                if (_isExecutingPath)
                {
                    AddOutputToHistory($"Executing path: step {_currentPathIndex + 1}/{_pendingPath.Count}");
                }
            };

            _commands["move"] = (args) =>
            {
                _isFreeMoveMode = true;
                AddOutputToHistory("[gold]Free move enabled.");
            };

            _commands["debugallcolors"] = (args) =>
            {
                DebugAllColors();
            };

            _commands["exit"] = (args) =>
            {
                Exit();
            };
        }

        private void QueueMovement(Vector2 direction, string[] args)
        {
            if (_isExecutingPath)
            {
                AddOutputToHistory("Cannot queue movements while executing a path.");
                return;
            }

            int count = 1;
            if (args.Length > 1 && int.TryParse(args[1], out int parsedCount))
            {
                count = Math.Max(1, Math.Min(20, parsedCount)); // Limit to reasonable range
            }

            // Check for backtracking from the end of the path
            Vector2 oppositeDirection = -direction;
            int removedSteps = 0;

            // Remove steps from the end that match the opposite direction
            while (_pendingPath.Count > 0 && removedSteps < count)
            {
                Vector2 lastStep = _pendingPath.Last();
                Vector2 prevPos = _pendingPath.Count > 1 ? _pendingPath[_pendingPath.Count - 2] : _playerWorldPos;
                Vector2 lastDirection = lastStep - prevPos;

                if (lastDirection == oppositeDirection)
                {
                    _pendingPath.RemoveAt(_pendingPath.Count - 1);
                    removedSteps++;
                }
                else
                {
                    break;
                }
            }

            // Add remaining forward steps
            int remainingSteps = count - removedSteps;
            if (remainingSteps > 0)
            {
                Vector2 currentPos = _pendingPath.Count > 0 ? _pendingPath.Last() : _playerWorldPos;

                for (int i = 0; i < remainingSteps; i++)
                {
                    currentPos += direction;
                    _pendingPath.Add(currentPos);
                }
            }

            if (removedSteps > 0)
            {
                AddOutputToHistory($"[teal]Backtracked {removedSteps} time(s), added {remainingSteps} move(s)");
            }
            else
            {
                AddOutputToHistory($"Queued {count} move(s) [dimgray]{args[0].ToUpper()}");
            }
        }

        private float GetNoiseAt(int x, int y)
        {
            float noiseValue = _noise.GetNoise(x, y);
            // Clamp to 0-1 range
            return (noiseValue + 1f) / 2f;
        }

        private string GetTerrainDescription(float noise)
        {
            if (noise < waterLevel) return "deep water";
            if (noise < flatlandsLevel) return "flatlands";
            if (noise < hillsLevel) return "hills";
            if (noise < mountainsLevel) return "mountains";
            return "peaks";
        }

        private void AddToHistory(string message, Color? baseColor = null)
        {
            _inputHistory.Add(message);

            // Parse colored message
            var coloredLine = ParseColoredText(message, baseColor);
            coloredLine.LineNumber = _nextLineNumber++;

            // Wrap the colored line
            var wrappedLines = WrapColoredText(coloredLine, GetTerminalWidthInChars());
            foreach (var line in wrappedLines)
            {
                _wrappedHistory.Add(line);
            }

            // Limit total wrapped lines
            while (_wrappedHistory.Count > MAX_HISTORY_LINES)
            {
                _wrappedHistory.RemoveAt(0);
            }

            if (_inputHistory.Count > 50)
            {
                _inputHistory.RemoveAt(0);
            }
        }

        private void AddOutputToHistory(string output)
        {
            AddToHistory(output, Color.Gray);
        }

        private ColoredLine ParseColoredText(string text, Color? baseColor = null)
        {
            var line = new ColoredLine();
            var currentColor = baseColor ?? _terminalTextColor;
            var currentText = "";

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    // Add current text segment
                    if (currentText.Length > 0)
                    {
                        line.Segments.Add(new ColoredText(currentText, currentColor));
                        currentText = "";
                    }

                    // Find the closing bracket
                    int closeIndex = text.IndexOf(']', i);
                    if (closeIndex != -1)
                    {
                        string colorTag = text.Substring(i + 1, closeIndex - i - 1);
                        i = closeIndex;

                        // Parse color
                        if (colorTag == "/")
                        {
                            currentColor = _terminalTextColor; // Reset to default
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

            // Add final segment
            if (currentText.Length > 0)
            {
                line.Segments.Add(new ColoredText(currentText, currentColor));
            }

            return line;
        }

        private Color ParseColor(string colorName)
        {
            // First check the hardcoded common colors for performance
            switch (colorName.ToLower())
            {
                case "red": return Color.Red;
                case "green": return Color.Green;
                case "blue": return Color.Blue;
                case "yellow": return Color.Yellow;
                case "cyan": return Color.Cyan;
                case "magenta": return Color.Magenta;
                case "white": return Color.White;
                case "orange": return Color.Orange;
                case "gray": return Color.Gray;
                case "grey": return Color.Gray; // Common alternate spelling
            }
    
            // If not found in common colors, use reflection to find XNA color
            try
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
    
            // Return default color if not found
            return _terminalTextColor;
        }

        private List<ColoredLine> WrapColoredText(ColoredLine line, int maxWidth)
        {
            var wrappedLines = new List<ColoredLine>();
    
            // First, check if any segment contains newlines
            var processedSegments = new List<ColoredText>();
    
            foreach (var segment in line.Segments)
            {
                if (segment.Text.Contains('\n'))
                {
                    // Split segment by newlines
                    string[] lines = segment.Text.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        processedSegments.Add(new ColoredText(lines[i], segment.Color));
                
                        // Add a special marker for line breaks (except for the last line)
                        if (i < lines.Length - 1)
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
    
            // Now process the segments, creating new lines when we encounter \n markers
            var currentLine = new ColoredLine { LineNumber = line.LineNumber };
            var currentWords = new List<string>();
            var currentColors = new List<Color>();
            int currentLineWidth = 0;

            foreach (var segment in processedSegments)
            {
                if (segment.Text == "\n")
                {
                    // Finish current line and start new one
                    if (currentWords.Count > 0 || wrappedLines.Count == 0)
                    {
                        var finishedLine = CombineColoredSegments(currentWords, currentColors);
                        finishedLine.LineNumber = wrappedLines.Count == 0 ? line.LineNumber : 0;
                        wrappedLines.Add(finishedLine);
                
                        currentWords.Clear();
                        currentColors.Clear();
                        currentLineWidth = 0;
                    }
            
                    // Start new line
                    currentLine = new ColoredLine { LineNumber = line.LineNumber };
                    continue;
                }
        
                // Process normal text segment (rest of the original logic)
                var words = segment.Text.Split(' ', StringSplitOptions.None);

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
                    // Color changed - add current segment and start new one
                    if (currentText.Length > 0)
                    {
                        line.Segments.Add(new ColoredText(currentText.ToString(), currentColor));
                        currentText.Clear();
                    }
                    currentColor = colors[i];
                }
        
                currentText.Append(words[i]);
            }
    
            // Add final segment
            if (currentText.Length > 0)
            {
                line.Segments.Add(new ColoredText(currentText.ToString(), currentColor));
            }
    
            return line;
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            try
            {
                _font = Content.Load<SpriteFont>("Fonts/Px437_IBM_BIOS");
            }
            catch
            {
                throw new Exception("Please add a SpriteFont called 'Px437_IBM_BIOS' to your Content/Fonts folder");
            }

            // Load sprite textures - try to load from Content, create fallback if not found
            try
            {
                _waterSprite = Content.Load<Texture2D>("Sprites/water");
            }
            catch
            {
                _waterSprite = CreateColoredTexture(8, 8, Color.Blue);
            }

            try
            {
                _flatlandSprite = Content.Load<Texture2D>("Sprites/flatland");
            }
            catch
            {
                _flatlandSprite = CreateColoredTexture(8, 8, Color.Green);
            }

            try
            {
                _hillSprite = Content.Load<Texture2D>("Sprites/hill");
            }
            catch
            {
                _hillSprite = CreateColoredTexture(8, 8, Color.DarkGray);
            }

            try
            {
                _mountainSprite = Content.Load<Texture2D>("Sprites/mountain");
            }
            catch
            {
                _mountainSprite = CreateColoredTexture(8, 8, Color.Gray);
            }

            try
            {
                _peakSprite = Content.Load<Texture2D>("Sprites/peak");
            }
            catch
            {
                _peakSprite = CreateColoredTexture(8, 8, Color.White);
            }

            try
            {
                _playerSprite = Content.Load<Texture2D>("Sprites/player");
            }
            catch
            {
                _playerSprite = CreatePlayerTexture();
            }

            try
            {
                _pathSprite = Content.Load<Texture2D>("Sprites/path");
            }
            catch
            {
                _pathSprite = CreatePathTexture();
            }

            try
            {
                _pathEndSprite = Content.Load<Texture2D>("Sprites/pathEnd");
            }
            catch
            {
                _pathEndSprite = CreatePathEndTexture();
            }
        }

        private Texture2D CreateColoredTexture(int width, int height, Color color)
        {
            var texture = new Texture2D(GraphicsDevice, width, height);
            var colorData = new Color[width * height];
            for (int i = 0; i < colorData.Length; i++)
            {
                colorData[i] = color;
            }
            texture.SetData(colorData);
            return texture;
        }

        private Texture2D CreatePlayerTexture()
        {
            var texture = new Texture2D(GraphicsDevice, 8, 8);
            var colorData = new Color[64];

            // Create a simple diamond shape for player
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int distance = Math.Abs(x - 4) + Math.Abs(y - 4);
                    if (distance <= 3)
                    {
                        colorData[y * 8 + x] = _playerColor;
                    }
                    else
                    {
                        colorData[y * 8 + x] = Color.Transparent;
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        private Texture2D CreatePathTexture()
        {
            var texture = new Texture2D(GraphicsDevice, 8, 8);
            var colorData = new Color[64];

            // Create a simple dot for path
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int distance = (x - 4) * (x - 4) + (y - 4) * (y - 4);
                    if (distance <= 4)
                    {
                        colorData[y * 8 + x] = _pathColor;
                    }
                    else
                    {
                        colorData[y * 8 + x] = Color.Transparent;
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        private Texture2D CreatePathEndTexture()
        {
            var texture = new Texture2D(GraphicsDevice, 8, 8);
            var colorData = new Color[64];

            // Create an X shape for path end
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    if (x == y || x == (7 - y))
                    {
                        colorData[y * 8 + x] = _pathEndColor;
                    }
                    else
                    {
                        colorData[y * 8 + x] = Color.Transparent;
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                Exit();

            HandleInput(gameTime); // Pass gameTime here
            UpdateMovement(gameTime);

            base.Update(gameTime);
        }

        private void HandleInput(GameTime gameTime)
        {
            KeyboardState currentKeyboardState = Keyboard.GetState();
            Keys[] pressedKeys = currentKeyboardState.GetPressedKeys();

            // Check for control key
            _controlPressed = currentKeyboardState.IsKeyDown(Keys.LeftControl) || 
                             currentKeyboardState.IsKeyDown(Keys.RightControl);

            if (_isFreeMoveMode)
            {
                // Handle freemove mode input
                foreach (Keys key in pressedKeys)
                {
                    if (!_previousKeyboardState.IsKeyDown(key) && !_processedKeys.Contains(key))
                    {
                        _processedKeys.Add(key);

                        switch (key)
                        {
                            case Keys.W:
                            case Keys.Up:
                                QueueMovement(new Vector2(0, -1), new string[] { "up", "1" });
                                break;
                            case Keys.S:
                            case Keys.Down:
                                QueueMovement(new Vector2(0, 1), new string[] { "down", "1" });
                                break;
                            case Keys.A:
                            case Keys.Left:
                                QueueMovement(new Vector2(-1, 0), new string[] { "left", "1" });
                                break;
                            case Keys.D:
                            case Keys.Right:
                                QueueMovement(new Vector2(1, 0), new string[] { "right", "1" });
                                break;
                            case Keys.Enter:
                                if (_pendingPath.Count > 0 && !_isExecutingPath)
                                {
                                    _isExecutingPath = true;
                                    _currentPathIndex = 0;
                                    AddOutputToHistory($"Executing path of [teal]{_pendingPath.Count}[gray] move(s)...");
                                }
                                else if (_isExecutingPath)
                                {
                                    AddOutputToHistory("[dimgray]Already executing a path.");
                                }
                                else
                                {
                                    AddOutputToHistory("[dimgray]No path queued.");
                                }
                                break;
                            case Keys.Escape:
                                _isFreeMoveMode = false;
                                _processedKeys.Clear();
                                AddOutputToHistory("[gold]Free move disabled.");
                                break;
                        }
                    }
                }

                // Clear processed keys that are no longer pressed
                _processedKeys.RemoveWhere(key => !currentKeyboardState.IsKeyDown(key));
            }
            else
            {
                // Handle enhanced text input
                foreach (Keys key in pressedKeys)
                {
                    if (!_previousKeyboardState.IsKeyDown(key))
                    {
                        if (key == Keys.Enter)
                        {
                            _showingSuggestions = false;
                            if (string.IsNullOrEmpty(_currentInput.Trim()) && _pendingPath.Count > 0 && !_isExecutingPath)
                            {
                                _isExecutingPath = true;
                                _currentPathIndex = 0;
                                AddOutputToHistory($"Executing path with {_pendingPath.Count} steps...");
                            }
                            else
                            {
                                // Save command to history if it's not empty
                                if (!string.IsNullOrEmpty(_currentInput.Trim()))
                                {
                                    _commandHistory.Add(_currentInput.Trim());
                                    // Keep history to reasonable size
                                    if (_commandHistory.Count > 50)
                                    {
                                        _commandHistory.RemoveAt(0);
                                    }
                                }
                                ProcessCommand(_currentInput.Trim().ToLower());
                
                                // Reset history navigation
                                _commandHistoryIndex = -1;
                                _currentEditingCommand = "";
                            }
                            _currentInput = "";
                            _cursorPosition = 0;
                        }
                        else if (key == Keys.Tab)
                        {
                            if (_showingSuggestions && _selectedSuggestionIndex >= 0)
                            {
                                _currentInput = _autoCompleteSuggestions[_selectedSuggestionIndex];
                                _cursorPosition = _currentInput.Length;
                                _showingSuggestions = false;
                            }
                        }
                        else if (key == Keys.Up && _showingSuggestions)
                        {
                            _selectedSuggestionIndex = Math.Min(_autoCompleteSuggestions.Count - 1, _selectedSuggestionIndex + 1);
                        }
                        else if (key == Keys.Down && _showingSuggestions)
                        {
                            _selectedSuggestionIndex = Math.Max(0, _selectedSuggestionIndex - 1);
                        }
                        else if (key == Keys.Up)
                        {
                            NavigateCommandHistory(1); // Go to previous (older) command
                        }
                        else if (key == Keys.Down)
                        {
                            NavigateCommandHistory(-1); // Go to next (newer) command
                        }
                        else if (key == Keys.Escape && _pendingPath.Count > 0 && !_isExecutingPath)
                        {
                            _pendingPath.Clear();
                            AddOutputToHistory("Pending path cleared.");
                        }
                        else if (_controlPressed)
                        {
                            HandleControlCommands(key);
                        }
                        else if (key == Keys.Back)
                        {
                            HandleBackspace();
                            UpdateAutoCompleteSuggestions();
                            _backspaceHeld = true;
                            _backspaceTimer = 0f;
                            _backspaceDelay = 0.3f;
                        }
                        else if (key == Keys.Delete)
                        {
                            // Delete character at cursor (for future cursor implementation)
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
                            int maxVisibleLines = (TERMINAL_HEIGHT - 80) / FONT_SIZE;
                            _scrollOffset = Math.Min(_scrollOffset + 5, Math.Max(0, _wrappedHistory.Count - maxVisibleLines));
                        }
                        else if (key == Keys.PageDown)
                        {
                            _scrollOffset = Math.Max(_scrollOffset - 5, 0);
                        }
                        else
                        {
                            // Handle regular character input
                            HandleCharacterInput(key);
                        }
                    }
                }

                // Handle held backspace with acceleration
                if (_backspaceHeld && currentKeyboardState.IsKeyDown(Keys.Back))
                {
                    _backspaceTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (_backspaceTimer >= _backspaceDelay)
                    {
                        HandleBackspace();
                        _backspaceTimer = 0f;
                        _backspaceDelay = Math.Max(MIN_BACKSPACE_DELAY, _backspaceDelay * BACKSPACE_ACCELERATION);
                    }
                }
                else if (_backspaceHeld)
                {
                    _backspaceHeld = false;
                }
            }

            _previousKeyboardState = currentKeyboardState;
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
                        AddOutputToHistory($"Cut text to clipboard: '{_clipboard}'   (CTRL + X)");
                    }
                    break;
            
                case Keys.V: // Paste
                    if (!string.IsNullOrEmpty(_clipboard))
                    {
                        _currentInput += _clipboard;
                        _cursorPosition = _currentInput.Length;
                        AddOutputToHistory($"Pasted from clipboard: '{_clipboard}'   (CTRL + V)");
                    }
                    break;
            
                case Keys.A: // Select All / Clear (without saving to clipboard)
                    if (!string.IsNullOrEmpty(_currentInput))
                    {
                        _currentInput = "";
                        _cursorPosition = 0;
                        AddOutputToHistory("Input cleared   (CTRL + A)");
                    }
                    break;
            
                // Removed Ctrl+Z case
            }
        }

        private void NavigateCommandHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;

            // If we're not currently browsing history, save what the user was typing
            if (_commandHistoryIndex == -1)
            {
                _currentEditingCommand = _currentInput;
            }

            // Calculate new index
            int newIndex = _commandHistoryIndex + direction;

            if (newIndex < -1)
            {
                // Already at the beginning, don't go further
                return;
            }
            else if (newIndex >= _commandHistory.Count)
            {
                // Already at the end, don't go further
                return;
            }
            else if (newIndex == -1)
            {
                // Back to current editing (what user was typing before browsing)
                _currentInput = _currentEditingCommand;
                _commandHistoryIndex = -1;
            }
            else
            {
                // Navigate to specific history entry
                _commandHistoryIndex = newIndex;
                _currentInput = _commandHistory[_commandHistory.Count - 1 - _commandHistoryIndex];
            }

            _cursorPosition = _currentInput.Length;
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

            UpdateAutoCompleteSuggestions();
        }

        private void ProcessCommand(string input)
        {
            if (string.IsNullOrEmpty(input)) return;

            AddToHistory($"> {input}");

            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string command = parts[0];

            if (_commands.ContainsKey(command))
            {
                _commands[command](parts);
            }
            else
            {
                AddOutputToHistory($"Unknown command: {command}. Type 'help' for available commands.");
            }
        }

        private void UpdateMovement(GameTime gameTime)
        {
            if (_isExecutingPath && _pendingPath.Count > 0)
            {
                _moveTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_moveTimer >= MOVE_DELAY)
                {
                    if (_currentPathIndex < _pendingPath.Count)
                    {
                        _playerWorldPos = _pendingPath[_currentPathIndex];
                        _currentPathIndex++;
                        _moveTimer = 0f;

                        // Check if we've completed the path
                        if (_currentPathIndex >= _pendingPath.Count)
                        {
                            _isExecutingPath = false;
                            _pendingPath.Clear();
                            _currentPathIndex = 0;
                            AddOutputToHistory("Path execution completed.");
                        }
                    }
                }
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin();

            DrawMap();
            DrawTerminal();

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawMap()
        {
            int mapStartX = 50;
            int mapStartY = 50;
            int mapWidth = VIEW_SIZE * CELL_SIZE + 10;
            int mapHeight = VIEW_SIZE * CELL_SIZE + 30;

            // Draw map border
            var pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            // Border rectangle
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, mapWidth, 2), Color.White); // Top
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY + mapHeight - 27, mapWidth, 2), Color.White); // Bottom
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX - 5, mapStartY - 25, 2, mapHeight), Color.White); // Left
            _spriteBatch.Draw(pixel, new Rectangle(mapStartX + mapWidth - 7, mapStartY - 25, 2, mapHeight), Color.White); // Right

            // Draw map title
            _spriteBatch.DrawString(_font, $"Map View - Pos: ({(int)_playerWorldPos.X}, {(int)_playerWorldPos.Y})",
                new Vector2(mapStartX, mapStartY - 20), Color.White);

            // Generate grid elements
            var gridElements = GenerateMapGridElements(mapStartX, mapStartY);

            // Draw each grid element
            foreach (var element in gridElements)
            {
                DrawGridElement(element);
            }
        }

        private List<GridElement> GenerateMapGridElements(int mapStartX, int mapStartY)
        {
            var elements = new List<GridElement>();

            // Calculate visible area centered on player
            int startX = (int)_playerWorldPos.X - VIEW_SIZE / 2;
            int startY = (int)_playerWorldPos.Y - VIEW_SIZE / 2;

            for (int y = 0; y < VIEW_SIZE; y++)
            {
                for (int x = 0; x < VIEW_SIZE; x++)
                {
                    int worldX = startX + x;
                    int worldY = startY + y;

                    float noise = GetNoiseAt(worldX, worldY);
                    Texture2D texture = GetTerrainTexture(noise);
                    Color color = GetTerrainColor(noise);

                    // Check if this is the player position
                    bool isPlayer = (worldX == (int)_playerWorldPos.X && worldY == (int)_playerWorldPos.Y);

                    // Check if this is part of the pending path
                    bool isPath = false;
                    bool isPathEnd = false;
                    Vector2 worldPos = new Vector2(worldX, worldY);

                    if (_pendingPath.Contains(worldPos))
                    {
                        isPath = true;
                        isPathEnd = worldPos == _pendingPath.Last();
                    }

                    if (isPlayer)
                    {
                        texture = _playerSprite;
                        color = Color.White; // Use white to preserve sprite colors
                    }
                    else if (isPathEnd)
                    {
                        texture = _pathEndSprite;
                        color = Color.White;
                    }
                    else if (isPath)
                    {
                        texture = _pathSprite;
                        color = Color.White;
                    }

                    // Calculate grid position (centered in cell)
                    Vector2 gridPos = new Vector2(
                        mapStartX + x * CELL_SIZE,
                        mapStartY + y * CELL_SIZE
                    );

                    elements.Add(new GridElement(texture, color, gridPos));
                }
            }

            return elements;
        }

        private Texture2D GetTerrainTexture(float noise)
        {
            if (noise < waterLevel) return _waterSprite;
            if (noise < flatlandsLevel) return _flatlandSprite;
            if (noise < hillsLevel) return _hillSprite;
            if (noise < mountainsLevel) return _mountainSprite;
            return _peakSprite;
        }

        private void DrawGridElement(GridElement element)
        {
            // Scale sprites to fill the entire cell with no gaps
            Rectangle destRect = new Rectangle(
                (int)element.Position.X,
                (int)element.Position.Y,
                CELL_SIZE,
                CELL_SIZE
            );

            _spriteBatch.Draw(element.Texture, destRect, element.Color);
        }

        private char GetTerrainSymbol(float noise)
        {
            // Legacy method - kept for compatibility but no longer used
            if (noise < waterLevel) return '░';
            if (noise < flatlandsLevel) return '∙';
            if (noise < hillsLevel) return '^';
            if (noise < mountainsLevel) return 'n';
            return 'A';
        }

        private Color GetTerrainColor(float noise)
        {
            if (noise < waterLevel) return _waterColor;
            if (noise < flatlandsLevel) return _flatlandColor;
            if (noise < hillsLevel) return _hillColor;
            if (noise < mountainsLevel) return _mountainColor;
            return _mountainColor;
        }

        private void DrawTerminal()
        {
            int terminalX = 400;
            int terminalY = 50;
            int terminalWidth = DEFAULT_TERMINAL_WIDTH;
            int terminalHeight = 600;

            // Create pixel texture for drawing rectangles
            var pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            // Draw terminal background
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, terminalHeight + 30), _terminalBg);

            // Draw terminal border with thicker lines (2 pixels thick)
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, 2), Color.White); // Top
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY + terminalHeight + 3, terminalWidth + 10, 2), Color.White); // Bottom
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, 2, terminalHeight + 30), Color.White); // Left
            _spriteBatch.Draw(pixel, new Rectangle(terminalX + terminalWidth + 3, terminalY - 25, 2, terminalHeight + 30), Color.White); // Right

            // Draw terminal title
            _spriteBatch.DrawString(_font, "Terminal Output", new Vector2(terminalX, terminalY - 20), _terminalTextColor);

            // Draw wrapped command history (bottom-up with scrolling)
            int maxVisibleLines = (terminalHeight - 40) / TERMINAL_LINE_SPACING; // Reduced from 80 to 40
            int totalLines = _wrappedHistory.Count;
            int startIndex = Math.Max(0, totalLines - maxVisibleLines - _scrollOffset);
            int endIndex = Math.Min(totalLines, startIndex + maxVisibleLines);

            for (int i = startIndex; i < endIndex; i++)
            {
                int lineIndex = i - startIndex;
                float x = terminalX;
                float y = terminalY + lineIndex * TERMINAL_LINE_SPACING;

                foreach (var segment in _wrappedHistory[i].Segments)
                {
                    _spriteBatch.DrawString(_font, segment.Text, new Vector2(x, y), segment.Color);
                    x += _font.MeasureString(segment.Text).X;
                }
    
                // Add this after the foreach loop:
                if (_wrappedHistory[i].LineNumber > 0) // Only show numbers for actual content lines
                {
                    string lineNumText = _wrappedHistory[i].LineNumber.ToString();
                    float lineNumX = terminalX + 710; // Position outside terminal, to the right
                    _spriteBatch.DrawString(_font, lineNumText, new Vector2(lineNumX, y), Color.DimGray);
                }
            }

            // Draw scroll indicator only when there's content that can be scrolled
            bool canScrollUp = _scrollOffset > 0;
            bool canScrollDown = _wrappedHistory.Count > maxVisibleLines;

            if (canScrollUp || canScrollDown)
            {
                string scrollIndicator;
                if (_scrollOffset > 0)
                {
                    scrollIndicator = $"(PgUp/PgDn to scroll) ^ Scrolled up {_scrollOffset} lines";
                }
                else
                {
                    scrollIndicator = "(PgUp/PgDn to scroll)";
                }
    
                int scrollY = terminalY + (endIndex - startIndex) * TERMINAL_LINE_SPACING + 5;
                _spriteBatch.DrawString(_font, scrollIndicator, new Vector2(terminalX, scrollY), Color.Gold);
            }

            // Draw separator line above input with thicker line (2 pixels thick)
            int inputLineY = terminalY + terminalHeight - 20;
            int separatorY = inputLineY - 5;
            _spriteBatch.Draw(pixel, new Rectangle(terminalX, separatorY, 690, 2), Color.Gray);

            // Then draw the input line
            string inputDisplay = $"> {_currentInput}_";
            string wrappedInput = WrapText(inputDisplay, GetTerminalWidthInChars());
            _spriteBatch.DrawString(_font, wrappedInput, new Vector2(terminalX, inputLineY), Color.Khaki);

            // Draw autocomplete suggestions
            if (_showingSuggestions && _autoCompleteSuggestions.Count > 0)
            {
                int suggestionY = inputLineY - 20;
                int visibleSuggestions = Math.Min(_autoCompleteSuggestions.Count, 5);
    
                // Calculate background dimensions
                int maxSuggestionWidth = 0;
                for (int i = 0; i < visibleSuggestions; i++)
                {
                    string prefix = (i == _selectedSuggestionIndex) ? " >" : "  ";
                    string fullText = prefix + _autoCompleteSuggestions[i];
                    int textWidth = (int)_font.MeasureString(fullText).X;
                    maxSuggestionWidth = Math.Max(maxSuggestionWidth, textWidth);
                }
    
                // Draw background rectangle
                int backgroundHeight = visibleSuggestions * FONT_SIZE;
                int backgroundY = suggestionY - (visibleSuggestions - 1) * FONT_SIZE;
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, maxSuggestionWidth + 4, backgroundHeight), Color.Black);
    
                // Draw border around suggestions
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, maxSuggestionWidth + 4, 1), Color.Gray); // Top
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY + backgroundHeight, maxSuggestionWidth + 4, 1), Color.Gray); // Bottom
                _spriteBatch.Draw(pixel, new Rectangle(terminalX, backgroundY, 1, backgroundHeight), Color.Gray); // Left
                _spriteBatch.Draw(pixel, new Rectangle(terminalX + maxSuggestionWidth + 4, backgroundY, 1, backgroundHeight), Color.Gray); // Right
    
                // Draw suggestions
                for (int i = 0; i < visibleSuggestions; i++)
                {
                    Color suggestionColor = (i == _selectedSuggestionIndex) ? Color.Khaki : Color.Gray;
                    string prefix = (i == _selectedSuggestionIndex) ? " >" : "  ";
                    _spriteBatch.DrawString(_font, prefix + _autoCompleteSuggestions[i], 
                        new Vector2(terminalX + 2, suggestionY - i * FONT_SIZE), suggestionColor);
                }
            }

            // Draw status line OUTSIDE terminal (below it)
            int statusY = terminalY + terminalHeight + 15;
            string statusText = $"Path: {_pendingPath.Count} steps";
            if (_isExecutingPath)
            {
                statusText += $" | Executing: {_currentPathIndex + 1}/{_pendingPath.Count}";
            }
            string wrappedStatus = WrapText(statusText, GetTerminalWidthInChars());
            _spriteBatch.DrawString(_font, wrappedStatus, new Vector2(terminalX, statusY), Color.Gray);

            // Draw prompt line OUTSIDE terminal (below status)
            int promptY = statusY + (wrappedStatus.Split('\n').Length * TERMINAL_LINE_SPACING) + 10;
            string promptText = GetPromptText();
            if (!string.IsNullOrEmpty(promptText))
            {
                var coloredPrompt = ParseColoredText(promptText, Color.Khaki);
                var wrappedPromptLines = WrapColoredText(coloredPrompt, GetTerminalWidthInChars());
    
                for (int i = 0; i < wrappedPromptLines.Count; i++)
                {
                    float x = terminalX;
                    float y = promptY + i * PROMPT_LINE_SPACING;
        
                    foreach (var segment in wrappedPromptLines[i].Segments)
                    {
                        _spriteBatch.DrawString(_font, segment.Text, new Vector2(x, y), segment.Color);
                        x += _font.MeasureString(segment.Text).X;
                    }
                }
            }
        }

        private string GetPromptText()
        {
            if (_isFreeMoveMode)
            {
                return "[khaki]You are FREE MOVING!\n[gold]Use (W/A/S/D) to queue moves.\nPress ENTER to confirm, ESC to cancel: ";
            }
            else if (_pendingPath.Count > 0 && !_isExecutingPath)
            {
                return $"[khaki]Previewing path...\n[gold]Pending {_pendingPath.Count} queued movements...\nPress ENTER to confirm, ESC to cancel: ";
                
            }
            return "";
        }

        private string WrapText(string text, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var finalLines = new List<string>();
    
            // First split by existing newlines
            string[] existingLines = text.Split('\n');
    
            foreach (string line in existingLines)
            {
                if (line.Length <= maxCharsPerLine)
                {
                    // Line doesn't need wrapping
                    finalLines.Add(line);
                }
                else
                {
                    // Line needs wrapping
                    var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
            int terminalWidth = DEFAULT_TERMINAL_WIDTH; // Your terminal pixel width
            float charWidth = _font.MeasureString("W").X; // Use a wide character for measurement
            return (int)(terminalWidth / charWidth) - 2; // Subtract 2 for padding
        }

        private void DebugAllColors()
        {
            AddOutputToHistory("[gray]Displaying all XNA Framework colors:");
    
            // Get all static Color properties using reflection
            var colorProperties = typeof(Color).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(Color))
                .OrderBy(p => p.Name);
    
            foreach (var property in colorProperties)
            {
                string colorName = property.Name;
                Color color = (Color)property.GetValue(null);
        
                // Format as [colorname]ColorName[/] to use the color system
                AddToHistory($"[{colorName.ToLower()}]{colorName}[/]", Color.Gray);
            }
    
            AddOutputToHistory($"[gray]Total colors displayed: {colorProperties.Count()}");
        }

        private void UpdateAutoCompleteSuggestions()
        {
            _autoCompleteSuggestions.Clear();
            _selectedSuggestionIndex = -1;
    
            if (string.IsNullOrEmpty(_currentInput))
            {
                _showingSuggestions = false;
                return;
            }
    
            var matches = _commands.Keys
                .Where(cmd => cmd.StartsWith(_currentInput.ToLower()))
                .OrderBy(cmd => cmd)
                .ToList();
    
            _autoCompleteSuggestions = matches;
            _showingSuggestions = matches.Count > 0;
    
            if (_showingSuggestions)
                _selectedSuggestionIndex = 0;
        }
    }


    // Enhanced Perlin noise implementation
    public class FastNoiseLite
    {
        private int _seed;
        private float _frequency = 0.01f;
        private NoiseType _noiseType = NoiseType.Perlin;
        
        public enum NoiseType { Perlin }
        
        public void SetSeed(int seed) { _seed = seed; }
        public void SetFrequency(float frequency) { _frequency = frequency; }
        public void SetNoiseType(NoiseType noiseType) { _noiseType = noiseType; }
        
        public float GetNoise(float x, float y)
        {
            // Enhanced noise function with better distribution
            float noise1 = (float)(Math.Sin(x * _frequency + _seed) * Math.Cos(y * _frequency + _seed));
            float noise2 = (float)(Math.Sin(x * _frequency * 2 + _seed * 2) * Math.Cos(y * _frequency * 2 + _seed * 2)) * 0.5f;
            float noise3 = (float)(Math.Sin(x * _frequency * 4 + _seed * 3) * Math.Cos(y * _frequency * 4 + _seed * 3)) * 0.25f;
            
            return (noise1 + noise2 + noise3) / 1.75f;
        }
    }

    public class ColoredText
    {
        public string Text { get; set; }
        public Color Color { get; set; }
    
        public ColoredText(string text, Color color)
        {
            Text = text;
            Color = color;
        }
    }

    public class ColoredLine
    {
        public List<ColoredText> Segments { get; set; } = new List<ColoredText>();
        public int LineNumber { get; set; } = 0;
    }
}