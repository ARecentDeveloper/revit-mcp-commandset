using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Custom class for storing bounding box information
    /// </summary>
    public class BoundingBoxInfo
    {
        public JZPoint Min { get; set; }
        public JZPoint Max { get; set; }
    }
} 