using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Minimal element information for token-efficient responses
    /// Only includes essential identification and requested parameters
    /// </summary>
    public class ElementMinimalInfo
    {
        /// <summary>
        /// Element ID - essential for identification and further operations
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Element name - essential for user recognition
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Parameter information list - only populated with specifically requested parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();
    }
}