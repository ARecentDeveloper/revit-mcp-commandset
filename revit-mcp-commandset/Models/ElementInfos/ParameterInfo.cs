using System;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Enhanced class for storing complete parameter information with unit handling and empty value detection
    /// </summary>
    public class ParameterInfo
    {
        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// For string parameters: the string value (null if empty)
        /// For numeric parameters: null (use RawValue instead)
        /// </summary>
        public string Value { get; set; }
        
        /// <summary>
        /// Raw numeric value in Revit internal units (null if empty)
        /// For dimensional parameters: feet, square feet, cubic feet, radians
        /// For counts/integers: as-is
        /// Always use this for calculations and conversions
        /// </summary>
        public double? RawValue { get; set; }
        
        /// <summary>
        /// Human-readable display value as shown in Revit UI
        /// For reference parameters (phases, materials, types): shows the name
        /// For formatted parameters: shows formatted string with units
        /// For text parameters: same as Value
        /// </summary>
        public string AsValueString { get; set; }
        
        /// <summary>
        /// Reason why parameter is empty (only present when parameter is empty)
        /// Examples: "No value set", "Empty string", "Parameter not found"
        /// </summary>
        public string EmptyReason { get; set; }
    }
} 