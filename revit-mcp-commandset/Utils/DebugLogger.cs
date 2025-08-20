using System;
using System.IO;

namespace RevitMCPCommandSet.Utils
{
    /// <summary>
    /// Simple debug logger that writes to C:\temp for debugging purposes
    /// </summary>
    public static class DebugLogger
    {
        private static readonly string LogFilePath = @"C:\temp\revit_mcp_debug.log";
        private static readonly object LockObject = new object();

        static DebugLogger()
        {
            // Ensure the temp directory exists
            Directory.CreateDirectory(@"C:\temp");
            
            // Clear the log file at startup
            try
            {
                File.WriteAllText(LogFilePath, $"=== Debug Log Started at {DateTime.Now} ===\n");
            }
            catch
            {
                // Ignore errors during initialization
            }
        }

        /// <summary>
        /// Write a debug message to the log file
        /// </summary>
        public static void Log(string message)
        {
            try
            {
                lock (LockObject)
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n";
                    File.AppendAllText(LogFilePath, logEntry);
                }
            }
            catch
            {
                // Ignore logging errors to prevent crashes
            }
        }

        /// <summary>
        /// Write a debug message with category prefix
        /// </summary>
        public static void Log(string category, string message)
        {
            Log($"[{category}] {message}");
        }

        /// <summary>
        /// Write an exception to the log
        /// </summary>
        public static void LogException(string context, Exception ex)
        {
            Log($"[ERROR] {context}: {ex.Message}");
            if (ex.StackTrace != null)
            {
                Log($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Write a separator line for clarity
        /// </summary>
        public static void LogSeparator()
        {
            Log("================================================");
        }
    }
}