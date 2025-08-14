using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Custom class for storing complete element type information
    /// </summary>
    public class ElementTypeInfo
    {
        /// <summary>
        /// ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Id
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
        /// Type Parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
    }
} 