using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Commands;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services.Filtering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class PAComplianceReportEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
        /// Report settings (incoming data)
        /// </summary>
        public PAComplianceReportSettings ReportSettings { get; private set; }

        /// <summary>
        /// Execution result (outgoing data)
        /// </summary>
        public object Result { get; private set; }

        /// <summary>
        /// Set report parameters
        /// </summary>
        public void SetParameters(PAComplianceReportSettings settings)
        {
            ReportSettings = settings;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                if (ReportSettings == null)
                    throw new ArgumentNullException(nameof(ReportSettings), "Report settings are required");

                var reportData = new PAComplianceReportData();
                var progressMessages = new List<string>();

                // Determine what to include based on step parameter
                bool includeAnnotationFamilies = ShouldIncludeStep("families", "annotation") && ReportSettings.IncludeAnnotationFamilies;
                bool includeModelFamilies = ShouldIncludeStep("families", "model") && ReportSettings.IncludeModelFamilies;
                bool includeWorksets = ShouldIncludeStep("worksets") && ReportSettings.IncludeWorksets;
                bool includeSheets = ShouldIncludeStep("sheets") && ReportSettings.IncludeSheets;
                bool includeModelIntegrity = ShouldIncludeStep("integrity") && ReportSettings.IncludeModelIntegrity;

                // Report what steps will be executed
                var stepsToExecute = new List<string>();
                if (includeAnnotationFamilies) stepsToExecute.Add("annotation families");
                if (includeModelFamilies) stepsToExecute.Add("model families");
                if (includeWorksets) stepsToExecute.Add("worksets");
                if (includeSheets) stepsToExecute.Add("sheets");
                if (includeModelIntegrity) stepsToExecute.Add("model integrity");

                progressMessages.Add($"Executing PA compliance report for: {string.Join(", ", stepsToExecute)}");

                // Query annotation families
                if (includeAnnotationFamilies)
                {
                    progressMessages.Add("Processing annotation families...");
                    System.Diagnostics.Trace.WriteLine("PA Compliance: Processing annotation families...");
                    reportData.AnnotationFamilies = GetAnnotationFamilies();
                    progressMessages.Add($"Found {reportData.AnnotationFamilies.Count} annotation families");
                }

                // Query model families
                if (includeModelFamilies)
                {
                    progressMessages.Add("Processing model families...");
                    System.Diagnostics.Trace.WriteLine("PA Compliance: Processing model families...");
                    reportData.ModelFamilies = GetModelFamilies();
                    progressMessages.Add($"Found {reportData.ModelFamilies.Count} model families");
                }

                // Query worksets
                if (includeWorksets)
                {
                    progressMessages.Add("Processing worksets...");
                    System.Diagnostics.Trace.WriteLine("PA Compliance: Processing worksets...");
                    reportData.Worksets = GetWorksets();
                    progressMessages.Add($"Found {reportData.Worksets.Count} worksets");
                }

                // Query sheets
                if (includeSheets)
                {
                    progressMessages.Add("Processing sheets...");
                    System.Diagnostics.Trace.WriteLine("PA Compliance: Processing sheets...");
                    reportData.Sheets = GetSheets();
                    progressMessages.Add($"Found {reportData.Sheets.Count} sheets");
                }

                // Query model integrity issues
                if (includeModelIntegrity)
                {
                    progressMessages.Add("Analyzing model integrity...");
                    System.Diagnostics.Trace.WriteLine("PA Compliance: Analyzing model integrity...");
                    reportData.ModelIntegrityIssues = GetModelIntegrityIssues();
                    progressMessages.Add($"Found {reportData.ModelIntegrityIssues.Count} model integrity issues");
                }

                // Generate Excel report
                string outputPath = ReportSettings.OutputPath;
                
                // If no output path specified, create default path in Documents folder
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    outputPath = System.IO.Path.Combine(documentsPath, $"PA_Compliance_Report_{timestamp}.xlsx");
                }
                
                progressMessages.Add("Generating Excel report...");
                System.Diagnostics.Trace.WriteLine("PA Compliance: Generating Excel report...");
                var (excelSuccess, excelErrorMessage) = PAExcelReportService.GenerateExcelReport(reportData, outputPath);
                var excelStatusMessage = excelSuccess ? $" Excel report saved to: {outputPath}" : $" Excel generation failed: {excelErrorMessage}";
                progressMessages.Add(excelSuccess ? "Excel report generated successfully" : $"Excel generation failed: {excelErrorMessage}");

                progressMessages.Add("PA compliance report generation completed");

                Result = new PAComplianceReportResult
                {
                    Success = excelSuccess, // Overall success depends on Excel generation success
                    Message = excelSuccess ? 
                        $"Successfully generated PA compliance report. " +
                        $"Annotation families: {reportData.AnnotationFamilies?.Count ?? 0}, " +
                        $"Model families: {reportData.ModelFamilies?.Count ?? 0}, " +
                        $"Worksets: {reportData.Worksets?.Count ?? 0}, " +
                        $"Sheets: {reportData.Sheets?.Count ?? 0}, " +
                        $"Model integrity issues: {reportData.ModelIntegrityIssues?.Count ?? 0}" +
                        excelStatusMessage :
                        $"PA compliance report data generated but Excel export failed: {excelErrorMessage}",
                    Response = reportData,
                    ProgressMessages = progressMessages,
                    StepExecuted = ReportSettings.Step ?? "all",
                    OutputPath = outputPath,
                    Summary = new
                    {
                        annotationFamilies = reportData.AnnotationFamilies?.Count ?? 0,
                        modelFamilies = reportData.ModelFamilies?.Count ?? 0,
                        worksets = reportData.Worksets?.Count ?? 0,
                        sheets = reportData.Sheets?.Count ?? 0,
                        integrityIssues = reportData.ModelIntegrityIssues?.Count ?? 0
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"PA Compliance Error: {ex.Message}");
                Result = new PAComplianceReportResult
                {
                    Success = false,
                    Message = $"Error generating PA compliance report: {ex.Message}",
                    Response = null,
                    ProgressMessages = new List<string> { $"Error: {ex.Message}" },
                    StepExecuted = ReportSettings?.Step ?? "unknown"
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
        public bool WaitForCompletion(int timeoutMilliseconds = 30000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// Determine if a step should be included based on the step parameter
        /// </summary>
        private bool ShouldIncludeStep(string stepName, string subStep = null)
        {
            if (string.IsNullOrWhiteSpace(ReportSettings.Step) || ReportSettings.Step.Equals("all", StringComparison.OrdinalIgnoreCase))
                return true;

            // Support comma-separated steps
            var steps = ReportSettings.Step.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .ToList();

            // Check if any of the steps match
            foreach (var step in steps)
            {
                if (step.Equals(stepName.ToLowerInvariant()))
                    return true;

                if (!string.IsNullOrWhiteSpace(subStep) && step.Equals(subStep.ToLowerInvariant()))
                    return true;

                // Support combined step names like "annotation-families" or "model-families"
                var combinedStep = !string.IsNullOrWhiteSpace(subStep) ? $"{subStep}-{stepName}" : stepName;
                if (step.Equals(combinedStep.ToLowerInvariant().Replace(" ", "-")))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get annotation families from the document
        /// </summary>
        private List<PAFamilyInfo> GetAnnotationFamilies()
        {
            var families = new List<PAFamilyInfo>();

            try
            {
                System.Diagnostics.Trace.WriteLine("PA Compliance: Starting annotation families collection...");

                // Get all family symbols directly using FilteredElementCollector
                var collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .WhereElementIsElementType();

                var familySymbols = collector.Cast<FamilySymbol>()
                    .Where(fs => fs.Family != null && IsAnnotationFamily(fs.Family))
                    .ToList();

                System.Diagnostics.Trace.WriteLine($"PA Compliance: Found {familySymbols.Count} annotation family symbols");

                // Group by family to avoid duplicates
                var familyGroups = familySymbols.GroupBy(fs => fs.Family.Id);

                foreach (var familyGroup in familyGroups)
                {
                    var firstSymbol = familyGroup.First();
                    var family = firstSymbol.Family;
                    var categoryName = family.FamilyCategory?.Name ?? "Unknown";
                    
                    System.Diagnostics.Trace.WriteLine($"PA Compliance: Processing annotation family: {family.Name} (Category: {categoryName})");
                    
                    // Generate PA-compliant suggestion
                    var suggestedName = PANamingConventionService.GenerateAnnotationFamilyName(categoryName, family.Name);
                    
                    // Get all types for this family
                    var typeNames = familyGroup.Select(fs => fs.Name).ToList();
                    var typeName = typeNames.Count > 1 ? $"{typeNames.Count} types" : typeNames.FirstOrDefault() ?? "No types";
                    
                    families.Add(new PAFamilyInfo
                    {
                        ElementId = family.Id.IntegerValue,
                        Category = categoryName,
                        CurrentName = family.Name,
                        TypeName = typeName,
                        IsAnnotationFamily = true,
                        SuggestedName = suggestedName
                    });
                }

                System.Diagnostics.Trace.WriteLine($"PA Compliance: Completed annotation families collection. Found {families.Count} unique families");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error getting annotation families: {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return families;
        }

        /// <summary>
        /// Get model families from the document
        /// </summary>
        private List<PAFamilyInfo> GetModelFamilies()
        {
            var families = new List<PAFamilyInfo>();

            try
            {
                System.Diagnostics.Trace.WriteLine("PA Compliance: Starting model families collection...");

                // Get all family symbols directly using FilteredElementCollector
                var collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .WhereElementIsElementType();

                var familySymbols = collector.Cast<FamilySymbol>()
                    .Where(fs => fs.Family != null && !IsAnnotationFamily(fs.Family))
                    .ToList();

                System.Diagnostics.Trace.WriteLine($"PA Compliance: Found {familySymbols.Count} model family symbols");

                // Group by family to avoid duplicates
                var familyGroups = familySymbols.GroupBy(fs => fs.Family.Id);

                foreach (var familyGroup in familyGroups)
                {
                    var firstSymbol = familyGroup.First();
                    var family = firstSymbol.Family;
                    var categoryName = family.FamilyCategory?.Name ?? "Unknown";
                    
                    System.Diagnostics.Trace.WriteLine($"PA Compliance: Processing model family: {family.Name} (Category: {categoryName})");
                    
                    // Detect manufacturer and generate PA-compliant suggestion
                    var manufacturer = PANamingConventionService.DetectManufacturer(family.Name);
                    var suggestedName = PANamingConventionService.GenerateModelFamilyName(categoryName, family.Name, manufacturer);
                    
                    // Get all types for this family
                    var typeNames = familyGroup.Select(fs => fs.Name).ToList();
                    var typeName = typeNames.Count > 1 ? $"{typeNames.Count} types" : typeNames.FirstOrDefault() ?? "No types";
                    
                    families.Add(new PAFamilyInfo
                    {
                        ElementId = family.Id.IntegerValue,
                        Category = categoryName,
                        CurrentName = family.Name,
                        TypeName = typeName,
                        IsAnnotationFamily = false,
                        SuggestedName = suggestedName,
                        Manufacturer = manufacturer
                    });
                }

                System.Diagnostics.Trace.WriteLine($"PA Compliance: Completed model families collection. Found {families.Count} unique families");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error getting model families: {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return families;
        }

        /// <summary>
        /// Get worksets from the document
        /// </summary>
        private List<PAWorksetInfo> GetWorksets()
        {
            var worksets = new List<PAWorksetInfo>();

            try
            {
                if (!doc.IsWorkshared)
                {
                    System.Diagnostics.Trace.WriteLine("Document is not workshared, no worksets to report");
                    return worksets;
                }

                var worksetTable = doc.GetWorksetTable();
                
                // Use FilteredWorksetCollector to get worksets in Revit 2024
                var collector = new FilteredWorksetCollector(doc);
                var allWorksets = collector.OfKind(WorksetKind.UserWorkset).ToWorksets();

                foreach (var workset in allWorksets)
                {
                    if (workset != null)
                    {
                        // Generate PA-compliant suggestion
                        var suggestedName = PANamingConventionService.GenerateWorksetName(workset.Name);
                        
                        worksets.Add(new PAWorksetInfo
                        {
                            WorksetId = workset.Id.IntegerValue,
                            CurrentName = workset.Name,
                            SuggestedName = suggestedName
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error getting worksets: {ex.Message}");
            }

            return worksets;
        }

        /// <summary>
        /// Get sheets from the document
        /// </summary>
        private List<PASheetInfo> GetSheets()
        {
            var sheets = new List<PASheetInfo>();

            try
            {
                // Create filter settings for sheets
                var filterSettings = new FilterSetting
                {
                    FilterCategory = "OST_Sheets",
                    IncludeTypes = false,
                    IncludeInstances = true,
                    MaxElements = 0 // No limit
                };

                var sheetElements = ElementFilterService.GetFilteredElements(doc, filterSettings)
                    .OfType<ViewSheet>()
                    .ToList();

                foreach (var sheet in sheetElements)
                {
                    // Generate PA-compliant suggestion
                    var suggestedName = PANamingConventionService.GenerateSheetName(sheet.Name, sheet.SheetNumber);
                    
                    sheets.Add(new PASheetInfo
                    {
                        ElementId = sheet.Id.IntegerValue,
                        CurrentName = sheet.Name,
                        SheetNumber = sheet.SheetNumber,
                        SuggestedName = suggestedName
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error getting sheets: {ex.Message}");
            }

            return sheets;
        }

        /// <summary>
        /// Get model integrity issues (elements in Generic category that should be in specific categories)
        /// </summary>
        private List<PAModelIntegrityIssue> GetModelIntegrityIssues()
        {
            var issues = new List<PAModelIntegrityIssue>();

            try
            {
                // Create filter settings for Generic Model category
                var filterSettings = new FilterSetting
                {
                    FilterCategory = "OST_GenericModel",
                    IncludeTypes = false,
                    IncludeInstances = true,
                    MaxElements = 0 // No limit
                };

                var genericElements = ElementFilterService.GetFilteredElements(doc, filterSettings);

                foreach (var element in genericElements)
                {
                    var elementName = element.Name ?? "Unnamed Element";
                    var currentCategory = element.Category?.Name ?? "Unknown";
                    
                    // Generate category suggestion
                    var suggestedCategory = PANamingConventionService.SuggestCategory(elementName, currentCategory);
                    
                    issues.Add(new PAModelIntegrityIssue
                    {
                        ElementId = element.Id.IntegerValue,
                        CurrentCategory = currentCategory,
                        ElementName = elementName,
                        SuggestedCategory = suggestedCategory
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error getting model integrity issues: {ex.Message}");
            }

            return issues;
        }

        /// <summary>
        /// Determine if a family is an annotation family
        /// </summary>
        private bool IsAnnotationFamily(Family family)
        {
            if (family.FamilyCategory == null)
            {
                System.Diagnostics.Trace.WriteLine($"PA Compliance: Family {family.Name} has no category");
                return false;
            }

            // Check if the category is an annotation category
            var categoryId = family.FamilyCategory.Id;
            var builtInCategory = (BuiltInCategory)categoryId.IntegerValue;
            var categoryName = family.FamilyCategory.Name;

            // Common annotation categories
            var annotationCategories = new[]
            {
                BuiltInCategory.OST_TextNotes,
                BuiltInCategory.OST_Dimensions,
                BuiltInCategory.OST_Tags,
                BuiltInCategory.OST_GenericAnnotation,
                BuiltInCategory.OST_DetailComponents,
                BuiltInCategory.OST_TitleBlocks,
                BuiltInCategory.OST_Callouts,
                BuiltInCategory.OST_ElevationMarks,
                BuiltInCategory.OST_Sections,
                BuiltInCategory.OST_Views,
                BuiltInCategory.OST_Viewports
            };

            bool isAnnotation = annotationCategories.Any(cat => cat == builtInCategory);
            
            // Additional check for annotation-like categories by name
            if (!isAnnotation)
            {
                var annotationKeywords = new[] { "tag", "symbol", "annotation", "detail", "title", "callout", "elevation", "section" };
                isAnnotation = annotationKeywords.Any(keyword => 
                    categoryName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            System.Diagnostics.Trace.WriteLine($"PA Compliance: Family {family.Name} (Category: {categoryName}, BuiltIn: {builtInCategory}) is {(isAnnotation ? "annotation" : "model")} family");
            
            return isAnnotation;
        }

        /// <summary>
        /// IExternalEventHandler.GetName implementation
        /// </summary>
        public string GetName()
        {
            return "PA Compliance Report Generation";
        }
    }

    /// <summary>
    /// Data structure for PA compliance report
    /// </summary>
    public class PAComplianceReportData
    {
        public List<PAFamilyInfo> AnnotationFamilies { get; set; } = new List<PAFamilyInfo>();
        public List<PAFamilyInfo> ModelFamilies { get; set; } = new List<PAFamilyInfo>();
        public List<PAWorksetInfo> Worksets { get; set; } = new List<PAWorksetInfo>();
        public List<PASheetInfo> Sheets { get; set; } = new List<PASheetInfo>();
        public List<PAModelIntegrityIssue> ModelIntegrityIssues { get; set; } = new List<PAModelIntegrityIssue>();
    }

    /// <summary>
    /// Information about a family for PA compliance
    /// </summary>
    public class PAFamilyInfo
    {
        public int ElementId { get; set; }
        public string Category { get; set; }
        public string CurrentName { get; set; }
        public string TypeName { get; set; }
        public bool IsAnnotationFamily { get; set; }
        public string SuggestedName { get; set; } = "";
        public string Manufacturer { get; set; } = "";
    }

    /// <summary>
    /// Information about a workset for PA compliance
    /// </summary>
    public class PAWorksetInfo
    {
        public int WorksetId { get; set; }
        public string CurrentName { get; set; }
        public string SuggestedName { get; set; } = "";
    }

    /// <summary>
    /// Information about a sheet for PA compliance
    /// </summary>
    public class PASheetInfo
    {
        public int ElementId { get; set; }
        public string CurrentName { get; set; }
        public string SheetNumber { get; set; }
        public string SuggestedName { get; set; } = "";
    }

    /// <summary>
    /// Information about a model integrity issue
    /// </summary>
    public class PAModelIntegrityIssue
    {
        public int ElementId { get; set; }
        public string CurrentCategory { get; set; }
        public string ElementName { get; set; }
        public string SuggestedCategory { get; set; } = "";
    }

    /// <summary>
    /// Result structure for PA compliance report with progress tracking
    /// </summary>
    public class PAComplianceReportResult
    {
        [Newtonsoft.Json.JsonProperty("success")]
        public bool Success { get; set; }
        
        [Newtonsoft.Json.JsonProperty("message", NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Message { get; set; }
        
        [Newtonsoft.Json.JsonProperty("response", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PAComplianceReportData Response { get; set; }
        
        [Newtonsoft.Json.JsonProperty("progressMessages", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public List<string> ProgressMessages { get; set; } = new List<string>();
        
        [Newtonsoft.Json.JsonProperty("stepExecuted", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string StepExecuted { get; set; }
        
        [Newtonsoft.Json.JsonProperty("outputPath", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string OutputPath { get; set; }
        
        [Newtonsoft.Json.JsonProperty("summary", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public object Summary { get; set; }
    }
}