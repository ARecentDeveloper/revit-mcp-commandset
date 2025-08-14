using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Class for storing basic information of groups and links
    /// </summary>
    public class GroupOrLinkInfo
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
        /// Number of group members
        /// </summary>
        public int? MemberCount { get; set; }
        /// <summary>
        /// Group type
        /// </summary>
        public string GroupType { get; set; }
        /// <summary>
        /// Link status
        /// </summary>
        public string LinkStatus { get; set; }
        /// <summary>
        /// Link path
        /// </summary>
        public string LinkPath { get; set; }
        /// <summary>
        /// Position information (in mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// BoundingBoxInfo
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }
} 