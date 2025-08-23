using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitMCPCommandSet.Services.ViewRange;

namespace RevitMCPCommandSet.Services
{
    public class ViewRangeUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public List<PlaneMovementInfo> MovementInfo { get; set; } = new List<PlaneMovementInfo>();
    }

    public class ViewRangeUpdateService
    {
        private readonly ViewRangeCoordinateService _coordinateService;
        private readonly ViewRangePlaneService _planeService;
        private readonly ViewRangeValidationService _validationService;

        public ViewRangeUpdateService()
        {
            _coordinateService = new ViewRangeCoordinateService();
            _planeService = new ViewRangePlaneService();
            _validationService = new ViewRangeValidationService();
        }

        public ViewRangeUpdateResult UpdateViewRangeFromPlanes(Document doc, ViewPlan planView, List<Element> visualizationElements)
        {
            var result = new ViewRangeUpdateResult();

            try
            {
                // Extract plane elevations and identify plane types
                var planeElevations = _planeService.ExtractPlaneElevations(visualizationElements);

                // Validate plane hierarchy
                if (!_planeService.ValidatePlaneHierarchy(planeElevations, out string validationError))
                {
                    result.Success = false;
                    result.Error = validationError;
                    return result;
                }

                // Get current view range
                var viewRange = planView.GetViewRange();
                var currentViewRangeInfo = _coordinateService.GetViewRangeInfo(planView);

                // Calculate movement information for reporting
                var movementInfo = CalculateMovementInfo(planeElevations, currentViewRangeInfo, visualizationElements);
                result.MovementInfo = movementInfo;

                // Calculate new offsets by converting internal elevations back to level-relative offsets
                var newTopOffset = _coordinateService.ConvertInternalToLevelOffset(
                    doc, currentViewRangeInfo.TopLevelId, planeElevations["top"]);
                var newCutOffset = _coordinateService.ConvertInternalToLevelOffset(
                    doc, currentViewRangeInfo.CutLevelId, planeElevations["cut"]);
                var newBottomOffset = _coordinateService.ConvertInternalToLevelOffset(
                    doc, currentViewRangeInfo.BottomLevelId, planeElevations["bottom"]);

                // Handle View Depth plane if user moved it
                if (planeElevations.ContainsKey("view_depth"))
                {
                    var newViewDepthOffset = _coordinateService.ConvertInternalToLevelOffset(
                        doc, currentViewRangeInfo.ViewDepthLevelId, planeElevations["view_depth"]);
                    viewRange.SetOffset(PlanViewPlane.ViewDepthPlane, newViewDepthOffset);
                }

                // Additional validation with existing View Depth if not moved by user
                if (!planeElevations.ContainsKey("view_depth"))
                {
                    var currentViewDepthElevation = _coordinateService.ConvertLevelOffsetToInternal(
                        doc, currentViewRangeInfo.ViewDepthLevelId, currentViewRangeInfo.ViewDepthOffset);

                    if (planeElevations["bottom"] < currentViewDepthElevation)
                    {
                        result.Success = false;
                        result.Error = "Bottom plane cannot be below View Depth plane";
                        return result;
                    }
                }

                // Update the view range
                viewRange.SetOffset(PlanViewPlane.TopClipPlane, newTopOffset);
                viewRange.SetOffset(PlanViewPlane.CutPlane, newCutOffset);
                viewRange.SetOffset(PlanViewPlane.BottomClipPlane, newBottomOffset);

                planView.SetViewRange(viewRange);

                // Build success message
                var messageParts = new List<string>
                {
                    $"Successfully updated view range for '{planView.Name}'!"
                };

                if (movementInfo.Any(m => Math.Abs(m.Distance) > 0.001))
                {
                    messageParts.Add("Plane movements:");
                    foreach (var movement in movementInfo.Where(m => Math.Abs(m.Distance) > 0.001))
                    {
                        messageParts.Add($"• {movement.PlaneName} plane moved {movement.Direction} {Math.Abs(movement.Distance):F2}'");
                    }
                }
                else
                {
                    messageParts.Add("No planes were moved.");
                }

                messageParts.Add("Visualization planes removed.");
                result.Message = string.Join("\n", messageParts);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"Error updating view range: {ex.Message}";
            }

            return result;
        }

        private List<PlaneMovementInfo> CalculateMovementInfo(Dictionary<string, double> planeElevations, 
            ViewRangeInfo currentViewRangeInfo, List<Element> visualizationElements)
        {
            var movementInfo = new List<PlaneMovementInfo>();

            // Get original elevations from metadata
            var planeMetadata = visualizationElements
                .Select(el => _planeService.GetPlaneMetadata(el))
                .Where(metadata => metadata != null)
                .ToDictionary(metadata => metadata.PlaneType.ToLower().Replace(" ", "_"), metadata => metadata);

            // Calculate movements for each plane type
            var planeTypes = new Dictionary<string, string>
            {
                { "top", "Top" },
                { "cut", "Cut" },
                { "bottom", "Bottom" },
                { "view_depth", "View Depth" }
            };

            foreach (var kvp in planeTypes)
            {
                var key = kvp.Key;
                var displayName = kvp.Value;

                if (planeElevations.ContainsKey(key) && planeMetadata.ContainsKey(key))
                {
                    var currentElevation = planeElevations[key];
                    var originalElevation = planeMetadata[key].OriginalHeight;
                    var movement = currentElevation - originalElevation;

                    movementInfo.Add(new PlaneMovementInfo
                    {
                        PlaneName = displayName,
                        Direction = movement > 0 ? "up" : movement < 0 ? "down" : "none",
                        Distance = Math.Abs(movement)
                    });
                }
            }

            return movementInfo;
        }

        public bool ValidateViewRangeUpdate(ViewPlan planView, Dictionary<string, double> newElevations, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                // Get current view range info
                var currentInfo = _coordinateService.GetViewRangeInfo(planView);

                // Validate that we have the required planes
                var requiredPlanes = new[] { "top", "cut", "bottom" };
                var missingPlanes = requiredPlanes.Where(plane => !newElevations.ContainsKey(plane)).ToList();

                if (missingPlanes.Any())
                {
                    errorMessage = $"Missing required planes for update: {string.Join(", ", missingPlanes)}";
                    return false;
                }

                // Validate plane hierarchy
                return _planeService.ValidatePlaneHierarchy(newElevations, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = $"Error validating view range update: {ex.Message}";
                return false;
            }
        }

        public ViewRangeUpdateResult PreviewViewRangeUpdate(Document doc, ViewPlan planView, List<Element> visualizationElements)
        {
            var result = new ViewRangeUpdateResult();

            try
            {
                // Extract plane elevations
                var planeElevations = _planeService.ExtractPlaneElevations(visualizationElements);

                // Validate without actually updating
                if (!ValidateViewRangeUpdate(planView, planeElevations, out string validationError))
                {
                    result.Success = false;
                    result.Error = validationError;
                    return result;
                }

                // Calculate movement information
                var currentViewRangeInfo = _coordinateService.GetViewRangeInfo(planView);
                result.MovementInfo = CalculateMovementInfo(planeElevations, currentViewRangeInfo, visualizationElements);

                result.Success = true;
                result.Message = "View range update validation successful";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"Error previewing view range update: {ex.Message}";
            }

            return result;
        }
    }
}