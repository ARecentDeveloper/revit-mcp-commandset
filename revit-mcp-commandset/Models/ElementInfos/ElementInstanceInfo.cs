using System.Collections.Generic;

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
        public string UniqueId { get; set; }
        /// <summary>
        /// Type Id
        /// </summary>
        public int TypeId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family Name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category Name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in Category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Containing Room Id
        /// </summary>
        public int RoomId { get; set; }
        /// <summary>
        /// Containing Level Name
        /// </summary>
        public LevelInfo Level { get; set; }
        /// <summary>
        /// BoundingBoxInfo
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Instance Parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
    }
} 