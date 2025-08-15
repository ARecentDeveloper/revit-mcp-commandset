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
            { BuiltInCategory.OST_Conduit, new ConduitParameterMapping() },
            { BuiltInCategory.OST_StructuralFraming, new StructuralFramingParameterMapping() },
            { BuiltInCategory.OST_Levels, new LevelParameterMapping() }
        };

        /// <summary>
        /// Get parameter from element using category-specific mapping
        /// </summary>
        public static Parameter GetParameter(Element element, string parameterName, BuiltInCategory category)
        {
            System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Looking for '{parameterName}' on element {element.Id.IntegerValue} in category {category}");
            
            // Try category-specific mapping first
            if (Mappings.TryGetValue(category, out var mapping))
            {
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Found mapping for {category}");
                var param = mapping.GetParameter(element, parameterName);
                if (param != null) 
                {
                    System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Found parameter '{parameterName}' via mapping");
                    return param;
                }
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Parameter '{parameterName}' not found via mapping");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: No mapping found for category {category}");
            }

            // Fallback to generic parameter lookup
            System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Trying generic lookup for '{parameterName}'");
            var genericParam = GetParameterGeneric(element, parameterName);
            if (genericParam != null)
            {
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Found parameter '{parameterName}' via generic lookup");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Parameter '{parameterName}' not found anywhere");
            }
            return genericParam;
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
        /// Get the parameter mapping for a specific category
        /// </summary>
        public static ParameterMappingBase GetMapping(BuiltInCategory category)
        {
            Mappings.TryGetValue(category, out var mapping);
            return mapping; // Returns null if not found
        }

        /// <summary>
        /// Add new parameter mapping
        /// </summary>
        public static void RegisterMapping(ParameterMappingBase mapping)
        {
            Mappings[mapping.Category] = mapping;
        }

        /// <summary>
        /// Get available categories as string list (for error messages)
        /// </summary>
        public static List<string> GetAvailableCategories()
        {
            return Mappings.Keys.Select(k => k.ToString()).ToList();
        }
    }
}