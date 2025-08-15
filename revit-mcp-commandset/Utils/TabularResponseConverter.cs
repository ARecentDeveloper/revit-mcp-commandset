using System;
using System.Collections.Generic;
using System.Linq;
using RevitMCPCommandSet.Models.ElementInfos;

namespace RevitMCPCommandSet.Utils
{
    /// <summary>
    /// Utility for converting standard element responses to tabular format
    /// Optimizes responses by grouping elements by parameter values
    /// </summary>
    public static class TabularResponseConverter
    {
        /// <summary>
        /// Convert a list of ElementMinimalInfo to tabular format
        /// </summary>
        public static TabularElementResponse ConvertToTabular(List<ElementMinimalInfo> elements)
        {
            if (elements == null || elements.Count == 0)
            {
                return new TabularElementResponse();
            }

            var result = new TabularElementResponse();
            
            // Collect all element IDs
            result.Elements = elements.Select(e => e.Id).ToList();
            
            // Find common properties and individual properties
            var (commonProperties, elementProperties) = AnalyzeElementProperties(elements);
            result.CommonProperties = commonProperties;
            result.ElementProperties = elementProperties;
            
            // Group parameters by values
            result.Parameters = GroupParametersByValues(elements);
            
            return result;
        }

        /// <summary>
        /// Analyze element properties to determine what's common vs individual
        /// </summary>
        private static (Dictionary<string, object> commonProperties, Dictionary<string, Dictionary<int, object>> elementProperties) 
            AnalyzeElementProperties(List<ElementMinimalInfo> elements)
        {
            var commonProperties = new Dictionary<string, object>();
            var elementProperties = new Dictionary<string, Dictionary<int, object>>();
            
            if (elements.Count == 0) 
                return (commonProperties, elementProperties);
            
            if (elements.Count == 1)
            {
                // Single element - put name in common properties
                var element = elements.First();
                if (!string.IsNullOrEmpty(element.Name))
                {
                    commonProperties["name"] = element.Name;
                }
                return (commonProperties, elementProperties);
            }
            
            // Multiple elements - analyze name distribution
            var nameGroups = elements
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .GroupBy(e => e.Name)
                .ToList();
            
            if (nameGroups.Count == 1)
            {
                // All elements have the same name - put in common properties
                commonProperties["name"] = nameGroups.First().Key;
            }
            else if (nameGroups.Count > 1)
            {
                // Elements have different names - put in element properties
                elementProperties["name"] = new Dictionary<int, object>();
                foreach (var element in elements)
                {
                    if (!string.IsNullOrEmpty(element.Name))
                    {
                        elementProperties["name"][element.Id] = element.Name;
                    }
                }
            }
            
            // Could add more property analysis here (category, family, etc.)
            
            return (commonProperties, elementProperties);
        }

        /// <summary>
        /// Group parameters by their values across all elements
        /// </summary>
        private static Dictionary<string, TabularParameterGroup> GroupParametersByValues(List<ElementMinimalInfo> elements)
        {
            var parameterGroups = new Dictionary<string, TabularParameterGroup>();
            
            // Collect all unique parameter names
            var allParameterNames = elements
                .SelectMany(e => e.Parameters)
                .Select(p => p.Name)
                .Distinct()
                .ToList();
            
            foreach (var paramName in allParameterNames)
            {
                var group = new TabularParameterGroup();
                
                foreach (var element in elements)
                {
                    var parameter = element.Parameters.FirstOrDefault(p => p.Name == paramName);
                    
                    if (parameter == null)
                    {
                        // Parameter not found for this element
                        AddToGroup(group, "Parameter not found", element.Id, null, "Parameter not found");
                    }
                    else if (!string.IsNullOrEmpty(parameter.EmptyReason))
                    {
                        // Parameter is empty
                        AddToGroup(group, "Empty", element.Id, parameter.RawValue, parameter.EmptyReason);
                    }
                    else
                    {
                        // Parameter has a value - use display value if available, otherwise raw value
                        string displayValue = GetDisplayValue(parameter);
                        AddToGroup(group, displayValue, element.Id, parameter.RawValue, null);
                    }
                }
                
                parameterGroups[paramName] = group;
            }
            
            return parameterGroups;
        }

        /// <summary>
        /// Get the best display value for a parameter
        /// </summary>
        private static string GetDisplayValue(ParameterInfo parameter)
        {
            // Priority: AsValueString > Value > RawValue > "null"
            if (!string.IsNullOrEmpty(parameter.AsValueString))
                return parameter.AsValueString;
            
            if (!string.IsNullOrEmpty(parameter.Value))
                return parameter.Value;
            
            if (parameter.RawValue.HasValue)
                return parameter.RawValue.Value.ToString("G");
            
            return "null";
        }

        /// <summary>
        /// Add an element to a parameter group
        /// </summary>
        private static void AddToGroup(TabularParameterGroup group, string displayValue, int elementId, double? rawValue, string emptyReason)
        {
            // Add to values grouping
            if (!group.Values.ContainsKey(displayValue))
            {
                group.Values[displayValue] = new List<int>();
            }
            group.Values[displayValue].Add(elementId);
            
            // Add raw value if present
            if (rawValue.HasValue)
            {
                group.RawValues[elementId] = rawValue.Value;
            }
            
            // Add empty reason if present
            if (!string.IsNullOrEmpty(emptyReason))
            {
                group.EmptyReasons[elementId] = emptyReason;
            }
        }

        /// <summary>
        /// Convert a list of ElementBasicInfo to tabular format
        /// </summary>
        public static TabularElementResponse ConvertToTabular(List<ElementBasicInfo> elements)
        {
            // Convert to ElementMinimalInfo first, then use existing logic
            var minimalElements = elements.Select(e => new ElementMinimalInfo
            {
                Id = e.Id,
                Name = e.Name,
                Parameters = e.Parameters
            }).ToList();
            
            return ConvertToTabular(minimalElements);
        }

        /// <summary>
        /// Convert a list of ElementInstanceInfo to tabular format
        /// </summary>
        public static TabularElementResponse ConvertToTabular(List<ElementInstanceInfo> elements)
        {
            // Convert to ElementMinimalInfo first, then use existing logic
            var minimalElements = elements.Select(e => new ElementMinimalInfo
            {
                Id = e.Id,
                Name = e.Name,
                Parameters = e.Parameters
            }).ToList();
            
            var result = ConvertToTabular(minimalElements);
            
            // Analyze additional properties from ElementInstanceInfo
            if (elements.Count > 0)
            {
                // Analyze category
                var categoryGroups = elements
                    .Where(e => !string.IsNullOrEmpty(e.Category))
                    .GroupBy(e => e.Category)
                    .ToList();
                
                if (categoryGroups.Count == 1)
                {
                    result.CommonProperties["category"] = categoryGroups.First().Key;
                }
                else if (categoryGroups.Count > 1)
                {
                    if (result.ElementProperties == null)
                        result.ElementProperties = new Dictionary<string, Dictionary<int, object>>();
                    
                    result.ElementProperties["category"] = new Dictionary<int, object>();
                    foreach (var element in elements.Where(e => !string.IsNullOrEmpty(e.Category)))
                    {
                        result.ElementProperties["category"][element.Id] = element.Category;
                    }
                }
                
                // Analyze family name
                var familyGroups = elements
                    .Where(e => !string.IsNullOrEmpty(e.FamilyName))
                    .GroupBy(e => e.FamilyName)
                    .ToList();
                
                if (familyGroups.Count == 1)
                {
                    result.CommonProperties["familyName"] = familyGroups.First().Key;
                }
                else if (familyGroups.Count > 1)
                {
                    if (result.ElementProperties == null)
                        result.ElementProperties = new Dictionary<string, Dictionary<int, object>>();
                    
                    result.ElementProperties["familyName"] = new Dictionary<int, object>();
                    foreach (var element in elements.Where(e => !string.IsNullOrEmpty(e.FamilyName)))
                    {
                        result.ElementProperties["familyName"][element.Id] = element.FamilyName;
                    }
                }
            }
            
            return result;
        }
    }
}