using System.Collections.Generic;
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Enhanced class for storing basic element information
    /// </summary>
    public class ElementBasicInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Element Unique ID
        /// </summary>
        [JsonProperty("uniqueId", NullValueHandling = NullValueHandling.Ignore)]
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }
        /// <summary>
        /// Family Name
        /// </summary>
        [JsonProperty("familyName", NullValueHandling = NullValueHandling.Ignore)]
        public string FamilyName { get; set; }
        /// <summary>
        /// Category Name
        /// </summary>
        [JsonProperty("category", NullValueHandling = NullValueHandling.Ignore)]
        public string Category { get; set; }
        /// <summary>
        /// Built-in Category (optional)
        /// </summary>
        [JsonProperty("builtInCategory", NullValueHandling = NullValueHandling.Ignore)]
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// BoundingBoxInfo
        /// </summary>
        [JsonProperty("boundingBox", NullValueHandling = NullValueHandling.Ignore)]
        public BoundingBoxInfo BoundingBox { get; set; }

        /// <summary>
        /// Parameter information list
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
    }
} 