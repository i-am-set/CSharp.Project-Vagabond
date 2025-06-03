using System.Collections.Generic;

namespace ProjectVagabond
{
    public class GameState
    {
        public int PlayerWorldX { get; set; } = 0;  // Player's actual world position
        public int PlayerWorldY { get; set; } = 0;
        public int CameraX { get; set; } = -7;      // Camera offset (player at center means camera at -7,-7)
        public int CameraY { get; set; } = -7;
        public int HP { get; set; } = 100;
        public int MaxHP { get; set; } = 100;
        public int AP { get; set; } = 20;  // Action Points
        public int MaxAP { get; set; } = 20;
        public bool IsFreeMoving { get; set; } = false;
        public List<string> OutputHistory { get; set; } = new List<string>();
        public int TotalOutputs { get; set; } = 0;
        public string CurrentTerrainType { get; set; } = "FLATLANDS";
        public string CurrentVegetation { get; set; } = "FOREST";
        public float CurrentDifficulty { get; set; } = 0.50f;
        public DisplayMode CurrentDisplayMode { get; set; } = DisplayMode.Normal;
        public string NoiseMapType { get; set; } = "terrain";
        public List<(int x, int y)> PreviewPath { get; set; } = new List<(int, int)>();
        
        public List<(string direction, int steps)> QueuedMoves { get; set; } = new List<(string, int)>();

        public NoiseMapManager NoiseManager { get; private set; }

        public GameState()
        {
            NoiseManager = new NoiseMapManager();
            UpdateCurrentTerrain();
            AddOutput("Welcome to the Text Adventure!");
            AddOutput("Type 'HELP' if needed:");
        }

        public char GetTerrainAt(int worldX, int worldY)
        {
            var mapData = NoiseManager.GetMapData(worldX, worldY);
            return mapData.TerrainSymbol;
        }

        public TerrainData GetTerrainDataAt(int worldX, int worldY)
        {
            var mapData = NoiseManager.GetMapData(worldX, worldY);
    
            return new TerrainData
            {
                TerrainType = mapData.TerrainType,
                VegetationType = mapData.VegetationType,
                Difficulty = mapData.Difficulty,
                EnergyCost = mapData.EnergyCost
            };
        }

        private void UpdateCurrentTerrain()
        {
            var currentTerrain = GetTerrainDataAt(PlayerWorldX, PlayerWorldY);
            CurrentTerrainType = currentTerrain.TerrainType;
            CurrentVegetation = currentTerrain.VegetationType;
            CurrentDifficulty = currentTerrain.Difficulty;
        }

        public void AddOutput(string message)
        {
            // Word wrap the message to fit in the output console
            var wrappedLines = WrapText(message, 60); // Approximate character limit for console width
            
            foreach (var line in wrappedLines)
            {
                OutputHistory.Add(line);
                TotalOutputs++;
            }
            
            // Keep only last 20 messages
            while (OutputHistory.Count > 20)
                OutputHistory.RemoveAt(0);
        }

        private List<string> WrapText(string text, int maxWidth)
        {
            var lines = new List<string>();
            
            if (string.IsNullOrEmpty(text))
            {
                lines.Add("");
                return lines;
            }

            var words = text.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(currentLine))
                {
                    currentLine = word;
                }
                else if ((currentLine + " " + word).Length <= maxWidth)
                {
                    currentLine += " " + word;
                }
                else
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        public void MovePlayer(string direction, int steps = 1)
        {
            for (int i = 0; i < steps; i++)
            {
                var currentTerrain = GetTerrainDataAt(PlayerWorldX, PlayerWorldY);
                
                if (AP <= 0 && !IsFreeMoving)
                {
                    AddOutput($"Cannot move {direction.ToUpper()} {steps - i} time(s)... not enough energy.");
                    break;
                }

                int newWorldX = PlayerWorldX, newWorldY = PlayerWorldY;
                
                switch (direction.ToLower())
                {
                    case "up": newWorldY--; break;
                    case "down": newWorldY++; break;
                    case "left": newWorldX--; break;
                    case "right": newWorldX++; break;
                }
                
                // Update player world position
                PlayerWorldX = newWorldX;
                PlayerWorldY = newWorldY;
                
                // Update camera to keep player centered
                CameraX = PlayerWorldX - 7;
                CameraY = PlayerWorldY - 7;
                
                // Update terrain info
                UpdateCurrentTerrain();
                
                if (!IsFreeMoving)
                {
                    AP -= currentTerrain.EnergyCost;
                    if (AP < 0) AP = 0;
                }
            }
        }

        public void Rest()
        {
            HP = MaxHP;
            AP = MaxAP;
            AddOutput("You rest and recover your health and energy.");
        }

        public void TakeDamage(int damage)
        {
            HP -= damage;
            if (HP < 0) HP = 0;
            AddOutput($"You take {damage} damage! HP: {HP}/{MaxHP}");
        }

        public TerrainData GetTerrainInDirection(string direction)
        {
            int checkWorldX = PlayerWorldX, checkWorldY = PlayerWorldY;
            
            switch (direction.ToLower())
            {
                case "up": checkWorldY--; break;
                case "down": checkWorldY++; break;
                case "left": checkWorldX--; break;
                case "right": checkWorldX++; break;
            }
            
            return GetTerrainDataAt(checkWorldX, checkWorldY);
        }
    }
}