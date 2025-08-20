using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Shared parameter mappings common across most categories
    /// </summary>
    public static class SharedParameterMapping
    {
        /// <summary>
        /// Common parameters shared across most categories (only confirmed ones from CSV)
        /// </summary>
        public static readonly Dictionary<string, BuiltInParameter> CommonParameters = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Identity Data - Common across all categories
            { "mark", BuiltInParameter.ALL_MODEL_MARK },
            { "comments", BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS },
            { "type comments", BuiltInParameter.ALL_MODEL_TYPE_COMMENTS },
            { "type mark", BuiltInParameter.WINDOW_TYPE_ID },
            { "description", BuiltInParameter.ALL_MODEL_DESCRIPTION },
            { "manufacturer", BuiltInParameter.ALL_MODEL_MANUFACTURER },
            { "model", BuiltInParameter.ALL_MODEL_MODEL },
            { "url", BuiltInParameter.ALL_MODEL_URL },
            { "cost", BuiltInParameter.ALL_MODEL_COST },
            { "keynote", BuiltInParameter.KEYNOTE_PARAM },
            { "image", BuiltInParameter.ALL_MODEL_IMAGE },
            { "type image", BuiltInParameter.ALL_MODEL_TYPE_IMAGE },
            
            // Assembly and Classification
            { "assembly code", BuiltInParameter.UNIFORMAT_CODE },
            { "assembly description", BuiltInParameter.UNIFORMAT_DESCRIPTION },
            { "omniclass number", BuiltInParameter.OMNICLASS_CODE },
            { "omniclass title", BuiltInParameter.OMNICLASS_DESCRIPTION },
            
            // Element System Properties
            { "type id", BuiltInParameter.SYMBOL_ID_PARAM },
            { "type", BuiltInParameter.ELEM_TYPE_PARAM },
            { "family", BuiltInParameter.ELEM_FAMILY_PARAM },
            { "family and type", BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM },
            { "type name", BuiltInParameter.SYMBOL_NAME_PARAM },
            { "family name", BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM },
            { "category", BuiltInParameter.ELEM_CATEGORY_PARAM },
            
            // Phasing
            { "phase created", BuiltInParameter.PHASE_CREATED },
            { "phase demolished", BuiltInParameter.PHASE_DEMOLISHED },
            
            // Design Options
            { "design option", BuiltInParameter.DESIGN_OPTION_ID },
            
            // IFC Parameters (confirmed from CSV)
            { "ifcguid", BuiltInParameter.IFC_GUID },
            
            // Common Geometry
            { "area", BuiltInParameter.HOST_AREA_COMPUTED },
            { "volume", BuiltInParameter.HOST_VOLUME_COMPUTED },
            { "perimeter", BuiltInParameter.HOST_PERIMETER_COMPUTED },
            
            // Level and Constraints (common pattern)
            { "level", BuiltInParameter.FAMILY_LEVEL_PARAM },
            { "reference level", BuiltInParameter.RBS_START_LEVEL_PARAM },
            
            // Materials
            { "structural material", BuiltInParameter.STRUCTURAL_MATERIAL_PARAM },
            
            // Fire Rating (common across many categories)
            { "fire rating", BuiltInParameter.DOOR_FIRE_RATING }
        };

        /// <summary>
        /// Common parameter aliases
        /// </summary>
        public static readonly Dictionary<string, string> CommonAliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Identity aliases
            { "number", "mark" },
            { "tag", "mark" },
            { "element mark", "mark" },
            { "element number", "mark" },
            
            // Manufacturer aliases
            { "mfr", "manufacturer" },
            { "mfg", "manufacturer" },
            
            // Fire rating aliases
            { "fire rate", "fire rating" },
            
            // Assembly aliases
            { "uniformat code", "assembly code" },
            { "uniformat description", "assembly description" },
            
            // Phase aliases (your use case!)
            { "created", "phase created" },
            { "demolished", "phase demolished" },
            
            // Design option aliases
            { "option", "design option" },
            { "options", "design option" },
            
            // Workset aliases
            { "workset", "workset" },
            
            // Common property aliases
            { "id", "type id" },
            { "name", "type name" },
            
            // Material aliases
            { "material", "structural material" },
            
            // Comment aliases
            { "comment", "comments" },
            { "note", "comments" },
            { "notes", "comments" }
        };

        /// <summary>
        /// Get parameter from element using shared parameter mappings
        /// </summary>
        public static Parameter GetSharedParameter(Element element, string parameterName)
        {
            DebugLogger.Log("SHARED_PARAM", $"GetSharedParameter called: element={element.Id.Value}, param='{parameterName}'");
            
            // Check aliases first
            string actualParamName = CommonAliases.ContainsKey(parameterName) ? CommonAliases[parameterName] : parameterName;
            DebugLogger.Log("SHARED_PARAM", $"After alias check: '{parameterName}' -> '{actualParamName}'");
            
            // Try built-in parameter mapping
            if (CommonParameters.TryGetValue(actualParamName, out var builtInParam))
            {
                DebugLogger.Log("SHARED_PARAM", $"Found '{actualParamName}' in CommonParameters as {builtInParam}");
                
                var param = GetBuiltInParameter(element, builtInParam);
                if (param != null) 
                {
                    DebugLogger.Log("SHARED_PARAM", $"GetBuiltInParameter found parameter '{actualParamName}' - SUCCESS");
                    return param;
                }
                DebugLogger.Log("SHARED_PARAM", $"GetBuiltInParameter failed for '{actualParamName}', trying type parameter");
                
                // Try type parameter
                param = GetBuiltInParameterFromType(element, builtInParam);
                if (param != null) 
                {
                    DebugLogger.Log("SHARED_PARAM", $"GetBuiltInParameterFromType found parameter '{actualParamName}' - SUCCESS");
                    return param;
                }
                DebugLogger.Log("SHARED_PARAM", $"GetBuiltInParameterFromType also failed for '{actualParamName}'");
            }
            else
            {
                DebugLogger.Log("SHARED_PARAM", $"Parameter '{actualParamName}' not found in CommonParameters dictionary");
            }

            DebugLogger.Log("SHARED_PARAM", $"SharedParameterMapping failed to find '{parameterName}' - returning NULL");
            return null;
        }

        /// <summary>
        /// Helper method to try getting built-in parameter
        /// </summary>
        private static Parameter GetBuiltInParameter(Element element, BuiltInParameter builtInParam)
        {
            try
            {
                return element.get_Parameter(builtInParam);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Helper method to try getting built-in parameter from element type
        /// </summary>
        private static Parameter GetBuiltInParameterFromType(Element element, BuiltInParameter builtInParam)
        {
            try
            {
                ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
                return elementType?.get_Parameter(builtInParam);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if parameter name is a shared parameter
        /// </summary>
        public static bool IsSharedParameter(string parameterName)
        {
            string actualParamName = CommonAliases.ContainsKey(parameterName) ? CommonAliases[parameterName] : parameterName;
            return CommonParameters.ContainsKey(actualParamName);
        }

        /// <summary>
        /// Get list of common parameter names
        /// </summary>
        public static List<string> GetCommonParameterNames()
        {
            return new List<string>(CommonParameters.Keys);
        }
    }
}