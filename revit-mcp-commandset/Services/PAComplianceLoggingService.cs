using RevitMCPCommandSet.Models.PACompliance;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Service for comprehensive PA compliance logging and error tracking
    /// </summary>
    public static class PAComplianceLoggingService
    {
        private static readonly object _lockObject = new object();
        private static string _logFilePath;

        /// <summary>
        /// Initialize logging for a PA compliance operation
        /// </summary>
        /// <param name="operationType">Type of operation (Report or Action)</param>
        /// <param name="projectName">Name of the Revit project</param>
        /// <returns>Path to the log file</returns>
        public static string InitializeLogging(string operationType, string projectName = "Unknown")
        {
            lock (_lockObject)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var sanitizedProjectName = SanitizeFileName(projectName);
                    var logFileName = $"PACompliance_{operationType}_{sanitizedProjectName}_{timestamp}.log";
                    
                    // Create logs directory in temp folder
                    var logsDirectory = Path.Combine(Path.GetTempPath(), "PAComplianceLogs");
                    if (!Directory.Exists(logsDirectory))
                    {
                        Directory.CreateDirectory(logsDirectory);
                    }

                    _logFilePath = Path.Combine(logsDirectory, logFileName);

                    // Write initial log entry
                    WriteLogEntry("INFO", $"PA Compliance {operationType} operation started", 
                        new Dictionary<string, object>
                        {
                            { "ProjectName", projectName },
                            { "Timestamp", DateTime.Now },
                            { "OperationType", operationType }
                        });

                    return _logFilePath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Failed to initialize PA compliance logging: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Log an information message
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="context">Additional context data</param>
        public static void LogInfo(string message, Dictionary<string, object> context = null)
        {
            WriteLogEntry("INFO", message, context);
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="context">Additional context data</param>
        public static void LogWarning(string message, Dictionary<string, object> context = null)
        {
            WriteLogEntry("WARN", message, context);
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        /// <param name="message">Log message</param>
        /// <param name="exception">Exception details</param>
        /// <param name="context">Additional context data</param>
        public static void LogError(string message, Exception exception = null, Dictionary<string, object> context = null)
        {
            var errorContext = context ?? new Dictionary<string, object>();
            
            if (exception != null)
            {
                errorContext["ExceptionType"] = exception.GetType().Name;
                errorContext["ExceptionMessage"] = exception.Message;
                errorContext["StackTrace"] = exception.StackTrace;
            }

            WriteLogEntry("ERROR", message, errorContext);
        }

        /// <summary>
        /// Log operation start
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="parameters">Operation parameters</param>
        public static void LogOperationStart(string operationName, Dictionary<string, object> parameters = null)
        {
            var context = parameters ?? new Dictionary<string, object>();
            context["OperationName"] = operationName;
            context["StartTime"] = DateTime.Now;
            
            WriteLogEntry("INFO", $"Starting operation: {operationName}", context);
        }

        /// <summary>
        /// Log operation completion
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="success">Whether the operation was successful</param>
        /// <param name="duration">Operation duration</param>
        /// <param name="results">Operation results</param>
        public static void LogOperationComplete(string operationName, bool success, TimeSpan duration, Dictionary<string, object> results = null)
        {
            var context = results ?? new Dictionary<string, object>();
            context["OperationName"] = operationName;
            context["Success"] = success;
            context["Duration"] = duration.ToString();
            context["EndTime"] = DateTime.Now;
            
            var level = success ? "INFO" : "ERROR";
            var message = $"Operation {operationName} {(success ? "completed successfully" : "failed")} in {duration.TotalSeconds:F2} seconds";
            
            WriteLogEntry(level, message, context);
        }

        /// <summary>
        /// Log element modification attempt
        /// </summary>
        /// <param name="elementId">Element ID</param>
        /// <param name="elementType">Element type</param>
        /// <param name="operationType">Type of modification</param>
        /// <param name="currentValue">Current value</param>
        /// <param name="targetValue">Target value</param>
        /// <param name="success">Whether the modification was successful</param>
        /// <param name="errorMessage">Error message if failed</param>
        public static void LogElementModification(string elementId, string elementType, PAComplianceOperationType operationType, 
            string currentValue, string targetValue, bool success, string errorMessage = null)
        {
            var context = new Dictionary<string, object>
            {
                { "ElementId", elementId },
                { "ElementType", elementType },
                { "OperationType", operationType.ToString() },
                { "CurrentValue", currentValue },
                { "TargetValue", targetValue },
                { "Success", success }
            };

            if (!string.IsNullOrEmpty(errorMessage))
            {
                context["ErrorMessage"] = errorMessage;
            }

            var level = success ? "INFO" : "ERROR";
            var message = $"Element modification {(success ? "succeeded" : "failed")}: {elementType} {elementId} {operationType}";
            
            WriteLogEntry(level, message, context);
        }

        /// <summary>
        /// Log validation error
        /// </summary>
        /// <param name="validationType">Type of validation</param>
        /// <param name="errorMessage">Validation error message</param>
        /// <param name="context">Additional context</param>
        public static void LogValidationError(string validationType, string errorMessage, Dictionary<string, object> context = null)
        {
            var validationContext = context ?? new Dictionary<string, object>();
            validationContext["ValidationType"] = validationType;
            validationContext["ValidationError"] = errorMessage;
            
            WriteLogEntry("ERROR", $"Validation failed: {validationType} - {errorMessage}", validationContext);
        }

        /// <summary>
        /// Log Excel operation
        /// </summary>
        /// <param name="operation">Excel operation type</param>
        /// <param name="filePath">Excel file path</param>
        /// <param name="success">Whether the operation was successful</param>
        /// <param name="details">Additional details</param>
        public static void LogExcelOperation(string operation, string filePath, bool success, Dictionary<string, object> details = null)
        {
            var context = details ?? new Dictionary<string, object>();
            context["ExcelOperation"] = operation;
            context["FilePath"] = filePath;
            context["Success"] = success;
            
            var level = success ? "INFO" : "ERROR";
            var message = $"Excel {operation} {(success ? "succeeded" : "failed")}: {Path.GetFileName(filePath)}";
            
            WriteLogEntry(level, message, context);
        }

        /// <summary>
        /// Log batch operation summary
        /// </summary>
        /// <param name="operationType">Type of batch operation</param>
        /// <param name="totalAttempted">Total operations attempted</param>
        /// <param name="totalSuccessful">Total successful operations</param>
        /// <param name="totalFailed">Total failed operations</param>
        /// <param name="duration">Operation duration</param>
        public static void LogBatchOperationSummary(string operationType, int totalAttempted, int totalSuccessful, int totalFailed, TimeSpan duration)
        {
            var context = new Dictionary<string, object>
            {
                { "OperationType", operationType },
                { "TotalAttempted", totalAttempted },
                { "TotalSuccessful", totalSuccessful },
                { "TotalFailed", totalFailed },
                { "SuccessRate", totalAttempted > 0 ? (double)totalSuccessful / totalAttempted * 100 : 0 },
                { "Duration", duration.ToString() }
            };
            
            var message = $"Batch {operationType} completed: {totalSuccessful}/{totalAttempted} successful ({totalFailed} failed) in {duration.TotalSeconds:F2} seconds";
            
            WriteLogEntry("INFO", message, context);
        }

        /// <summary>
        /// Get current log file path
        /// </summary>
        /// <returns>Path to current log file</returns>
        public static string GetCurrentLogFilePath()
        {
            return _logFilePath;
        }

        /// <summary>
        /// Finalize logging for an operation
        /// </summary>
        /// <param name="operationType">Type of operation</param>
        /// <param name="overallSuccess">Overall operation success</param>
        /// <param name="summary">Operation summary</param>
        public static void FinalizeLogging(string operationType, bool overallSuccess, Dictionary<string, object> summary = null)
        {
            var context = summary ?? new Dictionary<string, object>();
            context["OperationType"] = operationType;
            context["OverallSuccess"] = overallSuccess;
            context["FinalizedAt"] = DateTime.Now;
            
            var level = overallSuccess ? "INFO" : "ERROR";
            var message = $"PA Compliance {operationType} operation {(overallSuccess ? "completed successfully" : "completed with errors")}";
            
            WriteLogEntry(level, message, context);
            
            // Also write to trace for immediate visibility
            System.Diagnostics.Trace.WriteLine($"PA Compliance Log: {message}");
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                System.Diagnostics.Trace.WriteLine($"Full log available at: {_logFilePath}");
            }
        }

        /// <summary>
        /// Write a log entry to the log file
        /// </summary>
        /// <param name="level">Log level</param>
        /// <param name="message">Log message</param>
        /// <param name="context">Additional context data</param>
        private static void WriteLogEntry(string level, string message, Dictionary<string, object> context = null)
        {
            if (string.IsNullOrEmpty(_logFilePath))
                return;

            lock (_lockObject)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logEntry = new StringBuilder();
                    
                    logEntry.AppendLine($"[{timestamp}] [{level}] {message}");
                    
                    if (context != null && context.Count > 0)
                    {
                        logEntry.AppendLine("Context:");
                        foreach (var kvp in context)
                        {
                            logEntry.AppendLine($"  {kvp.Key}: {kvp.Value}");
                        }
                    }
                    
                    logEntry.AppendLine(); // Empty line for readability
                    
                    File.AppendAllText(_logFilePath, logEntry.ToString());
                    
                    // Also write to trace for immediate visibility
                    System.Diagnostics.Trace.WriteLine($"PA Compliance [{level}]: {message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Failed to write to PA compliance log: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Sanitize a filename by removing invalid characters
        /// </summary>
        /// <param name="fileName">Original filename</param>
        /// <returns>Sanitized filename</returns>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "Unknown";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = fileName;
            
            foreach (var invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }
            
            return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
        }

        /// <summary>
        /// Create error recovery context for continuing after failures
        /// </summary>
        /// <param name="failedOperation">The operation that failed</param>
        /// <param name="errorMessage">Error message</param>
        /// <param name="recoveryAction">Action taken for recovery</param>
        public static void LogErrorRecovery(string failedOperation, string errorMessage, string recoveryAction)
        {
            var context = new Dictionary<string, object>
            {
                { "FailedOperation", failedOperation },
                { "ErrorMessage", errorMessage },
                { "RecoveryAction", recoveryAction },
                { "RecoveryTime", DateTime.Now }
            };
            
            WriteLogEntry("WARN", $"Error recovery: {failedOperation} failed, {recoveryAction}", context);
        }

        /// <summary>
        /// Log permission or constraint validation
        /// </summary>
        /// <param name="elementId">Element ID</param>
        /// <param name="elementType">Element type</param>
        /// <param name="validationType">Type of validation</param>
        /// <param name="isValid">Whether validation passed</param>
        /// <param name="reason">Reason for validation result</param>
        public static void LogPermissionValidation(string elementId, string elementType, string validationType, bool isValid, string reason)
        {
            var context = new Dictionary<string, object>
            {
                { "ElementId", elementId },
                { "ElementType", elementType },
                { "ValidationType", validationType },
                { "IsValid", isValid },
                { "Reason", reason }
            };
            
            var level = isValid ? "INFO" : "WARN";
            var message = $"Permission validation {(isValid ? "passed" : "failed")}: {elementType} {elementId} - {reason}";
            
            WriteLogEntry(level, message, context);
        }
    }
}