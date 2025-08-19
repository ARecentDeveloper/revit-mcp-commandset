using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Color = System.Drawing.Color;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Service for generating PA compliance Excel reports
    /// </summary>
    public static class PAExcelReportService
    {
        /// <summary>
        /// Generate Excel report from PA compliance data
        /// </summary>
        /// <param name="reportData">PA compliance report data</param>
        /// <param name="outputPath">Output file path</param>
        /// <returns>Success status and message</returns>
        public static (bool Success, string Message) GenerateExcelReport(PAComplianceReportData reportData, string outputPath)
        {
            try
            {
                // Set EPPlus license context
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage())
                {
                    var sheetsCreated = 0;
                    
                    // Create worksheets based on available data
                    if (reportData.AnnotationFamilies?.Any() == true)
                    {
                        CreateAnnotationFamiliesSheet(package, reportData.AnnotationFamilies);
                        sheetsCreated++;
                    }

                    if (reportData.ModelFamilies?.Any() == true)
                    {
                        CreateModelFamiliesSheet(package, reportData.ModelFamilies);
                        sheetsCreated++;
                    }

                    if (reportData.Worksets?.Any() == true)
                    {
                        CreateWorksetsSheet(package, reportData.Worksets);
                        sheetsCreated++;
                    }

                    if (reportData.Sheets?.Any() == true)
                    {
                        CreateSheetsSheet(package, reportData.Sheets);
                        sheetsCreated++;
                    }

                    if (reportData.ModelIntegrityIssues?.Any() == true)
                    {
                        CreateModelIntegritySheet(package, reportData.ModelIntegrityIssues);
                        sheetsCreated++;
                    }

                    // Always create summary sheet
                    CreateSummarySheet(package, reportData);
                    sheetsCreated++;

                    // Ensure output directory exists
                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Save the file
                    var fileInfo = new FileInfo(outputPath);
                    package.SaveAs(fileInfo);

                    return (true, $"Excel report successfully generated with {sheetsCreated} sheets at: {outputPath}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error generating Excel report: {ex.Message}");
            }
        }

        /// <summary>
        /// Create annotation families worksheet
        /// </summary>
        private static void CreateAnnotationFamiliesSheet(ExcelPackage package, List<PAFamilyInfo> annotationFamilies)
        {
            var worksheet = package.Workbook.Worksheets.Add("Annotation Families");

            // Headers - match what the action service expects
            var headers = new[] { "Element ID", "Category", "Current Name", "Type Name", "Suggested Name", "Status", "Error Message" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            // Format headers
            using (var range = worksheet.Cells[1, 1, 1, headers.Length])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Data rows
            for (int i = 0; i < annotationFamilies.Count; i++)
            {
                var family = annotationFamilies[i];
                var row = i + 2;

                worksheet.Cells[row, 1].Value = family.ElementId;
                worksheet.Cells[row, 2].Value = family.Category;
                worksheet.Cells[row, 3].Value = family.CurrentName;
                worksheet.Cells[row, 4].Value = family.TypeName;
                worksheet.Cells[row, 5].Value = family.SuggestedName;
                worksheet.Cells[row, 6].Value = "Pending"; // Status
                worksheet.Cells[row, 7].Value = ""; // Error Message

                // Highlight rows where current name doesn't match suggested name
                if (!string.IsNullOrEmpty(family.SuggestedName) && 
                    !family.CurrentName.Equals(family.SuggestedName))
                {
                    using (var range = worksheet.Cells[row, 1, row, headers.Length])
                    {
                        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                    }
                }
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();

            // Add borders to all data
            using (var range = worksheet.Cells[1, 1, annotationFamilies.Count + 1, headers.Length])
            {
                range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }
        }

        /// <summary>
        /// Create model families worksheet
        /// </summary>
        private static void CreateModelFamiliesSheet(ExcelPackage package, List<PAFamilyInfo> modelFamilies)
        {
            var worksheet = package.Workbook.Worksheets.Add("Model Families");

            // Headers - match what the action service expects
            var headers = new[] { "Element ID", "Category", "Current Name", "Type Name", "Manufacturer", "Suggested Name", "Status", "Error Message" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            // Format headers
            using (var range = worksheet.Cells[1, 1, 1, headers.Length])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Data rows
            for (int i = 0; i < modelFamilies.Count; i++)
            {
                var family = modelFamilies[i];
                var row = i + 2;

                worksheet.Cells[row, 1].Value = family.ElementId;
                worksheet.Cells[row, 2].Value = family.Category;
                worksheet.Cells[row, 3].Value = family.CurrentName;
                worksheet.Cells[row, 4].Value = family.TypeName;
                worksheet.Cells[row, 5].Value = family.Manufacturer;
                worksheet.Cells[row, 6].Value = family.SuggestedName;
                worksheet.Cells[row, 7].Value = "Pending"; // Status
                worksheet.Cells[row, 8].Value = ""; // Error Message

                // Highlight rows where current name doesn't match suggested name
                if (!string.IsNullOrEmpty(family.SuggestedName) && 
                    !family.CurrentName.Equals(family.SuggestedName))
                {
                    using (var range = worksheet.Cells[row, 1, row, headers.Length])
                    {
                        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                    }
                }
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();

            // Add borders to all data
            using (var range = worksheet.Cells[1, 1, modelFamilies.Count + 1, headers.Length])
            {
                range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }
        }

        /// <summary>
        /// Create worksets worksheet
        /// </summary>
        private static void CreateWorksetsSheet(ExcelPackage package, List<PAWorksetInfo> worksets)
        {
            var worksheet = package.Workbook.Worksheets.Add("Worksets");

            // Headers
            var headers = new[] { "Workset ID", "Current Name", "Suggested PA Name" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            // Format headers
            using (var range = worksheet.Cells[1, 1, 1, headers.Length])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Data rows
            for (int i = 0; i < worksets.Count; i++)
            {
                var workset = worksets[i];
                var row = i + 2;

                worksheet.Cells[row, 1].Value = workset.WorksetId;
                worksheet.Cells[row, 2].Value = workset.CurrentName;
                worksheet.Cells[row, 3].Value = workset.SuggestedName;

                // Highlight rows where current name doesn't match suggested name
                if (!string.IsNullOrEmpty(workset.SuggestedName) && 
                    !workset.CurrentName.Equals(workset.SuggestedName))
                {
                    using (var range = worksheet.Cells[row, 1, row, headers.Length])
                    {
                        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                    }
                }
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();

            // Add borders to all data
            using (var range = worksheet.Cells[1, 1, worksets.Count + 1, headers.Length])
            {
                range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }
        }

        /// <summary>
        /// Create sheets worksheet
        /// </summary>
        private static void CreateSheetsSheet(ExcelPackage package, List<PASheetInfo> sheets)
        {
            var worksheet = package.Workbook.Worksheets.Add("Sheets");

            // Headers - match what the action service expects
            var headers = new[] { "Element ID", "Sheet Number", "Current Name", "Suggested Name", "Status", "Error Message" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            // Format headers
            using (var range = worksheet.Cells[1, 1, 1, headers.Length])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightSalmon);
                range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Data rows
            for (int i = 0; i < sheets.Count; i++)
            {
                var sheet = sheets[i];
                var row = i + 2;

                worksheet.Cells[row, 1].Value = sheet.ElementId;
                worksheet.Cells[row, 2].Value = sheet.SheetNumber;
                worksheet.Cells[row, 3].Value = sheet.CurrentName;
                worksheet.Cells[row, 4].Value = sheet.SuggestedName;
                worksheet.Cells[row, 5].Value = "Pending"; // Status
                worksheet.Cells[row, 6].Value = ""; // Error Message

                // Highlight rows where current name doesn't match suggested name
                if (!string.IsNullOrEmpty(sheet.SuggestedName) && 
                    !sheet.CurrentName.Equals(sheet.SuggestedName))
                {
                    using (var range = worksheet.Cells[row, 1, row, headers.Length])
                    {
                        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                    }
                }
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();

            // Add borders to all data
            using (var range = worksheet.Cells[1, 1, sheets.Count + 1, headers.Length])
            {
                range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }
        }

        /// <summary>
        /// Create model integrity worksheet
        /// </summary>
        private static void CreateModelIntegritySheet(ExcelPackage package, List<PAModelIntegrityIssue> issues)
        {
            var worksheet = package.Workbook.Worksheets.Add("Model Integrity");

            // Headers
            var headers = new[] { "Element ID", "Element Name", "Current Category", "Suggested Category" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            // Format headers
            using (var range = worksheet.Cells[1, 1, 1, headers.Length])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Orange);
                range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Data rows
            for (int i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                var row = i + 2;

                worksheet.Cells[row, 1].Value = issue.ElementId;
                worksheet.Cells[row, 2].Value = issue.ElementName;
                worksheet.Cells[row, 3].Value = issue.CurrentCategory;
                worksheet.Cells[row, 4].Value = issue.SuggestedCategory;

                // Highlight all rows as they represent issues
                using (var range = worksheet.Cells[row, 1, row, headers.Length])
                {
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.MistyRose);
                }
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();

            // Add borders to all data
            using (var range = worksheet.Cells[1, 1, issues.Count + 1, headers.Length])
            {
                range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }
        }

        /// <summary>
        /// Create summary worksheet
        /// </summary>
        private static void CreateSummarySheet(ExcelPackage package, PAComplianceReportData reportData)
        {
            var worksheet = package.Workbook.Worksheets.Add("Summary");

            // Title
            worksheet.Cells[1, 1].Value = "PA Compliance Report Summary";
            worksheet.Cells[1, 1].Style.Font.Size = 16;
            worksheet.Cells[1, 1].Style.Font.Bold = true;

            // Report generation date
            worksheet.Cells[2, 1].Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            worksheet.Cells[2, 1].Style.Font.Italic = true;

            // Summary statistics
            int row = 4;
            worksheet.Cells[row, 1].Value = "Category";
            worksheet.Cells[row, 2].Value = "Total Count";
            worksheet.Cells[row, 3].Value = "Needs Attention";
            worksheet.Cells[row, 4].Value = "Compliance %";

            // Format summary headers
            using (var range = worksheet.Cells[row, 1, row, 4])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            row++;

            // Annotation families summary
            if (reportData.AnnotationFamilies?.Any() == true)
            {
                var needsAttention = reportData.AnnotationFamilies.Count(f => 
                    !string.IsNullOrEmpty(f.SuggestedName) && 
                    !f.CurrentName.Equals(f.SuggestedName));
                var compliance = reportData.AnnotationFamilies.Count > 0 ? 
                    ((double)(reportData.AnnotationFamilies.Count - needsAttention) / reportData.AnnotationFamilies.Count * 100) : 100;

                worksheet.Cells[row, 1].Value = "Annotation Families";
                worksheet.Cells[row, 2].Value = reportData.AnnotationFamilies.Count;
                worksheet.Cells[row, 3].Value = needsAttention;
                worksheet.Cells[row, 4].Value = $"{compliance:F1}%";
                row++;
            }

            // Model families summary
            if (reportData.ModelFamilies?.Any() == true)
            {
                var needsAttention = reportData.ModelFamilies.Count(f => 
                    !string.IsNullOrEmpty(f.SuggestedName) && 
                    !f.CurrentName.Equals(f.SuggestedName));
                var compliance = reportData.ModelFamilies.Count > 0 ? 
                    ((double)(reportData.ModelFamilies.Count - needsAttention) / reportData.ModelFamilies.Count * 100) : 100;

                worksheet.Cells[row, 1].Value = "Model Families";
                worksheet.Cells[row, 2].Value = reportData.ModelFamilies.Count;
                worksheet.Cells[row, 3].Value = needsAttention;
                worksheet.Cells[row, 4].Value = $"{compliance:F1}%";
                row++;
            }

            // Worksets summary
            if (reportData.Worksets?.Any() == true)
            {
                var needsAttention = reportData.Worksets.Count(w => 
                    !string.IsNullOrEmpty(w.SuggestedName) && 
                    !w.CurrentName.Equals(w.SuggestedName));
                var compliance = reportData.Worksets.Count > 0 ? 
                    ((double)(reportData.Worksets.Count - needsAttention) / reportData.Worksets.Count * 100) : 100;

                worksheet.Cells[row, 1].Value = "Worksets";
                worksheet.Cells[row, 2].Value = reportData.Worksets.Count;
                worksheet.Cells[row, 3].Value = needsAttention;
                worksheet.Cells[row, 4].Value = $"{compliance:F1}%";
                row++;
            }

            // Sheets summary
            if (reportData.Sheets?.Any() == true)
            {
                var needsAttention = reportData.Sheets.Count(s => 
                    !string.IsNullOrEmpty(s.SuggestedName) && 
                    !s.CurrentName.Equals(s.SuggestedName));
                var compliance = reportData.Sheets.Count > 0 ? 
                    ((double)(reportData.Sheets.Count - needsAttention) / reportData.Sheets.Count * 100) : 100;

                worksheet.Cells[row, 1].Value = "Sheets";
                worksheet.Cells[row, 2].Value = reportData.Sheets.Count;
                worksheet.Cells[row, 3].Value = needsAttention;
                worksheet.Cells[row, 4].Value = $"{compliance:F1}%";
                row++;
            }

            // Model integrity summary
            if (reportData.ModelIntegrityIssues?.Any() == true)
            {
                worksheet.Cells[row, 1].Value = "Model Integrity Issues";
                worksheet.Cells[row, 2].Value = reportData.ModelIntegrityIssues.Count;
                worksheet.Cells[row, 3].Value = reportData.ModelIntegrityIssues.Count;
                worksheet.Cells[row, 4].Value = "0.0%";
                row++;
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();

            // Add borders to summary data
            if (row > 5)
            {
                using (var range = worksheet.Cells[4, 1, row - 1, 4])
                {
                    range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }
            }

            // Move summary sheet to the beginning
            package.Workbook.Worksheets.MoveToStart(worksheet.Index);
        }
    }
}