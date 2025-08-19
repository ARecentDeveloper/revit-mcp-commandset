using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models;
using RevitMCPCommandSet.Utils.ParameterMappings;

namespace RevitMCPCommandSet.Utils
{
    /// <summary>
    /// Shared utility for filtering Revit elements across all commands
    /// </summary>
    public static class ElementFilterUtility
    {
        // Category mappings for flexible category resolution
        private static readonly Dictionary<string, BuiltInCategory> CategoryMappings = 
            new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase)
        {
            // Architectural
            { "walls", BuiltInCategory.OST_Walls },
            { "wall", BuiltInCategory.OST_Walls },
            { "OST_Walls", BuiltInCategory.OST_Walls },
            
            { "doors", BuiltInCategory.OST_Doors },
            { "door", BuiltInCategory.OST_Doors },
            { "OST_Doors", BuiltInCategory.OST_Doors },
            
            { "windows", BuiltInCategory.OST_Windows },
            { "window", BuiltInCategory.OST_Windows },
            { "OST_Windows", BuiltInCategory.OST_Windows },
            
            { "floors", BuiltInCategory.OST_Floors },
            { "floor", BuiltInCategory.OST_Floors },
            { "flooring", BuiltInCategory.OST_Floors },
            { "slab", BuiltInCategory.OST_Floors },
            { "slabs", BuiltInCategory.OST_Floors },
            { "OST_Floors", BuiltInCategory.OST_Floors },
            
            { "ceilings", BuiltInCategory.OST_Ceilings },
            { "ceiling", BuiltInCategory.OST_Ceilings },
            { "OST_Ceilings", BuiltInCategory.OST_Ceilings },
            
            { "roofs", BuiltInCategory.OST_Roofs },
            { "roof", BuiltInCategory.OST_Roofs },
            { "OST_Roofs", BuiltInCategory.OST_Roofs },
            
            // Structural
            { "columns", BuiltInCategory.OST_Columns },
            { "column", BuiltInCategory.OST_Columns },
            { "OST_Columns", BuiltInCategory.OST_Columns },
            
            { "beams", BuiltInCategory.OST_StructuralFraming },
            { "beam", BuiltInCategory.OST_StructuralFraming },
            { "structural framing", BuiltInCategory.OST_StructuralFraming },
            { "OST_StructuralFraming", BuiltInCategory.OST_StructuralFraming },
            
            { "levels", BuiltInCategory.OST_Levels },
            { "level", BuiltInCategory.OST_Levels },
            { "OST_Levels", BuiltInCategory.OST_Levels },
            
            // MEP - Electrical
            { "conduits", BuiltInCategory.OST_Conduit },
            { "conduit", BuiltInCategory.OST_Conduit },
            { "OST_Conduit", BuiltInCategory.OST_Conduit },
            
            { "conduit fittings", BuiltInCategory.OST_ConduitFitting },
            { "conduit fitting", BuiltInCategory.OST_ConduitFitting },
            { "OST_ConduitFitting", BuiltInCategory.OST_ConduitFitting },
            
            { "cable trays", BuiltInCategory.OST_CableTray },
            { "cable tray", BuiltInCategory.OST_CableTray },
            { "OST_CableTray", BuiltInCategory.OST_CableTray },
            
            // MEP - Mechanical
            { "ducts", BuiltInCategory.OST_DuctCurves },
            { "duct", BuiltInCategory.OST_DuctCurves },
            { "OST_DuctCurves", BuiltInCategory.OST_DuctCurves },
            
            { "duct fittings", BuiltInCategory.OST_DuctFitting },
            { "duct fitting", BuiltInCategory.OST_DuctFitting },
            { "OST_DuctFitting", BuiltInCategory.OST_DuctFitting },
            
            // MEP - Plumbing
            { "pipes", BuiltInCategory.OST_PipeCurves },
            { "pipe", BuiltInCategory.OST_PipeCurves },
            { "OST_PipeCurves", BuiltInCategory.OST_PipeCurves },
            
            { "pipe fittings", BuiltInCategory.OST_PipeFitting },
            { "pipe fitting", BuiltInCategory.OST_PipeFitting },
            { "OST_PipeFitting", BuiltInCategory.OST_PipeFitting },
            
            // Equipment
            { "mechanical equipment", BuiltInCategory.OST_MechanicalEquipment },
            { "OST_MechanicalEquipment", BuiltInCategory.OST_MechanicalEquipment },
            
            { "electrical equipment", BuiltInCategory.OST_ElectricalEquipment },
            { "OST_ElectricalEquipment", BuiltInCategory.OST_ElectricalEquipment },
            
            { "plumbing fixtures", BuiltInCategory.OST_PlumbingFixtures },
            { "plumbing fixture", BuiltInCategory.OST_PlumbingFixtures },
            { "OST_PlumbingFixtures", BuiltInCategory.OST_PlumbingFixtures }
        };

        /// <summary>
        /// Main filtering method - filters elements based on criteria
        /// </summary>
        public static FilterResult FilterElements(Document doc, View view, FilterCriteria criteria)
        {
            try
            {
                var result = new FilterResult();
                
                switch (criteria.Scope)
                {
                    case FilterScope.All:
                        result.Elements = GetAllModelElements(doc, view);
                        break;
                        
                    case FilterScope.Category:
                        result.Elements = GetElementsByCategory(doc, view, criteria.Category);
                        break;
                        
                    case FilterScope.Parameter:
                        result.Elements = GetElementsByParameter(doc, view, criteria.Category, criteria.Parameter);
                        break;
                        
                    case FilterScope.Selected:
                        result.Elements = ConvertToElementIds(criteria.ElementIds);
                        break;
                        
                    default:
                        result.Success = false;
                        result.Message = $"Unknown filter scope: {criteria.Scope}";
                        return result;
                }

                result.ProcessedCount = result.Elements.Count;
                result.Message = $"Found {result.ProcessedCount} elements matching criteria";
                
                return result;
            }
            catch (Exception ex)
            {
                return new FilterResult
                {
                    Success = false,
                    Message = $"Error filtering elements: {ex.Message}",
                    ErrorCount = 1
                };
            }
        }

        /// <summary>
        /// Get all model elements in view (CategoryType.Model only)
        /// </summary>
        private static List<ElementId> GetAllModelElements(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
                .Select(e => e.Id)
                .ToList();
        }

        /// <summary>
        /// Get elements by category with flexible category name resolution
        /// </summary>
        private static List<ElementId> GetElementsByCategory(Document doc, View view, string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName))
                return new List<ElementId>();

            var builtInCategory = ResolveCategory(categoryName);
            if (builtInCategory == null)
            {
                throw new ArgumentException($"Unsupported or unknown category: '{categoryName}'. " +
                    $"Supported categories include: {string.Join(", ", GetSupportedCategoryNames().Take(10))}...");
            }

            return new FilteredElementCollector(doc, view.Id)
                .OfCategory(builtInCategory.Value)
                .WhereElementIsNotElementType()
                .ToElementIds()
                .ToList();
        }

        /// <summary>
        /// Get elements by parameter filter
        /// </summary>
        private static List<ElementId> GetElementsByParameter(Document doc, View view, string categoryName, ParameterFilter paramFilter)
        {
            if (paramFilter == null)
                return new List<ElementId>();

            // First get elements by category
            var categoryElements = GetElementsByCategory(doc, view, categoryName);
            if (categoryElements.Count == 0)
                return new List<ElementId>();

            var filteredElements = new List<ElementId>();

            foreach (var elementId in categoryElements)
            {
                Element element = doc.GetElement(elementId);
                if (element == null) continue;

                if (ElementMatchesParameterFilter(element, paramFilter, categoryName))
                {
                    filteredElements.Add(elementId);
                }
            }

            return filteredElements;
        }

        /// <summary>
        /// Check if element matches parameter filter
        /// </summary>
        private static bool ElementMatchesParameterFilter(Element element, ParameterFilter paramFilter, string categoryName)
        {
            Parameter param = GetParameterFromElement(element, paramFilter.Name, categoryName);
            if (param == null || !param.HasValue) 
                return false;

            return EvaluateParameterCondition(param, paramFilter, categoryName);
        }

        /// <summary>
        /// Get parameter from element using category-specific mappings
        /// </summary>
        private static Parameter GetParameterFromElement(Element element, string paramName, string categoryName)
        {
            // Get the built-in category
            var builtInCategory = ResolveCategory(categoryName);
            if (builtInCategory.HasValue)
            {
                // Use parameter mapping manager for category-specific parameter handling
                return ParameterMappingManager.GetParameter(element, paramName, builtInCategory.Value);
            }

            // Fallback to generic lookup
            Parameter param = element.LookupParameter(paramName);
            if (param != null) return param;

            // Try type parameter
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
            return elementType?.LookupParameter(paramName);
        }

        /// <summary>
        /// Evaluate parameter condition based on operator and value
        /// </summary>
        private static bool EvaluateParameterCondition(Parameter param, ParameterFilter filter, string categoryName)
        {
            try
            {
                // Convert value using category-specific knowledge
                var builtInCategory = ResolveCategory(categoryName);
                object convertedValue = filter.Value;
                
                if (builtInCategory.HasValue)
                {
                    convertedValue = ParameterMappingManager.ConvertValue(filter.Name, filter.Value, builtInCategory.Value);
                }

                switch (filter.ValueType)
                {
                    case ParameterValueType.Double:
                        return EvaluateNumericCondition(param.AsDouble(), filter.Operator, Convert.ToDouble(convertedValue));
                        
                    case ParameterValueType.Integer:
                        return EvaluateNumericCondition(param.AsInteger(), filter.Operator, Convert.ToInt32(convertedValue));
                        
                    case ParameterValueType.String:
                        return EvaluateStringCondition(param.AsString(), filter.Operator, convertedValue?.ToString());
                        
                    case ParameterValueType.Boolean:
                        return EvaluateBooleanCondition(param.AsInteger() != 0, filter.Operator, Convert.ToBoolean(convertedValue));
                        
                    default:
                        // Default to double for backward compatibility with unit conversion
                        return EvaluateNumericCondition(param.AsDouble(), filter.Operator, Convert.ToDouble(convertedValue));
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Evaluate numeric conditions
        /// </summary>
        private static bool EvaluateNumericCondition(double elementValue, string operatorType, double filterValue)
        {
            switch (operatorType)
            {
                case ">": return elementValue > filterValue;
                case "<": return elementValue < filterValue;
                case ">=": return elementValue >= filterValue;
                case "<=": return elementValue <= filterValue;
                case "=":
                case "==": return Math.Abs(elementValue - filterValue) < 0.001;
                case "!=": return Math.Abs(elementValue - filterValue) >= 0.001;
                default: return false;
            }
        }

        /// <summary>
        /// Evaluate string conditions
        /// </summary>
        private static bool EvaluateStringCondition(string elementValue, string operatorType, string filterValue)
        {
            if (elementValue == null) elementValue = "";
            if (filterValue == null) filterValue = "";

            switch (operatorType.ToLower())
            {
                case "=":
                case "==": return elementValue.Equals(filterValue, StringComparison.OrdinalIgnoreCase);
                case "!=": return !elementValue.Equals(filterValue, StringComparison.OrdinalIgnoreCase);
                case "contains": return elementValue.IndexOf(filterValue, StringComparison.OrdinalIgnoreCase) >= 0;
                case "startswith": return elementValue.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase);
                case "endswith": return elementValue.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase);
                default: return false;
            }
        }

        /// <summary>
        /// Evaluate boolean conditions
        /// </summary>
        private static bool EvaluateBooleanCondition(bool elementValue, string operatorType, bool filterValue)
        {
            switch (operatorType)
            {
                case "=":
                case "==": return elementValue == filterValue;
                case "!=": return elementValue != filterValue;
                default: return false;
            }
        }

        /// <summary>
        /// Convert long IDs to ElementIds
        /// </summary>
        private static List<ElementId> ConvertToElementIds(List<long> longIds)
        {
            if (longIds == null) return new List<ElementId>();
            return longIds.Select(id => new ElementId(id)).ToList();
        }

        /// <summary>
        /// Resolve category name to BuiltInCategory
        /// </summary>
        public static BuiltInCategory? ResolveCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return null;
            
            return CategoryMappings.TryGetValue(categoryName, out var category) ? category : (BuiltInCategory?)null;
        }

        /// <summary>
        /// Get list of supported category names for error messages
        /// </summary>
        public static List<string> GetSupportedCategoryNames()
        {
            return CategoryMappings.Keys.ToList();
        }

        /// <summary>
        /// Get category display name from BuiltInCategory
        /// </summary>
        public static string GetCategoryDisplayName(BuiltInCategory category)
        {
            var mapping = CategoryMappings.FirstOrDefault(kvp => kvp.Value == category);
            return mapping.Key ?? category.ToString();
        }
    }
}