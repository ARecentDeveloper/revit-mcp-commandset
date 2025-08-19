using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Window elements (based on CSV data)
    /// </summary>
    public class WindowParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_Windows;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Instance Parameters - Constraints
            { "sill height", BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM },
            { "head height", BuiltInParameter.INSTANCE_HEAD_HEIGHT_PARAM },
            { "level", BuiltInParameter.FAMILY_LEVEL_PARAM },
            { "schedule level", BuiltInParameter.SCHEDULE_LEVEL_PARAM },

            // Instance Parameters - Identity Data
            { "mark", BuiltInParameter.ALL_MODEL_MARK },
            { "type id", BuiltInParameter.SYMBOL_ID_PARAM },
            { "type name", BuiltInParameter.SYMBOL_NAME_PARAM },
            { "family name", BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM },
            { "family", BuiltInParameter.ELEM_FAMILY_PARAM },
            { "family and type", BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM },
            { "type", BuiltInParameter.ELEM_TYPE_PARAM },
            { "comments", BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS },
            { "image", BuiltInParameter.ALL_MODEL_IMAGE },
            { "design option", BuiltInParameter.DESIGN_OPTION_ID },
            { "category", BuiltInParameter.ELEM_CATEGORY_PARAM },
            { "host id", BuiltInParameter.HOST_ID_PARAM },

            // Instance Parameters - Geometry
            { "area", BuiltInParameter.HOST_AREA_COMPUTED },
            { "volume", BuiltInParameter.HOST_VOLUME_COMPUTED },

            // Instance Parameters - Phasing
            { "phase created", BuiltInParameter.PHASE_CREATED },
            { "phase demolished", BuiltInParameter.PHASE_DEMOLISHED },

            // Instance Parameters - IFC
            { "ifcguid", BuiltInParameter.IFC_GUID },
            { "export to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT },
            { "export to ifc as", BuiltInParameter.IFC_EXPORT_ELEMENT_AS },
            { "ifc predefined type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE }
        };

        private readonly Dictionary<string, BuiltInParameter> _typeParameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Type Parameters - Geometry
            { "height", BuiltInParameter.GENERIC_HEIGHT },
            { "width", BuiltInParameter.DOOR_WIDTH },
            { "rough height", BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM },
            { "rough width", BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM },

            // Type Parameters - Construction
            { "operation", BuiltInParameter.WINDOW_OPERATION_TYPE },
            { "construction type", BuiltInParameter.CASEWORK_CONSTRUCTION_TYPE },
            { "wall closure", BuiltInParameter.TYPE_WALL_CLOSURE },

            // Type Parameters - Identity Data
            { "cost", BuiltInParameter.ALL_MODEL_COST },
            { "type mark", BuiltInParameter.WINDOW_TYPE_ID },
            { "assembly code", BuiltInParameter.UNIFORMAT_CODE },
            { "model", BuiltInParameter.ALL_MODEL_MODEL },
            { "manufacturer", BuiltInParameter.ALL_MODEL_MANUFACTURER },
            { "url", BuiltInParameter.ALL_MODEL_URL },
            { "type comments", BuiltInParameter.ALL_MODEL_TYPE_COMMENTS },
            { "keynote", BuiltInParameter.KEYNOTE_PARAM },
            { "type image", BuiltInParameter.ALL_MODEL_TYPE_IMAGE },
            { "omniclass title", BuiltInParameter.OMNICLASS_DESCRIPTION },
            { "assembly description", BuiltInParameter.UNIFORMAT_DESCRIPTION },
            { "omniclass number", BuiltInParameter.OMNICLASS_CODE },
            { "code name", BuiltInParameter.STRUCTURAL_FAMILY_CODE_NAME },
            { "description", BuiltInParameter.ALL_MODEL_DESCRIPTION },

            // Type Parameters - IFC
            { "type ifcguid", BuiltInParameter.IFC_TYPE_GUID },
            { "export type to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE },
            { "export type to ifc as", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS },
            { "type ifc predefined type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE_TYPE },

            // Type Parameters - Analytical Properties
            { "heat transfer coefficient (u)", BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT },
            { "thermal resistance (r)", BuiltInParameter.ANALYTICAL_THERMAL_RESISTANCE },
            { "define thermal properties by", BuiltInParameter.ANALYTICAL_DEFINE_THERMAL_PROPERTIES_BY },
            { "analytic construction", BuiltInParameter.ANALYTIC_CONSTRUCTION_LOOKUP_TABLE },
            { "solar heat gain coefficient", BuiltInParameter.ANALYTICAL_SOLAR_HEAT_GAIN_COEFFICIENT },
            { "visual light transmittance", BuiltInParameter.ANALYTICAL_VISUAL_LIGHT_TRANSMITTANCE }
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common aliases for easier querying
            { "w", "width" },
            { "h", "height" },
            { "sill", "sill height" },
            { "head", "head height" },
            { "rough w", "rough width" },
            { "rough h", "rough height" },
            { "u value", "heat transfer coefficient (u)" },
            { "r value", "thermal resistance (r)" },
            { "shgc", "solar heat gain coefficient" },
            { "vlt", "visual light transmittance" },
            { "name", "type name" },
            { "comment", "comments" },
            { "note", "comments" },
            { "thermal u", "heat transfer coefficient (u)" },
            { "thermal r", "thermal resistance (r)" },
            { "fire rating", "fire rating" }
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
                case "width":
                case "height":
                case "rough width":
                case "rough height":
                case "sill height":
                case "head height":
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

                case "cost":
                    if (double.TryParse(inputValue.ToString(), out double costValue))
                        return costValue; // Currency values
                    break;

                case "heat transfer coefficient (u)":
                case "thermal resistance (r)":
                case "solar heat gain coefficient":
                case "visual light transmittance":
                    if (double.TryParse(inputValue.ToString(), out double thermalValue))
                        return thermalValue; // Thermal property values
                    break;
            }

            return inputValue;
        }

        public override List<string> GetCommonParameterNames()
        {
            return new List<string>
            {
                // Most commonly used parameters for windows
                "mark", "type name", "family name", "level", "sill height", "head height",
                "width", "height", "area", "operation", "comments", "phase created"
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