using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Structural Framing elements (based on CSV data)
    /// </summary>
    public class StructuralFramingParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_StructuralFraming;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Instance Parameters - Dimensional and Positioning
            { "reference level elevation", BuiltInParameter.STRUCTURAL_REFERENCE_LEVEL_ELEVATION },
            { "cross-section rotation", BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE },
            { "elevation at top", BuiltInParameter.STRUCTURAL_ELEVATION_AT_TOP },
            { "elevation at bottom", BuiltInParameter.STRUCTURAL_ELEVATION_AT_BOTTOM },
            { "length", BuiltInParameter.INSTANCE_LENGTH_PARAM },
            { "cut length", BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH },
            
            // Level and Offsets
            { "level", BuiltInParameter.SCHEDULE_LEVEL_PARAM },
            { "reference level", BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM },
            { "start level offset", BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION },
            { "end level offset", BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION },
            
            // Structural Properties
            { "orientation", BuiltInParameter.STRUCTURAL_BEAM_ORIENTATION },
            { "structural usage", BuiltInParameter.INSTANCE_STRUCT_USAGE_PARAM },
            { "structural material", BuiltInParameter.STRUCTURAL_MATERIAL_PARAM },
            
            // Extensions and Positioning
            { "start extension", BuiltInParameter.START_EXTENSION },
            { "end extension", BuiltInParameter.END_EXTENSION },
            { "yz justification", BuiltInParameter.YZ_JUSTIFICATION },
            { "y justification", BuiltInParameter.Y_JUSTIFICATION },
            { "z justification", BuiltInParameter.Z_JUSTIFICATION },
            { "y offset value", BuiltInParameter.Y_OFFSET_VALUE },
            { "z offset value", BuiltInParameter.Z_OFFSET_VALUE },
            
            // Structural Analysis
            { "stick symbol location", BuiltInParameter.STRUCTURAL_STICK_SYMBOL_LOCATION },
            { "number of studs", BuiltInParameter.STRUCTURAL_NUMBER_OF_STUDS },
            { "camber size", BuiltInParameter.STRUCTURAL_CAMBER },
            { "join status", BuiltInParameter.STRUCT_FRAM_JOIN_STATUS },
            
            // Connections
            { "start connection", BuiltInParameter.STRUCT_CONNECTION_BEAM_START },
            { "end connection", BuiltInParameter.STRUCT_CONNECTION_BEAM_END },
            
            // Identity Data
            { "mark", BuiltInParameter.ALL_MODEL_MARK },
            { "comments", BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS },
            { "image", BuiltInParameter.ALL_MODEL_IMAGE },
            
            // Element Properties
            { "type id", BuiltInParameter.SYMBOL_ID_PARAM },
            { "type name", BuiltInParameter.SYMBOL_NAME_PARAM },
            { "family and type", BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM },
            { "family name", BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM },
            { "type", BuiltInParameter.ELEM_TYPE_PARAM },
            { "family", BuiltInParameter.ELEM_FAMILY_PARAM },
            { "category", BuiltInParameter.ELEM_CATEGORY_PARAM },
            
            // Phasing
            { "phase created", BuiltInParameter.PHASE_CREATED },
            { "phase demolished", BuiltInParameter.PHASE_DEMOLISHED },
            
            // Geometry
            { "volume", BuiltInParameter.HOST_VOLUME_COMPUTED },
            { "work plane", BuiltInParameter.SKETCH_PLANE_PARAM },
            
            // Design Options
            { "design option", BuiltInParameter.DESIGN_OPTION_ID },
            
            // Host
            { "host id", BuiltInParameter.HOST_ID_PARAM }
        };

        private readonly Dictionary<string, BuiltInParameter> _typeParameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Type Parameters - Structural Analysis Properties
            { "nominal weight", BuiltInParameter.STRUCTURAL_SECTION_COMMON_NOMINAL_WEIGHT },
            { "moment of inertia strong axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_MOMENT_OF_INERTIA_STRONG_AXIS },
            { "moment of inertia weak axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_MOMENT_OF_INERTIA_WEAK_AXIS },
            { "torsional moment of inertia", BuiltInParameter.STRUCTURAL_SECTION_COMMON_TORSIONAL_MOMENT_OF_INERTIA },
            { "elastic modulus strong axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_ELASTIC_MODULUS_STRONG_AXIS },
            { "elastic modulus weak axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_ELASTIC_MODULUS_WEAK_AXIS },
            { "plastic modulus strong axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_PLASTIC_MODULUS_STRONG_AXIS },
            { "plastic modulus weak axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_PLASTIC_MODULUS_WEAK_AXIS },
            { "torsional modulus", BuiltInParameter.STRUCTURAL_SECTION_COMMON_TORSIONAL_MODULUS },
            { "warping constant", BuiltInParameter.STRUCTURAL_SECTION_COMMON_WARPING_CONSTANT },
            { "shear area strong axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_SHEAR_AREA_STRONG_AXIS },
            { "shear area weak axis", BuiltInParameter.STRUCTURAL_SECTION_COMMON_SHEAR_AREA_WEAK_AXIS },
            { "section area", BuiltInParameter.STRUCTURAL_SECTION_AREA },
            { "perimeter", BuiltInParameter.STRUCTURAL_SECTION_COMMON_PERIMETER },
            { "principal axes angle", BuiltInParameter.STRUCTURAL_SECTION_COMMON_ALPHA },
            
            // Section Geometry
            { "height", BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT },
            { "width", BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH },
            { "centroid vertical", BuiltInParameter.STRUCTURAL_SECTION_COMMON_CENTROID_VERTICAL },
            { "centroid horizontal", BuiltInParameter.STRUCTURAL_SECTION_COMMON_CENTROID_HORIZ },
            
            // I-Shape Specific Properties
            { "flange thickness", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_FLANGETHICKNESS },
            { "web thickness", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_WEBTHICKNESS },
            { "web fillet", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_WEBFILLET },
            { "bolt spacing", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_BOLT_SPACING },
            { "bolt diameter", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_BOLT_DIAMETER },
            { "clear web height", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_CLEAR_WEB_HEIGHT },
            { "flange toe of fillet", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_FLANGE_TOE_OF_FILLET },
            { "web toe of fillet", BuiltInParameter.STRUCTURAL_SECTION_ISHAPE_WEB_TOE_OF_FILLET },
            
            // Identity Data
            { "assembly code", BuiltInParameter.UNIFORMAT_CODE },
            { "assembly description", BuiltInParameter.UNIFORMAT_DESCRIPTION },
            { "omniclass number", BuiltInParameter.OMNICLASS_CODE },
            { "omniclass title", BuiltInParameter.OMNICLASS_DESCRIPTION },
            { "cost", BuiltInParameter.ALL_MODEL_COST },
            { "fire rating", BuiltInParameter.DOOR_FIRE_RATING },
            { "type mark", BuiltInParameter.WINDOW_TYPE_ID },
            { "type comments", BuiltInParameter.ALL_MODEL_TYPE_COMMENTS },
            { "manufacturer", BuiltInParameter.ALL_MODEL_MANUFACTURER },
            { "model", BuiltInParameter.ALL_MODEL_MODEL },
            { "code name", BuiltInParameter.STRUCTURAL_FAMILY_CODE_NAME },
            { "description", BuiltInParameter.ALL_MODEL_DESCRIPTION },
            { "url", BuiltInParameter.ALL_MODEL_URL },
            { "keynote", BuiltInParameter.KEYNOTE_PARAM },
            { "type image", BuiltInParameter.ALL_MODEL_TYPE_IMAGE },
            
            // Section Properties
            { "section shape", BuiltInParameter.STRUCTURAL_SECTION_SHAPE },
            { "section name key", BuiltInParameter.STRUCTURAL_SECTION_NAME_KEY },
            
            // Additional type parameters can be added here as needed
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common aliases for structural framing
            { "beam length", "length" },
            { "member length", "length" },
            { "frame length", "length" },
            { "l", "length" },
            { "len", "length" },
            
            // Height/Width aliases
            { "h", "height" },
            { "beam height", "height" },
            { "depth", "height" },
            { "d", "height" },
            { "w", "width" },
            { "beam width", "width" },
            { "b", "width" },
            
            // Elevation aliases
            { "top elevation", "elevation at top" },
            { "bottom elevation", "elevation at bottom" },
            { "start elevation", "start level offset" },
            { "end elevation", "end level offset" },
            
            // Structural aliases
            { "usage", "structural usage" },
            { "material", "structural material" },
            { "rotation", "cross-section rotation" },
            { "angle", "cross-section rotation" },
            
            // Section properties aliases
            { "area", "section area" },
            { "ix", "moment of inertia strong axis" },
            { "iy", "moment of inertia weak axis" },
            { "j", "torsional moment of inertia" },
            { "sx", "elastic modulus strong axis" },
            { "sy", "elastic modulus weak axis" },
            { "zx", "plastic modulus strong axis" },
            { "zy", "plastic modulus weak axis" },
            { "weight", "nominal weight" },
            { "wt", "nominal weight" },
            
            // Flange/Web aliases
            { "tf", "flange thickness" },
            { "tw", "web thickness" },
            { "flange", "flange thickness" },
            { "web", "web thickness" },
            
            // Camber aliases
            { "camber", "camber size" },
            { "beam camber", "camber size" },
            { "member camber", "camber size" },
            
            // Stud count aliases
            { "stud count", "number of studs" },
            { "studs", "number of studs" },
            { "stud", "number of studs" },
            { "number of stud", "number of studs" },
            { "stud number", "number of studs" },
            { "beam studs", "number of studs" },
            { "shear studs", "number of studs" }
        };     
   protected override Parameter GetCategorySpecificParameter(Element element, string parameterName)
        {
            System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping.GetCategorySpecificParameter: Looking for '{parameterName}' on element {element.Id.IntegerValue}");
            
            // Check aliases first
            string actualParamName = _aliases.ContainsKey(parameterName) ? _aliases[parameterName] : parameterName;
            if (actualParamName != parameterName)
            {
                System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Resolved alias '{parameterName}' to '{actualParamName}'");
            }
            
            // Try instance parameter mapping first
            if (_parameterMappings.TryGetValue(actualParamName, out var builtInParam))
            {
                System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Found instance mapping for '{actualParamName}' -> {builtInParam}");
                var param = GetBuiltInParameter(element, builtInParam);
                if (param != null) 
                {
                    System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Found instance parameter '{actualParamName}'");
                    return param;
                }
                System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Instance parameter '{actualParamName}' not found on element");
            }
            
            // Try type parameter mapping
            if (_typeParameterMappings.TryGetValue(actualParamName, out var typeBuiltInParam))
            {
                System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Found type mapping for '{actualParamName}' -> {typeBuiltInParam}");
                var param = GetBuiltInParameterFromType(element, typeBuiltInParam);
                if (param != null) 
                {
                    System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Found type parameter '{actualParamName}'");
                    return param;
                }
                System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Type parameter '{actualParamName}' not found on element type");
            }

            // Fallback to generic lookup
            System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Trying generic lookup for '{actualParamName}'");
            var genericParam = element.LookupParameter(actualParamName);
            if (genericParam != null) 
            {
                System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Found generic parameter '{actualParamName}'");
                return genericParam;
            }

            // Try type parameter
            System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Trying type generic lookup for '{actualParamName}'");
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
            var typeParam = elementType?.LookupParameter(actualParamName);
            if (typeParam != null)
            {
                System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Found type generic parameter '{actualParamName}'");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"StructuralFramingParameterMapping: Parameter '{actualParamName}' not found anywhere");
            }
            return typeParam;
        }

        public override object ConvertValue(string parameterName, object inputValue)
        {
            if (inputValue == null) return null;

            string actualParamName = _aliases.ContainsKey(parameterName) ? _aliases[parameterName] : parameterName;
            
            // Convert based on parameter type
            switch (actualParamName.ToLower())
            {
                // Length parameters - convert inches to feet
                case "length":
                case "cut length":
                case "start extension":
                case "end extension":
                case "reference level elevation":
                case "elevation at top":
                case "elevation at bottom":
                case "start level offset":
                case "end level offset":
                case "y offset value":
                case "z offset value":
                    if (double.TryParse(inputValue.ToString(), out double lengthValue))
                        return ConvertDimensionalValue(lengthValue, "length");
                    break;
                
                // Section dimension parameters - typically in inches
                case "height":
                case "width":
                case "flange thickness":
                case "web thickness":
                case "web fillet":
                case "centroid vertical":
                case "centroid horizontal":
                case "bolt spacing":
                case "bolt diameter":
                case "clear web height":
                case "flange toe of fillet":
                case "web toe of fillet":
                    if (double.TryParse(inputValue.ToString(), out double sectionValue))
                        return ConvertInchesToFeet(sectionValue);
                    break;
                
                // Angular parameters - convert to radians if needed
                case "cross-section rotation":
                case "principal axes angle":
                    if (double.TryParse(inputValue.ToString(), out double angleValue))
                        return ConvertDegreesToRadians(angleValue);
                    break;
                
                // Area parameters - convert square inches to square feet
                case "section area":
                case "shear area strong axis":
                case "shear area weak axis":
                    if (double.TryParse(inputValue.ToString(), out double areaValue))
                        return ConvertSquareInchesToSquareFeet(areaValue);
                    break;
                
                // Moment of inertia parameters - typically in in^4
                case "moment of inertia strong axis":
                case "moment of inertia weak axis":
                case "torsional moment of inertia":
                    // These are typically already in correct units (in^4)
                    if (double.TryParse(inputValue.ToString(), out double momentValue))
                        return momentValue;
                    break;
                
                // Section modulus parameters - typically in in^3
                case "elastic modulus strong axis":
                case "elastic modulus weak axis":
                case "plastic modulus strong axis":
                case "plastic modulus weak axis":
                case "torsional modulus":
                    // These are typically already in correct units (in^3)
                    if (double.TryParse(inputValue.ToString(), out double modulusValue))
                        return modulusValue;
                    break;
                
                // Weight per unit length - typically in lbs/ft
                case "nominal weight":
                    // Typically already in correct units (lbs/ft)
                    if (double.TryParse(inputValue.ToString(), out double weightValue))
                        return weightValue;
                    break;
                
                // Warping constant - typically in in^6
                case "warping constant":
                    // Typically already in correct units (in^6)
                    if (double.TryParse(inputValue.ToString(), out double warpingValue))
                        return warpingValue;
                    break;
                
                // Perimeter - surface area per unit length
                case "perimeter":
                    if (double.TryParse(inputValue.ToString(), out double perimeterValue))
                        return ConvertInchesToFeet(perimeterValue); // Convert from in/ft to ft/ft
                    break;
            }

            return inputValue;
        }

        /// <summary>
        /// Smart unit conversion for dimensional values
        /// </summary>
        private double ConvertDimensionalValue(double value, string parameterType)
        {
            // If value is very small (< 1), assume it's already in feet
            if (value < 1.0)
                return value;
            
            // For typical structural member lengths
            if (parameterType == "length")
            {
                // If value is in reasonable feet range (1-200), use as-is
                if (value >= 1.0 && value <= 200.0)
                    return value;
                
                // If value is in typical inch range (12-2400), convert from inches
                if (value > 200.0 && value <= 2400.0)
                    return ConvertInchesToFeet(value);
                
                // If value is very large, assume millimeters
                if (value > 2400.0)
                    return ConvertMillimetersToFeet(value);
            }
            
            // Default: assume inches and convert to feet
            return ConvertInchesToFeet(value);
        }

        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        private double ConvertDegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Convert square inches to square feet
        /// </summary>
        private double ConvertSquareInchesToSquareFeet(double squareInches)
        {
            return squareInches / 144.0; // 144 square inches = 1 square foot
        }

        public override List<string> GetCommonParameterNames()
        {
            return new List<string>
            {
                // Most commonly used parameters
                "length", "height", "width", "mark", "type name", "family name",
                "structural usage", "structural material", "level", "reference level",
                "start level offset", "end level offset", "elevation at top", "elevation at bottom",
                "cross-section rotation", "y justification", "z justification",
                "section area", "moment of inertia strong axis", "moment of inertia weak axis",
                "nominal weight", "flange thickness", "web thickness", "number of studs"
            };
        }

        public override Dictionary<string, string> GetParameterAliases()
        {
            return new Dictionary<string, string>(_aliases);
        }

        public override bool HasParameter(string parameterName)
        {
            if (string.IsNullOrEmpty(parameterName)) return false;
            
            // Check aliases first
            string actualParamName = _aliases.ContainsKey(parameterName.ToLower()) ? _aliases[parameterName.ToLower()] : parameterName;
            
            // Check if parameter exists in either mapping
            return _parameterMappings.ContainsKey(actualParamName) || 
                   _typeParameterMappings.ContainsKey(actualParamName);
        }
    }
}