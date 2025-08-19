using OfficeOpenXml;
using RevitMCPCommandSet.Models.PACompliance;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Service for reading and processing PA compliance Excel files for action execution
    /// </summary>
    public static class PAExcelActionService
    {
        /// <summary>
        /// Read Excel file and parse compliance data
        /// </summary>
        /// <param name="filePath">Path to Excel file</param>
        /// <returns>Parsed Excel data</returns>
        public static PAExcelActionData ReadExcelFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                PAComplianceLoggingService.LogValidationError("FilePath", "Excel file path cannot be null or empty");
                throw new ArgumentException("Excel file path cannot be null or empty");
            }

            if (!File.Exists(filePath))
            {
                PAComplianceLoggingService.LogValidationError("FileExists", $"Excel file not found: {filePath}");
                throw new FileNotFoundException($"Excel file not found: {filePath}");
            }

            try
            {
                // Set EPPlus license context
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    var data = new PAExcelActionData();

                    // Read each sheet if it exists with error recovery
                    data.AnnotationFamilies = ReadAnnotationFamiliesSheetWithErrorHandling(package);
                    data.ModelFamilies = ReadModelFamiliesSheetWithErrorHandling(package);
                    data.Worksets = ReadWorksetsSheetWithErrorHandling(package);
                    data.Sheets = ReadSheetsSheetWithErrorHandling(package);
                    data.ModelIntegrityIssues = ReadModelIntegritySheetWithErrorHandling(package);

                    PAComplianceLoggingService.LogInfo("Excel file read successfully", new Dictionary<string, object>
                    {
                        { "FilePath", filePath },
                        { "TotalRows", data.GetTotalRowCount() },
                        { "ProcessingRows", data.GetProcessingRowCount() },
                        { "AnnotationFamilies", data.AnnotationFamilies.Count },
                        { "ModelFamilies", data.ModelFamilies.Count },
                        { "Worksets", data.Worksets.Count },
                        { "Sheets", data.Sheets.Count },
                        { "ModelIntegrityIssues", data.ModelIntegrityIssues.Count }
                    });

                    return data;
                }
            }
            catch (Exception ex)
            {
                PAComplianceLoggingService.LogError("Failed to read Excel file", ex, new Dictionary<string, object>
                {
                    { "FilePath", filePath }
                });
                throw new Exception($"Error reading Excel file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update Excel file with execution results
        /// </summary>
        /// <param name="filePath">Path to Excel file</param>
        /// <param name="results">Execution results</param>
        public static void UpdateExcelWithResults(string filePath, PAComplianceActionResult results)
        {
            try
            {
                // Set EPPlus license context
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    // Update each sheet with results
                    if (results.ResultsByArea.ContainsKey(PAComplianceArea.AnnotationFamilies))
                    {
                        UpdateAnnotationFamiliesSheet(package, results.ResultsByArea[PAComplianceArea.AnnotationFamilies]);
                    }

                    if (results.ResultsByArea.ContainsKey(PAComplianceArea.ModelFamilies))
                    {
                        UpdateModelFamiliesSheet(package, results.ResultsByArea[PAComplianceArea.ModelFamilies]);
                    }

                    if (results.ResultsByArea.ContainsKey(PAComplianceArea.Worksets))
                    {
                        UpdateWorksetsSheet(package, results.ResultsByArea[PAComplianceArea.Worksets]);
                    }

                    if (results.ResultsByArea.ContainsKey(PAComplianceArea.Sheets))
                    {
                        UpdateSheetsSheet(package, results.ResultsByArea[PAComplianceArea.Sheets]);
                    }

                    if (results.ResultsByArea.ContainsKey(PAComplianceArea.ModelIntegrity))
                    {
                        UpdateModelIntegritySheet(package, results.ResultsByArea[PAComplianceArea.ModelIntegrity]);
                    }

                    // Save the updated file
                    package.Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Warning: Could not update Excel file with results: {ex.Message}");
            }
        }

        /// <summary>
        /// Read annotation families sheet with error handling
        /// </summary>
        private static List<PAAnnotationFamilyRow> ReadAnnotationFamiliesSheetWithErrorHandling(ExcelPackage package)
        {
            try
            {
                return ReadAnnotationFamiliesSheet(package);
            }
            catch (Exception ex)
            {
                PAComplianceLoggingService.LogErrorRecovery("ReadAnnotationFamiliesSheet", ex.Message, "Continuing with empty list");
                return new List<PAAnnotationFamilyRow>();
            }
        }

        /// <summary>
        /// Read model families sheet with error handling
        /// </summary>
        private static List<PAModelFamilyRow> ReadModelFamiliesSheetWithErrorHandling(ExcelPackage package)
        {
            try
            {
                return ReadModelFamiliesSheet(package);
            }
            catch (Exception ex)
            {
                PAComplianceLoggingService.LogErrorRecovery("ReadModelFamiliesSheet", ex.Message, "Continuing with empty list");
                return new List<PAModelFamilyRow>();
            }
        }

        /// <summary>
        /// Read worksets sheet with error handling
        /// </summary>
        private static List<PAWorksetRow> ReadWorksetsSheetWithErrorHandling(ExcelPackage package)
        {
            try
            {
                return ReadWorksetsSheet(package);
            }
            catch (Exception ex)
            {
                PAComplianceLoggingService.LogErrorRecovery("ReadWorksetsSheet", ex.Message, "Continuing with empty list");
                return new List<PAWorksetRow>();
            }
        }

        /// <summary>
        /// Read sheets sheet with error handling
        /// </summary>
        private static List<PASheetRow> ReadSheetsSheetWithErrorHandling(ExcelPackage package)
        {
            try
            {
                return ReadSheetsSheet(package);
            }
            catch (Exception ex)
            {
                PAComplianceLoggingService.LogErrorRecovery("ReadSheetsSheet", ex.Message, "Continuing with empty list");
                return new List<PASheetRow>();
            }
        }

        /// <summary>
        /// Read model integrity sheet with error handling
        /// </summary>
        private static List<PAModelIntegrityRow> ReadModelIntegritySheetWithErrorHandling(ExcelPackage package)
        {
            try
            {
                return ReadModelIntegritySheet(package);
            }
            catch (Exception ex)
            {
                PAComplianceLoggingService.LogErrorRecovery("ReadModelIntegritySheet", ex.Message, "Continuing with empty list");
                return new List<PAModelIntegrityRow>();
            }
        }

        /// <summary>
        /// Read annotation families sheet
        /// </summary>
        private static List<PAAnnotationFamilyRow> ReadAnnotationFamiliesSheet(ExcelPackage package)
        {
            var rows = new List<PAAnnotationFamilyRow>();
            var worksheet = package.Workbook.Worksheets[PAExcelSheets.ANNOTATION_FAMILIES];
            
            if (worksheet == null)
            {
                System.Diagnostics.Trace.WriteLine("PA Excel Action: Annotation Families sheet not found");
                return rows;
            }

            try
            {
                System.Diagnostics.Trace.WriteLine($"PA Excel Action: Reading Annotation Families sheet with {worksheet.Dimension.End.Row - 1} data rows");

                var structure = new PAExcelWorkbookStructure();
                var expectedColumns = structure.GetColumnsForSheet(PAExcelSheets.ANNOTATION_FAMILIES);
                
                // Validate sheet structure
                var actualColumns = GetHeaderRow(worksheet);
                if (!structure.ValidateSheetStructure(PAExcelSheets.ANNOTATION_FAMILIES, actualColumns))
                {
                    System.Diagnostics.Trace.WriteLine($"PA Excel Action: Invalid sheet structure. Expected: [{string.Join(", ", expectedColumns)}], Actual: [{string.Join(", ", actualColumns)}]");
                    throw new InvalidOperationException($"Invalid sheet structure for {PAExcelSheets.ANNOTATION_FAMILIES}");
                }

                // Read data rows (starting from row 2)
                for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                {
                    var rowData = new PAAnnotationFamilyRow
                    {
                        Category = GetCellValue(worksheet, row, PAExcelColumns.AnnotationFamilies.CATEGORY),
                        CurrentNameValue = GetCellValue(worksheet, row, PAExcelColumns.AnnotationFamilies.CURRENT_NAME),
                        SuggestedNameValue = GetCellValue(worksheet, row, PAExcelColumns.AnnotationFamilies.SUGGESTED_NAME),
                        Status = GetCellValue(worksheet, row, PAExcelColumns.AnnotationFamilies.STATUS),
                        ErrorMessage = GetCellValue(worksheet, row, PAExcelColumns.AnnotationFamilies.ERROR_MESSAGE)
                    };

                    // Extract element ID from the first column (Element ID)
                    rowData.ElementId = GetCellValue(worksheet, row, "Element ID");
                    if (string.IsNullOrWhiteSpace(rowData.ElementId))
                    {
                        rowData.ElementId = ExtractElementId(worksheet, row, "ElementId") ?? "0";
                    }

                    System.Diagnostics.Trace.WriteLine($"PA Excel Action: Row {row} - ID: '{rowData.ElementId}', Current: '{rowData.CurrentName}', Suggested: '{rowData.SuggestedName}', ShouldProcess: {rowData.ShouldProcess}, TargetName: '{rowData.GetTargetName()}'");

                    if (!string.IsNullOrWhiteSpace(rowData.CurrentName))
                    {
                        rows.Add(rowData);
                    }
                }

                System.Diagnostics.Trace.WriteLine($"PA Excel Action: Successfully read {rows.Count} annotation family rows, {rows.Count(r => r.ShouldProcess)} should be processed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error reading annotation families sheet: {ex.Message}");
            }

            return rows;
        }

        /// <summary>
        /// Read model families sheet
        /// </summary>
        private static List<PAModelFamilyRow> ReadModelFamiliesSheet(ExcelPackage package)
        {
            var rows = new List<PAModelFamilyRow>();
            var worksheet = package.Workbook.Worksheets[PAExcelSheets.MODEL_FAMILIES];
            
            if (worksheet == null)
                return rows;

            try
            {
                var structure = new PAExcelWorkbookStructure();
                var expectedColumns = structure.GetColumnsForSheet(PAExcelSheets.MODEL_FAMILIES);
                
                // Validate sheet structure
                var actualColumns = GetHeaderRow(worksheet);
                if (!structure.ValidateSheetStructure(PAExcelSheets.MODEL_FAMILIES, actualColumns))
                {
                    throw new InvalidOperationException($"Invalid sheet structure for {PAExcelSheets.MODEL_FAMILIES}");
                }

                // Read data rows (starting from row 2)
                for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                {
                    var rowData = new PAModelFamilyRow
                    {
                        Category = GetCellValue(worksheet, row, PAExcelColumns.ModelFamilies.CATEGORY),
                        Manufacturer = GetCellValue(worksheet, row, PAExcelColumns.ModelFamilies.MANUFACTURER),
                        CurrentNameValue = GetCellValue(worksheet, row, PAExcelColumns.ModelFamilies.CURRENT_NAME),
                        SuggestedNameValue = GetCellValue(worksheet, row, PAExcelColumns.ModelFamilies.SUGGESTED_NAME),
                        Status = GetCellValue(worksheet, row, PAExcelColumns.ModelFamilies.STATUS),
                        ErrorMessage = GetCellValue(worksheet, row, PAExcelColumns.ModelFamilies.ERROR_MESSAGE)
                    };

                    // Extract element ID from hidden columns or derive from data
                    rowData.ElementId = ExtractElementId(worksheet, row, "ElementId") ?? "0";

                    if (!string.IsNullOrWhiteSpace(rowData.CurrentName))
                    {
                        rows.Add(rowData);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error reading model families sheet: {ex.Message}");
            }

            return rows;
        }

        /// <summary>
        /// Read worksets sheet
        /// </summary>
        private static List<PAWorksetRow> ReadWorksetsSheet(ExcelPackage package)
        {
            var rows = new List<PAWorksetRow>();
            var worksheet = package.Workbook.Worksheets[PAExcelSheets.WORKSETS];
            
            if (worksheet == null)
                return rows;

            try
            {
                var structure = new PAExcelWorkbookStructure();
                var expectedColumns = structure.GetColumnsForSheet(PAExcelSheets.WORKSETS);
                
                // Validate sheet structure
                var actualColumns = GetHeaderRow(worksheet);
                if (!structure.ValidateSheetStructure(PAExcelSheets.WORKSETS, actualColumns))
                {
                    throw new InvalidOperationException($"Invalid sheet structure for {PAExcelSheets.WORKSETS}");
                }

                // Read data rows (starting from row 2)
                for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                {
                    var rowData = new PAWorksetRow
                    {
                        CurrentNameValue = GetCellValue(worksheet, row, PAExcelColumns.Worksets.CURRENT_NAME),
                        SuggestedNameValue = GetCellValue(worksheet, row, PAExcelColumns.Worksets.SUGGESTED_NAME),
                        Status = GetCellValue(worksheet, row, PAExcelColumns.Worksets.STATUS),
                        ErrorMessage = GetCellValue(worksheet, row, PAExcelColumns.Worksets.ERROR_MESSAGE)
                    };

                    // Extract workset ID from hidden columns or derive from data
                    var worksetIdStr = ExtractElementId(worksheet, row, "WorksetId") ?? "0";
                    if (int.TryParse(worksetIdStr, out int worksetId))
                    {
                        rowData.WorksetId = worksetId;
                    }

                    if (!string.IsNullOrWhiteSpace(rowData.CurrentName))
                    {
                        rows.Add(rowData);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error reading worksets sheet: {ex.Message}");
            }

            return rows;
        }

        /// <summary>
        /// Read sheets sheet
        /// </summary>
        private static List<PASheetRow> ReadSheetsSheet(ExcelPackage package)
        {
            var rows = new List<PASheetRow>();
            var worksheet = package.Workbook.Worksheets[PAExcelSheets.SHEETS];
            
            if (worksheet == null)
            {
                System.Diagnostics.Trace.WriteLine("PA Excel Action: Sheets sheet not found");
                return rows;
            }

            try
            {
                System.Diagnostics.Trace.WriteLine($"PA Excel Action: Reading Sheets sheet with {worksheet.Dimension.End.Row - 1} data rows");

                var structure = new PAExcelWorkbookStructure();
                var expectedColumns = structure.GetColumnsForSheet(PAExcelSheets.SHEETS);
                
                // Validate sheet structure
                var actualColumns = GetHeaderRow(worksheet);
                if (!structure.ValidateSheetStructure(PAExcelSheets.SHEETS, actualColumns))
                {
                    System.Diagnostics.Trace.WriteLine($"PA Excel Action: Invalid sheet structure. Expected: [{string.Join(", ", expectedColumns)}], Actual: [{string.Join(", ", actualColumns)}]");
                    throw new InvalidOperationException($"Invalid sheet structure for {PAExcelSheets.SHEETS}");
                }

                // Read data rows (starting from row 2)
                for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                {
                    var rowData = new PASheetRow
                    {
                        SheetNumber = GetCellValue(worksheet, row, PAExcelColumns.Sheets.SHEET_NUMBER),
                        CurrentNameValue = GetCellValue(worksheet, row, PAExcelColumns.Sheets.CURRENT_NAME),
                        SuggestedNameValue = GetCellValue(worksheet, row, PAExcelColumns.Sheets.SUGGESTED_NAME),
                        Status = GetCellValue(worksheet, row, PAExcelColumns.Sheets.STATUS),
                        ErrorMessage = GetCellValue(worksheet, row, PAExcelColumns.Sheets.ERROR_MESSAGE)
                    };

                    // Extract element ID from the Element ID column
                    rowData.ElementId = GetCellValue(worksheet, row, PAExcelColumns.Sheets.ELEMENT_ID);
                    if (string.IsNullOrWhiteSpace(rowData.ElementId))
                    {
                        rowData.ElementId = ExtractElementId(worksheet, row, "ElementId") ?? "0";
                    }

                    System.Diagnostics.Trace.WriteLine($"PA Excel Action: Sheet Row {row} - ID: '{rowData.ElementId}', Number: '{rowData.SheetNumber}', Current: '{rowData.CurrentName}', Suggested: '{rowData.SuggestedName}', ShouldProcess: {rowData.ShouldProcess}, TargetName: '{rowData.GetTargetName()}'");

                    if (!string.IsNullOrWhiteSpace(rowData.CurrentName))
                    {
                        rows.Add(rowData);
                    }
                }

                System.Diagnostics.Trace.WriteLine($"PA Excel Action: Successfully read {rows.Count} sheet rows, {rows.Count(r => r.ShouldProcess)} should be processed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error reading sheets sheet: {ex.Message}");
            }

            return rows;
        }

        /// <summary>
        /// Read model integrity sheet
        /// </summary>
        private static List<PAModelIntegrityRow> ReadModelIntegritySheet(ExcelPackage package)
        {
            var rows = new List<PAModelIntegrityRow>();
            var worksheet = package.Workbook.Worksheets[PAExcelSheets.MODEL_INTEGRITY];
            
            if (worksheet == null)
                return rows;

            try
            {
                var structure = new PAExcelWorkbookStructure();
                var expectedColumns = structure.GetColumnsForSheet(PAExcelSheets.MODEL_INTEGRITY);
                
                // Validate sheet structure
                var actualColumns = GetHeaderRow(worksheet);
                if (!structure.ValidateSheetStructure(PAExcelSheets.MODEL_INTEGRITY, actualColumns))
                {
                    throw new InvalidOperationException($"Invalid sheet structure for {PAExcelSheets.MODEL_INTEGRITY}");
                }

                // Read data rows (starting from row 2)
                for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                {
                    var rowData = new PAModelIntegrityRow
                    {
                        ElementId = GetCellValue(worksheet, row, PAExcelColumns.ModelIntegrity.ELEMENT_ID),
                        ElementType = GetCellValue(worksheet, row, PAExcelColumns.ModelIntegrity.ELEMENT_TYPE),
                        CurrentCategory = GetCellValue(worksheet, row, PAExcelColumns.ModelIntegrity.CURRENT_CATEGORY),
                        SuggestedCategory = GetCellValue(worksheet, row, PAExcelColumns.ModelIntegrity.SUGGESTED_CATEGORY),
                        IssueDescription = GetCellValue(worksheet, row, PAExcelColumns.ModelIntegrity.ISSUE_DESCRIPTION),
                        Status = GetCellValue(worksheet, row, PAExcelColumns.ModelIntegrity.STATUS),
                        ErrorMessage = GetCellValue(worksheet, row, PAExcelColumns.ModelIntegrity.ERROR_MESSAGE)
                    };

                    if (!string.IsNullOrWhiteSpace(rowData.ElementId))
                    {
                        rows.Add(rowData);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error reading model integrity sheet: {ex.Message}");
            }

            return rows;
        }

        /// <summary>
        /// Get header row from worksheet
        /// </summary>
        private static string[] GetHeaderRow(ExcelWorksheet worksheet)
        {
            var headers = new List<string>();
            
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var headerValue = worksheet.Cells[1, col].Value?.ToString() ?? "";
                headers.Add(headerValue);
            }

            return headers.ToArray();
        }

        /// <summary>
        /// Get cell value by column name
        /// </summary>
        private static string GetCellValue(ExcelWorksheet worksheet, int row, string columnName)
        {
            // Find column index by header name
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var headerValue = worksheet.Cells[1, col].Value?.ToString() ?? "";
                if (headerValue.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return worksheet.Cells[row, col].Value?.ToString() ?? "";
                }
            }

            return "";
        }

        /// <summary>
        /// Extract element ID from hidden columns or derive from data
        /// </summary>
        private static string ExtractElementId(ExcelWorksheet worksheet, int row, string idColumnName)
        {
            // Try to find a hidden column with element ID
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var headerValue = worksheet.Cells[1, col].Value?.ToString() ?? "";
                if (headerValue.Equals(idColumnName, StringComparison.OrdinalIgnoreCase) ||
                    headerValue.Equals("Element ID", StringComparison.OrdinalIgnoreCase))
                {
                    return worksheet.Cells[row, col].Value?.ToString() ?? "";
                }
            }

            return null;
        }

        /// <summary>
        /// Update annotation families sheet with results
        /// </summary>
        private static void UpdateAnnotationFamiliesSheet(ExcelPackage package, PAComplianceAreaResult results)
        {
            var worksheet = package.Workbook.Worksheets[PAExcelSheets.ANNOTATION_FAMILIES];
            if (worksheet == null) return;

            UpdateSheetWithResults(worksheet, results, PAExcelColumns.AnnotationFamilies.STATUS, PAExcelColumns.AnnotationFamilies.ERROR_MESSAGE);
        }

        /// <summary>
        /// Update model families sheet with results
        /// </summary>
        private static void UpdateModelFamiliesSheet(ExcelPackage package, PAComplianceAreaResult results)
        {
            var worksheet = package.Workbook.Worksheets[PAExcelSheets.MODEL_FAMILIES];
            if (worksheet == null) return;

            UpdateSheetWithResults(worksheet, results, PAExcelColumns.ModelFamilies.STATUS, PAExcelColumns.ModelFamilies.ERROR_MESSAGE);
        }

        /// <summary>
        /// Update worksets sheet with results
        /// </summary>
        private static void UpdateWorksetsSheet(ExcelPackage package, PAComplianceAreaResult results)
        {
            var worksheet = package.Workbook.Worksheets[PAExcelSheets.WORKSETS];
            if (worksheet == null) return;

            UpdateSheetWithResults(worksheet, results, PAExcelColumns.Worksets.STATUS, PAExcelColumns.Worksets.ERROR_MESSAGE);
        }

        /// <summary>
        /// Update sheets sheet with results
        /// </summary>
        private static void UpdateSheetsSheet(ExcelPackage package, PAComplianceAreaResult results)
        {
            var worksheet = package.Workbook.Worksheets[PAExcelSheets.SHEETS];
            if (worksheet == null) return;

            UpdateSheetWithResults(worksheet, results, PAExcelColumns.Sheets.STATUS, PAExcelColumns.Sheets.ERROR_MESSAGE);
        }

        /// <summary>
        /// Update model integrity sheet with results
        /// </summary>
        private static void UpdateModelIntegritySheet(ExcelPackage package, PAComplianceAreaResult results)
        {
            var worksheet = package.Workbook.Worksheets[PAExcelSheets.MODEL_INTEGRITY];
            if (worksheet == null) return;

            UpdateSheetWithResults(worksheet, results, PAExcelColumns.ModelIntegrity.STATUS, PAExcelColumns.ModelIntegrity.ERROR_MESSAGE);
        }

        /// <summary>
        /// Generic method to update sheet with results
        /// </summary>
        private static void UpdateSheetWithResults(ExcelWorksheet worksheet, PAComplianceAreaResult results, string statusColumnName, string errorColumnName)
        {
            // Find column indices
            int statusCol = -1, errorCol = -1;
            
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var headerValue = worksheet.Cells[1, col].Value?.ToString() ?? "";
                if (headerValue.Equals(statusColumnName, StringComparison.OrdinalIgnoreCase))
                    statusCol = col;
                if (headerValue.Equals(errorColumnName, StringComparison.OrdinalIgnoreCase))
                    errorCol = col;
            }

            if (statusCol == -1) return; // Can't update without status column

            // Update rows based on results
            foreach (var itemResult in results.ItemResults)
            {
                // Find the row for this item (this is simplified - in practice might need better matching)
                for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                {
                    // Update status
                    if (statusCol > 0)
                    {
                        worksheet.Cells[row, statusCol].Value = itemResult.Success ? PAExcelStatus.SUCCESS : PAExcelStatus.FAILED;
                    }

                    // Update error message if column exists and there's an error
                    if (errorCol > 0 && !itemResult.Success)
                    {
                        worksheet.Cells[row, errorCol].Value = itemResult.ErrorMessage;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Data structure for Excel action data
    /// </summary>
    public class PAExcelActionData
    {
        public List<PAAnnotationFamilyRow> AnnotationFamilies { get; set; } = new List<PAAnnotationFamilyRow>();
        public List<PAModelFamilyRow> ModelFamilies { get; set; } = new List<PAModelFamilyRow>();
        public List<PAWorksetRow> Worksets { get; set; } = new List<PAWorksetRow>();
        public List<PASheetRow> Sheets { get; set; } = new List<PASheetRow>();
        public List<PAModelIntegrityRow> ModelIntegrityIssues { get; set; } = new List<PAModelIntegrityRow>();

        /// <summary>
        /// Get total number of rows across all sheets
        /// </summary>
        public int GetTotalRowCount()
        {
            return AnnotationFamilies.Count + ModelFamilies.Count + Worksets.Count + Sheets.Count + ModelIntegrityIssues.Count;
        }

        /// <summary>
        /// Get total number of rows that need processing
        /// </summary>
        public int GetProcessingRowCount()
        {
            return AnnotationFamilies.Count(r => r.ShouldProcess) +
                   ModelFamilies.Count(r => r.ShouldProcess) +
                   Worksets.Count(r => r.ShouldProcess) +
                   Sheets.Count(r => r.ShouldProcess) +
                   ModelIntegrityIssues.Count(r => r.ShouldProcess);
        }
    }
}