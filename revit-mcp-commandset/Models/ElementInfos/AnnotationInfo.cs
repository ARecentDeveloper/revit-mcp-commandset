using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Class for storing basic information of annotation elements
    /// </summary>
    public class AnnotationInfo
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
        /// .NET class name of the element
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Owning View
        /// </summary>
        public string OwnerView { get; set; }
        /// <summary>
        /// Text content (for text notes)
        /// </summary>
        public string TextContent { get; set; }
        /// <summary>
        /// Position information (in mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// BoundingBoxInfo
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Dimension value (for dimensions)
        /// </summary>
        public string DimensionValue { get; set; }
    }
} 