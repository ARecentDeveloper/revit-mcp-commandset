using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitMCPCommandSet.Models.PACompliance
{
    /// <summary>
    /// Validation result for PA compliance operations
    /// </summary>
    public class PAValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        public string GetSummary()
        {
            var summary = new List<string>();
            
            if (Errors.Any())
            {
                summary.Add($"Errors: {string.Join("; ", Errors)}");
            }
            
            if (Warnings.Any())
            {
                summary.Add($"Warnings: {string.Join("; ", Warnings)}");
            }

            return summary.Any() ? string.Join(" | ", summary) : "Valid";
        }
    }

    /// <summary>
    /// Validator for PA compliance report parameters
    /// </summary>
    public static class PAComplianceReportValidator
    {
        public static PAValidationResult ValidateParameters(PAComplianceReportParams parameters)
        {
            var result = new PAValidationResult { IsValid = true };

            // Validate output path
            if (!string.IsNullOrWhiteSpace(parameters.OutputPath))
            {
                try
                {
                    var directory = Path.GetDirectoryName(parameters.OutputPath);
                    if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    {
                        result.AddError($"Output directory does not exist: {directory}");
                    }

                    var extension = Path.GetExtension(parameters.OutputPath);
                    if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        result.AddWarning("Output file should have .xlsx extension");
                    }
                }
                catch (Exception ex)
                {
                    result.AddError($"Invalid output path: {ex.Message}");
                }
            }

            // Validate step parameter
            if (!string.IsNullOrWhiteSpace(parameters.Step))
            {
                var validSteps = new[] { "all", "families", "worksets", "sheets", "integrity" };
                if (!validSteps.Contains(parameters.Step.ToLower()))
                {
                    result.AddError($"Invalid step value. Must be one of: {string.Join(", ", validSteps)}");
                }
            }

            // Validate that at least one area is included
            if (!parameters.IncludeAnnotationFamilies && 
                !parameters.IncludeModelFamilies && 
                !parameters.IncludeWorksets && 
                !parameters.IncludeSheets && 
                !parameters.IncludeModelIntegrity)
            {
                result.AddError("At least one compliance area must be included");
            }

            return result;
        }
    }

    /// <summary>
    /// Validator for PA compliance action parameters
    /// </summary>
    public static class PAComplianceActionValidator
    {
        public static PAValidationResult ValidateParameters(PAComplianceActionParams parameters)
        {
            var result = new PAValidationResult { IsValid = true };

            // Validate Excel file path
            if (string.IsNullOrWhiteSpace(parameters.ExcelFilePath))
            {
                result.AddError("Excel file path is required");
            }
            else
            {
                try
                {
                    if (!File.Exists(parameters.ExcelFilePath))
                    {
                        result.AddError($"Excel file does not exist: {parameters.ExcelFilePath}");
                    }
                    else
                    {
                        var extension = Path.GetExtension(parameters.ExcelFilePath);
                        if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                        {
                            result.AddError("Excel file must have .xlsx extension");
                        }

                        // Check if file is readable
                        try
                        {
                            using (var stream = File.OpenRead(parameters.ExcelFilePath))
                            {
                                // File is readable
                            }
                        }
                        catch (Exception ex)
                        {
                            result.AddError($"Cannot read Excel file: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.AddError($"Invalid Excel file path: {ex.Message}");
                }
            }

            // Validate step parameter
            if (!string.IsNullOrWhiteSpace(parameters.Step))
            {
                var validSteps = new[] { "all", "families", "worksets", "sheets", "integrity" };
                if (!validSteps.Contains(parameters.Step.ToLower()))
                {
                    result.AddError($"Invalid step value. Must be one of: {string.Join(", ", validSteps)}");
                }
            }

            // Validate backup option
            if (parameters.BackupProject && parameters.DryRun)
            {
                result.AddWarning("Backup is not needed for dry run operations");
            }

            return result;
        }
    }

    /// <summary>
    /// Validator for Excel file structure
    /// </summary>
    public static class PAExcelStructureValidator
    {
        public static PAValidationResult ValidateWorkbookStructure(Dictionary<string, string[]> actualSheets)
        {
            var result = new PAValidationResult { IsValid = true };
            var expectedStructure = new PAExcelWorkbookStructure();

            // Check for required sheets
            var expectedSheets = expectedStructure.GetAllSheetNames();
            var missingSheets = expectedSheets.Where(sheet => !actualSheets.ContainsKey(sheet)).ToList();
            
            foreach (var missingSheet in missingSheets)
            {
                result.AddError($"Missing required sheet: {missingSheet}");
            }

            // Check sheet column structures
            foreach (var actualSheet in actualSheets)
            {
                if (expectedSheets.Contains(actualSheet.Key))
                {
                    if (!expectedStructure.ValidateSheetStructure(actualSheet.Key, actualSheet.Value))
                    {
                        var expectedColumns = expectedStructure.GetColumnsForSheet(actualSheet.Key);
                        result.AddError($"Sheet '{actualSheet.Key}' has incorrect column structure. Expected: [{string.Join(", ", expectedColumns)}], Found: [{string.Join(", ", actualSheet.Value)}]");
                    }
                }
                else
                {
                    result.AddWarning($"Unexpected sheet found: {actualSheet.Key}");
                }
            }

            return result;
        }

        public static PAValidationResult ValidateSheetData<T>(string sheetName, List<T> data) where T : PAExcelRowBase
        {
            var result = new PAValidationResult { IsValid = true };

            if (data == null || !data.Any())
            {
                result.AddWarning($"Sheet '{sheetName}' contains no data");
                return result;
            }

            // Validate each row
            for (int i = 0; i < data.Count; i++)
            {
                var row = data[i];
                var rowNumber = i + 2; // Excel row number (1-based + header)

                if (string.IsNullOrWhiteSpace(row.CurrentName))
                {
                    result.AddError($"Sheet '{sheetName}', Row {rowNumber}: Current name is required");
                }

                // Additional validation based on row type
                ValidateSpecificRowType(sheetName, row, rowNumber, result);
            }

            return result;
        }

        private static void ValidateSpecificRowType(string sheetName, PAExcelRowBase row, int rowNumber, PAValidationResult result)
        {
            switch (row)
            {
                case PAAnnotationFamilyRow annotationRow:
                    if (string.IsNullOrWhiteSpace(annotationRow.Category))
                    {
                        result.AddError($"Sheet '{sheetName}', Row {rowNumber}: Category is required");
                    }
                    break;

                case PAModelFamilyRow modelRow:
                    if (string.IsNullOrWhiteSpace(modelRow.Category))
                    {
                        result.AddError($"Sheet '{sheetName}', Row {rowNumber}: Category is required");
                    }
                    if (string.IsNullOrWhiteSpace(modelRow.Manufacturer))
                    {
                        result.AddWarning($"Sheet '{sheetName}', Row {rowNumber}: Manufacturer is missing, will use 'Generic'");
                    }
                    break;

                case PASheetRow sheetRow:
                    if (string.IsNullOrWhiteSpace(sheetRow.SheetNumber))
                    {
                        result.AddError($"Sheet '{sheetName}', Row {rowNumber}: Sheet number is required");
                    }
                    break;

                case PAModelIntegrityRow integrityRow:
                    if (string.IsNullOrWhiteSpace(integrityRow.ElementId))
                    {
                        result.AddError($"Sheet '{sheetName}', Row {rowNumber}: Element ID is required");
                    }
                    if (string.IsNullOrWhiteSpace(integrityRow.CurrentCategory))
                    {
                        result.AddError($"Sheet '{sheetName}', Row {rowNumber}: Current category is required");
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Validator for naming conventions
    /// </summary>
    public static class PANamingValidator
    {
        public static PAValidationResult ValidateAnnotationFamilyName(string familyName)
        {
            var result = new PAValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(familyName))
            {
                result.AddError("Family name cannot be empty");
                return result;
            }

            if (!PANamingRules.IsValidAnnotationFamilyName(familyName))
            {
                result.AddError($"Family name '{familyName}' does not follow PA annotation family naming convention (PA-CATEGORY-DESCRIPTION)");
            }

            return result;
        }

        public static PAValidationResult ValidateModelFamilyName(string familyName)
        {
            var result = new PAValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(familyName))
            {
                result.AddError("Family name cannot be empty");
                return result;
            }

            if (!PANamingRules.IsValidModelFamilyName(familyName))
            {
                result.AddError($"Family name '{familyName}' does not follow PA model family naming convention (CATEGORY-MANUFACTURER-DESCRIPTION)");
            }

            return result;
        }

        public static PAValidationResult ValidateNameComponent(string component, string componentName)
        {
            var result = new PAValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(component))
            {
                result.AddError($"{componentName} cannot be empty");
                return result;
            }

            if (!PANamingRules.IsValidNameComponent(component))
            {
                result.AddError($"{componentName} '{component}' contains invalid characters. Only letters, numbers, spaces, and underscores are allowed");
            }

            return result;
        }
    }
}