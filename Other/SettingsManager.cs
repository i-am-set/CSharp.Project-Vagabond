using Microsoft.Xna.Framework;
using ProjectVagabond;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond
{
    public static class SettingsManager
    {
        private static readonly string _settingsFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProjectVagabond");
        private static readonly string _settingsFilePath = Path.Combine(_settingsFolderPath, "settings.json");
        private static readonly List<KeyValuePair<string, Point>> _resolutions;

        /// <summary>
        /// Custom JSON converter for the Point struct, as System.Text.Json cannot deserialize it directly.
        /// </summary>
        public class PointJsonConverter : JsonConverter<Point>
        {
            public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException("Expected StartObject token");
                }

                int x = 0, y = 0;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return new Point(x, y);
                    }

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string propertyName = reader.GetString();
                        reader.Read();
                        switch (propertyName.ToUpperInvariant())
                        {
                            case "X":
                                x = reader.GetInt32();
                                break;
                            case "Y":
                                y = reader.GetInt32();
                                break;
                        }
                    }
                }
                throw new JsonException("Unexpected end of JSON.");
            }

            public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("X", value.X);
                writer.WriteNumber("Y", value.Y);
                writer.WriteEndObject();
            }
        }

        static SettingsManager()
        {
            _resolutions = new List<KeyValuePair<string, Point>>
            {
                new("800 x 600 (4:3)", new Point(800, 600)),
                new("960 x 540 (16:9)", new Point(960, 540)),
                new("1024 x 768 (4:3)", new Point(1024, 768)),
                new("1280 x 720 (16:9)", new Point(1280, 720)),
                new("1280 x 800 (16:10)", new Point(1280, 800)),
                new("1440 x 900 (16:10)", new Point(1440, 900)),
                new("1600 x 900 (16:9)", new Point(1600, 900)),
                new("1680 x 1050 (16:10)", new Point(1680, 1050)),
                new("1920 x 1080 (16:9)", new Point(1920, 1080)),
                new("1920 x 1200 (16:10)", new Point(1920, 1200)),
                new("2560 x 1080 (21:9)", new Point(2560, 1080)),
                new("2560 x 1440 (16:9)", new Point(2560, 1440)),
                new("3440 x 1440 (21:9)", new Point(3440, 1440)),
                new("3840 x 2160 (4K)", new Point(3840, 2160)),
            };
        }

        public static List<KeyValuePair<string, Point>> GetResolutions() => _resolutions;

        public static Point FindClosestResolution(Point targetResolution)
        {
            if (_resolutions == null || !_resolutions.Any())
            {
                return targetResolution; // Return original if list is empty
            }

            return _resolutions
                .OrderBy(res => Math.Pow(res.Value.X - targetResolution.X, 2) + Math.Pow(res.Value.Y - targetResolution.Y, 2))
                .First().Value;
        }

        public static void SaveSettings(GameSettings settings)
        {
            try
            {
                Directory.CreateDirectory(_settingsFolderPath);
                var options = new JsonSerializerOptions { WriteIndented = true };
                options.Converters.Add(new PointJsonConverter());
                options.Converters.Add(new JsonStringEnumConverter());
                string jsonString = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFilePath, jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public static GameSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string jsonString = File.ReadAllText(_settingsFilePath);
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new PointJsonConverter());
                    options.Converters.Add(new JsonStringEnumConverter());
                    var settings = JsonSerializer.Deserialize<GameSettings>(jsonString, options);

                    if (settings != null)
                    {
                        // Match loaded resolution to the closest available one
                        settings.Resolution = FindClosestResolution(settings.Resolution);
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings, using defaults: {ex.Message}");
            }

            // Return default settings if file doesn't exist or fails to load/parse
            var defaultSettings = new GameSettings();
            defaultSettings.Resolution = FindClosestResolution(defaultSettings.Resolution);
            return defaultSettings;
        }
    }
}