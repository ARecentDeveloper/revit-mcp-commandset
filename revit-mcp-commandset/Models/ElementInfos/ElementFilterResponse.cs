using System.Collections.Generic;
using Newtonsoft.Json;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Unified response wrapper for element filter operations
    /// Supports both standard and tabular response formats
    /// </summary>
    public class ElementFilterResponse
    {
        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Human-readable message about the operation result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Response format used: "standard" or "tabular"
        /// </summary>
        public string ResponseFormat { get; set; }

        /// <summary>
        /// Standard response format - list of individual elements
        /// Only populated when ResponseFormat = "standard"
        /// </summary>
        [JsonProperty("response", NullValueHandling = NullValueHandling.Ignore)]
        public List<ElementMinimalInfo> Response { get; set; }

        /// <summary>
        /// Tabular response format - optimized grouping by parameter values
        /// Only populated when ResponseFormat = "tabular"
        /// </summary>
        [JsonProperty("tabularResponse", NullValueHandling = NullValueHandling.Ignore)]
        public TabularElementResponse TabularResponse { get; set; }

        /// <summary>
        /// Create a standard format response
        /// </summary>
        public static ElementFilterResponse CreateStandard(List<ElementMinimalInfo> elements, string message = null)
        {
            return new ElementFilterResponse
            {
                Success = true,
                ResponseFormat = "standard",
                Message = message ?? $"Successfully obtained {elements?.Count ?? 0} element information. The detailed information is stored in the Response property",
                Response = elements ?? new List<ElementMinimalInfo>()
            };
        }

        /// <summary>
        /// Create a tabular format response
        /// </summary>
        public static ElementFilterResponse CreateTabular(List<ElementMinimalInfo> elements, string message = null)
        {
            var tabularResponse = TabularResponseConverter.ConvertToTabular(elements);
            
            return new ElementFilterResponse
            {
                Success = true,
                ResponseFormat = "tabular",
                Message = message ?? $"Successfully obtained {elements?.Count ?? 0} element information in optimized tabular format. Elements grouped by parameter values for efficient processing.",
                TabularResponse = tabularResponse
            };
        }

        /// <summary>
        /// Create an error response
        /// </summary>
        public static ElementFilterResponse CreateError(string errorMessage)
        {
            return new ElementFilterResponse
            {
                Success = false,
                Message = errorMessage,
                ResponseFormat = "error"
            };
        }
    }
}