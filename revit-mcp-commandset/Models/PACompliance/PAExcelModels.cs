using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace RevitMCPCommandSet.Models.PACompliance
{
    /// <summary>
    /// Excel sheet definitions for PA compliance reports
    /// </summary>
    public static class PAExcelSheets
    {
        public const string ANNOTATION_FAMILIES = "Annotation Families";
        public const string MODEL_FAMILIES = "Model Families";
        public const string WORKSETS = "Worksets";
        public const string SHEETS = "Sheets";
        public const string MODEL_INTEGRITY = "Model Integrity";
    }

    /// <summary>
    /// Base class for Excel row data
    /// </summary>
    public abstract class PAExcelRowBase
    {
        public string Status { get; set; } = "Pending";
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicates if this row should be processed
        /// </summary>
        public virtual bool ShouldProcess
        {
            get
            {
                return !string.IsNullOrWhiteSpace(SuggestedName) && 
                       !SuggestedName.Equals(CurrentName);
            }
        }
        
        /// <summary>
        /// Gets the target name for renaming (the suggested name, which user can modify)
        /// </summary>
        public virtual string GetTargetName()
        {
            return SuggestedName;
        }
        
        public abstract string CurrentName { get; }
        public abstract string SuggestedName { get; }
    }

    /// <summary>
    /// Data model for annotation family Excel rows
    /// </summary>
    public class PAAnnotationFamilyRow : PAExcelRowBase
    {
        [DisplayName("Category")]
        public string Category { get; set; }

        [DisplayName("Current Name")]
        public string CurrentNameValue { get; set; }

        [DisplayName("Suggested Name")]
        public string SuggestedNameValue { get; set; }

        [DisplayName("Status")]
        public new string Status { get; set; } = "Pending";

        [DisplayName("Error Message")]
        public new string ErrorMessage { get; set; } = string.Empty;

        // Additional properties for processing
        public string ElementId { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }

        public override string CurrentName => CurrentNameValue;
        public override string SuggestedName => SuggestedNameValue;
    }

    /// <summary>
    /// Data model for model family Excel rows
    /// </summary>
    public class PAModelFamilyRow : PAExcelRowBase
    {
        [DisplayName("Category")]
        public string Category { get; set; }

        [DisplayName("Manufacturer")]
        public string Manufacturer { get; set; }

        [DisplayName("Current Name")]
        public string CurrentNameValue { get; set; }

        [DisplayName("Suggested Name")]
        public string SuggestedNameValue { get; set; }

        [DisplayName("Status")]
        public new string Status { get; set; } = "Pending";

        [DisplayName("Error Message")]
        public new string ErrorMessage { get; set; } = string.Empty;

        // Additional properties for processing
        public string ElementId { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }

        public override string CurrentName => CurrentNameValue;
        public override string SuggestedName => SuggestedNameValue;
    }

    /// <summary>
    /// Data model for workset Excel rows
    /// </summary>
    public class PAWorksetRow : PAExcelRowBase
    {
        [DisplayName("Current Name")]
        public string CurrentNameValue { get; set; }

        [DisplayName("Suggested Name")]
        public string SuggestedNameValue { get; set; }

        [DisplayName("Status")]
        public new string Status { get; set; } = "Pending";

        [DisplayName("Error Message")]
        public new string ErrorMessage { get; set; } = string.Empty;

        // Additional properties for processing
        public int WorksetId { get; set; }
        public bool IsUserCreated { get; set; }

        public override string CurrentName => CurrentNameValue;
        public override string SuggestedName => SuggestedNameValue;
    }

    /// <summary>
    /// Data model for sheet Excel rows
    /// </summary>
    public class PASheetRow : PAExcelRowBase
    {
        [DisplayName("Sheet Number")]
        public string SheetNumber { get; set; }

        [DisplayName("Current Name")]
        public string CurrentNameValue { get; set; }

        [DisplayName("Suggested Name")]
        public string SuggestedNameValue { get; set; }

        [DisplayName("Status")]
        public new string Status { get; set; } = "Pending";

        [DisplayName("Error Message")]
        public new string ErrorMessage { get; set; } = string.Empty;

        // Additional properties for processing
        public string ElementId { get; set; }
        public string SheetId { get; set; }

        public override string CurrentName => CurrentNameValue;
        public override string SuggestedName => SuggestedNameValue;
    }

    /// <summary>
    /// Data model for model integrity Excel rows
    /// </summary>
    public class PAModelIntegrityRow : PAExcelRowBase
    {
        [DisplayName("Element ID")]
        public string ElementId { get; set; }

        [DisplayName("Element Type")]
        public string ElementType { get; set; }

        [DisplayName("Current Category")]
        public string CurrentCategory { get; set; }

        [DisplayName("Suggested Category")]
        public string SuggestedCategory { get; set; }

        [DisplayName("Issue Description")]
        public string IssueDescription { get; set; }

        [DisplayName("Status")]
        public new string Status { get; set; } = "Pending";

        [DisplayName("Error Message")]
        public new string ErrorMessage { get; set; } = string.Empty;

        // Additional properties for processing
        public string FamilyName { get; set; }
        public string TypeName { get; set; }

        public override string CurrentName => CurrentCategory;
        public override string SuggestedName => SuggestedCategory;
    }

    /// <summary>
    /// Excel column definitions for each sheet type
    /// </summary>
    public static class PAExcelColumns
    {
        public static class AnnotationFamilies
        {
            public const string CATEGORY = "Category";
            public const string CURRENT_NAME = "Current Name";
            public const string SUGGESTED_NAME = "Suggested Name";
            public const string STATUS = "Status";
            public const string ERROR_MESSAGE = "Error Message";

            public static readonly string[] AllColumns = {
                CATEGORY, CURRENT_NAME, SUGGESTED_NAME, STATUS, ERROR_MESSAGE
            };
        }

        public static class ModelFamilies
        {
            public const string CATEGORY = "Category";
            public const string MANUFACTURER = "Manufacturer";
            public const string CURRENT_NAME = "Current Name";
            public const string SUGGESTED_NAME = "Suggested Name";
            public const string STATUS = "Status";
            public const string ERROR_MESSAGE = "Error Message";

            public static readonly string[] AllColumns = {
                CATEGORY, MANUFACTURER, CURRENT_NAME, SUGGESTED_NAME, STATUS, ERROR_MESSAGE
            };
        }

        public static class Worksets
        {
            public const string CURRENT_NAME = "Current Name";
            public const string SUGGESTED_NAME = "Suggested Name";
            public const string STATUS = "Status";
            public const string ERROR_MESSAGE = "Error Message";

            public static readonly string[] AllColumns = {
                CURRENT_NAME, SUGGESTED_NAME, STATUS, ERROR_MESSAGE
            };
        }

        public static class Sheets
        {
            public const string ELEMENT_ID = "Element ID";
            public const string SHEET_NUMBER = "Sheet Number";
            public const string CURRENT_NAME = "Current Name";
            public const string SUGGESTED_NAME = "Suggested Name";
            public const string STATUS = "Status";
            public const string ERROR_MESSAGE = "Error Message";

            public static readonly string[] AllColumns = {
                ELEMENT_ID, SHEET_NUMBER, CURRENT_NAME, SUGGESTED_NAME, STATUS, ERROR_MESSAGE
            };
        }

        public static class ModelIntegrity
        {
            public const string ELEMENT_ID = "Element ID";
            public const string ELEMENT_TYPE = "Element Type";
            public const string CURRENT_CATEGORY = "Current Category";
            public const string SUGGESTED_CATEGORY = "Suggested Category";
            public const string ISSUE_DESCRIPTION = "Issue Description";
            public const string STATUS = "Status";
            public const string ERROR_MESSAGE = "Error Message";

            public static readonly string[] AllColumns = {
                ELEMENT_ID, ELEMENT_TYPE, CURRENT_CATEGORY, SUGGESTED_CATEGORY, 
                ISSUE_DESCRIPTION, STATUS, ERROR_MESSAGE
            };
        }
    }

    /// <summary>
    /// Excel workbook structure definition
    /// </summary>
    public class PAExcelWorkbookStructure
    {
        public Dictionary<string, string[]> SheetColumns { get; private set; }

        public PAExcelWorkbookStructure()
        {
            SheetColumns = new Dictionary<string, string[]>
            {
                { PAExcelSheets.ANNOTATION_FAMILIES, PAExcelColumns.AnnotationFamilies.AllColumns },
                { PAExcelSheets.MODEL_FAMILIES, PAExcelColumns.ModelFamilies.AllColumns },
                { PAExcelSheets.WORKSETS, PAExcelColumns.Worksets.AllColumns },
                { PAExcelSheets.SHEETS, PAExcelColumns.Sheets.AllColumns },
                { PAExcelSheets.MODEL_INTEGRITY, PAExcelColumns.ModelIntegrity.AllColumns }
            };
        }

        /// <summary>
        /// Gets the column names for a specific sheet
        /// </summary>
        /// <param name="sheetName">Name of the sheet</param>
        /// <returns>Array of column names</returns>
        public string[] GetColumnsForSheet(string sheetName)
        {
            return SheetColumns.ContainsKey(sheetName) ? SheetColumns[sheetName] : new string[0];
        }

        /// <summary>
        /// Gets all sheet names in the workbook
        /// </summary>
        /// <returns>List of sheet names</returns>
        public List<string> GetAllSheetNames()
        {
            return new List<string>(SheetColumns.Keys);
        }

        /// <summary>
        /// Validates if a sheet structure is correct
        /// </summary>
        /// <param name="sheetName">Name of the sheet</param>
        /// <param name="actualColumns">Actual columns found in the sheet</param>
        /// <returns>True if structure is valid</returns>
        public bool ValidateSheetStructure(string sheetName, string[] actualColumns)
        {
            if (!SheetColumns.ContainsKey(sheetName))
                return false;

            var expectedColumns = SheetColumns[sheetName];
            
            if (actualColumns.Length != expectedColumns.Length)
                return false;

            for (int i = 0; i < expectedColumns.Length; i++)
            {
                if (!expectedColumns[i].Equals(actualColumns[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Status values for Excel processing
    /// </summary>
    public static class PAExcelStatus
    {
        public const string PENDING = "Pending";
        public const string SUCCESS = "Success";
        public const string FAILED = "Failed";
        public const string SKIPPED = "Skipped";
        public const string NOT_FOUND = "Not Found";
        public const string PERMISSION_DENIED = "Permission Denied";
        public const string INVALID_DATA = "Invalid Data";
    }
}