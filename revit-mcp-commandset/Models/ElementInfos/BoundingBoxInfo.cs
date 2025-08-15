using RevitMCPCommandSet.Models.Common;
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Custom class for storing bounding box information
    /// </summary>
    public class BoundingBoxInfo
    {
        [JsonProperty("min", NullValueHandling = NullValueHandling.Ignore)]
        public JZPoint Min { get; set; }
        [JsonProperty("max", NullValueHandling = NullValueHandling.Ignore)]
        public JZPoint Max { get; set; }
    }
} 