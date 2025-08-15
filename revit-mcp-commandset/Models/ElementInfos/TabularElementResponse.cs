using System.Collections.Generic;
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Tabular response format for efficient batch element queries
    /// Groups elements by parameter values to eliminate redundancy and reduce token usage
    /// </summary>
    public class TabularElementResponse
    {
        /// <summary>
        /// Array of all element IDs in the response
        /// </summary>
        public List<int> Elements { get; set; } = new List<int>();

        /// <summary>
        /// Properties that are common to all elements (e.g., same family name, category)
        /// Only populated when all elements share the same value
        /// </summary>
        [JsonProperty("commonProperties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> CommonProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Individual element properties that vary between elements
        /// Key: Property name (e.g., "name", "familyName"), Value: Dictionary of ElementId -> Property Value
        /// Only populated when elements have different values for these properties
        /// Example: {"name": {"1001": "W8x10", "1002": "W18x35"}}
        /// </summary>
        [JsonProperty("elementProperties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, Dictionary<int, object>> ElementProperties { get; set; } = new Dictionary<string, Dictionary<int, object>>();

        /// <summary>
        /// Parameters organized by value groups
        /// Structure: {ParameterName: {Value: [ElementIds]}}
        /// Example: {"Phase Created": {"Existing": [1001, 1002], "New Construction": [1003]}}
        /// </summary>
        public Dictionary<string, TabularParameterGroup> Parameters { get; set; } = new Dictionary<string, TabularParameterGroup>();
    }

    /// <summary>
    /// Represents a parameter's values grouped by element IDs
    /// </summary>
    public class TabularParameterGroup
    {
        /// <summary>
        /// Groups elements by their parameter values
        /// Key: Parameter value (display string or raw value as string)
        /// Value: List of element IDs that have this value
        /// </summary>
        public Dictionary<string, List<int>> Values { get; set; } = new Dictionary<string, List<int>>();

        /// <summary>
        /// Raw numeric values for elements that have them
        /// Key: Element ID, Value: Raw numeric value
        /// Only populated for numeric parameters
        /// </summary>
        [JsonProperty("rawValues", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<int, double> RawValues { get; set; } = new Dictionary<int, double>();

        /// <summary>
        /// Empty reasons for elements that have no value
        /// Key: Element ID, Value: Reason why parameter is empty
        /// </summary>
        [JsonProperty("emptyReasons", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<int, string> EmptyReasons { get; set; } = new Dictionary<int, string>();
    }
}