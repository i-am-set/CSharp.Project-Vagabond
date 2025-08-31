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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A utility class to resolve the absolute path to the project's source directory from a running application.
    /// This is essential for tools like editors that need to modify source content files, not the copies in the build output directory.
    /// </summary>
    public static class ProjectDirectoryResolver
    {
        private static string _projectRootPath;

        /// <summary>
        /// Resolves the absolute path for a given relative path from the project's source root.
        /// </summary>
        /// <param name="relativePath">The relative path from the project root (e.g., "Content/Actions").</param>
        /// <returns>The full, absolute path to the source directory.</returns>
        public static string Resolve(string relativePath)
        {
            if (string.IsNullOrEmpty(_projectRootPath))
            {
                _projectRootPath = FindProjectRoot();
            }

            if (string.IsNullOrEmpty(_projectRootPath))
            {
                // Fallback to the application's base directory if the project root can't be found.
                // This might happen in some deployment scenarios, but for the editor, it's an error state.
                System.Diagnostics.Debug.WriteLine("[ProjectDirectoryResolver] [ERROR] Could not find project root (.sln file). Falling back to application base directory. File saves may not target the source project files.");
                return Path.GetFullPath(relativePath);
            }

            return Path.Combine(_projectRootPath, relativePath);
        }

        /// <summary>
        /// Traverses up the directory tree from the application's execution path to find the directory containing the solution (.sln) file.
        /// </summary>
        /// <returns>The absolute path to the project root, or null if not found.</returns>
        private static string FindProjectRoot()
        {
            string currentDir = AppContext.BaseDirectory;
            DirectoryInfo dirInfo = new DirectoryInfo(currentDir);

            // Search up a maximum of 10 levels to prevent infinite loops in weird file structures.
            int maxLevels = 10;
            int level = 0;

            while (dirInfo != null && level < maxLevels)
            {
                // Check if the current directory contains a .sln file.
                if (Directory.GetFiles(dirInfo.FullName, "*.sln").Length > 0)
                {
                    return dirInfo.FullName;
                }

                dirInfo = dirInfo.Parent;
                level++;
            }

            return null; // .sln file not found within the search depth.
        }
    }
}