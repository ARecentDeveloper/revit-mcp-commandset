using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Conduit elements (simplified with confirmed parameters only)
    /// </summary>
    public class ConduitParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_Conduit;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Dimensional parameters (confirmed from CSV)
            { "diameter", BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM },
            { "diameter(trade size)", BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM },
            { "outside diameter", BuiltInParameter.RBS_CONDUIT_OUTER_DIAM_PARAM },
            { "inside diameter", BuiltInParameter.RBS_CONDUIT_INNER_DIAM_PARAM },
            { "length", BuiltInParameter.CURVE_ELEM_LENGTH },
            { "size", BuiltInParameter.RBS_CALCULATED_SIZE },
            
            // Elevation and positioning (confirmed from CSV)
            { "middle elevation", BuiltInParameter.RBS_OFFSET_PARAM },
            { "start middle elevation", BuiltInParameter.RBS_START_OFFSET_PARAM },
            { "end middle elevation", BuiltInParameter.RBS_END_OFFSET_PARAM },
            { "horizontal justification", BuiltInParameter.RBS_CURVE_HOR_OFFSET_PARAM },
            { "vertical justification", BuiltInParameter.RBS_CURVE_VERT_OFFSET_PARAM },
            
            // Elevation calculations (confirmed from CSV)
            { "upper end top elevation", BuiltInParameter.RBS_CTC_TOP_ELEVATION },
            { "lower end bottom elevation", BuiltInParameter.RBS_CTC_BOTTOM_ELEVATION },
            
            // Spot elevations (confirmed from CSV)
            { "spot top elevation", BuiltInParameter.FABRICATION_SPOT_TOP_ELEVATION_OF_PART },
            { "spot bottom elevation", BuiltInParameter.FABRICATION_SPOT_BOTTOM_ELEVATION_OF_PART },
            
            // Service and system (confirmed from CSV)
            { "service type", BuiltInParameter.RBS_CTC_SERVICE_TYPE },
            
            // Fittings (Type level) (confirmed from CSV)
            { "bend", BuiltInParameter.RBS_CURVETYPE_DEFAULT_BEND_PARAM },
            { "union", BuiltInParameter.RBS_CURVETYPE_DEFAULT_UNION_PARAM },
            { "tee", BuiltInParameter.RBS_CURVETYPE_DEFAULT_TEE_PARAM },
            { "transition", BuiltInParameter.RBS_CURVETYPE_DEFAULT_TRANSITION_PARAM },
            { "cross", BuiltInParameter.RBS_CURVETYPE_DEFAULT_CROSS_PARAM },
            
            // Electrical (confirmed from CSV)
            { "standard", BuiltInParameter.CONDUIT_STANDARD_TYPE_PARAM }
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "diameter", "diameter(trade size)" },
            { "dia", "diameter(trade size)" },
            { "d", "diameter(trade size)" },
            { "trade size", "diameter(trade size)" },
            { "conduit diameter", "diameter(trade size)" },
            { "outer diameter", "outside diameter" },
            { "inner diameter", "inside diameter" },
            { "len", "length" },
            { "conduit length", "length" },
            { "elevation", "middle elevation" },
            { "offset", "middle elevation" },
            { "level", "reference level" },
            { "service", "service type" }
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
            
            // Smart unit conversion - detect if value is already in feet or needs conversion
            double numericValue;
            if (!double.TryParse(inputValue.ToString(), out numericValue))
                return inputValue;
            
            // Convert based on parameter type
            switch (actualParamName.ToLower())
            {
                case "diameter":
                case "diameter(trade size)":
                case "outside diameter":
                case "inside diameter":
                    // Smart conversion for diameter values
                    // If value is very small (< 1), assume it's already in feet
                    // If value is reasonable for inches (1-48), convert from inches to feet
                    // If value is very large (> 100), assume millimeters and convert
                    return ConvertDimensionalValue(numericValue, "diameter");
                    
                case "length":
                case "middle elevation":
                case "start middle elevation":
                case "end middle elevation":
                case "upper end top elevation":
                case "lower end bottom elevation":
                case "spot top elevation":
                case "spot bottom elevation":
                    // Smart conversion for length/elevation values
                    return ConvertDimensionalValue(numericValue, "length");
            }

            return inputValue;
        }

        /// <summary>
        /// Smart unit conversion that tries to detect the input unit and convert appropriately
        /// </summary>
        private double ConvertDimensionalValue(double value, string parameterType)
        {
            // If value is very small (< 1), assume it's already in feet
            if (value < 1.0)
                return value;
            
            // For diameter parameters
            if (parameterType == "diameter")
            {
                // If value is in typical conduit size range (0.5" to 6"), convert from inches
                if (value >= 0.5 && value <= 48.0)
                    return ConvertInchesToFeet(value);
                
                // If value is very large (> 100), assume millimeters
                if (value > 100.0)
                    return ConvertMillimetersToFeet(value);
            }
            
            // For length parameters
            if (parameterType == "length")
            {
                // If value is in reasonable feet range (already converted), use as-is
                if (value < 1.0)
                    return value;
                
                // If value is in typical inch range (1-1200), convert from inches
                if (value >= 1.0 && value <= 1200.0)
                    return ConvertInchesToFeet(value);
                
                // If value is very large, assume millimeters
                if (value > 1200.0)
                    return ConvertMillimetersToFeet(value);
            }
            
            // Default: assume inches and convert to feet
            return ConvertInchesToFeet(value);
        }

        public override List<string> GetCommonParameterNames()
        {
            return new List<string>
            {
                "diameter", "outside diameter", "inside diameter", "length", "size",
                "middle elevation", "horizontal justification", "vertical justification",
                "service type", "standard"
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