using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.Editor;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// Manages loading, storing, and providing access to all combat Poses from data files.
    /// </summary>
    public class PoseManager
    {
        private readonly Dictionary<string, PoseData> _poses = new Dictionary<string, PoseData>(StringComparer.OrdinalIgnoreCase);

        public PoseManager() { }

        public void LoadPoses(string directoryPath)
        {
            Debug.WriteLine($"[PoseManager] --- Loading Poses from: {Path.GetFullPath(directoryPath)} ---");

            if (!Directory.Exists(directoryPath))
            {
                Debug.WriteLine($"[PoseManager] [WARNING] Pose directory not found. No poses will be loaded.");
                return;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = {
                    new JsonStringEnumConverter(),
                    new Vector2JsonConverter()
                }
            };

            string[] poseFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);
            Debug.WriteLine($"[PoseManager] Found {poseFiles.Length} JSON files to process.");

            foreach (var file in poseFiles)
            {
                try
                {
                    string jsonContent = File.ReadAllText(file);
                    var poseData = JsonSerializer.Deserialize<PoseData>(jsonContent, jsonOptions);

                    if (poseData != null && !string.IsNullOrEmpty(poseData.Id))
                    {
                        _poses[poseData.Id] = poseData;
                        Debug.WriteLine($"[PoseManager] Successfully loaded Pose: '{poseData.Id}'");
                    }
                    else
                    {
                        Debug.WriteLine($"[PoseManager] [WARNING] Could not load pose from {file}. Invalid format or missing ID.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PoseManager] [ERROR] Failed to load or parse pose file {file}: {ex.Message}");
                }
            }
            Debug.WriteLine($"[PoseManager] --- Finished loading. Total poses loaded: {_poses.Count} ---");
        }

        public PoseData GetPose(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _poses.TryGetValue(id, out var pose);
            return pose;
        }

        public IEnumerable<PoseData> GetAllPoses()
        {
            return _poses.Values;
        }
    }
}