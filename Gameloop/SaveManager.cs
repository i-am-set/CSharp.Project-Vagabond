using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProjectVagabond
{
    public static class SaveManager
    {
        private static readonly string _saveFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProjectVagabond");
        private static readonly string _saveFilePath = Path.Combine(_saveFolderPath, "save.json");
        private static readonly object _fileLock = new object();
        private static bool _isSaveValid = false;

        public static ScoundrelSaveData CurrentSave { get; set; }

        public static bool HasSave()
        {
            lock (_fileLock)
            {
                return File.Exists(_saveFilePath);
            }
        }

        public static void SaveGame(ScoundrelSaveData data)
        {
            lock (_fileLock)
            {
                _isSaveValid = true;
            }

            // Fire and forget serialization and disk write to prevent frame stutters
            Task.Run(() =>
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = false }; // Minified for speed
                    options.Converters.Add(new JsonStringEnumConverter());
                    string jsonString = JsonSerializer.Serialize(data, options);

                    lock (_fileLock)
                    {
                        // Abort if a delete happened while we were serializing
                        if (!_isSaveValid) return;

                        Directory.CreateDirectory(_saveFolderPath);
                        File.WriteAllText(_saveFilePath, jsonString);
                    }
                }
                catch (Exception ex)
                {
                    Utils.GameLogger.Log(Utils.LogSeverity.Error, $"Error saving game: {ex.Message}");
                }
            });
        }

        public static ScoundrelSaveData LoadGame()
        {
            try
            {
                lock (_fileLock)
                {
                    if (File.Exists(_saveFilePath))
                    {
                        string jsonString = File.ReadAllText(_saveFilePath);
                        var options = new JsonSerializerOptions();
                        options.Converters.Add(new JsonStringEnumConverter());
                        return JsonSerializer.Deserialize<ScoundrelSaveData>(jsonString, options);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.GameLogger.Log(Utils.LogSeverity.Error, $"Error loading game: {ex.Message}");
            }
            return null;
        }

        public static void DeleteSave()
        {
            // Synchronous to guarantee immediate deletion (prevents save scumming via Alt+F4)
            try
            {
                lock (_fileLock)
                {
                    _isSaveValid = false;
                    if (File.Exists(_saveFilePath))
                    {
                        File.Delete(_saveFilePath);
                    }
                }
                CurrentSave = null;
            }
            catch (Exception ex)
            {
                Utils.GameLogger.Log(Utils.LogSeverity.Error, $"Error deleting save: {ex.Message}");
            }
        }
    }
}