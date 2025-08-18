using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMCPCommandSet.Models.ElementInfos;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Services.ElementInfoFactories
{
    /// <summary>
    /// Factory for creating SpatialElementInfo objects
    /// </summary>
    public class SpatialElementInfoFactory : IElementInfoFactory
    {
        public bool CanHandle(Element element)
        {
            return element is SpatialElement;
        }

        public object CreateInfo(Document doc, Element element)
        {
            return CreateInfo(doc, element, "basic", null);
        }

        public object CreateInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters)
        {
            try
            {
                if (element == null || !(element is SpatialElement))
                    return null;
                SpatialElement spatialElement = element as SpatialElement;
                SpatialElementInfo info = new SpatialElementInfo
                {
                    Id = (int)element.Id.Value,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.Value) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = ElementInfoUtility.GetBoundingBoxInfo(element)
                };

                // Get the number of the room or area
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
                info.Level = ElementInfoUtility.GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating spatial element information: {ex.Message}");
                return null;
            }
        }
    }
} 