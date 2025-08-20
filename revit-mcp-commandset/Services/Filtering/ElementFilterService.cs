using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils.ParameterMappings;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Services.Filtering
{
    /// <summary>
    /// Service for filtering elements in Revit documents
    /// </summary>
    public class ElementFilterService
    {
        /// <summary>
        /// Get elements that meet the conditions in the Revit document according to the filter settings, supporting multi-condition combined filtering
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="settings">Filter settings</param>
        /// <returns>A collection of elements that meet all filter conditions</returns>
        public static IList<Element> GetFilteredElements(Document doc, FilterSetting settings)
        {
            DebugLogger.LogSeparator();
            DebugLogger.Log("FILTER", $"Starting GetFilteredElements with category: {settings?.FilterCategory ?? "null"}");
            
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            
            DebugLogger.Log("FILTER", $"Settings - IncludeTypes: {settings.IncludeTypes}, IncludeInstances: {settings.IncludeInstances}");
            
            // Validate filter settings
            if (!settings.Validate(out string errorMessage))
            {
                DebugLogger.Log("FILTER", $"Invalid filter settings: {errorMessage}");
                return new List<Element>();
            }
            DebugLogger.Log("FILTER", "Filter settings validation passed");
            // Record the application of filter conditions
            List<string> appliedFilters = new List<string>();
            List<Element> result = new List<Element>();
            // If both types and instances are included, they need to be filtered separately and then the results merged
            if (settings.IncludeTypes && settings.IncludeInstances)
            {
                DebugLogger.Log("FILTER", "Collecting both types and instances");
                // Collect type elements
                var typeElements = GetElementsByKind(doc, settings, true, appliedFilters);
                DebugLogger.Log("FILTER", $"Found {typeElements.Count} type elements");
                result.AddRange(typeElements);

                // Collect instance elements
                var instanceElements = GetElementsByKind(doc, settings, false, appliedFilters);
                DebugLogger.Log("FILTER", $"Found {instanceElements.Count} instance elements");
                result.AddRange(instanceElements);
            }
            else if (settings.IncludeInstances)
            {
                DebugLogger.Log("FILTER", "Collecting only instance elements");
                // Collect only instance elements
                result = GetElementsByKind(doc, settings, false, appliedFilters);
                DebugLogger.Log("FILTER", $"Found {result.Count} instance elements");
            }
            else if (settings.IncludeTypes)
            {
                DebugLogger.Log("FILTER", "Collecting only type elements");
                // Collect only type elements
                result = GetElementsByKind(doc, settings, true, appliedFilters);
                DebugLogger.Log("FILTER", $"Found {result.Count} type elements");
            }

            // Output applied filter information
            if (appliedFilters.Count > 0)
            {
                System.Diagnostics.Trace.WriteLine($"Applied {appliedFilters.Count} filter conditions: {string.Join(", ", appliedFilters)}");
                System.Diagnostics.Trace.WriteLine($"Final filtering result: a total of {result.Count} elements were found");
            }
            return result;

        }

        /// <summary>
        /// Get elements that meet the filter conditions based on the element type (type or instance)
        /// </summary>
        private static List<Element> GetElementsByKind(Document doc, FilterSetting settings, bool isElementType, List<string> appliedFilters)
        {
            DebugLogger.Log("KIND", $"GetElementsByKind - isElementType: {isElementType}, category: {settings.FilterCategory}");
            
            // Create a basic FilteredElementCollector
            FilteredElementCollector collector;
            // Check if it is necessary to filter elements visible in the current view (only applicable to instance elements)
            if (!isElementType && settings.FilterVisibleInCurrentView && doc.ActiveView != null)
            {
                collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                appliedFilters.Add("Elements visible in current view");
            }
            else
            {
                collector = new FilteredElementCollector(doc);
            }
            // Filter by element type
            if (isElementType)
            {
                collector = collector.WhereElementIsElementType();
                appliedFilters.Add("Element type only");
            }
            else
            {
                collector = collector.WhereElementIsNotElementType();
                appliedFilters.Add("Element instance only");
            }
            // Create filter list
            List<ElementFilter> filters = new List<ElementFilter>();
            // 1. Category filter
            if (!string.IsNullOrWhiteSpace(settings.FilterCategory))
            {
                DebugLogger.Log("CATEGORY", $"Processing category filter: {settings.FilterCategory}");
                
                BuiltInCategory category;
                if (!Enum.TryParse(settings.FilterCategory, true, out category))
                {
                    DebugLogger.Log("CATEGORY", $"FAILED to parse category '{settings.FilterCategory}' as BuiltInCategory enum");
                    throw new ArgumentException($"Unable to convert '{settings.FilterCategory}' to a valid Revit category.");
                }
                
                DebugLogger.Log("CATEGORY", $"Successfully parsed '{settings.FilterCategory}' as BuiltInCategory.{category} (value: {(int)category})");
                
                // Test the category filter before applying it
                var testCollector = new FilteredElementCollector(doc);
                if (!isElementType)
                {
                    testCollector = testCollector.WhereElementIsNotElementType();
                }
                else
                {
                    testCollector = testCollector.WhereElementIsElementType();
                }
                
                DebugLogger.Log("CATEGORY", $"Testing category filter with collector (isElementType={isElementType})");
                
                var testCategoryFilter = new ElementCategoryFilter(category);
                var testElements = testCollector.WherePasses(testCategoryFilter).ToList();
                DebugLogger.Log("CATEGORY", $"Direct category filter test found {testElements.Count} elements");
                
                if (testElements.Count > 0)
                {
                    var firstElement = testElements.First();
                    DebugLogger.Log("CATEGORY", $"First found element - ID: {firstElement.Id}, Name: {firstElement.Name ?? "null"}, Category: {firstElement.Category?.Name ?? "null"}, IsElementType: {firstElement is ElementType}");
                }
                else
                {
                    DebugLogger.Log("CATEGORY", "No elements found with this category filter - investigating why...");
                    
                    // Let's see what categories actually exist in the document
                    var allCollector = new FilteredElementCollector(doc);
                    if (!isElementType)
                    {
                        allCollector = allCollector.WhereElementIsNotElementType();
                    }
                    else
                    {
                        allCollector = allCollector.WhereElementIsElementType();
                    }
                    
                    var allElements = allCollector.ToList().Take(20); // Just check first 20
                    var foundCategories = allElements.Select(e => e.Category?.Name ?? "NULL").Distinct().ToList();
                    DebugLogger.Log("CATEGORY", $"Sample categories in document: {string.Join(", ", foundCategories)}");
                }
                
                ElementCategoryFilter categoryFilter = new ElementCategoryFilter(category);
                filters.Add(categoryFilter);
                appliedFilters.Add($"Category: {settings.FilterCategory}");
                DebugLogger.Log("CATEGORY", "Category filter added to filters list");
            }
            // 2. Element type filter
            if (!string.IsNullOrWhiteSpace(settings.FilterElementType))
            {

                Type elementType = null;
                // Try to parse various possible forms of the type name
                string[] possibleTypeNames = new string[]
                {
                    settings.FilterElementType,                                    // Original input
                    $"Autodesk.Revit.DB.{settings.FilterElementType}, RevitAPI",  // Revit API namespace
                    $"{settings.FilterElementType}, RevitAPI"                      // Fully qualified with assembly
                };
                foreach (string typeName in possibleTypeNames)
                {
                    elementType = Type.GetType(typeName);
                    if (elementType != null)
                        break;
                }
                if (elementType != null)
                {
                    ElementClassFilter classFilter = new ElementClassFilter(elementType);
                    filters.Add(classFilter);
                    appliedFilters.Add($"Element type: {elementType.Name}");
                }
                else
                {
                    throw new Exception($"Warning: Could not find type '{settings.FilterElementType}'");
                }
            }
            // 3. Family symbol filter (only for element instances)
            if (!isElementType && settings.FilterFamilySymbolId > 0)
            {
                ElementId symbolId = new ElementId((long)settings.FilterFamilySymbolId);
                // Check if the element exists and is a family type
                Element symbolElement = doc.GetElement(symbolId);
                if (symbolElement != null && symbolElement is FamilySymbol)
                {
                    FamilyInstanceFilter familyFilter = new FamilyInstanceFilter(doc, symbolId);
                    filters.Add(familyFilter);
                    // Add more detailed family information log
                    FamilySymbol symbol = symbolElement as FamilySymbol;
                    string familyName = symbol.Family?.Name ?? "Unknown Family";
                    string symbolName = symbol.Name ?? "Unknown Type";
                    appliedFilters.Add($"Family type: {familyName} - {symbolName} (ID: {settings.FilterFamilySymbolId})");
                }
                else
                {
                    string elementType = symbolElement != null ? symbolElement.GetType().Name : "Does not exist";
                    System.Diagnostics.Trace.WriteLine($"Warning: Element with ID {settings.FilterFamilySymbolId} {(symbolElement == null ? "does not exist" : "is not a valid FamilySymbol")} (actual type: {elementType})");
                }
            }
            // 4. Spatial bounding box filter
            if (settings.BoundingBoxMin != null && settings.BoundingBoxMax != null)
            {
                // Convert to Revit's XYZ coordinates (mm to internal units)
                XYZ minXYZ = JZPoint.ToXYZ(settings.BoundingBoxMin);
                XYZ maxXYZ = JZPoint.ToXYZ(settings.BoundingBoxMax);
                // Create a spatial bounding box Outline object
                Outline outline = new Outline(minXYZ, maxXYZ);
                // Create an intersection filter
                BoundingBoxIntersectsFilter boundingBoxFilter = new BoundingBoxIntersectsFilter(outline);
                filters.Add(boundingBoxFilter);
                appliedFilters.Add($"Spatial filter: Min({settings.BoundingBoxMin.X:F2}, {settings.BoundingBoxMin.Y:F2}, {settings.BoundingBoxMin.Z:F2}), " +
                                  $"Max({settings.BoundingBoxMax.X:F2}, {settings.BoundingBoxMax.Y:F2}, {settings.BoundingBoxMax.Z:F2}) mm");
            }
            
            // 5. Parameter-based filters
            if (settings.ParameterFilters != null && settings.ParameterFilters.Count > 0)
            {
                // Get the appropriate parameter mapping for the category
                BuiltInCategory? categoryEnum = null;
                if (!string.IsNullOrWhiteSpace(settings.FilterCategory))
                {
                    if (Enum.TryParse(settings.FilterCategory, true, out BuiltInCategory category))
                    {
                        categoryEnum = category;
                    }
                }
                
                foreach (var paramFilter in settings.ParameterFilters)
                {
                    try
                    {
                        // Special handling for ElementId filtering
                        if (string.Equals(paramFilter.Name, "ElementId", StringComparison.OrdinalIgnoreCase))
                        {
                            var elementIdFilter = CreateElementIdFilter(paramFilter);
                            if (elementIdFilter != null)
                            {
                                filters.Add(elementIdFilter);
                                
                                // Enhanced logging for arrays
                                string valueDescription = GetValueDescription(paramFilter.Value);
                                appliedFilters.Add($"ElementId filter: {paramFilter.Operator} {valueDescription}");
                            }
                        }
                        else
                        {
                            var elementFilter = CreateParameterFilter(doc, paramFilter, categoryEnum, isElementType);
                            if (elementFilter != null)
                            {
                                filters.Add(elementFilter);
                                
                                // Enhanced logging for arrays
                                string valueDescription = GetValueDescription(paramFilter.Value);
                                appliedFilters.Add($"Parameter filter: {paramFilter.Name} {paramFilter.Operator} {valueDescription}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"Warning: Failed to create parameter filter for '{paramFilter.Name}': {ex.Message}");
                    }
                }
            }
            // Count elements before applying filters
            var elementsBeforeFilter = collector.ToElements();
            DebugLogger.Log("APPLY", $"Elements before filtering: {elementsBeforeFilter.Count} (isElementType={isElementType})");
            
            // Apply combined filter
            if (filters.Count > 0)
            {
                DebugLogger.Log("APPLY", $"Applying {filters.Count} filter(s)");
                
                ElementFilter combinedFilter = filters.Count == 1
                    ? filters[0]
                    : new LogicalAndFilter(filters);
                collector = collector.WherePasses(combinedFilter);
                
                DebugLogger.Log("APPLY", $"Combined filter applied (logical AND for {filters.Count} conditions)");
            }
            else
            {
                DebugLogger.Log("APPLY", "No filters to apply - returning all elements");
            }
            
            var finalElements = collector.ToElements().ToList();
            DebugLogger.Log("APPLY", $"Final result: {finalElements.Count} elements after filtering");
            
            // Log some details about the found elements
            if (finalElements.Count > 0)
            {
                foreach (var element in finalElements.Take(5))
                {
                    DebugLogger.Log("RESULT", $"Element found - ID: {element.Id}, Name: {element.Name ?? "null"}, Category: {element.Category?.Name ?? "null"}");
                }
                if (finalElements.Count > 5)
                {
                    DebugLogger.Log("RESULT", $"... and {finalElements.Count - 5} more elements");
                }
            }
            else
            {
                DebugLogger.Log("RESULT", "No elements found - this is the issue we're investigating");
            }
            
            return finalElements;
        }

        /// <summary>
        /// Get a descriptive string for filter values (handles both single values and arrays)
        /// </summary>
        private static string GetValueDescription(object value)
        {
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(item?.ToString() ?? "null");
                }
                return $"[{string.Join(", ", items)}] ({items.Count} items)";
            }
            else
            {
                return value?.ToString() ?? "null";
            }
        }

        /// <summary>
        /// Create a parameter-based element filter using the parameter mapping system
        /// </summary>
        private static ElementFilter CreateParameterFilter(Document doc, ParameterFilter paramFilter, BuiltInCategory? category, bool isElementType)
        {
            try
            {
                // Get the appropriate parameter mapping for the category
                ParameterMappingBase mapping = null;
                if (category.HasValue)
                {
                    mapping = ParameterMappingManager.GetMapping(category.Value);
                }

                // Find the parameter using the mapping system
                Parameter param = null;
                BuiltInParameter? builtInParam = null;
                
                if (mapping != null)
                {
                    // Create a temporary element to use for parameter resolution
                    // This is a bit of a hack, but necessary for the mapping system
                    var tempCollector = new FilteredElementCollector(doc);
                    if (isElementType)
                    {
                        tempCollector = tempCollector.WhereElementIsElementType();
                    }
                    else
                    {
                        tempCollector = tempCollector.WhereElementIsNotElementType();
                    }
                    
                    var sampleElement = tempCollector.FirstOrDefault();
                    if (sampleElement != null)
                    {
                        param = mapping.GetParameter(sampleElement, paramFilter.Name);
                    }
                }
                
                // If mapping didn't work, try direct parameter lookup
                if (param == null)
                {
                    // Try to find by BuiltInParameter enum
                    if (Enum.TryParse<BuiltInParameter>(paramFilter.Name, true, out var directBuiltIn))
                    {
                        builtInParam = directBuiltIn;
                    }
                }
                else
                {
                    // Get the BuiltInParameter from the found parameter
                    if (param.Definition is InternalDefinition internalDef)
                    {
                        builtInParam = internalDef.BuiltInParameter;
                    }
                }

                if (!builtInParam.HasValue)
                {
                    System.Diagnostics.Trace.WriteLine($"Warning: Could not resolve parameter '{paramFilter.Name}' for filtering");
                    return null;
                }

                // Convert the filter value using the mapping if available
                object convertedValue = paramFilter.Value;
                if (mapping != null)
                {
                    convertedValue = mapping.ConvertValue(paramFilter.Name, paramFilter.Value);
                }

                // Create the appropriate filter based on the operator
                return CreateRevitParameterFilter(builtInParam.Value, paramFilter.Operator, convertedValue);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating parameter filter for '{paramFilter.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create a Revit parameter filter based on the operator and value (supports single values and arrays)
        /// </summary>
        private static ElementFilter CreateRevitParameterFilter(BuiltInParameter builtInParam, string operatorStr, object value)
        {
            try
            {
                // Handle arrays by creating multiple filters and combining them with OR logic
                if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    var filters = new List<ElementFilter>();
                    
                    foreach (var item in enumerable)
                    {
                        var singleFilter = CreateSingleValueParameterFilter(builtInParam, operatorStr, item);
                        if (singleFilter != null)
                        {
                            filters.Add(singleFilter);
                        }
                    }
                    
                    if (filters.Count == 0)
                    {
                        System.Diagnostics.Trace.WriteLine($"Warning: No valid filters created from array values");
                        return null;
                    }
                    
                    if (filters.Count == 1)
                    {
                        return filters[0];
                    }
                    
                    // Combine multiple filters with OR logic
                    System.Diagnostics.Trace.WriteLine($"Creating OR filter with {filters.Count} conditions for parameter array");
                    return new LogicalOrFilter(filters);
                }
                else
                {
                    // Handle single value
                    return CreateSingleValueParameterFilter(builtInParam, operatorStr, value);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating Revit parameter filter: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create a Revit parameter filter for a single value
        /// </summary>
        private static ElementFilter CreateSingleValueParameterFilter(BuiltInParameter builtInParam, string operatorStr, object value)
        {
            try
            {
                // Handle different value types
                if (value is string stringValue)
                {
                    return CreateStringParameterFilter(builtInParam, operatorStr, stringValue);
                }
                else if (value is double doubleValue)
                {
                    return CreateNumericParameterFilter(builtInParam, operatorStr, doubleValue);
                }
                else if (value is int intValue)
                {
                    return CreateNumericParameterFilter(builtInParam, operatorStr, (double)intValue);
                }
                else if (value is bool boolValue)
                {
                    return CreateNumericParameterFilter(builtInParam, operatorStr, boolValue ? 1.0 : 0.0);
                }
                else
                {
                    // Try to convert to string as fallback
                    return CreateStringParameterFilter(builtInParam, operatorStr, value?.ToString() ?? "");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating single value parameter filter: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create a string-based parameter filter
        /// </summary>
        private static ElementFilter CreateStringParameterFilter(BuiltInParameter builtInParam, string operatorStr, string value)
        {
            try
            {
                var provider = new ParameterValueProvider(new ElementId(builtInParam));
                var evaluator = GetStringEvaluator(operatorStr);
                if (evaluator == null)
                    throw new ArgumentException($"Unsupported string operator: {operatorStr}");

                var rule = new FilterStringRule(provider, evaluator, value);
                var filter = new ElementParameterFilter(rule);

                return filter;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating string parameter filter: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create a numeric parameter filter
        /// </summary>
        private static ElementFilter CreateNumericParameterFilter(BuiltInParameter builtInParam, string operatorStr, double value)
        {
            try
            {
                var provider = new ParameterValueProvider(new ElementId(builtInParam));
                var evaluator = GetNumericEvaluator(operatorStr);
                if (evaluator == null)
                    throw new ArgumentException($"Unsupported numeric operator: {operatorStr}");

                var rule = new FilterDoubleRule(provider, evaluator, value, 1e-9);
                var filter = new ElementParameterFilter(rule);

                return filter;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating numeric parameter filter: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get string evaluator for the given operator
        /// </summary>
        private static FilterStringRuleEvaluator GetStringEvaluator(string operatorStr)
        {
            switch (operatorStr.ToLower())
            {
                case "equals":
                case "=":
                case "==":
                    return new FilterStringEquals();
                
                // Note: "notequals" and "!=" are not currently supported
                
                case "contains":
                    return new FilterStringContains();
                
                case "startswith":
                    return new FilterStringBeginsWith();
                
                case "endswith":
                    return new FilterStringEndsWith();
                
                default:
                    return null;
            }
        }

        /// <summary>
        /// Get numeric evaluator for the given operator
        /// </summary>
        private static FilterNumericRuleEvaluator GetNumericEvaluator(string operatorStr)
        {
            switch (operatorStr.ToLower())
            {
                case "equals":
                case "=":
                case "==":
                    return new FilterNumericEquals();
                
                // Note: "notequals" and "!=" are not currently supported
                
                case "greater":
                case ">":
                    return new FilterNumericGreater();
                
                case "greaterequal":
                case "greaterequals":
                case ">=":
                    return new FilterNumericGreaterOrEqual();
                
                case "less":
                case "<":
                    return new FilterNumericLess();
                
                case "lessequal":
                case "lessequals":
                case "<=":
                    return new FilterNumericLessOrEqual();
                
                default:
                    return null;
            }
        }

        /// <summary>
        /// Create an ElementId-based filter for filtering by element ID (supports single values and arrays)
        /// </summary>
        private static ElementFilter CreateElementIdFilter(ParameterFilter paramFilter)
        {
            try
            {
                var elementIds = new List<ElementId>();

                // Handle both single values and arrays
                if (paramFilter.Value is System.Collections.IEnumerable enumerable && !(paramFilter.Value is string))
                {
                    // Handle array of values
                    foreach (var item in enumerable)
                    {
                        if (int.TryParse(item?.ToString(), out int elementIdValue))
                        {
                            elementIds.Add(new ElementId((long)elementIdValue));
                        }
                        else
                        {
                            System.Diagnostics.Trace.WriteLine($"Warning: Could not parse ElementId array item '{item}' as integer");
                        }
                    }
                    
                    if (elementIds.Count == 0)
                    {
                        System.Diagnostics.Trace.WriteLine($"Warning: No valid ElementId values found in array");
                        return null;
                    }
                    
                    System.Diagnostics.Trace.WriteLine($"ElementId array filter: Found {elementIds.Count} valid element IDs");
                }
                else
                {
                    // Handle single value
                    if (!int.TryParse(paramFilter.Value?.ToString(), out int elementIdValue))
                    {
                        System.Diagnostics.Trace.WriteLine($"Warning: Could not parse ElementId value '{paramFilter.Value}' as integer");
                        return null;
                    }
                    
                    elementIds.Add(new ElementId((long)elementIdValue));
                }

                // For ElementId filtering, we typically only support "equals" operation
                switch (paramFilter.Operator.ToLower())
                {
                    case "equals":
                    case "=":
                    case "==":
                        return new ElementIdSetFilter(elementIds);
                    
                    default:
                        System.Diagnostics.Trace.WriteLine($"Warning: ElementId filtering only supports 'equals' operator, got '{paramFilter.Operator}'");
                        return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating ElementId filter: {ex.Message}");
                return null;
            }
        }
    }
} 