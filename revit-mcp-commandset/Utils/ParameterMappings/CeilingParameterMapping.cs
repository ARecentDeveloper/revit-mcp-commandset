using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Ceiling elements (based on CSV data)
    /// </summary>
    public class CeilingParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_Ceilings;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Instance Parameters
            { "slope", BuiltInParameter.ROOF_SLOPE },
            { "height_offset_from_level", BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM },
            { "height_offset", BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM },
            { "level", BuiltInParameter.LEVEL_PARAM },
            { "schedule_level", BuiltInParameter.SCHEDULE_LEVEL_PARAM },
            { "mark", BuiltInParameter.ALL_MODEL_MARK },
            { "room_bounding", BuiltInParameter.WALL_ATTR_ROOM_BOUNDING },
            { "perimeter", BuiltInParameter.HOST_PERIMETER_COMPUTED },
            { "ifc_predefined_type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE },
            { "export_to_ifc_as", BuiltInParameter.IFC_EXPORT_ELEMENT_AS },
            { "export_to_ifc", BuiltInParameter.IFC_EXPORT_ELEMENT },
            { "image", BuiltInParameter.ALL_MODEL_IMAGE },
            { "ifcguid", BuiltInParameter.IFC_GUID },
            { "phase_demolished", BuiltInParameter.PHASE_DEMOLISHED },
            { "phase_created", BuiltInParameter.PHASE_CREATED },
            { "comments", BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS },
            { "design_option", BuiltInParameter.DESIGN_OPTION_ID },
            { "volume", BuiltInParameter.HOST_VOLUME_COMPUTED },
            { "area", BuiltInParameter.HOST_AREA_COMPUTED },
            { "family_name", BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM },
            { "type_name", BuiltInParameter.SYMBOL_NAME_PARAM },
            { "type_id", BuiltInParameter.SYMBOL_ID_PARAM },
            { "family_and_type", BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM },
            { "family", BuiltInParameter.ELEM_FAMILY_PARAM },
            { "type", BuiltInParameter.ELEM_TYPE_PARAM },
            { "category", BuiltInParameter.ELEM_CATEGORY_PARAM }
        };

        private readonly Dictionary<string, BuiltInParameter> _typeParameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Type Parameters
            { "heat_transfer_coefficient", BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT },
            { "assembly_description", BuiltInParameter.UNIFORMAT_DESCRIPTION },
            { "assembly_code", BuiltInParameter.UNIFORMAT_CODE },
            { "absorptance", BuiltInParameter.ANALYTICAL_ABSORPTANCE },
            { "thermal_mass", BuiltInParameter.ANALYTICAL_THERMAL_MASS },
            { "thermal_resistance", BuiltInParameter.ANALYTICAL_THERMAL_RESISTANCE },
            { "coarse_scale_fill_pattern", BuiltInParameter.COARSE_SCALE_FILL_PATTERN_ID_PARAM },
            { "type_mark", BuiltInParameter.WINDOW_TYPE_ID },
            { "cost", BuiltInParameter.ALL_MODEL_COST },
            { "thickness", BuiltInParameter.CEILING_THICKNESS },
            { "structure", BuiltInParameter.CEILING_STRUCTURE_ID_PARAM },
            { "coarse_scale_fill_color", BuiltInParameter.COARSE_SCALE_FILL_PATTERN_COLOR },
            { "export_type_to_ifc_as", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS },
            { "export_type_to_ifc", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE },
            { "type_ifcguid", BuiltInParameter.IFC_TYPE_GUID },
            { "type_image", BuiltInParameter.ALL_MODEL_TYPE_IMAGE },
            { "keynote", BuiltInParameter.KEYNOTE_PARAM },
            { "type_ifc_predefined_type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE_TYPE },
            { "url", BuiltInParameter.ALL_MODEL_URL },
            { "description", BuiltInParameter.ALL_MODEL_DESCRIPTION },
            { "roughness", BuiltInParameter.ANALYTICAL_ROUGHNESS },
            { "model", BuiltInParameter.ALL_MODEL_MODEL },
            { "manufacturer", BuiltInParameter.ALL_MODEL_MANUFACTURER },
            { "type_comments", BuiltInParameter.ALL_MODEL_TYPE_COMMENTS }
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common aliases
            { "height", "height_offset_from_level" },
            { "offset", "height_offset_from_level" },
            { "ceiling_height", "height_offset_from_level" },
            { "ceiling_thickness", "thickness" },
            { "ceiling_area", "area" },
            { "ceiling_volume", "volume" },
            { "ceiling_perimeter", "perimeter" },
            { "ceiling_slope", "slope" },
            { "u_value", "heat_transfer_coefficient" },
            { "r_value", "thermal_resistance" },
            { "material_cost", "cost" },
            { "ceiling_mark", "mark" },
            { "ceiling_comments", "comments" },
            { "ceiling_level", "level" },
            { "family_type", "family_and_type" },
            { "ceiling_family", "family_name" },
            { "ceiling_type", "type_name" }
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
                case "height_offset_from_level":
                case "thickness":
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
                        return slopeValue; // Rise per 12 inches
                    break;
                case "room_bounding":
                case "export_to_ifc":
                case "export_type_to_ifc":
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
                case "cost":
                    if (double.TryParse(inputValue.ToString(), out double costValue))
                        return costValue; // Currency value
                    break;
                case "absorptance":
                case "heat_transfer_coefficient":
                case "thermal_mass":
                case "thermal_resistance":
                    if (double.TryParse(inputValue.ToString(), out double thermalValue))
                        return thermalValue; // Thermal properties
                    break;
                case "roughness":
                    if (int.TryParse(inputValue.ToString(), out int roughnessValue))
                        return roughnessValue;
                    break;
            }

            return inputValue;
        }

        public override List<string> GetCommonParameterNames()
        {
            return new List<string>
            {
                // Most commonly used parameters for ceilings
                "mark", "level", "height_offset_from_level", "type_name", "family_name",
                "area", "volume", "perimeter", "thickness", "slope", "room_bounding",
                "comments", "cost", "material", "assembly_code", "phase_created"
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