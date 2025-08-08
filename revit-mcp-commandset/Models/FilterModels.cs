using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models
{
    /// <summary>
    /// Main filter criteria for element filtering operations
    /// </summary>
    public class FilterCriteria
    {
        [JsonProperty("scope")]
        public FilterScope Scope { get; set; } = FilterScope.All;

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("parameter")]
        public ParameterFilter Parameter { get; set; }

        [JsonProperty("elementIds")]
        public List<long> ElementIds { get; set; }
    }

    /// <summary>
    /// Filter scope options
    /// </summary>
    public enum FilterScope
    {
        All,
        Category,
        Parameter,
        Selected
    }

    /// <summary>
    /// Parameter-based filtering criteria
    /// </summary>
    public class ParameterFilter
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("operator")]
        public string Operator { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("valueType")]
        public ParameterValueType ValueType { get; set; } = ParameterValueType.Double;
    }

    /// <summary>
    /// Parameter value types for proper parsing
    /// </summary>
    public enum ParameterValueType
    {
        Double,
        Integer,
        String,
        Boolean
    }

    /// <summary>
    /// Result of filtering operation
    /// </summary>
    public class FilterResult
    {
        public List<ElementId> Elements { get; set; } = new List<ElementId>();
        public int ProcessedCount { get; set; }
        public int ErrorCount { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; } = true;
    }

    /// <summary>
    /// Category mapping information
    /// </summary>
    public class CategoryMapping
    {
        public BuiltInCategory BuiltInCategory { get; set; }
        public string DisplayName { get; set; }
        public List<string> Aliases { get; set; } = new List<string>();
    }
}