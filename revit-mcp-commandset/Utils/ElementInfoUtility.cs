using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.ElementInfos;
using System;

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
    }
} 