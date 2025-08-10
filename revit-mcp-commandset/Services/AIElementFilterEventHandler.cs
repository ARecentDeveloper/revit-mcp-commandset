using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPCommandSet.Utils.ParameterMappings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace RevitMCPCommandSet.Services
{
    public class AIElementFilterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;
        /// <summary>
        /// Event wait object
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        /// <summary>
        /// Creation data (input data)
        /// </summary>
        public FilterSetting FilterSetting { get; private set; }
        /// <summary>
        /// Execution result (output data)
        /// </summary>
        public AIResult<List<object>> Result { get; private set; }

        /// <summary>
        /// Set creation parameters
        /// </summary>
        public void SetParameters(FilterSetting data)
        {
            FilterSetting = data;
            _resetEvent.Reset();
        }
        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementInfoList = new List<object>();
                // Check if filter settings are valid
                if (!FilterSetting.Validate(out string errorMessage))
                    throw new Exception(errorMessage);
                // Get elements with specified conditions
                var elementList = GetFilteredElements(doc, FilterSetting);
                if (elementList == null || !elementList.Any())
                    throw new Exception("No specified elements found in the project, please check if the filter settings are correct");
                // Filter maximum count limit
                string message = "";
                if (FilterSetting.MaxElements > 0)
                {
                    if (elementList.Count > FilterSetting.MaxElements)
                    {
                        elementList = elementList.Take(FilterSetting.MaxElements).ToList();
                        message = $". Additionally, there are {elementList.Count} elements matching the filter criteria, only showing the first {FilterSetting.MaxElements}";
                    }
                }

                // Get information for specified Id elements
                elementInfoList = GetElementFullInfo(doc, elementList);

                Result = new AIResult<List<object>>
                {
                    Success = true,
                    Message = $"Successfully retrieved {elementInfoList.Count} element information, detailed information stored in Response property"+ message,
                    Response = elementInfoList,
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<object>>
                {
                    Success = false,
                    Message = $"Error occurred while retrieving element information: {ex.Message}",
                };
            }
            finally
            {
                _resetEvent.Set(); // Notify waiting thread that operation is complete
            }
        }

        /// <summary>
        /// Wait for creation completion
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout duration (milliseconds)</param>
        /// <returns>Whether the operation completed before timeout</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// IExternalEventHandler.GetName implementation
        /// </summary>
        public string GetName()
        {
            return "Get Element Information";
        }

        /// <summary>
        /// Get elements that meet conditions in Revit document based on filter settings, supports multi-condition combined filtering
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="settings">Filter settings</param>
        /// <returns>Collection of elements that meet all filter conditions</returns>
        public static IList<Element> GetFilteredElements(Document doc, FilterSetting settings)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
                
            // Parse natural language query if no explicit parameter filters
            if (settings.ParameterFilters.Count == 0 && !string.IsNullOrWhiteSpace(settings.NaturalLanguageQuery))
            {
                ParseNaturalLanguageQuery(settings);
            }
            
            // For debugging: If we're filtering structural framing, let's assume parameter filtering might be intended
            if (settings.FilterCategory == "OST_StructuralFraming" && settings.ParameterFilters.Count == 0)
            {
                System.Diagnostics.Trace.WriteLine("Structural framing filter detected - parameter filtering may be intended but not specified");
            }
            
            // Validate filter settings
            if (!settings.Validate(out string errorMessage))
            {
                System.Diagnostics.Trace.WriteLine($"Filter settings invalid: {errorMessage}");
                return new List<Element>();
            }
            // Record filter condition application status
            List<string> appliedFilters = new List<string>();
            List<Element> result = new List<Element>();
            // If both types and instances are included, need to filter separately and merge results
            if (settings.IncludeTypes && settings.IncludeInstances)
            {
                // Collect type elements
                result.AddRange(GetElementsByKind(doc, settings, true, appliedFilters));

                // Collect instance elements
                result.AddRange(GetElementsByKind(doc, settings, false, appliedFilters));
            }
            else if (settings.IncludeInstances)
            {
                // Only collect instance elements
                result = GetElementsByKind(doc, settings, false, appliedFilters);
            }
            else if (settings.IncludeTypes)
            {
                // Only collect type elements
                result = GetElementsByKind(doc, settings, true, appliedFilters);
            }

            // Output applied filter information
            if (appliedFilters.Count > 0)
            {
                System.Diagnostics.Trace.WriteLine($"Applied {appliedFilters.Count} filter conditions: {string.Join(", ", appliedFilters)}");
                System.Diagnostics.Trace.WriteLine($"Final filtering result: Found {result.Count} elements in total");
            }
            return result;

        }

        /// <summary>
        /// Get elements that meet filter conditions based on element kind (type or instance)
        /// </summary>
        private static List<Element> GetElementsByKind(Document doc, FilterSetting settings, bool isElementType, List<string> appliedFilters)
        {
            // Create basic FilteredElementCollector
            FilteredElementCollector collector;
            // Check if need to filter elements visible in current view (only applicable to instance elements)
            if (!isElementType && settings.FilterVisibleInCurrentView && doc.ActiveView != null)
            {
                collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                appliedFilters.Add("Elements visible in current view");
            }
            else
            {
                collector = new FilteredElementCollector(doc);
            }
            // Filter by element kind
            if (isElementType)
            {
                collector = collector.WhereElementIsElementType();
                appliedFilters.Add("Element types only");
            }
            else
            {
                collector = collector.WhereElementIsNotElementType();
                appliedFilters.Add("Element instances only");
            }
            // Create filter list
            List<ElementFilter> filters = new List<ElementFilter>();
            // 1. Category filter
            if (!string.IsNullOrWhiteSpace(settings.FilterCategory))
            {
                BuiltInCategory category;
                if (!Enum.TryParse(settings.FilterCategory, true, out category))
                {
                    throw new ArgumentException($"Cannot convert '{settings.FilterCategory}' to a valid Revit category.");
                }
                ElementCategoryFilter categoryFilter = new ElementCategoryFilter(category);
                filters.Add(categoryFilter);
                appliedFilters.Add($"Category: {settings.FilterCategory}");
            }
            // 2. Element type filter
            if (!string.IsNullOrWhiteSpace(settings.FilterElementType))
            {

                Type elementType = null;
                // Try to parse various possible forms of type names
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
                    throw new Exception($"Warning: Cannot find type '{settings.FilterElementType}'");
                }
            }
            // 3. Family symbol filter (only applicable to element instances)
            if (!isElementType && settings.FilterFamilySymbolId > 0)
            {
                ElementId symbolId = new ElementId(settings.FilterFamilySymbolId);
                // Check if element exists and is a family type
                Element symbolElement = doc.GetElement(symbolId);
                if (symbolElement != null && symbolElement is FamilySymbol)
                {
                    FamilyInstanceFilter familyFilter = new FamilyInstanceFilter(doc, symbolId);
                    filters.Add(familyFilter);
                    // Add more detailed family information log
                    FamilySymbol symbol = symbolElement as FamilySymbol;
                    string familyName = symbol.Family?.Name ?? "Unknown family";
                    string symbolName = symbol.Name ?? "Unknown type";
                    appliedFilters.Add($"Family type: {familyName} - {symbolName} (ID: {settings.FilterFamilySymbolId})");
                }
                else
                {
                    string elementType = symbolElement != null ? symbolElement.GetType().Name : "Does not exist";
                    System.Diagnostics.Trace.WriteLine($"Warning: Element with ID {settings.FilterFamilySymbolId} {(symbolElement == null ? "does not exist" : "is not a valid FamilySymbol")} (actual type: {elementType})");
                }
            }
            // 4. Spatial range filter
            if (settings.BoundingBoxMin != null && settings.BoundingBoxMax != null)
            {
                // Convert to Revit XYZ coordinates (millimeters to internal units)
                XYZ minXYZ = JZPoint.ToXYZ(settings.BoundingBoxMin);
                XYZ maxXYZ = JZPoint.ToXYZ(settings.BoundingBoxMax);
                // Create spatial range Outline object
                Outline outline = new Outline(minXYZ, maxXYZ);
                // Create intersection filter
                BoundingBoxIntersectsFilter boundingBoxFilter = new BoundingBoxIntersectsFilter(outline);
                filters.Add(boundingBoxFilter);
                appliedFilters.Add($"Spatial range filter: Min({settings.BoundingBoxMin.X:F2}, {settings.BoundingBoxMin.Y:F2}, {settings.BoundingBoxMin.Z:F2}), " +
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
                    System.Diagnostics.Trace.WriteLine($"Applied combined filter with {filters.Count} filter conditions (logical AND relationship)");
                }
            }

            // Get initial elements from collector
            var elements = collector.ToElements().ToList();

            // 5. Apply parameter filters (post-processing since Revit's ElementFilter doesn't support custom parameter filtering)
            if (settings.ParameterFilters != null && settings.ParameterFilters.Count > 0)
            {
                elements = ApplyParameterFilters(elements, settings, appliedFilters);
            }

            return elements;
        }

        /// <summary>
        /// Parse natural language query and extract parameter filters
        /// </summary>
        private static void ParseNaturalLanguageQuery(FilterSetting settings)
        {
            if (string.IsNullOrWhiteSpace(settings.NaturalLanguageQuery))
                return;

            var query = settings.NaturalLanguageQuery.ToLower();
            
            // Common parameter patterns
            var patterns = new Dictionary<string, string[]>
            {
                { "length", new[] { "length", "long", "l " } },
                { "height", new[] { "height", "high", "h ", "depth", "d " } },
                { "width", new[] { "width", "wide", "w " } },
                { "flange thickness", new[] { "flange thickness", "flange", "tf" } },
                { "web thickness", new[] { "web thickness", "web", "tw" } },
                { "moment of inertia strong axis", new[] { "moment of inertia strong", "ix", "strong axis" } },
                { "moment of inertia weak axis", new[] { "moment of inertia weak", "iy", "weak axis" } },
                { "section area", new[] { "section area", "area" } },
                { "nominal weight", new[] { "weight", "nominal weight" } },
                { "structural usage", new[] { "structural usage", "usage" } }
            };

            // Operator patterns
            var operators = new Dictionary<string, string>
            {
                { "greater than", ">" },
                { "more than", ">" },
                { "larger than", ">" },
                { "bigger than", ">" },
                { "> ", ">" },
                { "less than", "<" },
                { "smaller than", "<" },
                { "< ", "<" },
                { "equal to", "=" },
                { "equals", "=" },
                { "= ", "=" },
                { "not equal", "!=" },
                { "contains", "contains" }
            };

            foreach (var paramPattern in patterns)
            {
                foreach (var paramKeyword in paramPattern.Value)
                {
                    if (query.Contains(paramKeyword))
                    {
                        // Found parameter, now look for operator and value
                        foreach (var opPattern in operators)
                        {
                            var opIndex = query.IndexOf(opPattern.Key);
                            if (opIndex > 0)
                            {
                                // Extract value after operator
                                var valueStart = opIndex + opPattern.Key.Length;
                                var valueText = query.Substring(valueStart).Trim();
                                
                                // Extract numeric value
                                var valueMatch = System.Text.RegularExpressions.Regex.Match(valueText, @"(\d+\.?\d*)");
                                if (valueMatch.Success)
                                {
                                    if (double.TryParse(valueMatch.Value, out double value))
                                    {
                                        var paramFilter = new ParameterFilter
                                        {
                                            Name = paramPattern.Key,
                                            Operator = opPattern.Value,
                                            Value = value
                                        };
                                        
                                        settings.ParameterFilters.Add(paramFilter);
                                        System.Diagnostics.Trace.WriteLine($"Parsed parameter filter: {paramFilter.Name} {paramFilter.Operator} {paramFilter.Value}");
                                        return; // Found one filter, that's enough for now
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Apply parameter-based filters using ParameterMappingManager
        /// </summary>
        private static List<Element> ApplyParameterFilters(List<Element> elements, FilterSetting settings, List<string> appliedFilters)
        {
            if (elements == null || elements.Count == 0 || settings.ParameterFilters == null || settings.ParameterFilters.Count == 0)
                return elements;

            // Resolve category for parameter mapping
            BuiltInCategory? builtInCategory = null;
            if (!string.IsNullOrWhiteSpace(settings.FilterCategory))
            {
                if (Enum.TryParse(settings.FilterCategory, true, out BuiltInCategory category))
                {
                    builtInCategory = category;
                }
            }

            var filteredElements = new List<Element>();

            foreach (var element in elements)
            {
                bool elementMatches = true;

                // Apply all parameter filters (AND logic)
                foreach (var paramFilter in settings.ParameterFilters)
                {
                    if (!ElementMatchesParameterFilter(element, paramFilter, builtInCategory))
                    {
                        elementMatches = false;
                        break;
                    }
                }

                if (elementMatches)
                {
                    filteredElements.Add(element);
                }
            }

            // Add applied filter information
            foreach (var paramFilter in settings.ParameterFilters)
            {
                appliedFilters.Add($"Parameter: {paramFilter.Name} {paramFilter.Operator} {paramFilter.Value}");
            }

            System.Diagnostics.Trace.WriteLine($"Parameter filtering: {elements.Count} → {filteredElements.Count} elements");

            return filteredElements;
        }

        /// <summary>
        /// Check if element matches a parameter filter using ParameterMappingManager
        /// </summary>
        private static bool ElementMatchesParameterFilter(Element element, ParameterFilter paramFilter, BuiltInCategory? category)
        {
            try
            {
                // Get parameter using ParameterMappingManager if category is available
                Parameter param = null;
                if (category.HasValue)
                {
                    param = ParameterMappingManager.GetParameter(element, paramFilter.Name, category.Value);
                }
                else
                {
                    // Fallback to generic parameter lookup
                    param = element.LookupParameter(paramFilter.Name);
                    if (param == null)
                    {
                        // Try type parameter
                        ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
                        param = elementType?.LookupParameter(paramFilter.Name);
                    }
                }

                if (param == null || !param.HasValue)
                    return false;

                // Convert filter value using ParameterMappingManager if category is available
                object convertedValue = paramFilter.Value;
                if (category.HasValue)
                {
                    convertedValue = ParameterMappingManager.ConvertValue(paramFilter.Name, paramFilter.Value, category.Value);
                }

                // Determine value type if not specified
                var valueType = paramFilter.ValueType ?? DetermineParameterValueType(param);

                // Evaluate condition
                return EvaluateParameterCondition(param, paramFilter.Operator, convertedValue, valueType);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error evaluating parameter filter '{paramFilter.Name}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Determine parameter value type from Revit parameter
        /// </summary>
        private static ParameterValueType DetermineParameterValueType(Parameter param)
        {
            switch (param.StorageType)
            {
                case StorageType.String:
                    return ParameterValueType.String;
                case StorageType.Integer:
                    // Check if it's a boolean (0/1 integer) - simplified for Revit 2024
                    var paramName = param.Definition.Name?.ToLower();
                    if (paramName != null && (paramName.Contains("yes") || paramName.Contains("no") || paramName.Contains("bool")))
                        return ParameterValueType.Boolean;
                    return ParameterValueType.Integer;
                case StorageType.Double:
                    return ParameterValueType.Double;
                default:
                    return ParameterValueType.String;
            }
        }

        /// <summary>
        /// Evaluate parameter condition based on operator and value type
        /// </summary>
        private static bool EvaluateParameterCondition(Parameter param, string operatorType, object convertedValue, ParameterValueType valueType)
        {
            try
            {
                switch (valueType)
                {
                    case ParameterValueType.Double:
                        return EvaluateNumericCondition(param.AsDouble(), operatorType, Convert.ToDouble(convertedValue));

                    case ParameterValueType.Integer:
                        return EvaluateNumericCondition(param.AsInteger(), operatorType, Convert.ToInt32(convertedValue));

                    case ParameterValueType.String:
                        return EvaluateStringCondition(param.AsString(), operatorType, convertedValue?.ToString());

                    case ParameterValueType.Boolean:
                        return EvaluateBooleanCondition(param.AsInteger() != 0, operatorType, Convert.ToBoolean(convertedValue));

                    default:
                        return EvaluateStringCondition(param.AsValueString(), operatorType, convertedValue?.ToString());
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
        /// Get model element information
        /// </summary>
        public static List<object> GetElementFullInfo(Document doc, IList<Element> elementCollector)
        {
            List<object> infoList = new List<object>();

            // Get and process elements
            foreach (var element in elementCollector)
            {
                // Determine if it's a solid model element
                // Get element instance information
                if (element?.Category?.HasMaterialQuantities ?? false)
                {
                    var info = CreateElementFullInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // Get element type information
                else if (element is ElementType elementType)
                {
                    var info = CreateTypeFullInfo(doc, elementType);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 3. Spatial positioning elements (high frequency)
                else if (element is Level || element is Grid)
                {
                    var info = CreatePositioningElementInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 4. Spatial elements (medium-high frequency)
                else if (element is SpatialElement) // Room, Area, etc.
                {
                    var info = CreateSpatialElementInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 5. View elements (high frequency)
                else if (element is View)
                {
                    var info = CreateViewInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 6. Annotation elements (medium frequency)
                else if (element is TextNote || element is Dimension ||
                         element is IndependentTag || element is AnnotationSymbol ||
                         element is SpotDimension)
                {
                    var info = CreateAnnotationInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 7. Handle groups and links
                else if (element is Group || element is RevitLinkInstance)
                {
                    var info = CreateGroupOrLinkInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 8. Get element basic information (fallback handling)
                else
                {
                    var info = CreateElementBasicInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
            }

            return infoList;
        }

        /// <summary>
        /// Create complete ElementInfo object for a single element
        /// </summary>
        public static ElementInstanceInfo CreateElementFullInfo(Document doc, Element element)
        {
            try
            {
                if (element?.Category == null)
                    return null;

                ElementInstanceInfo elementInfo = new ElementInstanceInfo();        // Create custom class to store complete element information
                // ID
                elementInfo.Id = element.Id.IntegerValue;
                // UniqueId
                elementInfo.UniqueId = element.UniqueId;
                // Type name
                elementInfo.Name = element.Name;
                // Family name
                elementInfo.FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString();
                // Category
                elementInfo.Category = element.Category.Name;
                // Built-in category
                elementInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue);
                // Type Id
                elementInfo.TypeId = element.GetTypeId().IntegerValue;
                // Owning room Id  
                if (element is FamilyInstance instance)
                    elementInfo.RoomId = instance.Room?.Id.IntegerValue ?? -1;
                // Level
                elementInfo.Level = GetElementLevel(doc, element);
                // Maximum bounding box
                BoundingBoxInfo boundingBoxInfo = new BoundingBoxInfo();
                elementInfo.BoundingBox = GetBoundingBoxInfo(element);
                // Parameters - Extract comprehensive parameters using category-specific mapping
                elementInfo.Parameters = ExtractElementParameters(element);
                
                // Add legacy parameters for compatibility
                ParameterInfo thicknessParam = GetThicknessInfo(element);      // Thickness parameter
                if (thicknessParam != null)
                {
                    elementInfo.Parameters.Add(thicknessParam);
                }
                ParameterInfo heightParam = GetBoundingBoxHeight(elementInfo.BoundingBox);      // Height parameter
                if (heightParam != null)
                {
                    elementInfo.Parameters.Add(heightParam);
                }

                return elementInfo;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create complete TypeFullInfo object for a single type
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="elementType"></param>
        /// <returns></returns>
        public static ElementTypeInfo CreateTypeFullInfo(Document doc, ElementType elementType)
        {
            ElementTypeInfo typeInfo = new ElementTypeInfo();
            // Id
            typeInfo.Id = elementType.Id.IntegerValue;
            // UniqueId
            typeInfo.UniqueId = elementType.UniqueId;
            // Type name
            typeInfo.Name = elementType.Name;
            // Family name
            typeInfo.FamilyName = elementType.FamilyName;
            // Category
            typeInfo.Category = elementType.Category.Name;
            // Built-in category
            typeInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), elementType.Category.Id.IntegerValue);
            // Parameter dictionary
            typeInfo.Parameters = GetDimensionParameters(elementType);
            ParameterInfo thicknessParam = GetThicknessInfo(elementType);      // Thickness parameter
            if (thicknessParam != null)
            {
                typeInfo.Parameters.Add(thicknessParam);
            }
            return typeInfo;
        }

        /// <summary>
        /// Create information for spatial positioning elements
        /// </summary>
        public static PositioningElementInfo CreatePositioningElementInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                PositioningElementInfo info = new PositioningElementInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Handle levels
                if (element is Level level)
                {
                    // Convert to mm
                    info.Elevation = level.Elevation * 304.8;
                }
                // Handle grids
                else if (element is Grid grid)
                {
                    Curve curve = grid.Curve;
                    if (curve != null)
                    {
                        XYZ start = curve.GetEndPoint(0);
                        XYZ end = curve.GetEndPoint(1);
                        // Create JZLine (convert to mm)
                        info.GridLine = new JZLine(
                            start.X * 304.8, start.Y * 304.8, start.Z * 304.8,
                            end.X * 304.8, end.Y * 304.8, end.Z * 304.8);
                    }
                }

                // Get level information
                info.Level = GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating spatial positioning element information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for spatial elements
        /// </summary>
        public static SpatialElementInfo CreateSpatialElementInfo(Document doc, Element element)
        {
            try
            {
                if (element == null || !(element is SpatialElement))
                    return null;
                SpatialElement spatialElement = element as SpatialElement;
                SpatialElementInfo info = new SpatialElementInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Get room or area number
                if (element is Room room)
                {
                    info.Number = room.Number;
                    // Convert to mm³
                    info.Volume = room.Volume * Math.Pow(304.8, 3);
                }
                else if (element is Area area)
                {
                    info.Number = area.Number;
                }

                // Get area
                Parameter areaParam = element.get_Parameter(BuiltInParameter.ROOM_AREA);
                if (areaParam != null && areaParam.HasValue)
                {
                    // Convert to mm²
                    info.Area = areaParam.AsDouble() * Math.Pow(304.8, 2);
                }

                // Get perimeter
                Parameter perimeterParam = element.get_Parameter(BuiltInParameter.ROOM_PERIMETER);
                if (perimeterParam != null && perimeterParam.HasValue)
                {
                    // Convert to mm
                    info.Perimeter = perimeterParam.AsDouble() * 304.8;
                }

                // Get level
                info.Level = GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating spatial element information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for view elements
        /// </summary>
        public static ViewInfo CreateViewInfo(Document doc, Element element)
        {
            try
            {
                if (element == null || !(element is View))
                    return null;
                View view = element as View;

                ViewInfo info = new ViewInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue) : null,
                    ElementClass = element.GetType().Name,
                    ViewType = view.ViewType.ToString(),
                    Scale = view.Scale,
                    IsTemplate = view.IsTemplate,
                    DetailLevel = view.DetailLevel.ToString(),
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Get level associated with view
                if (view is ViewPlan viewPlan && viewPlan.GenLevel != null)
                {
                    Level level = viewPlan.GenLevel;
                    info.AssociatedLevel = new LevelInfo
                    {
                        Id = level.Id.IntegerValue,
                        Name = level.Name,
                        Height = level.Elevation * 304.8 // Convert to mm
                    };
                }

                // Determine if view is open and active
                UIDocument uidoc = new UIDocument(doc);

                // Get all open views
                IList<UIView> openViews = uidoc.GetOpenUIViews();

                foreach (UIView uiView in openViews)
                {
                    // Check if view is open
                    if (uiView.ViewId.IntegerValue == view.Id.IntegerValue)
                    {
                        info.IsOpen = true;

                        // Check if view is the currently active view
                        if (uidoc.ActiveView.Id.IntegerValue == view.Id.IntegerValue)
                        {
                            info.IsActive = true;
                        }
                        break;
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating view element information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for annotation elements
        /// </summary>
        public static AnnotationInfo CreateAnnotationInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                AnnotationInfo info = new AnnotationInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Get owner view
                Parameter viewParam = element.get_Parameter(BuiltInParameter.VIEW_NAME);
                if (viewParam != null && viewParam.HasValue)
                {
                    info.OwnerView = viewParam.AsString();
                }
                else if (element.OwnerViewId != ElementId.InvalidElementId)
                {
                    View ownerView = doc.GetElement(element.OwnerViewId) as View;
                    info.OwnerView = ownerView?.Name;
                }

                // Handle text notes
                if (element is TextNote textNote)
                {
                    info.TextContent = textNote.Text;
                    XYZ position = textNote.Coord;
                    // Convert to mm
                    info.Position = new JZPoint(
                        position.X * 304.8,
                        position.Y * 304.8,
                        position.Z * 304.8);
                }
                // Handle dimensions
                else if (element is Dimension dimension)
                {
                    info.DimensionValue = dimension.Value.ToString();
                    XYZ origin = dimension.Origin;
                    // Convert to mm
                    info.Position = new JZPoint(
                        origin.X * 304.8,
                        origin.Y * 304.8,
                        origin.Z * 304.8);
                }
                // Handle other annotation elements
                else if (element is AnnotationSymbol annotationSymbol)
                {
                    if (annotationSymbol.Location is LocationPoint locationPoint)
                    {
                        XYZ position = locationPoint.Point;
                        // Convert to mm
                        info.Position = new JZPoint(
                            position.X * 304.8,
                            position.Y * 304.8,
                            position.Z * 304.8);
                    }
                }
                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating annotation element information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for groups or links
        /// </summary>
        public static GroupOrLinkInfo CreateGroupOrLinkInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                GroupOrLinkInfo info = new GroupOrLinkInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Handle groups
                if (element is Group group)
                {
                    ICollection<ElementId> memberIds = group.GetMemberIds();
                    info.MemberCount = memberIds?.Count;
                    info.GroupType = group.GroupType?.Name;
                }
                // Handle links
                else if (element is RevitLinkInstance linkInstance)
                {
                    RevitLinkType linkType = doc.GetElement(linkInstance.GetTypeId()) as RevitLinkType;
                    if (linkType != null)
                    {
                        ExternalFileReference extFileRef = linkType.GetExternalFileReference();
                        // Get absolute path
                        string absPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extFileRef.GetAbsolutePath());
                        info.LinkPath = absPath;

                        // Get link status using GetLinkedFileStatus
                        LinkedFileStatus linkStatus = linkType.GetLinkedFileStatus();
                        info.LinkStatus = linkStatus.ToString();
                    }
                    else
                    {
                        info.LinkStatus = LinkedFileStatus.Invalid.ToString();
                    }

                    // Get location
                    LocationPoint location = linkInstance.Location as LocationPoint;
                    if (location != null)
                    {
                        XYZ point = location.Point;
                        // Convert to mm
                        info.Position = new JZPoint(
                            point.X * 304.8,
                            point.Y * 304.8,
                            point.Z * 304.8);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating group and link information: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Enhanced base information for creating elements
        /// </summary>
        public static ElementBasicInfo CreateElementBasicInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                ElementBasicInfo basicInfo = new ElementBasicInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue) : null,
                    BoundingBox = GetBoundingBoxInfo(element)
                };
                return basicInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating element basic information: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get thickness parameter information for system family components
        /// </summary>
        /// <param name="element">System family component (walls, floors, doors, etc.)</param>
        /// <returns>Parameter information object, returns null if invalid</returns>
        public static ParameterInfo GetThicknessInfo(Element element)
        {
            if (element == null)
            {
                return null;
            }

            // Get component type
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
            if (elementType == null)
            {
                return null;
            }

            // Get corresponding built-in thickness parameter based on different component types
            Parameter thicknessParam = null;

            if (elementType is WallType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
            }
            else if (elementType is FloorType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
            }
            else if (elementType is FamilySymbol familySymbol)
            {
                switch (familySymbol.Category?.Id.IntegerValue)
                {
                    case (int)BuiltInCategory.OST_Doors:
                    case (int)BuiltInCategory.OST_Windows:
                        thicknessParam = elementType.get_Parameter(BuiltInParameter.FAMILY_THICKNESS_PARAM);
                        break;
                }
            }
            else if (elementType is CeilingType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.CEILING_THICKNESS);
            }

            if (thicknessParam != null && thicknessParam.HasValue)
            {
                return new ParameterInfo
                {
                    Name = "Thickness",
                    Value = $"{thicknessParam.AsDouble() * 304.8}"
                };
            }
            return null;
        }

        /// <summary>
        /// Get level information that the element belongs to
        /// </summary>
        public static LevelInfo GetElementLevel(Document doc, Element element)
        {
            try
            {
                Level level = null;

                // Handle level retrieval for different element types
                if (element is Wall wall) // Walls
                {
                    level = doc.GetElement(wall.LevelId) as Level;
                }
                else if (element is Floor floor) // Floors
                {
                    Parameter levelParam = floor.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }
                else if (element is FamilyInstance familyInstance) // Family instances (including generic models, etc.)
                {
                    // Try to get level parameter of family instance
                    Parameter levelParam = familyInstance.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                    // If the above method doesn't work, try using SCHEDULE_LEVEL_PARAM
                    if (level == null)
                    {
                        levelParam = familyInstance.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                        if (levelParam != null && levelParam.HasValue)
                        {
                            level = doc.GetElement(levelParam.AsElementId()) as Level;
                        }
                    }
                }
                else // Other elements
                {
                    // Try to get generic level parameter
                    Parameter levelParam = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }

                if (level != null)
                {
                    LevelInfo levelInfo = new LevelInfo
                    {
                        Id = level.Id.IntegerValue,
                        Name = level.Name,
                        Height = level.Elevation * 304.8
                    };
                    return levelInfo;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get bounding box information of element
        /// </summary>
        public static BoundingBoxInfo GetBoundingBoxInfo(Element element)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox == null)
                    return null;
                return new BoundingBoxInfo
                {
                    Min = new JZPoint(
                        bbox.Min.X * 304.8,
                        bbox.Min.Y * 304.8,
                        bbox.Min.Z * 304.8),
                    Max = new JZPoint(
                        bbox.Max.X * 304.8,
                        bbox.Max.Y * 304.8,
                        bbox.Max.Z * 304.8)
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get height parameter information of bounding box
        /// </summary>
        /// <param name="boundingBoxInfo">Bounding box information</param>
        /// <returns>Parameter information object, returns null if invalid</returns>
        public static ParameterInfo GetBoundingBoxHeight(BoundingBoxInfo boundingBoxInfo)
        {
            try
            {
                // Parameter check
                if (boundingBoxInfo?.Min == null || boundingBoxInfo?.Max == null)
                {
                    return null;
                }

                // The difference in Z-axis direction is the height
                double height = Math.Abs(boundingBoxInfo.Max.Z - boundingBoxInfo.Min.Z);

                return new ParameterInfo
                {
                    Name = "Height",
                    Value = $"{height}"
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get names and values of all non-empty parameters in element
        /// </summary>
        /// <param name="element">Revit element</param>
        /// <returns>Parameter information list</returns>
        public static List<ParameterInfo> GetDimensionParameters(Element element)
        {
            // Check if element is null
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

                    // If current parameter is dimension-related parameter
                    if (IsDimensionParameter(param))
                    {
                        // Get string representation of parameter value
                        string value = param.AsValueString();

                        // If value is not empty, add to list
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
                    // If error occurs getting a parameter value, continue with next one
                    continue;
                }
            }

            // Sort by parameter name and return
            return parameters.OrderBy(p => p.Name).ToList();
        }

        /// <summary>
        /// Determine if parameter is a writable dimension parameter
        /// </summary>
        public static bool IsDimensionParameter(Parameter param)
        {

#if REVIT2023_OR_GREATER
            // In Revit 2023, use Definition's GetDataType() method to get parameter type
            ForgeTypeId paramTypeId = param.Definition.GetDataType();

            // Determine if parameter is dimension-related type
            bool isDimensionType = paramTypeId.Equals(SpecTypeId.Length) ||
                                   paramTypeId.Equals(SpecTypeId.Angle) ||
                                   paramTypeId.Equals(SpecTypeId.Area) ||
                                   paramTypeId.Equals(SpecTypeId.Volume);
            // Only store dimension type parameters
            return isDimensionType;
#else
            // Determine if parameter is dimension-related type
            bool isDimensionType = param.Definition.ParameterType == ParameterType.Length ||
                                   param.Definition.ParameterType == ParameterType.Angle ||
                                   param.Definition.ParameterType == ParameterType.Area ||
                                   param.Definition.ParameterType == ParameterType.Volume;

            // Only store dimension type parameters
            return isDimensionType;
#endif
        }

        /// <summary>
        /// Extract comprehensive element parameters using category-specific parameter mappings
        /// </summary>
        /// <param name="element">The Revit element</param>
        /// <returns>List of parameter information</returns>
        private static List<ParameterInfo> ExtractElementParameters(Element element)
        {
            var parameters = new List<ParameterInfo>();
            
            if (element?.Category == null)
                return parameters;

            try
            {
                // Get the built-in category
                var builtInCategory = (BuiltInCategory)element.Category.Id.IntegerValue;
                
                // Check if we have specific mapping for this category
                if (ParameterMappingManager.HasMapping(builtInCategory))
                {
                    // Get common parameter names for this category
                    var commonParams = ParameterMappingManager.GetCommonParameterNames(builtInCategory);
                    
                    foreach (var paramName in commonParams)
                    {
                        var param = ParameterMappingManager.GetParameter(element, paramName, builtInCategory);
                        if (param != null && param.HasValue)
                        {
                            var paramInfo = CreateParameterInfo(param, paramName);
                            if (paramInfo != null)
                            {
                                parameters.Add(paramInfo);
                            }
                        }
                    }
                    
                    // For structural framing, also extract additional important parameters
                    if (builtInCategory == BuiltInCategory.OST_StructuralFraming)
                    {
                        // Extract additional structural parameters that might not be in common list
                        var additionalParams = new[] { 
                            "cut length", "volume", "section area", "nominal weight",
                            "moment of inertia strong axis", "moment of inertia weak axis",
                            "elastic modulus strong axis", "elastic modulus weak axis"
                        };
                        
                        foreach (var paramName in additionalParams)
                        {
                            var param = ParameterMappingManager.GetParameter(element, paramName, builtInCategory);
                            if (param != null && param.HasValue)
                            {
                                var paramInfo = CreateParameterInfo(param, paramName);
                                if (paramInfo != null && !parameters.Any(p => p.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    parameters.Add(paramInfo);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Fallback to basic parameter extraction for unmapped categories
                    parameters = GetDimensionParameters(element);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error extracting parameters for element {element.Id}: {ex.Message}");
                // Fallback to basic parameter extraction
                parameters = GetDimensionParameters(element);
            }
            
            return parameters ?? new List<ParameterInfo>();
        }

        /// <summary>
        /// Create ParameterInfo from a Revit Parameter
        /// </summary>
        /// <param name="param">The Revit parameter</param>
        /// <param name="displayName">Display name for the parameter</param>
        /// <returns>ParameterInfo object</returns>
        private static ParameterInfo CreateParameterInfo(Parameter param, string displayName)
        {
            if (param == null || !param.HasValue)
                return null;

            try
            {
                var paramInfo = new ParameterInfo
                {
                    Name = displayName,
                    Value = param.AsValueString() ?? param.AsString() ?? param.AsDouble().ToString() ?? param.AsInteger().ToString()
                };

                return paramInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating parameter info for {displayName}: {ex.Message}");
                return null;
            }
        }

    }

    /// <summary>
    /// Custom class for storing complete element information
    /// </summary>
    public class ElementInstanceInfo
    {
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Id
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Type Id
        /// </summary>
        public int TypeId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Owning room Id
        /// </summary>
        public int RoomId { get; set; }
        /// <summary>
        /// Owning level name
        /// </summary>
        public LevelInfo Level { get; set; }
        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Instance parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

    }

    /// <summary>
    /// Custom class for storing complete element type information
    /// </summary>
    public class ElementTypeInfo
    {
        /// <summary>
        /// ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Id
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category ID
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Type parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

    }

    /// <summary>
    /// Class for basic information of spatial positioning elements (levels, grids, etc.)
    /// </summary>
    public class PositioningElementInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Element unique ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Element's .NET class name
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Elevation value (applicable to levels, unit mm)
        /// </summary>
        public double? Elevation { get; set; }
        /// <summary>
        /// Owning level
        /// </summary>
        public LevelInfo Level { get; set; }
        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Grid line (applicable to grids)
        /// </summary>
        public JZLine GridLine { get; set; }
    }
    /// <summary>
    /// Class for storing basic information of spatial elements (rooms, areas, etc.)
    /// </summary>
    public class SpatialElementInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Element unique ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Number
        /// </summary>
        public string Number { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Element's .NET class name
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Area (unit mm²)
        /// </summary>
        public double? Area { get; set; }
        /// <summary>
        /// Volume (unit mm³)
        /// </summary>
        public double? Volume { get; set; }
        /// <summary>
        /// Perimeter (unit mm)
        /// </summary>
        public double? Perimeter { get; set; }
        /// <summary>
        /// Located level
        /// </summary>
        public LevelInfo Level { get; set; }

        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }
    /// <summary>
    /// Class for storing basic information of view elements
    /// </summary>
    public class ViewInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Element unique ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Element's .NET class name
        /// </summary>
        public string ElementClass { get; set; }

        /// <summary>
        /// View type
        /// </summary>
        public string ViewType { get; set; }

        /// <summary>
        /// View scale
        /// </summary>
        public int? Scale { get; set; }

        /// <summary>
        /// Whether it's a template view
        /// </summary>
        public bool IsTemplate { get; set; }

        /// <summary>
        /// Detail level
        /// </summary>
        public string DetailLevel { get; set; }

        /// <summary>
        /// Associated level
        /// </summary>
        public LevelInfo AssociatedLevel { get; set; }

        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }

        /// <summary>
        /// Whether the view is open
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// Whether it's the currently active view
        /// </summary>
        public bool IsActive { get; set; }
    }
    /// <summary>
    /// Class for storing basic information of annotation elements
    /// </summary>
    public class AnnotationInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Element unique ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Element's .NET class name
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Owner view
        /// </summary>
        public string OwnerView { get; set; }
        /// <summary>
        /// Text content (applicable to text notes)
        /// </summary>
        public string TextContent { get; set; }
        /// <summary>
        /// Position information (unit mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Dimension value (applicable to dimensions)
        /// </summary>
        public string DimensionValue { get; set; }
    }
    /// <summary>
    /// Class for storing basic information of groups and links
    /// </summary>
    public class GroupOrLinkInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Element unique ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Element's .NET class name
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Group member count
        /// </summary>
        public int? MemberCount { get; set; }
        /// <summary>
        /// Group type
        /// </summary>
        public string GroupType { get; set; }
        /// <summary>
        /// Link status
        /// </summary>
        public string LinkStatus { get; set; }
        /// <summary>
        /// Link path
        /// </summary>
        public string LinkPath { get; set; }
        /// <summary>
        /// Position information (unit mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }
    /// <summary>
    /// Enhanced class for storing element basic information
    /// </summary>
    public class ElementBasicInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Element unique ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }



    /// <summary>
    /// Custom class for storing complete parameter information
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// Custom class for storing bounding box information
    /// </summary>
    public class BoundingBoxInfo
    {
        public JZPoint Min { get; set; }
        public JZPoint Max { get; set; }
    }

    /// <summary>
    /// Custom class for storing level information
    /// </summary>
    public class LevelInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Height { get; set; }
    }

}

