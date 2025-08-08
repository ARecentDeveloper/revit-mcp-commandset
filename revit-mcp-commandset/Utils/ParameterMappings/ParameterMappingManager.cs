using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Manages parameter mappings for different categories
    /// </summary>
    public static class ParameterMappingManager
    {
        private static readonly Dictionary<BuiltInCategory, ParameterMappingBase> Mappings = 
            new Dictionary<BuiltInCategory, ParameterMappingBase>
        {
            // Start with basic working mappings
            { BuiltInCategory.OST_Walls, new WallParameterMapping() },
            { BuiltInCategory.OST_Conduit, new ConduitParameterMapping() }
        };

        /// <summary>
        /// Get parameter from element using category-specific mapping
        /// </summary>
        public static Parameter GetParameter(Element element, string parameterName, BuiltInCategory category)
        {
            // Try category-specific mapping first
            if (Mappings.TryGetValue(category, out var mapping))
            {
                var param = mapping.GetParameter(element, parameterName);
                if (param != null) return param;
            }

            // Fallback to generic parameter lookup
            return GetParameterGeneric(element, parameterName);
        }

        /// <summary>
        /// Convert value using category-specific knowledge
        /// </summary>
        public static object ConvertValue(string parameterName, object inputValue, BuiltInCategory category)
        {
            if (Mappings.TryGetValue(category, out var mapping))
            {
                return mapping.ConvertValue(parameterName, inputValue);
            }

            return inputValue; // No conversion
        }

        /// <summary>
        /// Get common parameter names for a category
        /// </summary>
        public static List<string> GetCommonParameterNames(BuiltInCategory category)
        {
            if (Mappings.TryGetValue(category, out var mapping))
            {
                return mapping.GetCommonParameterNames();
            }

            return new List<string>(); // No suggestions
        }

        /// <summary>
        /// Get parameter aliases for a category
        /// </summary>
        public static Dictionary<string, string> GetParameterAliases(BuiltInCategory category)
        {
            if (Mappings.TryGetValue(category, out var mapping))
            {
                return mapping.GetParameterAliases();
            }

            return new Dictionary<string, string>(); // No aliases
        }

        /// <summary>
        /// Check if category has specific parameter mapping
        /// </summary>
        public static bool HasMapping(BuiltInCategory category)
        {
            return Mappings.ContainsKey(category);
        }

        /// <summary>
        /// Get all supported categories with mappings
        /// </summary>
        public static List<BuiltInCategory> GetSupportedCategories()
        {
            return Mappings.Keys.ToList();
        }

        /// <summary>
        /// Generic parameter lookup (fallback)
        /// </summary>
        private static Parameter GetParameterGeneric(Element element, string parameterName)
        {
            // Try instance parameter
            var param = element.LookupParameter(parameterName);
            if (param != null) return param;

            // Try type parameter
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
            return elementType?.LookupParameter(parameterName);
        }

        /// <summary>
        /// Add new parameter mapping
        /// </summary>
        public static void RegisterMapping(ParameterMappingBase mapping)
        {
            Mappings[mapping.Category] = mapping;
        }
    }
}