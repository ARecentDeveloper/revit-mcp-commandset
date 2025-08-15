using System.Collections.Generic;
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Custom class for storing complete element information
    /// </summary>
    public class ElementInstanceInfo
    {
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Id
        /// </summary>
        [JsonProperty("uniqueId", NullValueHandling = NullValueHandling.Ignore)]
        public string UniqueId { get; set; }
        /// <summary>
        /// Type Id
        /// </summary>
        public int TypeId { get; set; }
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
        /// Containing Room Id
        /// </summary>
        public int RoomId { get; set; }
        /// <summary>
        /// Containing Level Name
        /// </summary>
        [JsonProperty("level", NullValueHandling = NullValueHandling.Ignore)]
        public LevelInfo Level { get; set; }
        /// <summary>
        /// BoundingBoxInfo
        /// </summary>
        [JsonProperty("boundingBox", NullValueHandling = NullValueHandling.Ignore)]
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Instance Parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
    }
} 