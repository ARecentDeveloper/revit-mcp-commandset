using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.PACompliance
{
    /// <summary>
    /// Base interface for PA compliance operations
    /// </summary>
    public interface IPAComplianceOperation
    {
        string OperationType { get; }
        bool IsValid { get; }
        string ValidationMessage { get; }
    }

    /// <summary>
    /// Interface for compliance report operations
    /// </summary>
    public interface IPAComplianceReport : IPAComplianceOperation
    {
        string OutputPath { get; set; }
        PAComplianceStep Step { get; set; }
        List<PAComplianceArea> IncludedAreas { get; set; }
        PAComplianceReportResult GenerateReport();
    }

    /// <summary>
    /// Interface for compliance action operations
    /// </summary>
    public interface IPAComplianceAction : IPAComplianceOperation
    {
        string ExcelFilePath { get; set; }
        PAComplianceStep Step { get; set; }
        bool DryRun { get; set; }
        bool BackupProject { get; set; }
        PAComplianceActionResult ExecuteActions();
    }

    /// <summary>
    /// PA compliance execution steps
    /// </summary>
    public enum PAComplianceStep
    {
        All,
        Families,
        Worksets,
        Sheets,
        ModelIntegrity
    }

    /// <summary>
    /// PA compliance areas
    /// </summary>
    public enum PAComplianceArea
    {
        AnnotationFamilies,
        ModelFamilies,
        Worksets,
        Sheets,
        ModelIntegrity
    }

    /// <summary>
    /// PA compliance report parameters
    /// </summary>
    public class PAComplianceReportParams
    {
        [JsonProperty("outputPath")]
        public string OutputPath { get; set; }

        [JsonProperty("step")]
        public string Step { get; set; } = "all";

        [JsonProperty("includeAnnotationFamilies")]
        public bool IncludeAnnotationFamilies { get; set; } = true;

        [JsonProperty("includeModelFamilies")]
        public bool IncludeModelFamilies { get; set; } = true;

        [JsonProperty("includeWorksets")]
        public bool IncludeWorksets { get; set; } = true;

        [JsonProperty("includeSheets")]
        public bool IncludeSheets { get; set; } = true;

        [JsonProperty("includeModelIntegrity")]
        public bool IncludeModelIntegrity { get; set; } = true;
    }

    /// <summary>
    /// PA compliance action parameters
    /// </summary>
    public class PAComplianceActionParams
    {
        [JsonProperty("excelFilePath")]
        public string ExcelFilePath { get; set; }

        [JsonProperty("step")]
        public string Step { get; set; } = "all";

        [JsonProperty("dryRun")]
        public bool DryRun { get; set; } = false;

        [JsonProperty("backupProject")]
        public bool BackupProject { get; set; } = true;
    }

    /// <summary>
    /// Result of PA compliance report generation
    /// </summary>
    public class PAComplianceReportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string OutputFilePath { get; set; }
        public int TotalIssuesFound { get; set; }
        public Dictionary<PAComplianceArea, int> IssuesByArea { get; set; } = new Dictionary<PAComplianceArea, int>();
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan ExecutionTime { get; set; }
    }

    /// <summary>
    /// Result of PA compliance action execution
    /// </summary>
    public class PAComplianceActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int TotalCorrectionsAttempted { get; set; }
        public int TotalCorrectionsSuccessful { get; set; }
        public int TotalCorrectionsFailed { get; set; }
        public Dictionary<PAComplianceArea, PAComplianceAreaResult> ResultsByArea { get; set; } = new Dictionary<PAComplianceArea, PAComplianceAreaResult>();
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan ExecutionTime { get; set; }
        public bool WasDryRun { get; set; }
    }

    /// <summary>
    /// Result for a specific compliance area
    /// </summary>
    public class PAComplianceAreaResult
    {
        public PAComplianceArea Area { get; set; }
        public int CorrectionsAttempted { get; set; }
        public int CorrectionsSuccessful { get; set; }
        public int CorrectionsFailed { get; set; }
        public List<PAComplianceItemResult> ItemResults { get; set; } = new List<PAComplianceItemResult>();
    }

    /// <summary>
    /// Result for a specific compliance item
    /// </summary>
    public class PAComplianceItemResult
    {
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public string CurrentValue { get; set; }
        public string TargetValue { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public PAComplianceOperationType OperationType { get; set; }
    }

    /// <summary>
    /// Types of compliance operations
    /// </summary>
    public enum PAComplianceOperationType
    {
        Rename,
        Recategorize,
        Skip
    }
}