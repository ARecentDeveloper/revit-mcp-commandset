using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Commands;
using RevitMCPCommandSet.Models.PACompliance;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class PAComplianceActionEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;

        /// <summary>
        /// Event wait object
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        /// <summary>
        /// Action settings (incoming data)
        /// </summary>
        public PAComplianceActionSettings ActionSettings { get; private set; }

        /// <summary>
        /// Execution result (outgoing data)
        /// </summary>
        public object Result { get; private set; }

        /// <summary>
        /// Set action parameters
        /// </summary>
        public void SetParameters(PAComplianceActionSettings settings)
        {
            ActionSettings = settings;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                if (ActionSettings == null)
                    throw new ArgumentNullException(nameof(ActionSettings), "Action settings are required");

                if (string.IsNullOrWhiteSpace(ActionSettings.ExcelFilePath))
                    throw new ArgumentException("Excel file path is required");

                if (!File.Exists(ActionSettings.ExcelFilePath))
                    throw new FileNotFoundException($"Excel file not found: {ActionSettings.ExcelFilePath}");

                // Initialize logging
                var projectName = doc?.Title ?? "Unknown";
                var logFilePath = PAComplianceLoggingService.InitializeLogging("Action", projectName);
                
                PAComplianceLoggingService.LogOperationStart("PA Compliance Action Execution", new Dictionary<string, object>
                {
                    { "ExcelFilePath", ActionSettings.ExcelFilePath },
                    { "Step", ActionSettings.Step },
                    { "DryRun", ActionSettings.DryRun },
                    { "BackupProject", ActionSettings.BackupProject },
                    { "ProjectName", projectName }
                });

                var progressMessages = new List<string>();
                var actionResult = new PAComplianceActionResult
                {
                    WasDryRun = ActionSettings.DryRun,
                    ResultsByArea = new Dictionary<PAComplianceArea, PAComplianceAreaResult>()
                };

                var startTime = DateTime.Now;

                // Create backup if requested and not dry run
                if (ActionSettings.BackupProject && !ActionSettings.DryRun)
                {
                    progressMessages.Add("Creating project backup...");
                    PAComplianceLoggingService.LogInfo("Creating project backup");
                    CreateProjectBackup();
                    progressMessages.Add("Project backup created successfully");
                    PAComplianceLoggingService.LogInfo("Project backup created successfully");
                }

                // Read Excel file and validate structure
                progressMessages.Add("Reading Excel file...");
                PAComplianceLoggingService.LogInfo("Reading Excel file", new Dictionary<string, object> { { "FilePath", ActionSettings.ExcelFilePath } });
                var excelData = PAExcelActionService.ReadExcelFile(ActionSettings.ExcelFilePath);
                progressMessages.Add($"Excel file read successfully. Found {excelData.GetTotalRowCount()} rows to process");
                PAComplianceLoggingService.LogExcelOperation("Read", ActionSettings.ExcelFilePath, true, new Dictionary<string, object>
                {
                    { "TotalRows", excelData.GetTotalRowCount() },
                    { "ProcessingRows", excelData.GetProcessingRowCount() }
                });

                // Determine what steps to execute
                var stepsToExecute = DetermineStepsToExecute();
                progressMessages.Add($"Executing steps: {string.Join(", ", stepsToExecute)}");
                
                PAComplianceLoggingService.LogInfo("Step execution plan determined", new Dictionary<string, object>
                {
                    { "StepsToExecute", string.Join(", ", stepsToExecute) },
                    { "TotalSteps", stepsToExecute.Count },
                    { "DryRun", ActionSettings.DryRun }
                });

                // Provide dry-run preview if requested
                if (ActionSettings.DryRun)
                {
                    var preview = GenerateDryRunPreview(excelData, stepsToExecute);
                    progressMessages.Add($"Dry run preview: {preview}");
                    PAComplianceLoggingService.LogInfo("Dry run preview generated", new Dictionary<string, object>
                    {
                        { "Preview", preview }
                    });
                }

                // Process each step with validation
                var validationMessages = new List<string>();
                foreach (var step in stepsToExecute)
                {
                    PAComplianceLoggingService.LogOperationStart($"Process {step}", new Dictionary<string, object>
                    {
                        { "Step", step.ToString() },
                        { "DryRun", ActionSettings.DryRun }
                    });

                    var stepStartTime = DateTime.Now;
                    var stepValid = ValidateStepPrerequisites(step, validationMessages);
                    
                    if (!stepValid)
                    {
                        progressMessages.AddRange(validationMessages);
                        continue;
                    }

                    try
                    {
                        switch (step)
                        {
                            case PAComplianceArea.AnnotationFamilies:
                                ProcessAnnotationFamilies(excelData.AnnotationFamilies, actionResult, progressMessages);
                                break;
                            case PAComplianceArea.ModelFamilies:
                                ProcessModelFamilies(excelData.ModelFamilies, actionResult, progressMessages);
                                break;
                            case PAComplianceArea.Worksets:
                                ProcessWorksets(excelData.Worksets, actionResult, progressMessages);
                                break;
                            case PAComplianceArea.Sheets:
                                ProcessSheets(excelData.Sheets, actionResult, progressMessages);
                                break;
                            case PAComplianceArea.ModelIntegrity:
                                ProcessModelIntegrity(excelData.ModelIntegrityIssues, actionResult, progressMessages);
                                break;
                        }

                        var stepDuration = DateTime.Now - stepStartTime;
                        var stepResult = actionResult.ResultsByArea.ContainsKey(step) ? actionResult.ResultsByArea[step] : null;
                        var stepSuccess = stepResult?.CorrectionsFailed == 0;

                        PAComplianceLoggingService.LogOperationComplete($"Process {step}", stepSuccess, stepDuration, new Dictionary<string, object>
                        {
                            { "Step", step.ToString() },
                            { "Attempted", stepResult?.CorrectionsAttempted ?? 0 },
                            { "Successful", stepResult?.CorrectionsSuccessful ?? 0 },
                            { "Failed", stepResult?.CorrectionsFailed ?? 0 }
                        });
                    }
                    catch (Exception stepEx)
                    {
                        var stepDuration = DateTime.Now - stepStartTime;
                        PAComplianceLoggingService.LogOperationComplete($"Process {step}", false, stepDuration, new Dictionary<string, object>
                        {
                            { "Step", step.ToString() },
                            { "Error", stepEx.Message }
                        });

                        progressMessages.Add($"Error processing {step}: {stepEx.Message}");
                        actionResult.Errors.Add($"Step {step}: {stepEx.Message}");
                        
                        // Continue with other steps
                        PAComplianceLoggingService.LogErrorRecovery($"Process {step}", stepEx.Message, "Continuing with remaining steps");
                    }
                }

                // Calculate totals
                actionResult.TotalCorrectionsAttempted = actionResult.ResultsByArea.Values.Sum(r => r.CorrectionsAttempted);
                actionResult.TotalCorrectionsSuccessful = actionResult.ResultsByArea.Values.Sum(r => r.CorrectionsSuccessful);
                actionResult.TotalCorrectionsFailed = actionResult.ResultsByArea.Values.Sum(r => r.CorrectionsFailed);
                actionResult.ExecutionTime = DateTime.Now - startTime;

                // Update Excel file with results
                if (!ActionSettings.DryRun)
                {
                    progressMessages.Add("Updating Excel file with results...");
                    PAComplianceLoggingService.LogInfo("Updating Excel file with results");
                    PAExcelActionService.UpdateExcelWithResults(ActionSettings.ExcelFilePath, actionResult);
                    progressMessages.Add("Excel file updated with execution results");
                    PAComplianceLoggingService.LogExcelOperation("Update", ActionSettings.ExcelFilePath, true);
                }

                actionResult.Success = actionResult.TotalCorrectionsFailed == 0;
                actionResult.Message = ActionSettings.DryRun 
                    ? $"Dry run completed. Would attempt {actionResult.TotalCorrectionsAttempted} corrections"
                    : $"Action execution completed. {actionResult.TotalCorrectionsSuccessful} successful, {actionResult.TotalCorrectionsFailed} failed";

                progressMessages.Add("PA compliance action execution completed");

                // Add detailed step execution summary
                var stepSummary = GenerateStepExecutionSummary(actionResult, stepsToExecute);
                progressMessages.Add($"Step execution summary: {stepSummary}");

                // Log batch operation summary
                PAComplianceLoggingService.LogBatchOperationSummary(
                    "PA Compliance Actions",
                    actionResult.TotalCorrectionsAttempted,
                    actionResult.TotalCorrectionsSuccessful,
                    actionResult.TotalCorrectionsFailed,
                    actionResult.ExecutionTime
                );

                // Finalize logging
                PAComplianceLoggingService.FinalizeLogging("Action", actionResult.Success, new Dictionary<string, object>
                {
                    { "TotalAttempted", actionResult.TotalCorrectionsAttempted },
                    { "TotalSuccessful", actionResult.TotalCorrectionsSuccessful },
                    { "TotalFailed", actionResult.TotalCorrectionsFailed },
                    { "ExecutionTime", actionResult.ExecutionTime.ToString() },
                    { "LogFilePath", logFilePath }
                });

                Result = new PAComplianceActionExecutionResult
                {
                    Success = actionResult.Success,
                    Message = actionResult.Message,
                    Response = actionResult,
                    ProgressMessages = progressMessages,
                    StepExecuted = ActionSettings.Step ?? "all"
                };
            }
            catch (Exception ex)
            {
                PAComplianceLoggingService.LogError("PA Compliance Action execution failed", ex, new Dictionary<string, object>
                {
                    { "ExcelFilePath", ActionSettings?.ExcelFilePath },
                    { "Step", ActionSettings?.Step },
                    { "DryRun", ActionSettings?.DryRun }
                });

                PAComplianceLoggingService.FinalizeLogging("Action", false, new Dictionary<string, object>
                {
                    { "ErrorMessage", ex.Message },
                    { "ExceptionType", ex.GetType().Name }
                });

                System.Diagnostics.Trace.WriteLine($"PA Compliance Action Error: {ex.Message}");
                Result = new PAComplianceActionExecutionResult
                {
                    Success = false,
                    Message = $"Error executing PA compliance actions: {ex.Message}",
                    Response = null,
                    ProgressMessages = new List<string> { $"Error: {ex.Message}" },
                    StepExecuted = ActionSettings?.Step ?? "unknown"
                };
            }
            finally
            {
                _resetEvent.Set(); // Notify the waiting thread that the operation is complete
            }
        }

        /// <summary>
        /// Wait for operation to complete
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout (milliseconds)</param>
        /// <returns>Whether the operation was completed before the timeout</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 60000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// Determine which steps to execute based on settings
        /// </summary>
        private List<PAComplianceArea> DetermineStepsToExecute()
        {
            var steps = new List<PAComplianceArea>();

            if (string.IsNullOrWhiteSpace(ActionSettings.Step) || ActionSettings.Step.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                steps.AddRange(new[] { 
                    PAComplianceArea.AnnotationFamilies, 
                    PAComplianceArea.ModelFamilies, 
                    PAComplianceArea.Worksets, 
                    PAComplianceArea.Sheets, 
                    PAComplianceArea.ModelIntegrity 
                });
            }
            else
            {
                var stepNames = ActionSettings.Step.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToList();

                foreach (var stepName in stepNames)
                {
                    switch (stepName)
                    {
                        case "families":
                            steps.Add(PAComplianceArea.AnnotationFamilies);
                            steps.Add(PAComplianceArea.ModelFamilies);
                            break;
                        case "annotation":
                        case "annotation-families":
                            steps.Add(PAComplianceArea.AnnotationFamilies);
                            break;
                        case "model":
                        case "model-families":
                            steps.Add(PAComplianceArea.ModelFamilies);
                            break;
                        case "worksets":
                            steps.Add(PAComplianceArea.Worksets);
                            break;
                        case "sheets":
                            steps.Add(PAComplianceArea.Sheets);
                            break;
                        case "integrity":
                        case "model-integrity":
                            steps.Add(PAComplianceArea.ModelIntegrity);
                            break;
                        default:
                            PAComplianceLoggingService.LogWarning($"Unknown step name: {stepName}", new Dictionary<string, object>
                            {
                                { "StepName", stepName },
                                { "AvailableSteps", "families, annotation, model, worksets, sheets, integrity" }
                            });
                            break;
                    }
                }
            }

            var distinctSteps = steps.Distinct().ToList();
            
            // Validate that we have at least one valid step
            if (!distinctSteps.Any())
            {
                PAComplianceLoggingService.LogWarning("No valid steps determined, defaulting to all steps", new Dictionary<string, object>
                {
                    { "OriginalStep", ActionSettings.Step }
                });
                
                distinctSteps.AddRange(new[] { 
                    PAComplianceArea.AnnotationFamilies, 
                    PAComplianceArea.ModelFamilies, 
                    PAComplianceArea.Worksets, 
                    PAComplianceArea.Sheets, 
                    PAComplianceArea.ModelIntegrity 
                });
            }

            return distinctSteps;
        }

        /// <summary>
        /// Generate a dry-run preview of what would be executed
        /// </summary>
        private string GenerateDryRunPreview(PAExcelActionData excelData, List<PAComplianceArea> stepsToExecute)
        {
            var preview = new List<string>();
            
            foreach (var step in stepsToExecute)
            {
                switch (step)
                {
                    case PAComplianceArea.AnnotationFamilies:
                        var annotationCount = excelData.AnnotationFamilies.Count(r => r.ShouldProcess);
                        if (annotationCount > 0)
                            preview.Add($"{annotationCount} annotation families would be renamed");
                        break;
                        
                    case PAComplianceArea.ModelFamilies:
                        var modelCount = excelData.ModelFamilies.Count(r => r.ShouldProcess);
                        if (modelCount > 0)
                            preview.Add($"{modelCount} model families would be renamed");
                        break;
                        
                    case PAComplianceArea.Worksets:
                        var worksetCount = excelData.Worksets.Count(r => r.ShouldProcess);
                        if (worksetCount > 0)
                            preview.Add($"{worksetCount} worksets would be renamed");
                        break;
                        
                    case PAComplianceArea.Sheets:
                        var sheetCount = excelData.Sheets.Count(r => r.ShouldProcess);
                        if (sheetCount > 0)
                            preview.Add($"{sheetCount} sheets would be renamed");
                        break;
                        
                    case PAComplianceArea.ModelIntegrity:
                        var integrityCount = excelData.ModelIntegrityIssues.Count(r => r.ShouldProcess);
                        if (integrityCount > 0)
                            preview.Add($"{integrityCount} model integrity issues would be addressed");
                        break;
                }
            }
            
            return preview.Any() ? string.Join(", ", preview) : "No changes would be made";
        }

        /// <summary>
        /// Validate step execution prerequisites
        /// </summary>
        private bool ValidateStepPrerequisites(PAComplianceArea step, List<string> validationMessages)
        {
            switch (step)
            {
                case PAComplianceArea.Worksets:
                    if (!doc.IsWorkshared)
                    {
                        validationMessages.Add("Document is not workshared - workset operations will be skipped");
                        PAComplianceLoggingService.LogWarning("Workset step requested but document is not workshared");
                        return false;
                    }
                    break;
                    
                case PAComplianceArea.AnnotationFamilies:
                case PAComplianceArea.ModelFamilies:
                case PAComplianceArea.Sheets:
                case PAComplianceArea.ModelIntegrity:
                    // These steps don't have special prerequisites
                    break;
            }
            
            return true;
        }

        /// <summary>
        /// Generate step execution summary
        /// </summary>
        private string GenerateStepExecutionSummary(PAComplianceActionResult actionResult, List<PAComplianceArea> stepsExecuted)
        {
            var summaryParts = new List<string>();
            
            foreach (var step in stepsExecuted)
            {
                if (actionResult.ResultsByArea.ContainsKey(step))
                {
                    var result = actionResult.ResultsByArea[step];
                    summaryParts.Add($"{step}: {result.CorrectionsSuccessful}/{result.CorrectionsAttempted} successful");
                }
            }
            
            return summaryParts.Any() ? string.Join(", ", summaryParts) : "No steps executed";
        }

        /// <summary>
        /// Create project backup
        /// </summary>
        private void CreateProjectBackup()
        {
            try
            {
                var originalPath = doc.PathName;
                if (string.IsNullOrEmpty(originalPath))
                {
                    var message = "Cannot create backup: document has not been saved";
                    PAComplianceLoggingService.LogWarning(message);
                    System.Diagnostics.Trace.WriteLine(message);
                    return;
                }

                var backupPath = Path.ChangeExtension(originalPath, $".backup_{DateTime.Now:yyyyMMdd_HHmmss}.rvt");
                
                // Use SaveAs to create backup
                var saveOptions = new SaveAsOptions();
                saveOptions.OverwriteExistingFile = true;
                
                doc.SaveAs(backupPath, saveOptions);
                
                PAComplianceLoggingService.LogInfo("Project backup created successfully", new Dictionary<string, object>
                {
                    { "OriginalPath", originalPath },
                    { "BackupPath", backupPath }
                });
                System.Diagnostics.Trace.WriteLine($"Project backup created: {backupPath}");
            }
            catch (Exception ex)
            {
                var message = $"Warning: Could not create project backup: {ex.Message}";
                PAComplianceLoggingService.LogWarning(message, new Dictionary<string, object>
                {
                    { "OriginalPath", doc.PathName },
                    { "ExceptionType", ex.GetType().Name }
                });
                System.Diagnostics.Trace.WriteLine(message);
            }
        }

        /// <summary>
        /// Process annotation family corrections
        /// </summary>
        private void ProcessAnnotationFamilies(List<PAAnnotationFamilyRow> rows, PAComplianceActionResult result, List<string> progressMessages)
        {
            var areaResult = new PAComplianceAreaResult { Area = PAComplianceArea.AnnotationFamilies };
            var rowsToProcess = rows.Where(r => r.ShouldProcess).ToList();
            
            progressMessages.Add($"Processing {rowsToProcess.Count} annotation family corrections...");
            areaResult.CorrectionsAttempted = rowsToProcess.Count;

            System.Diagnostics.Trace.WriteLine($"PA Compliance Action: Processing {rowsToProcess.Count} annotation families");

            // Create operations for batch processing
            var operations = rowsToProcess.Select(row => new PAElementModificationOperation
            {
                ElementId = row.ElementId,
                ElementType = "Family",
                CurrentValue = row.CurrentName,
                NewValue = row.GetTargetName(),
                OperationType = PAComplianceOperationType.Rename
            }).ToList();

            // Log what we're about to do
            foreach (var op in operations)
            {
                System.Diagnostics.Trace.WriteLine($"  - Annotation Family: Rename '{op.CurrentValue}' to '{op.NewValue}' (ID: {op.ElementId})");
            }

            // Process operations using the modification service
            var itemResults = PAElementModificationService.PerformBatchModifications(doc, operations, ActionSettings.DryRun);
            
            // Update rows with results
            for (int i = 0; i < rowsToProcess.Count && i < itemResults.Count; i++)
            {
                var row = rowsToProcess[i];
                var itemResult = itemResults[i];

                if (itemResult.Success)
                {
                    row.Status = ActionSettings.DryRun ? "Dry Run - Would Rename" : PAExcelStatus.SUCCESS;
                    areaResult.CorrectionsSuccessful++;
                    System.Diagnostics.Trace.WriteLine($"  - SUCCESS: Renamed '{row.CurrentName}' to '{row.GetTargetName()}'");
                }
                else
                {
                    row.Status = PAExcelStatus.FAILED;
                    row.ErrorMessage = itemResult.ErrorMessage;
                    areaResult.CorrectionsFailed++;
                    result.Errors.Add($"Annotation Family {row.CurrentName}: {itemResult.ErrorMessage}");
                    System.Diagnostics.Trace.WriteLine($"  - FAILED: Could not rename '{row.CurrentName}': {itemResult.ErrorMessage}");
                }

                areaResult.ItemResults.Add(itemResult);
            }

            result.ResultsByArea[PAComplianceArea.AnnotationFamilies] = areaResult;
            progressMessages.Add($"Annotation families: {areaResult.CorrectionsSuccessful} successful, {areaResult.CorrectionsFailed} failed");
        }

        /// <summary>
        /// Process model family corrections
        /// </summary>
        private void ProcessModelFamilies(List<PAModelFamilyRow> rows, PAComplianceActionResult result, List<string> progressMessages)
        {
            var areaResult = new PAComplianceAreaResult { Area = PAComplianceArea.ModelFamilies };
            var rowsToProcess = rows.Where(r => r.ShouldProcess).ToList();
            
            progressMessages.Add($"Processing {rowsToProcess.Count} model family corrections...");
            areaResult.CorrectionsAttempted = rowsToProcess.Count;

            System.Diagnostics.Trace.WriteLine($"PA Compliance Action: Processing {rowsToProcess.Count} model families");

            // Create operations for batch processing
            var operations = rowsToProcess.Select(row => new PAElementModificationOperation
            {
                ElementId = row.ElementId,
                ElementType = "Family",
                CurrentValue = row.CurrentName,
                NewValue = row.GetTargetName(),
                OperationType = PAComplianceOperationType.Rename
            }).ToList();

            // Log what we're about to do
            foreach (var op in operations)
            {
                System.Diagnostics.Trace.WriteLine($"  - Model Family: Rename '{op.CurrentValue}' to '{op.NewValue}' (ID: {op.ElementId})");
            }

            // Process operations using the modification service
            var itemResults = PAElementModificationService.PerformBatchModifications(doc, operations, ActionSettings.DryRun);
            
            // Update rows with results
            for (int i = 0; i < rowsToProcess.Count && i < itemResults.Count; i++)
            {
                var row = rowsToProcess[i];
                var itemResult = itemResults[i];

                if (itemResult.Success)
                {
                    row.Status = ActionSettings.DryRun ? "Dry Run - Would Rename" : PAExcelStatus.SUCCESS;
                    areaResult.CorrectionsSuccessful++;
                    System.Diagnostics.Trace.WriteLine($"  - SUCCESS: Renamed '{row.CurrentName}' to '{row.GetTargetName()}'");
                }
                else
                {
                    row.Status = PAExcelStatus.FAILED;
                    row.ErrorMessage = itemResult.ErrorMessage;
                    areaResult.CorrectionsFailed++;
                    result.Errors.Add($"Model Family {row.CurrentName}: {itemResult.ErrorMessage}");
                    System.Diagnostics.Trace.WriteLine($"  - FAILED: Could not rename '{row.CurrentName}': {itemResult.ErrorMessage}");
                }

                areaResult.ItemResults.Add(itemResult);
            }

            result.ResultsByArea[PAComplianceArea.ModelFamilies] = areaResult;
            progressMessages.Add($"Model families: {areaResult.CorrectionsSuccessful} successful, {areaResult.CorrectionsFailed} failed");
        }

        /// <summary>
        /// Process workset corrections
        /// </summary>
        private void ProcessWorksets(List<PAWorksetRow> rows, PAComplianceActionResult result, List<string> progressMessages)
        {
            var areaResult = new PAComplianceAreaResult { Area = PAComplianceArea.Worksets };
            var rowsToProcess = rows.Where(r => r.ShouldProcess).ToList();
            
            progressMessages.Add($"Processing {rowsToProcess.Count} workset corrections...");
            areaResult.CorrectionsAttempted = rowsToProcess.Count;

            if (!doc.IsWorkshared)
            {
                progressMessages.Add("Document is not workshared, skipping workset corrections");
                result.ResultsByArea[PAComplianceArea.Worksets] = areaResult;
                return;
            }

            // Create operations for batch processing
            var operations = rowsToProcess.Select(row => new PAElementModificationOperation
            {
                ElementId = row.WorksetId.ToString(),
                ElementType = "Workset",
                CurrentValue = row.CurrentName,
                NewValue = row.GetTargetName(),
                OperationType = PAComplianceOperationType.Rename
            }).ToList();

            // Log what we're about to do
            foreach (var op in operations)
            {
                System.Diagnostics.Trace.WriteLine($"  - Workset: Rename '{op.CurrentValue}' to '{op.NewValue}' (ID: {op.ElementId})");
            }

            // Process operations using the modification service
            var itemResults = PAElementModificationService.PerformBatchModifications(doc, operations, ActionSettings.DryRun);
            
            // Update rows with results
            for (int i = 0; i < rowsToProcess.Count && i < itemResults.Count; i++)
            {
                var row = rowsToProcess[i];
                var itemResult = itemResults[i];

                if (itemResult.Success)
                {
                    row.Status = ActionSettings.DryRun ? "Dry Run - Would Rename" : PAExcelStatus.SUCCESS;
                    areaResult.CorrectionsSuccessful++;
                }
                else
                {
                    row.Status = PAExcelStatus.FAILED;
                    row.ErrorMessage = itemResult.ErrorMessage;
                    areaResult.CorrectionsFailed++;
                    result.Errors.Add($"Workset {row.CurrentName}: {itemResult.ErrorMessage}");
                }

                areaResult.ItemResults.Add(itemResult);
            }

            result.ResultsByArea[PAComplianceArea.Worksets] = areaResult;
            progressMessages.Add($"Worksets: {areaResult.CorrectionsSuccessful} successful, {areaResult.CorrectionsFailed} failed");
        }

        /// <summary>
        /// Process sheet corrections
        /// </summary>
        private void ProcessSheets(List<PASheetRow> rows, PAComplianceActionResult result, List<string> progressMessages)
        {
            var areaResult = new PAComplianceAreaResult { Area = PAComplianceArea.Sheets };
            var rowsToProcess = rows.Where(r => r.ShouldProcess).ToList();
            
            progressMessages.Add($"Processing {rowsToProcess.Count} sheet corrections...");
            areaResult.CorrectionsAttempted = rowsToProcess.Count;

            // Create operations for batch processing
            var operations = rowsToProcess.Select(row => new PAElementModificationOperation
            {
                ElementId = row.ElementId,
                ElementType = "ViewSheet",
                CurrentValue = row.CurrentName,
                NewValue = row.GetTargetName(),
                OperationType = PAComplianceOperationType.Rename
            }).ToList();

            // Log what we're about to do
            foreach (var op in operations)
            {
                System.Diagnostics.Trace.WriteLine($"  - Sheet: Rename '{op.CurrentValue}' to '{op.NewValue}' (ID: {op.ElementId})");
            }

            // Process operations using the modification service
            var itemResults = PAElementModificationService.PerformBatchModifications(doc, operations, ActionSettings.DryRun);
            
            // Update rows with results
            for (int i = 0; i < rowsToProcess.Count && i < itemResults.Count; i++)
            {
                var row = rowsToProcess[i];
                var itemResult = itemResults[i];

                if (itemResult.Success)
                {
                    row.Status = ActionSettings.DryRun ? "Dry Run - Would Rename" : PAExcelStatus.SUCCESS;
                    areaResult.CorrectionsSuccessful++;
                }
                else
                {
                    row.Status = PAExcelStatus.FAILED;
                    row.ErrorMessage = itemResult.ErrorMessage;
                    areaResult.CorrectionsFailed++;
                    result.Errors.Add($"Sheet {row.CurrentName}: {itemResult.ErrorMessage}");
                }

                areaResult.ItemResults.Add(itemResult);
            }

            result.ResultsByArea[PAComplianceArea.Sheets] = areaResult;
            progressMessages.Add($"Sheets: {areaResult.CorrectionsSuccessful} successful, {areaResult.CorrectionsFailed} failed");
        }

        /// <summary>
        /// Process model integrity corrections
        /// </summary>
        private void ProcessModelIntegrity(List<PAModelIntegrityRow> rows, PAComplianceActionResult result, List<string> progressMessages)
        {
            var areaResult = new PAComplianceAreaResult { Area = PAComplianceArea.ModelIntegrity };
            var rowsToProcess = rows.Where(r => r.ShouldProcess).ToList();
            
            progressMessages.Add($"Processing {rowsToProcess.Count} model integrity corrections...");
            areaResult.CorrectionsAttempted = rowsToProcess.Count;

            // Create operations for batch processing
            var operations = rowsToProcess.Select(row => new PAElementModificationOperation
            {
                ElementId = row.ElementId,
                ElementType = "Element",
                CurrentValue = row.CurrentCategory,
                NewValue = row.GetTargetName(),
                OperationType = PAComplianceOperationType.Recategorize
            }).ToList();

            // Log what we're about to do
            foreach (var op in operations)
            {
                System.Diagnostics.Trace.WriteLine($"  - Model Integrity: Change category '{op.CurrentValue}' to '{op.NewValue}' (ID: {op.ElementId})");
            }

            // Process operations using the modification service
            var itemResults = PAElementModificationService.PerformBatchModifications(doc, operations, ActionSettings.DryRun);
            
            // Update rows with results
            for (int i = 0; i < rowsToProcess.Count && i < itemResults.Count; i++)
            {
                var row = rowsToProcess[i];
                var itemResult = itemResults[i];

                if (itemResult.Success)
                {
                    row.Status = ActionSettings.DryRun ? "Dry Run - Would Recategorize" : "Manual Review Required";
                    areaResult.CorrectionsSuccessful++;
                }
                else
                {
                    row.Status = PAExcelStatus.FAILED;
                    row.ErrorMessage = itemResult.ErrorMessage;
                    areaResult.CorrectionsFailed++;
                    result.Errors.Add($"Model Integrity {row.ElementId}: {itemResult.ErrorMessage}");
                }

                areaResult.ItemResults.Add(itemResult);
            }

            result.ResultsByArea[PAComplianceArea.ModelIntegrity] = areaResult;
            progressMessages.Add($"Model integrity: {areaResult.CorrectionsSuccessful} successful, {areaResult.CorrectionsFailed} failed");
        }

        /// <summary>
        /// IExternalEventHandler.GetName implementation
        /// </summary>
        public string GetName()
        {
            return "PA Compliance Action Execution";
        }
    }

    /// <summary>
    /// Result structure for PA compliance action execution with progress tracking
    /// </summary>
    public class PAComplianceActionExecutionResult
    {
        [Newtonsoft.Json.JsonProperty("success")]
        public bool Success { get; set; }
        
        [Newtonsoft.Json.JsonProperty("message", NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Message { get; set; }
        
        [Newtonsoft.Json.JsonProperty("response", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PAComplianceActionResult Response { get; set; }
        
        [Newtonsoft.Json.JsonProperty("progressMessages", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public List<string> ProgressMessages { get; set; } = new List<string>();
        
        [Newtonsoft.Json.JsonProperty("stepExecuted", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string StepExecuted { get; set; }
    }
}