using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.ElementInfos;
using System;
using System.Collections.Generic;
using RevitMCPCommandSet.Utils.ParameterMappings;

namespace RevitMCPCommandSet.Utils
{
    /// <summary>
    /// Utility class for common element info operations
    /// </summary>
    public static class ElementInfoUtility
    {
        /// <summary>
        /// Get the level information to which the element belongs
        /// </summary>
        public static LevelInfo GetElementLevel(Document doc, Element element)
        {
            try
            {
                Level level = null;

                // Process level acquisition for different types of elements
                if (element is Wall wall) // Wall
                {
                    level = doc.GetElement(wall.LevelId) as Level;
                }
                else if (element is Floor floor) // Floor
                {
                    Parameter levelParam = floor.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }
                else if (element is FamilyInstance familyInstance) // Family instance (including generic models, etc.)
                {
                    // Try to get the level parameter of the family instance
                    Parameter levelParam = familyInstance.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                    // If the above method fails, try using SCHEDULE_LEVEL_PARAM
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
                    // Try to get the common level parameter
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
        /// Get element's bounding box information
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
        /// Get height parameter information of the bounding box
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

                // The difference in the Z-axis direction is the height
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
        /// Get thickness parameter information of system family components
        /// </summary>
        /// <param name="element">System family component (wall, floor, door, etc.)</param>
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

            // Get the corresponding built-in thickness parameter according to different component types
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
        /// Retrieve basic parameters for an element (minimal set for general info)
        /// </summary>
        public static List<ParameterInfo> GetBasicParameters(Element element)
        {
            var list = new List<ParameterInfo>();
            if (element?.Category == null) 
            {
                System.Diagnostics.Trace.WriteLine($"GetBasicParameters: Element or category is null");
                return list;
            }

            try
            {
                var bic = (BuiltInCategory)element.Category.Id.IntegerValue;
                System.Diagnostics.Trace.WriteLine($"GetBasicParameters: Element {element.Id.IntegerValue} ({element.Name}), Category: {bic}");
                
                if (!ParameterMappingManager.HasMapping(bic)) 
                {
                    System.Diagnostics.Trace.WriteLine($"GetBasicParameters: No mapping found for category {bic}");
                    return list;
                }

                // Basic parameters that are always useful
                var basicParams = new List<string> { "length", "height", "width", "mark", "level", "reference level" };
                System.Diagnostics.Trace.WriteLine($"GetBasicParameters: Looking for basic parameters: {string.Join(", ", basicParams)}");
                
                foreach (var name in basicParams)
                {
                    System.Diagnostics.Trace.WriteLine($"GetBasicParameters: Looking for parameter '{name}'");
                    var param = ParameterMappingManager.GetParameter(element, name, bic);
                    
                    if (param == null)
                    {
                        System.Diagnostics.Trace.WriteLine($"GetBasicParameters: Parameter '{name}' not found");
                        continue;
                    }
                    
                    if (!param.HasValue)
                    {
                        System.Diagnostics.Trace.WriteLine($"GetBasicParameters: Parameter '{name}' has no value");
                        continue;
                    }

                    var valStr = GetParameterDisplayValue(param);
                    System.Diagnostics.Trace.WriteLine($"GetBasicParameters: Parameter '{name}' value: '{valStr}'");
                    
                    if (!string.IsNullOrWhiteSpace(valStr))
                    {
                        list.Add(new ParameterInfo { Name = name, Value = valStr });
                        System.Diagnostics.Trace.WriteLine($"GetBasicParameters: Added parameter '{name}' = '{valStr}'");
                    }
                }
                
                System.Diagnostics.Trace.WriteLine($"GetBasicParameters: Returning {list.Count} parameters");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"GetBasicParameters: Error - {ex.Message}");
            }

            return list;
        }

        /// <summary>
        /// Retrieve specific parameters for an element based on requested parameter names
        /// </summary>
        public static List<ParameterInfo> GetSpecificParameters(Element element, List<string> parameterNames)
        {
            var list = new List<ParameterInfo>();
            if (element?.Category == null || parameterNames == null || !parameterNames.Any()) 
            {
                System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: Early return - element category: {element?.Category?.Name}, parameterNames count: {parameterNames?.Count}");
                return list;
            }

            try
            {
                var bic = (BuiltInCategory)element.Category.Id.IntegerValue;
                System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: Element {element.Id.IntegerValue} ({element.Name}), Category: {bic}");
                
                if (!ParameterMappingManager.HasMapping(bic)) 
                {
                    System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: No mapping found for category {bic}");
                    return list;
                }

                System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: Requesting parameters: {string.Join(", ", parameterNames)}");

                // DIAGNOSTIC: Try to get ANY parameter from this element to see if the issue is element-specific
                System.Diagnostics.Trace.WriteLine($"DIAGNOSTIC: Checking if element has ANY parameters...");
                var allParams = element.Parameters;
                System.Diagnostics.Trace.WriteLine($"DIAGNOSTIC: Element has {allParams.Size} total parameters");
                
                // List first 10 parameters for debugging
                int count = 0;
                foreach (Parameter p in allParams)
                {
                    if (count >= 10) break;
                    try
                    {
                        string paramName = p.Definition?.Name ?? "Unknown";
                        string paramValue = p.HasValue ? (p.AsValueString() ?? p.AsString() ?? p.AsDouble().ToString()) : "No Value";
                        System.Diagnostics.Trace.WriteLine($"DIAGNOSTIC: Parameter[{count}]: '{paramName}' = '{paramValue}'");
                        count++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"DIAGNOSTIC: Error reading parameter[{count}]: {ex.Message}");
                        count++;
                    }
                }

                foreach (var name in parameterNames)
                {
                    System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: Looking for parameter '{name}'");
                    var param = ParameterMappingManager.GetParameter(element, name, bic);
                    
                    if (param == null)
                    {
                        System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: Parameter '{name}' not found");
                        continue;
                    }
                    
                    if (!param.HasValue)
                    {
                        System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: Parameter '{name}' has no value");
                        continue;
                    }

                    var valStr = GetParameterDisplayValue(param);
                    System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: Parameter '{name}' value: '{valStr}'");
                    
                    if (!string.IsNullOrWhiteSpace(valStr))
                    {
                        list.Add(new ParameterInfo { Name = name, Value = valStr });
                        System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: Added parameter '{name}' = '{valStr}'");
                    }
                }
                
                // DIAGNOSTIC: Try direct parameter lookup as fallback test
                if (list.Count == 0)
                {
                    System.Diagnostics.Trace.WriteLine($"DIAGNOSTIC: No parameters found via mapping, trying direct lookup...");
                    foreach (var name in parameterNames)
                    {
                        try
                        {
                            var directParam = element.LookupParameter(name);
                            if (directParam != null && directParam.HasValue)
                            {
                                var directValue = GetParameterDisplayValue(directParam);
                                if (!string.IsNullOrWhiteSpace(directValue))
                                {
                                    list.Add(new ParameterInfo { Name = name, Value = directValue });
                                    System.Diagnostics.Trace.WriteLine($"DIAGNOSTIC: Found via direct lookup: '{name}' = '{directValue}'");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine($"DIAGNOSTIC: Direct lookup failed for '{name}': {ex.Message}");
                        }
                    }
                }

                System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: Returning {list.Count} parameters");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"GetSpecificParameters: Error - {ex.Message}");
            }

            return list;
        }

        /// <summary>
        /// Retrieve a set of parameters for an element based on its category-specific mapping.
        /// Only parameters that have a non-empty display value are returned.
        /// </summary>
        public static List<ParameterInfo> GetMappedParameters(Element element)
        {
            var list = new List<ParameterInfo>();
            if (element?.Category == null) return list;

            try
            {
                var bic = (BuiltInCategory)element.Category.Id.IntegerValue;
                if (!ParameterMappingManager.HasMapping(bic)) return list;

                var names = ParameterMappingManager.GetCommonParameterNames(bic);
                if (names == null || names.Count == 0) return list;

                foreach (var name in names)
                {
                    var param = ParameterMappingManager.GetParameter(element, name, bic);
                    if (param == null || !param.HasValue) continue;

                    var valStr = GetParameterDisplayValue(param);
                    if (!string.IsNullOrWhiteSpace(valStr))
                    {
                        list.Add(new ParameterInfo { Name = name, Value = valStr });
                    }
                }
            }
            catch
            {
                // Ignore mapping errors
            }

            return list;
        }

        /// <summary>
        /// Helper method to get parameter display value consistently
        /// </summary>
        private static string GetParameterDisplayValue(Parameter param)
        {
            var valStr = param.AsValueString() ?? param.AsString();
            if (string.IsNullOrWhiteSpace(valStr))
            {
                // Try numeric value for doubles
                if (param.StorageType == StorageType.Double)
                {
                    valStr = param.AsDouble().ToString();
                }
                else if (param.StorageType == StorageType.Integer)
                {
                    valStr = param.AsInteger().ToString();
                }
            }
            return valStr;
        }
    }
} 