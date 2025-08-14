using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.ElementInfos;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Utils
{
    /// <summary>
    /// Utility class for parameter operations
    /// </summary>
    public static class ParameterUtility
    {
        /// <summary>
        /// Get the names and values of all non-empty parameters in the element
        /// </summary>
        /// <param name="element">Revit element</param>
        /// <returns>List of parameter information</returns>
        public static List<ParameterInfo> GetDimensionParameters(Element element)
        {
            // Check if the element is null
            if (element == null)
            {
                return new List<ParameterInfo>();
            }

            var parameters = new List<ParameterInfo>();

            // Get all parameters of the element
            foreach (Parameter param in element.Parameters)
            {
                try
                {
                    // Skip invalid parameters
                    if (!param.HasValue || param.IsReadOnly)
                    {
                        continue;
                    }

                    // If the current parameter is a dimension-related parameter
                    if (IsDimensionParameter(param))
                    {
                        // Get the string representation of the parameter value
                        string value = param.AsValueString();

                        // If the value is not empty, add it to the list
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            parameters.Add(new ParameterInfo
                            {
                                Name = param.Definition.Name,
                                Value = value
                            });
                        }
                    }
                }
                catch
                {
                    // If there is an error in getting a parameter value, continue to the next one
                    continue;
                }
            }

            // Return after sorting by parameter name
            return parameters.OrderBy(p => p.Name).ToList();
        }

        /// <summary>
        /// Determine if the parameter is a writable dimension parameter
        /// </summary>
        public static bool IsDimensionParameter(Parameter param)
        {

#if REVIT2023_OR_GREATER
            // In Revit 2023, use the Definition's GetDataType() method to get the parameter type
            ForgeTypeId paramTypeId = param.Definition.GetDataType();

            // Determine if the parameter is a dimension-related type
            bool isDimensionType = paramTypeId.Equals(SpecTypeId.Length) ||
                                   paramTypeId.Equals(SpecTypeId.Angle) ||
                                   paramTypeId.Equals(SpecTypeId.Area) ||
                                   paramTypeId.Equals(SpecTypeId.Volume);
            // Only store dimension type parameters
            return isDimensionType;
#else
            // Determine if the parameter is a dimension-related type
            bool isDimensionType = param.Definition.ParameterType == ParameterType.Length ||
                                   param.Definition.ParameterType == ParameterType.Angle ||
                                   param.Definition.ParameterType == ParameterType.Area ||
                                   param.Definition.ParameterType == ParameterType.Volume;

            // Only store dimension type parameters
            return isDimensionType;
#endif
        }
    }
} 