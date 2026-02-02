using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectVagabond.Utils
{
    public enum LogSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }
    public struct LogMessage
    {
        public DateTime Timestamp;
        public LogSeverity Severity;
        public string Text;
    }

    /// <summary>
    /// A fortified, thread-safe, async logging system.
    /// Features: Log Rotation, Async Disk I/O, Memory Capping.
    /// </summary>
    public static class GameLogger
    {
        // --- TUNING: Memory Safety ---
        private const int MAX_UI_LOG_COUNT = 1000; // Max messages to keep in UI memory before dropping old ones.
        private const int MAX_FILE_QUEUE_COUNT = 5000; // Max messages waiting to be written to disk before dropping.

        // Queue for the UI (DebugConsole) to consume.
        public static readonly ConcurrentQueue<LogMessage> LogQueue = new ConcurrentQueue<LogMessage>();

        // Queue for the background file writer.
        // FIX: Initialized with a bounded capacity to support TryAdd and prevent memory explosions.
        private static readonly BlockingCollection<string> _fileQueue = new BlockingCollection<string>(new ConcurrentQueue<string>(), MAX_FILE_QUEUE_COUNT);

        private static bool _isInitialized = false;
        private static Task _fileWriterTask;
        private static CancellationTokenSource _cancellationTokenSource;

        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProjectVagabond");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string logPath = Path.Combine(folder, "game.log");
                string prevLogPath = Path.Combine(folder, "game_prev.log");

                // --- FIX #1: Log Rotation ---
                // If a log exists, move it to prev. If prev exists, it gets overwritten.
                if (File.Exists(logPath))
                {
                    if (File.Exists(prevLogPath)) File.Delete(prevLogPath);
                    File.Move(logPath, prevLogPath);
                }

                // --- FIX #2: Async I/O (Lag Spike Fix) ---
                // Start a background task that processes the file queue.
                _cancellationTokenSource = new CancellationTokenSource();
                _fileWriterTask = Task.Factory.StartNew(() => WriteToFileLoop(logPath, _cancellationTokenSource.Token),
                                                      TaskCreationOptions.LongRunning);

                Log(LogSeverity.Info, $"=== Game Session Started: {DateTime.Now} ===");
                Log(LogSeverity.Info, $"Version: {Global.GAME_VERSION}");

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FAILED TO INIT LOGGER: {ex.Message}");
            }
        }

        public static void Log(LogSeverity severity, string message)
        {
            var entry = new LogMessage
            {
                Timestamp = DateTime.Now,
                Severity = severity,
                Text = message
            };

            // 1. Enqueue for UI (Memory Safe)
            LogQueue.Enqueue(entry);
            // Prevent UI queue from exploding if the console isn't consuming it fast enough
            if (LogQueue.Count > MAX_UI_LOG_COUNT)
            {
                LogQueue.TryDequeue(out _);
            }

            // 2. Enqueue for File (Async)
            // Format the string here to save work on the file thread
            string prefix = severity == LogSeverity.Info ? "" : $"[{severity.ToString().ToUpper()}] ";
            string fileLine = $"[{entry.Timestamp:HH:mm:ss}] {prefix}{message}";

            // If the queue is full, we silently drop the log to preserve framerate.
            _fileQueue.TryAdd(fileLine);
        }

        private static void WriteToFileLoop(string path, CancellationToken token)
        {
            try
            {
                // Open with FileShare.Read to allow external tools (like Notepad++) to read while game runs
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(fs, Encoding.UTF8))
                {
                    writer.AutoFlush = true; // Ensure data hits disk reasonably fast

                    foreach (var line in _fileQueue.GetConsumingEnumerable(token))
                    {
                        writer.WriteLine(line);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception)
            {
                // If file writing fails, we silently stop logging to disk to prevent crashing the game.
            }
        }

        public static void Close()
        {
            if (_isInitialized)
            {
                Log(LogSeverity.Info, "=== Session Ended ===");
                _fileQueue.CompleteAdding(); // Tell the thread no more items are coming
                _cancellationTokenSource.Cancel();

                // Wait briefly for the writer to finish flushing
                try { _fileWriterTask.Wait(1000); } catch { }

                _isInitialized = false;
            }
        }
    }
}