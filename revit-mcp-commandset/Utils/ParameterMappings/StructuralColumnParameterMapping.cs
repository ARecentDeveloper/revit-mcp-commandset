using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Structural Column elements (based on CSV data)
    /// </summary>
    public class StructuralColumnParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_StructuralColumns;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Instance Parameters - Constraints
            { "base level", BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM },
            { "top level", BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM },
            { "level", BuiltInParameter.SCHEDULE_LEVEL_PARAM },
            { "base offset", BuiltInParameter.SCHEDULE_BASE_LEVEL_OFFSET_PARAM },
            { "top offset", BuiltInParameter.SCHEDULE_TOP_LEVEL_OFFSET_PARAM },
            { "family base level", BuiltInParameter.FAMILY_BASE_LEVEL_PARAM },
            { "family top level", BuiltInParameter.FAMILY_TOP_LEVEL_PARAM },
            { "family base offset", BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM },
            { "family top offset", BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM },
            { "cross-section rotation", BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE },
            { "move top with grids", BuiltInParameter.INSTANCE_MOVE_TOP_WITH_GRIDS },
            { "move base with grids", BuiltInParameter.INSTANCE_MOVE_BASE_WITH_GRIDS },
            { "column style", BuiltInParameter.SLANTED_COLUMN_TYPE_PARAM },

            // Instance Parameters - Identity Data
            { "mark", BuiltInParameter.ALL_MODEL_MARK },
            { "type name", BuiltInParameter.SYMBOL_NAME_PARAM },
            { "family name", BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM },
            { "type id", BuiltInParameter.SYMBOL_ID_PARAM },
            { "comments", BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS },
            { "image", BuiltInParameter.ALL_MODEL_IMAGE },
            { "design option", BuiltInParameter.DESIGN_OPTION_ID },
            { "has association", BuiltInParameter.ANALYTICAL_ELEMENT_HAS_ASSOCIATION },

            // Instance Parameters - Materials
            { "structural material", BuiltInParameter.STRUCTURAL_MATERIAL_PARAM },

            // Instance Parameters - Geometry
            { "length", BuiltInParameter.INSTANCE_LENGTH_PARAM },
            { "volume", BuiltInParameter.HOST_VOLUME_COMPUTED },

            // Instance Parameters - Construction
            { "base extension", BuiltInParameter.SLANTED_COLUMN_BASE_EXTENSION },
            { "top extension", BuiltInParameter.SLANTED_COLUMN_TOP_EXTENSION },
            { "top cut style", BuiltInParameter.SLANTED_COLUMN_TOP_CUT_STYLE },
            { "base cut style", BuiltInParameter.SLANTED_COLUMN_BASE_CUT_STYLE },

            // Instance Parameters - Phasing
            { "phase created", BuiltInParameter.PHASE_CREATED },
            { "phase demolished", BuiltInParameter.PHASE_DEMOLISHED },

            // Instance Parameters - IFC
            { "ifc predefined type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE },
            { "export to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT },
            { "export to ifc as", BuiltInParameter.IFC_EXPORT_ELEMENT_AS },
            { "ifcguid", BuiltInParameter.IFC_GUID },

            // Instance Parameters - Structural
            { "top connection", BuiltInParameter.STRUCT_CONNECTION_COLUMN_TOP },
            { "base connection", BuiltInParameter.STRUCT_CONNECTION_COLUMN_BASE },

            // Common aliases
            { "family", BuiltInParameter.ELEM_FAMILY_PARAM },
            { "family and type", BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM },
            { "type", BuiltInParameter.ELEM_TYPE_PARAM },
            { "category", BuiltInParameter.ELEM_CATEGORY_PARAM },
            { "host id", BuiltInParameter.HOST_ID_PARAM },
            { "column location mark", BuiltInParameter.COLUMN_LOCATION_MARK }
        };

        private readonly Dictionary<string, BuiltInParameter> _typeParameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Type Parameters - Structural Analysis
            { "moment of inertia strong axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_MOMENT_OF_INERTIA_STRONG_AXIS },
            { "moment of inertia weak axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_MOMENT_OF_INERTIA_WEAK_AXIS },
            { "nominal weight", BuiltInParameter.STRUCTURAL_SECTION_COMMON_NOMINAL_WEIGHT },
            { "principal axes angle", BuiltInParameter.STRUCTURAL_SECTION_COMMON_ALPHA },
            { "perimeter", BuiltInParameter.STRUCTURAL_SECTION_COMMON_PERIMETER },
            { "elastic modulus strong axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_ELASTIC_MODULUS_STRONG_AXIS },
            { "torsional moment of inertia", BuiltInParameter.STRUCTURAL_SECTION_COMMON_TORSIONAL_MOMENT_OF_INERTIA },
            { "torsional modulus", BuiltInParameter.STRUCTURAL_SECTION_COMMON_TORSIONAL_MODULUS },
            { "plastic modulus weak axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_PLASTIC_MODULUS_WEAK_AXIS },
            { "elastic modulus weak axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_ELASTIC_MODULUS_WEAK_AXIS },
            { "plastic modulus strong axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_PLASTIC_MODULUS_STRONG_AXIS },
            { "section area", BuiltInParameter.STRUCTURAL_SECTION_AREA },
            { "warping constant", BuiltInParameter.STRUCTURAL_SECTION_COMMON_WARPING_CONSTANT },
            { "shear area strong axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_SHEAR_AREA_STRONG_AXIS },
            { "shear area weak axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_SHEAR_AREA_WEAK_AXIS },

            // Type Parameters - Structural Section Geometry
            { "centroid vertical", BuiltInParameter.STRUCTURAL_SECTION_COMMON_CENTROID_VERTICAL },
            { "centroid horizontal", BuiltInParameter.STRUCTURAL_SECTION_COMMON_CENTROID_HORIZ },
            { "height", BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT },
            { "width", BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH },
            { "web thickness", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_WEBTHICKNESS },
            { "web fillet", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_WEBFILLET },
            { "flange thickness", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_FLANGETHICKNESS },
            { "clear web height", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_CLEAR_WEB_HEIGHT },
            { "bolt diameter", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_BOLT_DIAMETER },
            { "bolt spacing", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_BOLT_SPACING },
            { "flange toe of fillet", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_FLANGE_TOE_OF_FILLET },
            { "web toe of fillet", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_WEB_TOE_OF_FILLET },

            // Type Parameters - Identity Data
            { "assembly description", BuiltInParameter.UNIFORMAT_DESCRIPTION },
            { "omniclass number", BuiltInParameter.OMNICLASS_CODE },
            { "assembly code", BuiltInParameter.UNIFORMAT_CODE },
            { "cost", BuiltInParameter.ALL_MODEL_COST },
            { "type mark", BuiltInParameter.WINDOW_TYPE_ID },
            { "omniclass title", BuiltInParameter.OMNICLASS_DESCRIPTION },
            { "manufacturer", BuiltInParameter.ALL_MODEL_MANUFACTURER },
            { "model", BuiltInParameter.ALL_MODEL_MODEL },
            { "type comments", BuiltInParameter.ALL_MODEL_TYPE_COMMENTS },
            { "description", BuiltInParameter.ALL_MODEL_DESCRIPTION },
            { "url", BuiltInParameter.ALL_MODEL_URL },
            { "keynote", BuiltInParameter.KEYNOTE_PARAM },
            { "type image", BuiltInParameter.ALL_MODEL_TYPE_IMAGE },
            { "code name", BuiltInParameter.STRUCTURAL_FAMILY_CODE_NAME },
            { "section name key", BuiltInParameter.STRUCTURAL_SECTION_NAME_KEY },

            // Type Parameters - Structural
            { "section shape", BuiltInParameter.STRUCTURAL_SECTION_SHAPE },

            // Type Parameters - IFC
            { "type ifcguid", BuiltInParameter.IFC_TYPE_GUID },
            { "type ifc predefined type", BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE_TYPE },
            { "export type to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE },
            { "export type to ifc as", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS }
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common aliases for easier querying
            { "h", "height" },
            { "w", "width" },
            { "ix", "moment of inertia strong axis" },
            { "iy", "moment of inertia weak axis" },
            { "area", "section area" },
            { "weight", "nominal weight" },
            { "sx", "elastic modulus strong axis" },
            { "sy", "elastic modulus weak axis" },
            { "zx", "plastic modulus strong axis" },
            { "zy", "plastic modulus weak axis" },
            { "j", "torsional moment of inertia" },
            { "cw", "warping constant" },
            { "base level offset", "base offset" },
            { "top level offset", "top offset" },
            { "rotation", "cross-section rotation" },
            { "angle", "cross-section rotation" },
            { "comment", "comments" },
            { "note", "comments" },
            { "material", "structural material" },
            { "name", "type name" }
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
                case "base offset":
                case "top offset":
                case "family base offset":
                case "family top offset":
                case "length":
                case "height":
                case "width":
                case "web thickness":
                case "flange thickness":
                case "base extension":
                case "top extension":
                case "clear web height":
                case "bolt diameter":
                case "bolt spacing":
                case "flange toe of fillet":
                case "web toe of fillet":
                case "centroid vertical":
                case "centroid horizontal":
                    if (double.TryParse(inputValue.ToString(), out double lengthValue))
                        return lengthValue; // Assume input is already in feet
                    break;

                case "cross-section rotation":
                case "principal axes angle":
                    if (double.TryParse(inputValue.ToString(), out double angleValue))
                        return angleValue * Math.PI / 180.0; // Convert degrees to radians
                    break;

                case "section area":
                case "shear area strong axis":
                case "shear area weak axis":
                    if (double.TryParse(inputValue.ToString(), out double areaValue))
                        return areaValue; // Assume input is already in square feet
                    break;

                case "volume":
                    if (double.TryParse(inputValue.ToString(), out double volumeValue))
                        return volumeValue; // Assume input is already in cubic feet
                    break;

                case "move top with grids":
                case "move base with grids":
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

                case "cost":
                    if (double.TryParse(inputValue.ToString(), out double costValue))
                        return costValue; // Currency values
                    break;

                case "nominal weight":
                    if (double.TryParse(inputValue.ToString(), out double weightValue))
                        return weightValue; // Weight per unit length
                    break;
            }

            return inputValue;
        }

        public override List<string> GetCommonParameterNames()
        {
            return new List<string>
            {
                // Most commonly used parameters for structural columns
                "mark", "type name", "family name", "base level", "top level", 
                "base offset", "top offset", "length", "structural material",
                "height", "width", "section area", "moment of inertia strong axis",
                "moment of inertia weak axis", "cross-section rotation", "comments"
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