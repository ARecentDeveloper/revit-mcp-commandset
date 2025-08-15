using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Level elements (based on CSV data)
    /// </summary>
    public class LevelParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_Levels;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Instance Parameters - Core Level Properties
            { "name", BuiltInParameter.DATUM_TEXT },
            { "elevation", BuiltInParameter.LEVEL_ELEV },
            { "computation height", BuiltInParameter.LEVEL_ROOM_COMPUTATION_HEIGHT },
            { "story above", BuiltInParameter.LEVEL_UP_TO_LEVEL },
            
            // Level Properties
            { "building story", BuiltInParameter.LEVEL_IS_BUILDING_STORY },
            { "structural", BuiltInParameter.LEVEL_IS_STRUCTURAL },
            
            // Identity Data
            { "family and type", BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM },
            { "family", BuiltInParameter.ELEM_FAMILY_PARAM },
            { "type name", BuiltInParameter.SYMBOL_NAME_PARAM },
            { "type id", BuiltInParameter.SYMBOL_ID_PARAM },
            { "type", BuiltInParameter.ELEM_TYPE_PARAM },
            { "family name", BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM },
            { "category", BuiltInParameter.ELEM_CATEGORY_PARAM },
            
            // Design and Phasing
            { "design option", BuiltInParameter.DESIGN_OPTION_ID },
            { "design option param", BuiltInParameter.DESIGN_OPTION_PARAM },
            
            // View and Extents
            { "scope box", BuiltInParameter.DATUM_VOLUME_OF_INTEREST },
            
            // IFC Parameters
            { "export to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT },
            { "ifc guid", BuiltInParameter.IFC_GUID }
        };

        private readonly Dictionary<string, BuiltInParameter> _typeParameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Type Parameters - Graphics and Display
            { "line pattern", BuiltInParameter.LINE_PATTERN },
            { "symbol", BuiltInParameter.LEVEL_HEAD_TAG },
            { "line weight", BuiltInParameter.LINE_PEN },
            { "color", BuiltInParameter.LINE_COLOR },
            
            // Constraints
            { "elevation base", BuiltInParameter.LEVEL_RELATIVE_BASE_TYPE },
            
            // Symbol Display
            { "symbol at end 1 default", BuiltInParameter.DATUM_BUBBLE_END_1 },
            { "symbol at end 2 default", BuiltInParameter.DATUM_BUBBLE_END_2 },
            
            // IFC Type Parameters
            { "type ifc guid", BuiltInParameter.IFC_TYPE_GUID },
            { "export type to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE }
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common aliases for levels
            { "level name", "name" },
            { "level elevation", "elevation" },
            { "elev", "elevation" },
            { "height", "elevation" },
            { "level height", "elevation" },
            
            // Computation height aliases
            { "room computation height", "computation height" },
            { "comp height", "computation height" },
            
            // Story aliases
            { "is building story", "building story" },
            { "building", "building story" },
            { "story", "building story" },
            
            // Structural aliases
            { "is structural", "structural" },
            { "structural level", "structural" },
            
            // Above level aliases
            { "above", "story above" },
            { "next level", "story above" },
            { "upper level", "story above" },
            
            // Graphics aliases
            { "line style", "line pattern" },
            { "pattern", "line pattern" },
            { "weight", "line weight" },
            { "line colour", "color" },
            { "colour", "color" },
            
            // Symbol aliases
            { "head symbol", "symbol" },
            { "level head", "symbol" },
            { "bubble", "symbol" },
            { "tag", "symbol" },
            
            // Scope aliases
            { "scope", "scope box" },
            { "crop box", "scope box" }
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
            
            // Convert based on parameter type
            switch (actualParamName.ToLower())
            {
                // Elevation parameters - convert to feet
                case "elevation":
                case "computation height":
                    if (double.TryParse(inputValue.ToString(), out double elevationValue))
                        return ConvertDimensionalValue(elevationValue, "elevation");
                    break;
                
                // Boolean parameters (stored as integers in Revit)
                case "building story":
                case "structural":
                case "export to ifc":
                case "export type to ifc":
                case "symbol at end 1 default":
                case "symbol at end 2 default":
                    if (bool.TryParse(inputValue.ToString(), out bool boolValue))
                        return boolValue ? 1 : 0;
                    if (int.TryParse(inputValue.ToString(), out int intValue))
                        return intValue;
                    break;
                
                // String parameters
                case "name":
                case "ifc guid":
                case "type ifc guid":
                case "design option param":
                    return inputValue.ToString();
                
                // Integer parameters
                case "line weight":
                case "color":
                case "elevation base":
                    if (int.TryParse(inputValue.ToString(), out int integerValue))
                        return integerValue;
                    break;
                
                // ElementId parameters - these are typically handled by Revit internally
                case "story above":
                case "scope box":
                case "line pattern":
                case "symbol":
                case "design option":
                    // For ElementId parameters, we might need special handling
                    // For now, return as-is and let Revit handle the conversion
                    return inputValue;
            }

            return inputValue;
        }

        /// <summary>
        /// Smart unit conversion for elevation values
        /// </summary>
        private double ConvertDimensionalValue(double value, string parameterType)
        {
            // If value is very small (< 1), assume it's already in feet
            if (value < 1.0)
                return value;
            
            // For elevation values
            if (parameterType == "elevation")
            {
                // If value is in reasonable feet range (-100 to 1000), use as-is
                if (value >= -100.0 && value <= 1000.0)
                    return value;
                
                // If value is in typical inch range (-1200 to 12000), convert from inches
                if (value > 1000.0 && value <= 12000.0)
                    return ConvertInchesToFeet(value);
                
                // If value is very large, assume millimeters
                if (value > 12000.0)
                    return ConvertMillimetersToFeet(value);
                
                // If value is very negative, assume millimeters
                if (value < -1200.0)
                    return ConvertMillimetersToFeet(value);
            }
            
            // Default: assume inches and convert to feet
            return ConvertInchesToFeet(value);
        }

        public override List<string> GetCommonParameterNames()
        {
            return new List<string>
            {
                // Most commonly used level parameters
                "name", "elevation", "building story", "structural", 
                "computation height", "story above", "scope box",
                "line pattern", "symbol", "line weight", "color"
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