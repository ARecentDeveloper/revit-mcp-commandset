using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Roof elements (based on CSV data)
    /// </summary>
    public class RoofParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_Roofs;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Instance Parameters
            { "related to mass", BuiltInParameter.RELATED_TO_MASS },
            { "fascia depth", BuiltInParameter.FASCIA_DEPTH_PARAM },
            { "rafter cut", BuiltInParameter.ROOF_EAVE_CUT_PARAM },
            { "type id", BuiltInParameter.SYMBOL_ID_PARAM },
            { "type", BuiltInParameter.ELEM_TYPE_PARAM },
            { "family name", BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM },
            { "type name", BuiltInParameter.SYMBOL_NAME_PARAM },
            { "base level", BuiltInParameter.ROOF_BASE_LEVEL_PARAM },
            { "thickness", BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM },
            { "mark", BuiltInParameter.ALL_MODEL_MARK },
            { "room bounding", BuiltInParameter.WALL_ATTR_ROOM_BOUNDING },
            { "base offset from level", BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM },
            { "maximum ridge height", BuiltInParameter.ACTUAL_MAX_RIDGE_HEIGHT_PARAM },
            { "cutoff offset", BuiltInParameter.ROOF_UPTO_LEVEL_OFFSET_PARAM },
            { "cutoff level", BuiltInParameter.ROOF_UPTO_LEVEL_PARAM },
            { "family", BuiltInParameter.ELEM_FAMILY_PARAM },
            { "export to ifc as", BuiltInParameter.IFC_EXPORT_ELEMENT_AS },
            { "export to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT },
            { "ifcguid", BuiltInParameter.IFC_GUID },
            { "ifc predefined type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE },
            { "image", BuiltInParameter.ALL_MODEL_IMAGE },
            { "category", BuiltInParameter.ELEM_CATEGORY_PARAM_MT },
            { "design option", BuiltInParameter.DESIGN_OPTION_ID },
            { "comments", BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS },
            { "slope", BuiltInParameter.ROOF_SLOPE },
            { "family and type", BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM },
            { "phase created", BuiltInParameter.PHASE_CREATED },
            { "volume", BuiltInParameter.HOST_VOLUME_COMPUTED },
            { "area", BuiltInParameter.HOST_AREA_COMPUTED },
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
            { "default thickness", BuiltInParameter.ROOF_ATTR_DEFAULT_THICKNESS_PARAM },
            { "type mark", BuiltInParameter.WINDOW_TYPE_ID },
            { "cost", BuiltInParameter.ALL_MODEL_COST },
            { "structure", BuiltInParameter.ROOF_STRUCTURE_ID_PARAM },
            { "coarse scale fill color", BuiltInParameter.COARSE_SCALE_FILL_PATTERN_COLOR },
            { "coarse scale fill pattern", BuiltInParameter.COARSE_SCALE_FILL_PATTERN_ID_PARAM },
            { "export type to ifc as", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS },
            { "export type to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE },
            { "type ifcguid", BuiltInParameter.IFC_TYPE_GUID },
            { "type image", BuiltInParameter.ALL_MODEL_TYPE_IMAGE },
            { "keynote", BuiltInParameter.KEYNOTE_PARAM },
            { "type ifc predefined type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE_TYPE },
            { "url", BuiltInParameter.ALL_MODEL_URL },
            { "description", BuiltInParameter.ALL_MODEL_DESCRIPTION },
            { "roughness", BuiltInParameter.ANALYTICAL_ROUGHNESS },
            { "model", BuiltInParameter.ALL_MODEL_MODEL },
            { "manufacturer", BuiltInParameter.ALL_MODEL_MANUFACTURER },
            { "type comments", BuiltInParameter.ALL_MODEL_TYPE_COMMENTS }
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common aliases
            { "is room bounding", "room bounding" },
            { "roof thickness", "thickness" },
            { "roof area", "area" },
            { "roof volume", "volume" },
            { "roof level", "base level" },
            { "roof mark", "mark" },
            { "roof comments", "comments" },
            { "roof type", "type name" },
            { "roof family", "family name" },
            { "u value", "heat transfer coefficient (u)" },
            { "r value", "thermal resistance (r)" },
            { "roof structure", "structure" },
            { "roof cost", "cost" },
            { "base offset", "base offset from level" },
            { "cutoff", "cutoff level" }
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
                case "fascia depth":
                case "base offset from level":
                case "maximum ridge height":
                case "cutoff offset":
                case "thickness":
                case "default thickness":
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
                    
                case "room bounding":
                case "related to mass":
                case "export to ifc":
                case "export type to ifc":
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
                // Most commonly used parameters for roofs
                "base level", "thickness", "area", "type name", 
                "mark", "comments", "base offset from level", "room bounding",
                "slope", "volume", "fascia depth", "maximum ridge height"
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