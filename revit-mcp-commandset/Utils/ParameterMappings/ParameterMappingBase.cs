using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ParameterMappings
{
    /// <summary>
    /// Base class for category-specific parameter mappings
    /// </summary>
    public abstract class ParameterMappingBase
    {
        /// <summary>
        /// Category this mapping applies to
        /// </summary>
        public abstract BuiltInCategory Category { get; }

        /// <summary>
        /// Get parameter from element using category-specific knowledge
        /// </summary>
        public virtual Parameter GetParameter(Element element, string parameterName)
        {
            // First try shared parameters
            var sharedParam = SharedParameterMapping.GetSharedParameter(element, parameterName);
            if (sharedParam != null) return sharedParam;

            // Then try category-specific parameters
            return GetCategorySpecificParameter(element, parameterName);
        }

        /// <summary>
        /// Get category-specific parameter (to be implemented by derived classes)
        /// </summary>
        protected abstract Parameter GetCategorySpecificParameter(Element element, string parameterName);

        /// <summary>
        /// Convert user input value to Revit internal units
        /// </summary>
        public abstract object ConvertValue(string parameterName, object inputValue);

        /// <summary>
        /// Get list of common parameter names for this category
        /// </summary>
        public abstract List<string> GetCommonParameterNames();

        /// <summary>
        /// Get parameter aliases (user-friendly name → actual parameter name)
        /// </summary>
        public abstract Dictionary<string, string> GetParameterAliases();

        /// <summary>
        /// Helper method to try getting built-in parameter
        /// </summary>
        protected Parameter GetBuiltInParameter(Element element, BuiltInParameter builtInParam)
        {
            try
            {
                return element.get_Parameter(builtInParam);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Helper method to try getting built-in parameter from element type
        /// </summary>
        protected Parameter GetBuiltInParameterFromType(Element element, BuiltInParameter builtInParam)
        {
            try
            {
                ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
                return elementType?.get_Parameter(builtInParam);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Helper method to convert inches to feet
        /// </summary>
        protected double ConvertInchesToFeet(double inches)
        {
            return inches / 12.0;
        }

        /// <summary>
        /// Helper method to convert millimeters to feet
        /// </summary>
        protected double ConvertMillimetersToFeet(double millimeters)
        {
            return millimeters / 304.8;
        }
    }
}