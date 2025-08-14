using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.ElementInfos;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Services.ElementInfoFactories
{
    /// <summary>
    /// Factory for creating PositioningElementInfo objects
    /// </summary>
    public class PositioningElementInfoFactory : IElementInfoFactory
    {
        public bool CanHandle(Element element)
        {
            return element is Level || element is Grid;
        }

        public object CreateInfo(Document doc, Element element)
        {
            return CreateInfo(doc, element, "basic", null);
        }

        public object CreateInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters)
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
                    BoundingBox = ElementInfoUtility.GetBoundingBoxInfo(element)
                };

                // Process level
                if (element is Level level)
                {
                    // Convert to mm
                    info.Elevation = level.Elevation * 304.8;
                }
                // Process grid
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
                info.Level = ElementInfoUtility.GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating spatial positioning element information: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Factory for creating AnnotationInfo objects
    /// </summary>
    public class AnnotationInfoFactory : IElementInfoFactory
    {
        public bool CanHandle(Element element)
        {
            return element is TextNote || element is Dimension ||
                   element is IndependentTag || element is AnnotationSymbol ||
                   element is SpotDimension;
        }

        public object CreateInfo(Document doc, Element element)
        {
            return CreateInfo(doc, element, "basic", null);
        }

        public object CreateInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters)
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
                    BoundingBox = ElementInfoUtility.GetBoundingBoxInfo(element)
                };

                // Get the owner view
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

                // Process text note
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
                // Process dimension
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
                // Process other annotation elements
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
    }

    /// <summary>
    /// Factory for creating GroupOrLinkInfo objects
    /// </summary>
    public class GroupOrLinkInfoFactory : IElementInfoFactory
    {
        public bool CanHandle(Element element)
        {
            return element is Group || element is RevitLinkInstance;
        }

        public object CreateInfo(Document doc, Element element)
        {
            return CreateInfo(doc, element, "basic", null);
        }

        public object CreateInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters)
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
                    BoundingBox = ElementInfoUtility.GetBoundingBoxInfo(element)
                };

                // Process group
                if (element is Group group)
                {
                    ICollection<ElementId> memberIds = group.GetMemberIds();
                    info.MemberCount = memberIds?.Count;
                    info.GroupType = group.GroupType?.Name;
                }
                // Process link
                else if (element is RevitLinkInstance linkInstance)
                {
                    RevitLinkType linkType = doc.GetElement(linkInstance.GetTypeId()) as RevitLinkType;
                    if (linkType != null)
                    {
                        ExternalFileReference extFileRef = linkType.GetExternalFileReference();
                        // Get absolute path
                        string absPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extFileRef.GetAbsolutePath());
                        info.LinkPath = absPath;

                        // Use GetLinkedFileStatus to get link status
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
    }

    /// <summary>
    /// Factory for creating ElementBasicInfo objects (fallback)
    /// </summary>
    public class ElementBasicInfoFactory : IElementInfoFactory
    {
        public bool CanHandle(Element element)
        {
            // This is the fallback factory - it can handle any element
            return element != null;
        }

        public object CreateInfo(Document doc, Element element)
        {
            return CreateInfo(doc, element, "basic", null);
        }

        public object CreateInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters)
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
                    BoundingBox = ElementInfoUtility.GetBoundingBoxInfo(element)
                };

                // Add parameter extraction for fallback factory too!
                List<ParameterInfo> parameters = new List<ParameterInfo>();
                
                if (requestedParameters != null && requestedParameters.Any())
                {
                    // Extract specific requested parameters
                    parameters = ElementInfoUtility.GetSpecificParameters(element, requestedParameters);
                }
                else
                {
                    // Extract based on detail level
                    switch (detailLevel?.ToLower())
                    {
                        case "detailed":
                        case "all":
                            parameters = ElementInfoUtility.GetMappedParameters(element);
                            break;
                        case "standard":
                            parameters = ElementInfoUtility.GetMappedParameters(element);
                            break;
                        case "basic":
                        default:
                            parameters = ElementInfoUtility.GetBasicParameters(element);
                            break;
                    }
                }
                
                basicInfo.Parameters.AddRange(parameters);
                
                return basicInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating basic element information: {ex.Message}");
                return null;
            }
        }
    }
} 