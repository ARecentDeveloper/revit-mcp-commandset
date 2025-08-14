using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Class for basic information of spatial positioning elements (levels, grids, etc.)
    /// </summary>
    public class PositioningElementInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Element Unique ID
        /// </summary>
        public string UniqueId { get; set; }
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
        /// Element Class Name
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Elevation value (applicable to levels, in mm)
        /// </summary>
        public double? Elevation { get; set; }
        /// <summary>
        /// Associated Level
        /// </summary>
        public LevelInfo Level { get; set; }
        /// <summary>
        /// BoundingBoxInfo
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Grid line (for grids)
        /// </summary>
        public JZLine GridLine { get; set; }
    }
} 