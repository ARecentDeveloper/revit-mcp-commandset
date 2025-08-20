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
            { BuiltInCategory.OST_Levels, new LevelParameterMapping() },
            { BuiltInCategory.OST_Floors, new FloorParameterMapping() },
            { BuiltInCategory.OST_StructuralColumns, new StructuralColumnParameterMapping() },
            { BuiltInCategory.OST_StructuralFoundation, new StructuralFoundationParameterMapping() },
            { BuiltInCategory.OST_Windows, new WindowParameterMapping() },
            { BuiltInCategory.OST_Doors, new DoorParameterMapping() },
            { BuiltInCategory.OST_Ceilings, new CeilingParameterMapping() },
            { BuiltInCategory.OST_Roofs, new RoofParameterMapping() },
            { BuiltInCategory.OST_Grids, new GridParameterMapping() },
            { BuiltInCategory.OST_VolumeOfInterest, new ScopeBoxParameterMapping() }
        };

        /// <summary>
        /// Get parameter from element using category-specific mapping
        /// </summary>
        public static Parameter GetParameter(Element element, string parameterName, BuiltInCategory category)
        {
            System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Looking for '{parameterName}' on element {element.Id.Value} in category {category}");
            DebugLogger.Log("PARAM_MGR", $"GetParameter called: element={element.Id.Value}, param='{parameterName}', category={category}");
            
            // Try category-specific mapping first
            if (Mappings.TryGetValue(category, out var mapping))
            {
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Found mapping for {category}");
                DebugLogger.Log("PARAM_MGR", $"Found specific mapping for {category}, calling mapping.GetParameter()");
                var param = mapping.GetParameter(element, parameterName);
                if (param != null) 
                {
                    System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Found parameter '{parameterName}' via mapping");
                    DebugLogger.Log("PARAM_MGR", $"Specific mapping found parameter '{parameterName}' - SUCCESS");
                    return param;
                }
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Parameter '{parameterName}' not found via mapping");
                DebugLogger.Log("PARAM_MGR", $"Specific mapping could not find parameter '{parameterName}'");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: No mapping found for category {category}");
                DebugLogger.Log("PARAM_MGR", $"No specific mapping for {category}, falling back to generic lookup");
            }

            // For unmapped categories, try SharedParameterMapping before generic lookup
            System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Trying SharedParameterMapping for '{parameterName}'");
            DebugLogger.Log("PARAM_MGR", $"Calling SharedParameterMapping.GetSharedParameter for '{parameterName}'");
            var sharedParam = SharedParameterMapping.GetSharedParameter(element, parameterName);
            if (sharedParam != null)
            {
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Found parameter '{parameterName}' via SharedParameterMapping");
                DebugLogger.Log("PARAM_MGR", $"SharedParameterMapping found parameter '{parameterName}' - SUCCESS");
                return sharedParam;
            }
            DebugLogger.Log("PARAM_MGR", $"SharedParameterMapping failed for '{parameterName}', trying generic lookup");

            // Final fallback to generic parameter lookup
            System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Trying generic lookup for '{parameterName}'");
            DebugLogger.Log("PARAM_MGR", $"Calling GetParameterGeneric for '{parameterName}'");
            var genericParam = GetParameterGeneric(element, parameterName);
            if (genericParam != null)
            {
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Found parameter '{parameterName}' via generic lookup");
                DebugLogger.Log("PARAM_MGR", $"Generic lookup found parameter '{parameterName}' - SUCCESS");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"ParameterMappingManager.GetParameter: Parameter '{parameterName}' not found anywhere");
                DebugLogger.Log("PARAM_MGR", $"Generic lookup failed for '{parameterName}' - TOTAL FAILURE");
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