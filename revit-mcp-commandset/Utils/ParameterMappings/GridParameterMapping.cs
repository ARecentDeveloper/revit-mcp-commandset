using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Parameter mapping for Grid elements (based on CSV data)
    /// </summary>
    public class GridParameterMapping : ParameterMappingBase
    {
        public override BuiltInCategory Category => BuiltInCategory.OST_Grids;

        private readonly Dictionary<string, BuiltInParameter> _parameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Instance Parameters
            { "name", BuiltInParameter.DATUM_TEXT },
            { "datum text", BuiltInParameter.DATUM_TEXT },
            { "type id", BuiltInParameter.SYMBOL_ID_PARAM },
            { "type", BuiltInParameter.ELEM_TYPE_PARAM },
            { "family name", BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM },
            { "type name", BuiltInParameter.SYMBOL_NAME_PARAM },
            { "family and type", BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM },
            { "family", BuiltInParameter.ELEM_FAMILY_PARAM },
            { "category", BuiltInParameter.ELEM_CATEGORY_PARAM_MT },
            { "design option", BuiltInParameter.DESIGN_OPTION_ID },
            { "export to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT },
            { "scope box", BuiltInParameter.DATUM_VOLUME_OF_INTEREST },
            { "ifcguid", BuiltInParameter.IFC_GUID }
        };

        private readonly Dictionary<string, BuiltInParameter> _typeParameterMappings = 
            new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
        {
            // Type Parameters
            { "end segment color", BuiltInParameter.GRID_END_SEGMENT_COLOR },
            { "end segment pattern", BuiltInParameter.GRID_END_SEGMENT_PATTERN },
            { "end segment weight", BuiltInParameter.GRID_END_SEGMENT_WEIGHT },
            { "symbol", BuiltInParameter.GRID_HEAD_TAG },
            { "center segment", BuiltInParameter.GRID_CENTER_SEGMENT_STYLE },
            { "type ifcguid", BuiltInParameter.IFC_TYPE_GUID },
            { "export type to ifc", BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE },
            { "plan view symbols end 2", BuiltInParameter.GRID_BUBBLE_END_2 },
            { "non-plan view symbols", BuiltInParameter.DATUM_BUBBLE_LOCATION_IN_ELEV },
            { "plan view symbols end 1", BuiltInParameter.GRID_BUBBLE_END_1 }
        };

        private readonly Dictionary<string, string> _aliases = 
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common aliases
            { "grid name", "name" },
            { "text", "name" },
            { "grid text", "name" },
            { "grid type", "type name" },
            { "bubble end 1", "plan view symbols end 1" },
            { "bubble end 2", "plan view symbols end 2" },
            { "elevation bubble", "non-plan view symbols" }
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
                case "export to ifc":
                case "export type to ifc":
                case "bubble end 1":
                case "bubble end 2":
                case "elevation bubble":
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
                // Most commonly used parameters for grids
                "name", "type name", "family name", "design option", 
                "export to ifc", "scope box", "center segment"
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