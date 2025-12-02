using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// Intercepts System.Console and System.Diagnostics.Debug output and redirects it
    /// to the GameLogger.
    /// </summary>
    public static class ConsoleRedirection
    {
        private static GameConsoleWriter _outWriter;
        private static GameConsoleWriter _errorWriter;
        private static GameTraceListener _traceListener;

        public static void Initialize()
        {
            // Ensure the logger is ready to receive data
            GameLogger.Initialize();

            // 1. Redirect Console.Out (Standard Log)
            _outWriter = new GameConsoleWriter(LogSeverity.Info);
            Console.SetOut(_outWriter);

            // 2. Redirect Console.Error (Exceptions/Errors)
            _errorWriter = new GameConsoleWriter(LogSeverity.Error);
            Console.SetError(_errorWriter);

            // 3. Add Listener for Debug.WriteLine (Debug Output)
            _traceListener = new GameTraceListener();
            Trace.Listeners.Add(_traceListener);
        }

        private class GameConsoleWriter : TextWriter
        {
            private readonly LogSeverity _severity;
            private readonly StringBuilder _buffer = new StringBuilder();

            public override Encoding Encoding => Encoding.UTF8;

            public GameConsoleWriter(LogSeverity severity)
            {
                _severity = severity;
            }

            public override void Write(char value)
            {
                if (value == '\n') FlushBuffer();
                else if (value != '\r') _buffer.Append(value);
            }

            public override void Write(string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                _buffer.Append(value);
                if (value.EndsWith("\n")) FlushBuffer();
            }

            public override void WriteLine(string value)
            {
                _buffer.Append(value);
                FlushBuffer();
            }

            private void FlushBuffer()
            {
                if (_buffer.Length > 0)
                {
                    GameLogger.Log(_severity, _buffer.ToString());
                    _buffer.Clear();
                }
            }
        }

        private class GameTraceListener : TraceListener
        {
            public override void Write(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                    GameLogger.Log(LogSeverity.Warning, message); // Treat Debug output as Warnings/Yellow
            }

            public override void WriteLine(string message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                    GameLogger.Log(LogSeverity.Warning, message);
            }
        }
    }
}