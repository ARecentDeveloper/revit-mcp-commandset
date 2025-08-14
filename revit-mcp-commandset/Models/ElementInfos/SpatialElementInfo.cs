namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Class for storing basic information of spatial elements (rooms, areas, etc.)
    /// </summary>
    public class SpatialElementInfo
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
        /// Number
        /// </summary>
        public string Number { get; set; }
        /// <summary>
        /// Category Name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Optional built-in category
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// .NET class name of the element
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Area (in mm²)
        /// </summary>
        public double? Area { get; set; }
        /// <summary>
        /// Volume (in mm³)
        /// </summary>
        public double? Volume { get; set; }
        /// <summary>
        /// Perimeter (in mm)
        /// </summary>
        public double? Perimeter { get; set; }
        /// <summary>
        /// Containing Level
        /// </summary>
        public LevelInfo Level { get; set; }

        /// <summary>
        /// BoundingBoxInfo
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }
} 