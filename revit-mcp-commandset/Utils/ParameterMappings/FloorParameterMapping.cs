using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Floor elements (based on CSV data)
    /// </summary>
    public class FloorParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_Floors;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Instance Parameters
            { "structural", BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL },
            { "type id", BuiltInParameter.SYMBOL_ID_PARAM },
            { "level", BuiltInParameter.LEVEL_PARAM },
            { "perimeter", BuiltInParameter.HOST_PERIMETER_COMPUTED },
            { "type", BuiltInParameter.ELEM_TYPE_PARAM },
            { "family", BuiltInParameter.ELEM_FAMILY_PARAM },
            { "type name", BuiltInParameter.SYMBOL_NAME_PARAM },
            { "family name", BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM },
            { "height offset from level", BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM },
            { "elevation at bottom", BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM },
            { "elevation at top", BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP },
            { "room bounding", BuiltInParameter.WALL_ATTR_ROOM_BOUNDING },
            { "mark", BuiltInParameter.ALL_MODEL_MARK },
            { "related to mass", BuiltInParameter.RELATED_TO_MASS },
            { "thickness", BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM },
            { "elevation at top core", BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP_CORE },
            { "elevation at bottom core", BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM_CORE },
            { "export to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT },
            { "export to ifc as", BuiltInParameter.IFC_EXPORT_ELEMENT_AS },
            { "has association", BuiltInParameter.ANALYTICAL_ELEMENT_HAS_ASSOCIATION },
            { "ifcguid", BuiltInParameter.IFC_GUID },
            { "category", BuiltInParameter.ELEM_CATEGORY_PARAM_MT },
            { "image", BuiltInParameter.ALL_MODEL_IMAGE },
            { "ifc predefined type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE },
            { "design option", BuiltInParameter.DESIGN_OPTION_ID },
            { "slope", BuiltInParameter.ROOF_SLOPE },
            { "comments", BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS },
            { "family and type", BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM },
            { "schedule level", BuiltInParameter.SCHEDULE_LEVEL_PARAM },
            { "area", BuiltInParameter.HOST_AREA_COMPUTED },
            { "volume", BuiltInParameter.HOST_VOLUME_COMPUTED },
            { "phase created", BuiltInParameter.PHASE_CREATED },
            { "phase demolished", BuiltInParameter.PHASE_DEMOLISHED }
        };

        private readonly Dictionary<string, BuiltInParameter> _typeParameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Type Parameters
            { "heat transfer coefficient (u)", BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT },
            { "assembly description", BuiltInParameter.UNIFORMAT_DESCRIPTION },
            { "assembly code", BuiltInParameter.UNIFORMAT_CODE },
            { "absorptance", BuiltInParameter.ANALYTICAL_ABSORPTANCE },
            { "thermal mass", BuiltInParameter.ANALYTICAL_THERMAL_MASS },
            { "thermal resistance (r)", BuiltInParameter.ANALYTICAL_THERMAL_RESISTANCE },
            { "structure", BuiltInParameter.FLOOR_STRUCTURE_ID_PARAM },
            { "type mark", BuiltInParameter.WINDOW_TYPE_ID },
            { "cost", BuiltInParameter.ALL_MODEL_COST },
            { "function", BuiltInParameter.FUNCTION_PARAM },
            { "coarse scale fill color", BuiltInParameter.COARSE_SCALE_FILL_PATTERN_COLOR },
            { "coarse scale fill pattern", BuiltInParameter.COARSE_SCALE_FILL_PATTERN_ID_PARAM },
            { "default thickness", BuiltInParameter.FLOOR_ATTR_DEFAULT_THICKNESS_PARAM },
            { "export type to ifc as", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS },
            { "export type to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE },
            { "type ifcguid", BuiltInParameter.IFC_TYPE_GUID },
            { "type image", BuiltInParameter.ALL_MODEL_TYPE_IMAGE },
            { "keynote", BuiltInParameter.KEYNOTE_PARAM },
            { "type ifc predefined type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE_TYPE },
            { "model", BuiltInParameter.ALL_MODEL_MODEL },
            { "description", BuiltInParameter.ALL_MODEL_DESCRIPTION },
            { "structural material", BuiltInParameter.STRUCTURAL_MATERIAL_PARAM },
            { "roughness", BuiltInParameter.ANALYTICAL_ROUGHNESS },
            { "manufacturer", BuiltInParameter.ALL_MODEL_MANUFACTURER },
            { "type comments", BuiltInParameter.ALL_MODEL_TYPE_COMMENTS },
            { "url", BuiltInParameter.ALL_MODEL_URL }
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common aliases
            { "is structural", "structural" },
            { "height offset", "height offset from level" },
            { "bottom elevation", "elevation at bottom" },
            { "top elevation", "elevation at top" },
            { "is room bounding", "room bounding" },
            { "floor thickness", "thickness" },
            { "floor area", "area" },
            { "floor volume", "volume" },
            { "floor level", "level" },
            { "floor mark", "mark" },
            { "floor comments", "comments" },
            { "floor type", "type name" },
            { "floor family", "family name" },
            { "u value", "heat transfer coefficient (u)" },
            { "r value", "thermal resistance (r)" },
            { "floor structure", "structure" },
            { "floor function", "function" },
            { "floor cost", "cost" }
        };

        protected override Parameter GetCategorySpecificParameter(Element element, string parameterName)
        {
            // Check aliases first
            string actualParamName = _aliases.ContainsKey(parameterName) ? _aliases[parameterName] : parameterName;
            
            // Try instance parameter mapping first
            if (_parameterMappings.TryGetValue(actualParamName, out var builtInParam))
            {
                var param = GetBuiltInParameter(element, builtInParam);
                if (param != null) return param;
            }
            
            // Try type parameter mapping
            if (_typeParameterMappings.TryGetValue(actualParamName, out var typeBuiltInParam))
            {
                var param = GetBuiltInParameterFromType(element, typeBuiltInParam);
                if (param != null) return param;
            }

            // Fallback to generic lookup
            var genericParam = element.LookupParameter(actualParamName);
            if (genericParam != null) return genericParam;

            // Try type parameter
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
            return elementType?.LookupParameter(actualParamName);
        }

        public override object ConvertValue(string parameterName, object inputValue)
        {
            if (inputValue == null) return null;

            string actualParamName = _aliases.ContainsKey(parameterName) ? _aliases[parameterName] : parameterName;
            
            // Add category-specific unit conversions
            switch (actualParamName.ToLower())
            {
                case "height offset from level":
                case "elevation at bottom":
                case "elevation at top":
                case "thickness":
                case "elevation at top core":
                case "elevation at bottom core":
                case "default thickness":
                case "perimeter":
                    if (double.TryParse(inputValue.ToString(), out double lengthValue))
                        return lengthValue; // Assume input is already in feet
                    break;
                    
                case "area":
                    if (double.TryParse(inputValue.ToString(), out double areaValue))
                        return areaValue; // Assume input is already in square feet
                    break;
                    
                case "volume":
                    if (double.TryParse(inputValue.ToString(), out double volumeValue))
                        return volumeValue; // Assume input is already in cubic feet
                    break;
                    
                case "slope":
                    if (double.TryParse(inputValue.ToString(), out double slopeValue))
                        return slopeValue; // Assume input is already in correct units
                    break;
                    
                case "cost":
                    if (double.TryParse(inputValue.ToString(), out double costValue))
                        return costValue; // Currency - no conversion needed
                    break;
                    
                case "structural":
                case "room bounding":
                case "related to mass":
                case "export to ifc":
                case "export type to ifc":
                case "has association":
                    // Boolean parameters
                    if (bool.TryParse(inputValue.ToString(), out bool boolValue))
                        return boolValue ? 1 : 0;
                    if (inputValue.ToString().Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                        inputValue.ToString().Equals("true", StringComparison.OrdinalIgnoreCase))
                        return 1;
                    if (inputValue.ToString().Equals("no", StringComparison.OrdinalIgnoreCase) ||
                        inputValue.ToString().Equals("false", StringComparison.OrdinalIgnoreCase))
                        return 0;
                    break;
            }

            return inputValue;
        }

        public override List<string> GetCommonParameterNames()
        {
            return new List<string>
            {
                // Most commonly used parameters for floors
                "level", "thickness", "area", "structural", "type name", 
                "mark", "comments", "height offset from level", "room bounding",
                "elevation at bottom", "elevation at top", "volume", "perimeter"
            };
        }

        public override Dictionary<string, string> GetParameterAliases()
        {
            return new Dictionary<string, string>(_aliases);
        }

        public override bool HasParameter(string parameterName)
        {
            // Check aliases first
            string actualParamName = _aliases.ContainsKey(parameterName) ? _aliases[parameterName] : parameterName;
            
            // Check if parameter exists in instance or type mappings
            return _parameterMappings.ContainsKey(actualParamName) || 
                   _typeParameterMappings.ContainsKey(actualParamName);
        }
    }
}