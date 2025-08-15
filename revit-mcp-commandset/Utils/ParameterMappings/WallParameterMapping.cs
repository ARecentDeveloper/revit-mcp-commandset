using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Wall elements (simplified with confirmed parameters only)
    /// </summary>
    public class WallParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_Walls;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Dimensional parameters (confirmed from CSV)
            { "width", BuiltInParameter.WALL_ATTR_WIDTH_PARAM },
            { "length", BuiltInParameter.CURVE_ELEM_LENGTH },
            { "thickness", BuiltInParameter.WALL_ATTR_WIDTH_PARAM }, // Same as width for walls
            
            // Level and positioning (confirmed from CSV)
            { "base constraint", BuiltInParameter.WALL_BASE_CONSTRAINT },
            { "top constraint", BuiltInParameter.WALL_HEIGHT_TYPE },
            { "base offset", BuiltInParameter.WALL_BASE_OFFSET },
            { "top offset", BuiltInParameter.WALL_TOP_OFFSET },
            { "unconnected height", BuiltInParameter.WALL_USER_HEIGHT_PARAM },
            { "base is attached", BuiltInParameter.WALL_BOTTOM_IS_ATTACHED },
            { "top is attached", BuiltInParameter.WALL_TOP_IS_ATTACHED },
            { "top extension distance", BuiltInParameter.WALL_TOP_EXTENSION_DIST_PARAM },
            { "base extension distance", BuiltInParameter.WALL_BOTTOM_EXTENSION_DIST_PARAM },
            
            // Structural properties (confirmed from CSV)
            { "structural", BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT },
            { "structural usage", BuiltInParameter.WALL_STRUCTURAL_USAGE_PARAM },
            
            // Material and construction (confirmed from CSV)
            { "function", BuiltInParameter.FUNCTION_PARAM },
            { "wrapping at inserts", BuiltInParameter.WRAPPING_AT_INSERTS_PARAM },
            { "wrapping at ends", BuiltInParameter.WRAPPING_AT_ENDS_PARAM },
            
            // Constraints (confirmed from CSV)
            { "room bounding", BuiltInParameter.WALL_ATTR_ROOM_BOUNDING },
            { "location line", BuiltInParameter.WALL_KEY_REF_PARAM },
            { "related to mass", BuiltInParameter.RELATED_TO_MASS },
            
            // Analytical properties (confirmed from CSV)
            { "thermal resistance", BuiltInParameter.ANALYTICAL_THERMAL_RESISTANCE },
            { "heat transfer coefficient", BuiltInParameter.ANALYTICAL_HEAT_TRANSFER_COEFFICIENT },
            { "thermal mass", BuiltInParameter.ANALYTICAL_THERMAL_MASS },
            { "roughness", BuiltInParameter.ANALYTICAL_ROUGHNESS },
            { "absorptance", BuiltInParameter.ANALYTICAL_ABSORPTANCE },
            
            // Graphics (confirmed from CSV)
            { "coarse scale fill pattern", BuiltInParameter.COARSE_SCALE_FILL_PATTERN_ID_PARAM },
            { "coarse scale fill color", BuiltInParameter.COARSE_SCALE_FILL_PATTERN_COLOR }
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "w", "width" },
            { "wall width", "width" },
            { "wall thickness", "thickness" },
            { "wall length", "length" },
            { "base level", "base constraint" },
            { "top level", "top constraint" },
            { "height", "unconnected height" },
            { "is structural", "structural" },
            { "u-value", "heat transfer coefficient" },
            { "r-value", "thermal resistance" }
        };

        protected override Parameter GetCategorySpecificParameter(Element element, string parameterName)
        {
            // Check aliases first
            string actualParamName = _aliases.ContainsKey(parameterName) ? _aliases[parameterName] : parameterName;
            
            // Try built-in parameter mapping
            if (_parameterMappings.TryGetValue(actualParamName, out var builtInParam))
            {
                var param = GetBuiltInParameter(element, builtInParam);
                if (param != null) return param;
                
                // Try type parameter
                param = GetBuiltInParameterFromType(element, builtInParam);
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
            
            // Convert based on parameter type
            switch (actualParamName.ToLower())
            {
                case "width":
                case "thickness":
                case "length":
                case "base offset":
                case "top offset":
                case "unconnected height":
                case "top extension distance":
                case "base extension distance":
                    // Convert inches to feet for dimensional parameters
                    if (double.TryParse(inputValue.ToString(), out double inches))
                        return ConvertInchesToFeet(inches);
                    break;
            }

            return inputValue;
        }

        public override List<string> GetCommonParameterNames()
        {
            return new List<string>
            {
                "width", "thickness", "length", "base constraint", "top constraint",
                "base offset", "top offset", "unconnected height", "structural", 
                "function", "room bounding", "location line"
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
            
            // Check if parameter exists in mapping
            return _parameterMappings.ContainsKey(actualParamName);
        }
    }
}