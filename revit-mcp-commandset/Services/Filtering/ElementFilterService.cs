using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.Common;
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
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            // Validate filter settings
            if (!settings.Validate(out string errorMessage))
            {
                System.Diagnostics.Trace.WriteLine($"Invalid filter settings: {errorMessage}");
                return new List<Element>();
            }
            // Record the application of filter conditions
            List<string> appliedFilters = new List<string>();
            List<Element> result = new List<Element>();
            // If both types and instances are included, they need to be filtered separately and then the results merged
            if (settings.IncludeTypes && settings.IncludeInstances)
            {
                // Collect type elements
                result.AddRange(GetElementsByKind(doc, settings, true, appliedFilters));

                // Collect instance elements
                result.AddRange(GetElementsByKind(doc, settings, false, appliedFilters));
            }
            else if (settings.IncludeInstances)
            {
                // Collect only instance elements
                result = GetElementsByKind(doc, settings, false, appliedFilters);
            }
            else if (settings.IncludeTypes)
            {
                // Collect only type elements
                result = GetElementsByKind(doc, settings, true, appliedFilters);
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
                BuiltInCategory category;
                if (!Enum.TryParse(settings.FilterCategory, true, out category))
                {
                    throw new ArgumentException($"Unable to convert '{settings.FilterCategory}' to a valid Revit category.");
                }
                ElementCategoryFilter categoryFilter = new ElementCategoryFilter(category);
                filters.Add(categoryFilter);
                appliedFilters.Add($"Category: {settings.FilterCategory}");
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
                ElementId symbolId = new ElementId(settings.FilterFamilySymbolId);
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
            // Apply combined filter
            if (filters.Count > 0)
            {
                ElementFilter combinedFilter = filters.Count == 1
                    ? filters[0]
                    : new LogicalAndFilter(filters);
                collector = collector.WherePasses(combinedFilter);
                if (filters.Count > 1)
                {
                    System.Diagnostics.Trace.WriteLine($"Applied a combined filter with {filters.Count} conditions (logical AND)");
                }
            }
            return collector.ToElements().ToList();
        }
    }
} 