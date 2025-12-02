using System;
using System.Threading;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A helper to handle the platform-specific mess of setting clipboard text.
    /// </summary>
    public static class ClipboardHelper
    {
        public static void SetText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Try standard SDL2 approach (MonoGame DesktopGL)
            try
            {
                Sdl.SDL_SetClipboardText(text);
                return;
            }
            catch
            {
                // Fallback or ignore if SDL not available
            }

            // Fallback: Windows Forms (requires STA thread)
            // This is a common fallback for Windows DX builds
            try
            {
                Thread thread = new Thread(() =>
                {
                    try
                    {
                        // Reflection to avoid hard dependency on System.Windows.Forms
                        Type clipboardType = Type.GetType("System.Windows.Forms.Clipboard, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                        if (clipboardType != null)
                        {
                            var method = clipboardType.GetMethod("SetText", new[] { typeof(string) });
                            method?.Invoke(null, new object[] { text });
                        }
                    }
                    catch { }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }
            catch
            {
                GameLogger.Log(LogSeverity.Warning, "Clipboard copy failed: Platform not supported.");
            }
        }

        // Minimal SDL2 P/Invoke wrapper
        private static class Sdl
        {
            [System.Runtime.InteropServices.DllImport("SDL2.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            public static extern int SDL_SetClipboardText(string text);
        }
    }
}