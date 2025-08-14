namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Class for storing basic information of view elements
    /// </summary>
    public class ViewInfo
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
        /// View Type
        /// </summary>
        public string ViewType { get; set; }

        /// <summary>
        /// View Scale
        /// </summary>
        public int? Scale { get; set; }

        /// <summary>
        /// Is Template View
        /// </summary>
        public bool IsTemplate { get; set; }

        /// <summary>
        /// Detail Level
        /// </summary>
        public string DetailLevel { get; set; }

        /// <summary>
        /// Associated Level
        /// </summary>
        public LevelInfo AssociatedLevel { get; set; }

        /// <summary>
        /// 位置信息
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }

        /// <summary>
        /// Is View Open
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// Is Currently Active View
        /// </summary>
        public bool IsActive { get; set; }
    }
} 