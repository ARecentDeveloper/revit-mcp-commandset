using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Structural Foundation elements (based on CSV data)
    /// </summary>
    public class StructuralFoundationParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_StructuralFoundation;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Instance Parameters - Identity Data
            { "type id", BuiltInParameter.SYMBOL_ID_PARAM },
            { "type name", BuiltInParameter.SYMBOL_NAME_PARAM },
            { "family name", BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM },
            { "family and type", BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM },
            { "type", BuiltInParameter.ELEM_TYPE_PARAM },
            { "family", BuiltInParameter.ELEM_FAMILY_PARAM },
            { "mark", BuiltInParameter.ALL_MODEL_MARK },
            { "comments", BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS },
            { "image", BuiltInParameter.ALL_MODEL_IMAGE },
            { "has association", BuiltInParameter.ANALYTICAL_ELEMENT_HAS_ASSOCIATION },
            { "design option", BuiltInParameter.DESIGN_OPTION_ID },
            { "category", BuiltInParameter.ELEM_CATEGORY_PARAM },
            { "host id", BuiltInParameter.HOST_ID_PARAM },

            // Instance Parameters - Constraints
            { "elevation at bottom survey", BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM_SURVEY },
            { "height offset from level", BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM },
            { "level", BuiltInParameter.SCHEDULE_LEVEL_PARAM },
            { "family level", BuiltInParameter.FAMILY_LEVEL_PARAM },
            { "host", BuiltInParameter.INSTANCE_FREE_HOST_PARAM },
            { "moves with grids", BuiltInParameter.INSTANCE_MOVES_WITH_GRID_PARAM },

            // Instance Parameters - Geometry
            { "width", BuiltInParameter.CONTINUOUS_FOOTING_WIDTH },
            { "elevation at top", BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP },
            { "elevation at top survey", BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP_SURVEY },
            { "elevation at bottom", BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM },
            { "length", BuiltInParameter.CONTINUOUS_FOOTING_LENGTH },
            { "area", BuiltInParameter.HOST_AREA_COMPUTED },
            { "volume", BuiltInParameter.HOST_VOLUME_COMPUTED },

            // Instance Parameters - IFC
            { "ifcguid", BuiltInParameter.IFC_GUID },
            { "export to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT },
            { "export to ifc as", BuiltInParameter.IFC_EXPORT_ELEMENT_AS },
            { "ifc predefined type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE },

            // Instance Parameters - Structural
            { "rebar cover - bottom face", BuiltInParameter.CLEAR_COVER_BOTTOM },
            { "rebar cover - other faces", BuiltInParameter.CLEAR_COVER_OTHER },
            { "rebar cover - top face", BuiltInParameter.CLEAR_COVER_TOP },

            // Instance Parameters - Phasing
            { "phase created", BuiltInParameter.PHASE_CREATED },
            { "phase demolished", BuiltInParameter.PHASE_DEMOLISHED }
        };

        private readonly Dictionary<string, BuiltInParameter> _typeParameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Type Parameters - Identity Data
            { "cost", BuiltInParameter.ALL_MODEL_COST },
            { "type mark", BuiltInParameter.WINDOW_TYPE_ID },
            { "assembly code", BuiltInParameter.UNIFORMAT_CODE },
            { "manufacturer", BuiltInParameter.ALL_MODEL_MANUFACTURER },
            { "model", BuiltInParameter.ALL_MODEL_MODEL },
            { "keynote", BuiltInParameter.KEYNOTE_PARAM },
            { "type image", BuiltInParameter.ALL_MODEL_TYPE_IMAGE },
            { "omniclass title", BuiltInParameter.OMNICLASS_DESCRIPTION },
            { "assembly description", BuiltInParameter.UNIFORMAT_DESCRIPTION },
            { "omniclass number", BuiltInParameter.OMNICLASS_CODE },
            { "url", BuiltInParameter.ALL_MODEL_URL },
            { "type comments", BuiltInParameter.ALL_MODEL_TYPE_COMMENTS },
            { "code name", BuiltInParameter.STRUCTURAL_FAMILY_CODE_NAME },
            { "description", BuiltInParameter.ALL_MODEL_DESCRIPTION },

            // Type Parameters - Geometry
            { "foundation width", BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH },
            { "foundation length", BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH },
            { "foundation thickness", BuiltInParameter.STRUCTURAL_FOUNDATION_THICKNESS },

            // Type Parameters - Materials
            { "structural material", BuiltInParameter.STRUCTURAL_MATERIAL_PARAM },

            // Type Parameters - IFC
            { "type ifcguid", BuiltInParameter.IFC_TYPE_GUID },
            { "export type to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE },
            { "export type to ifc as", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS },
            { "type ifc predefined type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE_TYPE }
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common aliases for easier querying
            { "w", "width" },
            { "l", "length" },
            { "t", "foundation thickness" },
            { "thickness", "foundation thickness" },
            { "h", "foundation thickness" },
            { "height", "foundation thickness" },
            { "elev bottom", "elevation at bottom" },
            { "elev top", "elevation at top" },
            { "bottom elevation", "elevation at bottom" },
            { "top elevation", "elevation at top" },
            { "level offset", "height offset from level" },
            { "offset", "height offset from level" },
            { "foundation width", "width" },
            { "foundation length", "length" },
            { "material", "structural material" },
            { "comment", "comments" },
            { "note", "comments" },
            { "name", "type name" },
            { "rebar bottom", "rebar cover - bottom face" },
            { "rebar top", "rebar cover - top face" },
            { "rebar other", "rebar cover - other faces" },
            { "cover bottom", "rebar cover - bottom face" },
            { "cover top", "rebar cover - top face" },
            { "cover other", "rebar cover - other faces" }
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
                case "length":
                case "foundation width":
                case "foundation length":
                case "foundation thickness":
                case "height offset from level":
                case "elevation at bottom":
                case "elevation at top":
                case "elevation at bottom survey":
                case "elevation at top survey":
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

                case "moves with grids":
                case "has association":
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
            }

            return inputValue;
        }

        public override List<string> GetCommonParameterNames()
        {
            return new List<string>
            {
                // Most commonly used parameters for structural foundations
                "mark", "type name", "family name", "level", "height offset from level",
                "width", "length", "foundation thickness", "area", "volume",
                "elevation at bottom", "elevation at top", "structural material",
                "comments", "phase created"
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