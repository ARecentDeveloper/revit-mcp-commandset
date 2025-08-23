using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Services
{
    public class ViewRangePlaneService
    {
        private const string VISUALIZATION_GROUP = "ViewRangeVisualization";

        public ElementId CreateColoredPlane(Document doc, List<XYZ> boundary, double height, 
            Color color, string levelName, string planeName, ElementId solidFillId, ElementId planViewId)
        {
            // Adjust boundary to the specified height
            var elevatedBoundary = boundary.Select(pt => new XYZ(pt.X, pt.Y, height)).ToList();

            // Create a rectangular face based on the elevated boundary
            var boundaryLoop = CurveLoop.Create(new List<Curve>
            {
                Line.CreateBound(elevatedBoundary[0], elevatedBoundary[1]),
                Line.CreateBound(elevatedBoundary[1], elevatedBoundary[2]),
                Line.CreateBound(elevatedBoundary[2], elevatedBoundary[3]),
                Line.CreateBound(elevatedBoundary[3], elevatedBoundary[0])
            });

            // Generate the surface as a DirectShape
            var profile = new List<CurveLoop> { boundaryLoop };
            var solid = GeometryCreationUtilities.CreateExtrusionGeometry(
                profile, new XYZ(0, 0, 1), 0.01); // Thin extrusion (0.01 feet)

            var directShape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            directShape.SetShape(new List<GeometryObject> { solid });

            // Tag the element with a group name
            directShape.Name = VISUALIZATION_GROUP;

            // Apply Override Graphics in View
            var ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceTransparency(50); // 50% transparency
            ogs.SetSurfaceForegroundPatternColor(color);
            ogs.SetSurfaceForegroundPatternId(solidFillId);
            doc.ActiveView.SetElementOverrides(directShape.Id, ogs);

            // Set the comments parameter to include plan view ID and original elevation for automatic linking
            var commentsParam = directShape.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (commentsParam != null)
            {
                var commentValue = $"{planeName}|{levelName}|{planViewId.Value}|{height}";
                commentsParam.Set(commentValue);
            }

            return directShape.Id;
        }

        public double? GetPlaneElevationFromGeometry(Element directShape)
        {
            try
            {
                // Use the element's bounding box directly - this should reflect any moves
                var bbox = directShape.get_BoundingBox(null); // null means use the element's coordinate system
                if (bbox != null)
                {
                    // Since our extrusion is very thin (0.01), use Min Z as the base elevation
                    return bbox.Min.Z;
                }
            }
            catch (Exception)
            {
                // Ignore errors and return null
            }

            return null;
        }

        public Dictionary<string, double> ExtractPlaneElevations(List<Element> visualizationElements)
        {
            var planeElevations = new Dictionary<string, double>();

            foreach (var element in visualizationElements)
            {
                var commentsParam = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (commentsParam?.AsString() != null)
                {
                    var comment = commentsParam.AsString();
                    var elevation = GetPlaneElevationFromGeometry(element);

                    if (elevation.HasValue)
                    {
                        // Parse the comment format: "PlaneType|LevelName|ViewID|OriginalHeight"
                        var commentParts = comment.Split('|');
                        if (commentParts.Length >= 1)
                        {
                            var planeType = commentParts[0];
                            var key = planeType.ToLower().Replace(" ", "_");
                            planeElevations[key] = elevation.Value;
                        }
                    }
                }
            }

            return planeElevations;
        }

        public PlaneMetadata GetPlaneMetadata(Element visualizationElement)
        {
            var commentsParam = visualizationElement.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (commentsParam?.AsString() == null)
            {
                return null;
            }

            var commentParts = commentsParam.AsString().Split('|');
            if (commentParts.Length < 4)
            {
                return null;
            }

            var metadata = new PlaneMetadata
            {
                PlaneType = commentParts[0],
                LevelName = commentParts[1]
            };

            if (long.TryParse(commentParts[2], out long viewId))
            {
                metadata.OriginalViewId = new ElementId(viewId);
            }

            if (double.TryParse(commentParts[3], out double originalHeight))
            {
                metadata.OriginalHeight = originalHeight;
            }

            metadata.CurrentElevation = GetPlaneElevationFromGeometry(visualizationElement);

            return metadata;
        }

        public List<Element> GetVisualizationElements(Document doc, View activeView)
        {
            var collector = new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(DirectShape));

            return collector
                .Where(el => el.Name == VISUALIZATION_GROUP)
                .ToList();
        }

        public ElementId GetOriginalPlanViewId(List<Element> visualizationElements)
        {
            foreach (var element in visualizationElements)
            {
                var metadata = GetPlaneMetadata(element);
                if (metadata?.OriginalViewId != null)
                {
                    return metadata.OriginalViewId;
                }
            }
            return null;
        }

        public bool ValidatePlaneHierarchy(Dictionary<string, double> planeElevations, out string errorMessage)
        {
            errorMessage = null;

            // Required planes for validation
            var requiredPlanes = new[] { "top", "cut", "bottom" };
            var missingPlanes = requiredPlanes.Where(plane => !planeElevations.ContainsKey(plane)).ToList();

            if (missingPlanes.Any())
            {
                errorMessage = $"Missing required planes: {string.Join(", ", missingPlanes)}";
                return false;
            }

            // Validate hierarchy: Top >= Cut >= Bottom >= View Depth (if present)
            if (planeElevations["top"] < planeElevations["cut"])
            {
                errorMessage = "Cut plane cannot be above Top plane";
                return false;
            }

            if (planeElevations["cut"] < planeElevations["bottom"])
            {
                errorMessage = "Bottom plane cannot be above Cut plane";
                return false;
            }

            if (planeElevations.ContainsKey("view_depth"))
            {
                if (planeElevations["bottom"] < planeElevations["view_depth"])
                {
                    errorMessage = "View Depth plane cannot be above Bottom plane";
                    return false;
                }
            }

            return true;
        }
    }

    public class PlaneMetadata
    {
        public string PlaneType { get; set; }
        public string LevelName { get; set; }
        public ElementId OriginalViewId { get; set; }
        public double OriginalHeight { get; set; }
        public double? CurrentElevation { get; set; }

        public double? MovementDistance => CurrentElevation.HasValue ? 
            CurrentElevation.Value - OriginalHeight : null;

        public string MovementDirection => MovementDistance.HasValue ?
            (MovementDistance.Value > 0 ? "up" : MovementDistance.Value < 0 ? "down" : "none") : "unknown";
    }
}