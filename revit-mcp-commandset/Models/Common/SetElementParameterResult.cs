using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// Result model for set_element_parameter command with proper JSON serialization
    /// </summary>
    public class SetElementParameterResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
        
        [JsonProperty("message", NullValueHandling = NullValueHandling.Include)]
        public string Message { get; set; }
        
        [JsonProperty("successCount", NullValueHandling = NullValueHandling.Ignore)]
        public int SuccessCount { get; set; }
        
        [JsonProperty("failureCount", NullValueHandling = NullValueHandling.Ignore)]
        public int FailureCount { get; set; }
        
        [JsonProperty("failures", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Failures { get; set; } = new List<string>();
        
        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }
    }
}