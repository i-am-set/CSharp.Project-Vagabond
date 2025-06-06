using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond
{
    public sealed class Global
    {
        private static readonly Global _instance = new Global();
        private Global() { }
        public static Global Instance => _instance;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // CONSTANTS
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        // Map settings Global
        public const int GRID_SIZE = 32;
        public const int GRID_CELL_SIZE = 8;
        public const int FONT_SIZE = 12;
        public const int TERMINAL_LINE_SPACING = 12;
        public const int PROMPT_LINE_SPACING = 16;
        public const float NOISE_SCALE = 0.2f;
        public const int DEFAULT_TERMINAL_WIDTH = 700;

        // Input system Global
        public const float MOVE_DELAY_SECONDS = 0.5f;
        public const int MAX_SINGLE_MOVE_LIMIT = 20;
        public const int MAX_HISTORY_LINES = 200;
        public const int TERMINAL_HEIGHT = 600;
        public const float MIN_BACKSPACE_DELAY = 0.02f;
        public const float BACKSPACE_ACCELERATION = 0.25f;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // INSTANCE VARIABLES
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        
        // Core varaibles
        public SpriteFont DefaultFont { get; set; }
        public GraphicsDeviceManager CurrentGraphics;
        public SpriteBatch CurrentSpriteBatch;

        // Terrain levels
        public float WaterLevel { get; set; } = 0.3f;
        public float FlatlandsLevel { get; set; } = 0.6f;
        public float HillsLevel { get; set; } = 0.7f;
        public float MountainsLevel { get; set; } = 0.8f;

        // Colors
        public Color WaterColor { get; set; } = Color.CornflowerBlue;
        public Color FlatlandColor { get; set; } = Color.DarkGray;
        public Color HillColor { get; set; } = Color.White;
        public Color MountainColor { get; set; } = Color.White;
        public Color PlayerColor { get; set; } = Color.Red;
        public Color PathColor { get; set; } = Color.Yellow;
        public Color PathEndColor { get; set; } = Color.Orange;
        public Color TerminalBg { get; set; } = Color.Black;
        public Color TerminalTextColor { get; set; } = Color.White;
    }
}
