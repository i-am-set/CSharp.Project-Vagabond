using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

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
        private const float NOISE_SCALE = 0.2f; // Scale factor for noise generation

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
        private const int TERMINAL_WIDTH_CHARS = 80; // Characters per line
        private bool _isExecutingPath = false;
        private int _currentPathIndex = 0;
        private bool _isFreeMoveMode = false;
        private HashSet<Keys> _processedKeys = new HashSet<Keys>();

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
                AddOutputToHistory("  help - Show this help message");
                AddOutputToHistory("  look - Look around current area");
                AddOutputToHistory("  up/down/left/right <count> - Queue movement");
                AddOutputToHistory("  submit - Execute queued path");
                AddOutputToHistory("  clear - Clear pending path");
                AddOutputToHistory("  pos - Show current position");
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
                AddOutputToHistory("Entered free move mode. Use WASD or arrow keys to queue movements. Press ESC to exit.");
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
                AddOutputToHistory($"Backtracked {removedSteps} step(s), added {remainingSteps} step(s). Path length: {_pendingPath.Count}");
            }
            else
            {
                AddOutputToHistory($"Queued {count} {args[0]} step(s). Path length: {_pendingPath.Count}");
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

            // Wrap the colored line
            var wrappedLines = WrapColoredText(coloredLine, TERMINAL_WIDTH_CHARS);
            foreach (var line in wrappedLines)
            {
                _wrappedHistory.Add(line);
            }

            // Limit total wrapped lines
            while (_wrappedHistory.Count > 25)
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
            output = "  " + output;
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
            return colorName.ToLower() switch
            {
                "red" => Color.Red,
                "green" => Color.Green,
                "blue" => Color.Blue,
                "yellow" => Color.Yellow,
                "cyan" => Color.Cyan,
                "magenta" => Color.Magenta,
                "white" => Color.White,
                "orange" => Color.Orange,
                "gray" => Color.Gray,
                _ => _terminalTextColor
            };
        }

        private List<ColoredLine> WrapColoredText(ColoredLine line, int maxWidth)
        {
            var wrappedLines = new List<ColoredLine>();
            var currentLine = new ColoredLine();
            int currentWidth = 0;

            foreach (var segment in line.Segments)
            {
                string remainingText = segment.Text;

                while (remainingText.Length > 0)
                {
                    int availableWidth = maxWidth - currentWidth;

                    if (remainingText.Length <= availableWidth)
                    {
                        // Fits in current line
                        currentLine.Segments.Add(new ColoredText(remainingText, segment.Color));
                        currentWidth += remainingText.Length;
                        break;
                    }
                    else
                    {
                        // Need to wrap
                        if (availableWidth > 0)
                        {
                            string part = remainingText.Substring(0, availableWidth);
                            currentLine.Segments.Add(new ColoredText(part, segment.Color));
                            remainingText = remainingText.Substring(availableWidth);
                        }

                        // Start new line
                        wrappedLines.Add(currentLine);
                        currentLine = new ColoredLine();
                        currentWidth = 0;
                    }
                }
            }

            if (currentLine.Segments.Count > 0)
            {
                wrappedLines.Add(currentLine);
            }

            return wrappedLines;
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

            HandleInput();
            UpdateMovement(gameTime);

            base.Update(gameTime);
        }

        private void HandleInput()
        {
            KeyboardState currentKeyboardState = Keyboard.GetState();
            Keys[] pressedKeys = currentKeyboardState.GetPressedKeys();

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
                                    AddOutputToHistory($"Executing path with {_pendingPath.Count} steps...");
                                }
                                else if (_isExecutingPath)
                                {
                                    AddOutputToHistory("Already executing a path.");
                                }
                                else
                                {
                                    AddOutputToHistory("No path queued.");
                                }
                                break;
                            case Keys.Escape:
                                _isFreeMoveMode = false;
                                _processedKeys.Clear();
                                AddOutputToHistory("Exited free move mode.");
                                break;
                        }
                    }
                }

                // Clear processed keys that are no longer pressed
                _processedKeys.RemoveWhere(key => !currentKeyboardState.IsKeyDown(key));
            }
            else
            {
                // Handle command line input (existing logic)
                foreach (Keys key in pressedKeys)
                {
                    if (!_previousKeyboardState.IsKeyDown(key))
                    {
                        if (key == Keys.Enter)
                        {
                            ProcessCommand(_currentInput.Trim().ToLower());
                            _currentInput = "";
                        }
                        else if (key == Keys.Back && _currentInput.Length > 0)
                        {
                            _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
                        }
                        else if (key == Keys.Space)
                        {
                            _currentInput += " ";
                        }
                        else
                        {
                            // Handle letter and number keys
                            if (key == Keys.Enter)
                            {
                                if (string.IsNullOrEmpty(_currentInput.Trim()) && _pendingPath.Count > 0 && !_isExecutingPath)
                                {
                                    // Submit pending path with empty input
                                    _isExecutingPath = true;
                                    _currentPathIndex = 0;
                                    AddOutputToHistory($"Executing path with {_pendingPath.Count} steps...");
                                }
                                else
                                {
                                    ProcessCommand(_currentInput.Trim().ToLower());
                                }
                                _currentInput = "";
                            }
                            else if (key == Keys.Escape && _pendingPath.Count > 0 && !_isExecutingPath)
                            {
                                _pendingPath.Clear();
                                AddOutputToHistory("Pending path cleared.");
                            }

                            string keyString = key.ToString();
                            if (keyString.Length == 1)
                            {
                                _currentInput += keyString.ToLower();
                            }
                            else if (keyString.StartsWith("D") && keyString.Length == 2)
                            {
                                _currentInput += keyString.Substring(1);
                            }
                        }
                    }
                }
            }

            _previousKeyboardState = currentKeyboardState;
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
            int terminalWidth = 700;
            int terminalHeight = 600;

            // Create pixel texture for drawing rectangles
            var pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            // Draw terminal background
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, terminalHeight + 30), _terminalBg);

            // Draw terminal border
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, terminalWidth + 10, 2), Color.White); // Top
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY + terminalHeight + 3, terminalWidth + 10, 2), Color.White); // Bottom
            _spriteBatch.Draw(pixel, new Rectangle(terminalX - 5, terminalY - 25, 2, terminalHeight + 30), Color.White); // Left
            _spriteBatch.Draw(pixel, new Rectangle(terminalX + terminalWidth + 3, terminalY - 25, 2, terminalHeight + 30), Color.White); // Right

            // Draw terminal title
            _spriteBatch.DrawString(_font, "Terminal Output", new Vector2(terminalX, terminalY - 20), _terminalTextColor);

            // Draw wrapped command history
            int maxLines = (terminalHeight - 80) / FONT_SIZE;
            int startIndex = Math.Max(0, _wrappedHistory.Count - maxLines);

            for (int i = startIndex; i < _wrappedHistory.Count; i++)
            {
                int lineIndex = i - startIndex;
                float x = terminalX;
                float y = terminalY + lineIndex * FONT_SIZE;

                foreach (var segment in _wrappedHistory[i].Segments)
                {
                    _spriteBatch.DrawString(_font, segment.Text, new Vector2(x, y), segment.Color);
                    x += _font.MeasureString(segment.Text).X;
                }
            }

            // Draw current input line (with wrapping)
            int inputLineY = terminalY + terminalHeight - 40;
            string inputDisplay = $"> {_currentInput}_";
            string wrappedInput = WrapText(inputDisplay, TERMINAL_WIDTH_CHARS);
            _spriteBatch.DrawString(_font, wrappedInput, new Vector2(terminalX, inputLineY), Color.Yellow);

            // Draw status line (with wrapping) - inside the terminal
            int statusY = terminalY + terminalHeight - 20;
            string statusText = $"Path: {_pendingPath.Count} steps";
            if (_isExecutingPath)
            {
                statusText += $" | Executing: {_currentPathIndex + 1}/{_pendingPath.Count}";
            }
            string wrappedStatus = WrapText(statusText, TERMINAL_WIDTH_CHARS);
            _spriteBatch.DrawString(_font, wrappedStatus, new Vector2(terminalX, statusY), Color.Cyan);

            // Draw prompt line (with wrapping) - OUTSIDE the terminal box, below it
            int promptY = terminalY + terminalHeight + 15;
            string promptText = GetPromptText();
            if (!string.IsNullOrEmpty(promptText))
            {
                string wrappedPrompt = WrapText(promptText, TERMINAL_WIDTH_CHARS);
                _spriteBatch.DrawString(_font, wrappedPrompt, new Vector2(terminalX, promptY), Color.Orange);
            }
        }

        private string GetPromptText()
        {
            if (_isFreeMoveMode)
            {
                return "You are free moving! Move with WASD or arrow keys. Press ENTER to submit movement. Press ESC to exit freemove.";
            }
            else if (_pendingPath.Count > 0 && !_isExecutingPath)
            {
                return $"Pending path of {_pendingPath.Count} movements queued. Press ENTER to submit movement. Press ESC to clear movement.";
            }
            return "";
        }

        private string WrapText(string text, int maxCharsPerLine)
        {
            if (text.Length <= maxCharsPerLine)
                return text;

            var lines = new List<string>();
            int currentIndex = 0;

            while (currentIndex < text.Length)
            {
                int remainingChars = text.Length - currentIndex;
                int charsToTake = Math.Min(maxCharsPerLine, remainingChars);

                if (charsToTake == maxCharsPerLine && currentIndex + charsToTake < text.Length)
                {
                    // Try to break at a space to avoid breaking words
                    int lastSpace = text.LastIndexOf(' ', currentIndex + charsToTake - 1, charsToTake);
                    if (lastSpace > currentIndex)
                    {
                        charsToTake = lastSpace - currentIndex;
                    }
                }

                lines.Add(text.Substring(currentIndex, charsToTake));
                currentIndex += charsToTake;

                // Skip the space if we broke at one
                if (currentIndex < text.Length && text[currentIndex] == ' ')
                {
                    currentIndex++;
                }
            }

            return string.Join("\n", lines);
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
    }
}