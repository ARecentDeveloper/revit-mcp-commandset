using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Services
{
    public class ViewRangeValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public Dictionary<string, double> ValidatedElevations { get; set; } = new Dictionary<string, double>();
    }

    public class ViewRangeValidationService
    {
        private const double TOLERANCE = 0.001; // 1/1000 of a foot tolerance

        public ViewRangeValidationResult ValidateViewRangeConfiguration(ViewPlan planView)
        {
            var result = new ViewRangeValidationResult { IsValid = true };

            try
            {
                var viewRange = planView.GetViewRange();
                var doc = planView.Document;

                // Check each plane
                var planes = new Dictionary<string, PlanViewPlane>
                {
                    { "Top", PlanViewPlane.TopClipPlane },
                    { "Cut", PlanViewPlane.CutPlane },
                    { "Bottom", PlanViewPlane.BottomClipPlane },
                    { "View Depth", PlanViewPlane.ViewDepthPlane }
                };

                var elevations = new Dictionary<string, double?>();

                foreach (var plane in planes)
                {
                    var levelId = viewRange.GetLevelId(plane.Value);
                    var offset = viewRange.GetOffset(plane.Value);

                    if (levelId.Value < 0)
                    {
                        elevations[plane.Key] = null; // Unlimited
                        result.Warnings.Add($"{plane.Key} plane is set to unlimited");
                    }
                    else
                    {
                        var level = doc.GetElement(levelId) as Level;
                        if (level == null)
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"Level for {plane.Key} plane not found";
                            return result;
                        }

                        elevations[plane.Key] = level.Elevation + offset;
                        result.ValidatedElevations[plane.Key.ToLower().Replace(" ", "_")] = elevations[plane.Key].Value;
                    }
                }

                // Validate hierarchy for non-unlimited planes
                if (!ValidateElevationHierarchy(elevations, out string hierarchyError))
                {
                    result.IsValid = false;
                    result.ErrorMessage = hierarchyError;
                    return result;
                }

                // Check for potential issues
                CheckForPotentialIssues(elevations, result);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Error validating view range: {ex.Message}";
            }

            return result;
        }

        public bool ValidateElevationHierarchy(Dictionary<string, double?> elevations, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                // Get non-null elevations for comparison
                var validElevations = elevations
                    .Where(kvp => kvp.Value.HasValue)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);

                // Check Top >= Cut
                if (validElevations.ContainsKey("Top") && validElevations.ContainsKey("Cut"))
                {
                    if (validElevations["Top"] < validElevations["Cut"] - TOLERANCE)
                    {
                        errorMessage = $"Top plane ({validElevations["Top"]:F3}') cannot be below Cut plane ({validElevations["Cut"]:F3}')";
                        return false;
                    }
                }

                // Check Cut >= Bottom
                if (validElevations.ContainsKey("Cut") && validElevations.ContainsKey("Bottom"))
                {
                    if (validElevations["Cut"] < validElevations["Bottom"] - TOLERANCE)
                    {
                        errorMessage = $"Cut plane ({validElevations["Cut"]:F3}') cannot be below Bottom plane ({validElevations["Bottom"]:F3}')";
                        return false;
                    }
                }

                // Check Bottom >= View Depth
                if (validElevations.ContainsKey("Bottom") && validElevations.ContainsKey("View Depth"))
                {
                    if (validElevations["Bottom"] < validElevations["View Depth"] - TOLERANCE)
                    {
                        errorMessage = $"Bottom plane ({validElevations["Bottom"]:F3}') cannot be below View Depth plane ({validElevations["View Depth"]:F3}')";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error validating elevation hierarchy: {ex.Message}";
                return false;
            }
        }

        public bool ValidatePlaneMovement(Dictionary<string, double> originalElevations, 
            Dictionary<string, double> newElevations, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                // Check that we're not moving planes in invalid ways
                foreach (var kvp in newElevations)
                {
                    var planeName = kvp.Key;
                    var newElevation = kvp.Value;

                    if (originalElevations.ContainsKey(planeName))
                    {
                        var originalElevation = originalElevations[planeName];
                        var movement = Math.Abs(newElevation - originalElevation);

                        // Check for excessive movement (more than 100 feet)
                        if (movement > 100.0)
                        {
                            errorMessage = $"{planeName} plane moved {movement:F2}' which seems excessive. Please verify the movement.";
                            return false;
                        }
                    }
                }

                // Validate the final hierarchy
                var elevationsForValidation = newElevations.ToDictionary(
                    kvp => CapitalizePlaneName(kvp.Key), 
                    kvp => (double?)kvp.Value);

                return ValidateElevationHierarchy(elevationsForValidation, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = $"Error validating plane movement: {ex.Message}";
                return false;
            }
        }

        public bool ValidateActiveView(Document doc, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                var activeView = doc.ActiveView;

                if (!(activeView is View3D))
                {
                    errorMessage = "Please switch to a 3D view to visualize view ranges";
                    return false;
                }

                if (activeView.IsTemplate)
                {
                    errorMessage = "Cannot create visualization in a view template";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error validating active view: {ex.Message}";
                return false;
            }
        }

        public bool ValidatePlanView(ViewPlan planView, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (planView == null)
                {
                    errorMessage = "Plan view is null";
                    return false;
                }

                if (planView.IsTemplate)
                {
                    errorMessage = "Cannot visualize view range for view templates";
                    return false;
                }

                if (planView.ViewType != ViewType.FloorPlan && planView.ViewType != ViewType.EngineeringPlan)
                {
                    errorMessage = "Selected view is not a suitable plan view type";
                    return false;
                }

                var cropBox = planView.CropBox;
                if (cropBox == null)
                {
                    errorMessage = $"No crop box defined for view '{planView.Name}'";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error validating plan view: {ex.Message}";
                return false;
            }
        }

        private void CheckForPotentialIssues(Dictionary<string, double?> elevations, ViewRangeValidationResult result)
        {
            // Check for planes that are very close together
            var validElevations = elevations
                .Where(kvp => kvp.Value.HasValue)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);

            var planeNames = validElevations.Keys.ToList();
            for (int i = 0; i < planeNames.Count; i++)
            {
                for (int j = i + 1; j < planeNames.Count; j++)
                {
                    var plane1 = planeNames[i];
                    var plane2 = planeNames[j];
                    var diff = Math.Abs(validElevations[plane1] - validElevations[plane2]);

                    if (diff < 0.1) // Less than 0.1 feet apart
                    {
                        result.Warnings.Add($"{plane1} and {plane2} planes are very close ({diff:F3}' apart)");
                    }
                }
            }

            // Check for unlimited planes
            var unlimitedPlanes = elevations.Where(kvp => !kvp.Value.HasValue).Select(kvp => kvp.Key).ToList();
            if (unlimitedPlanes.Count == elevations.Count)
            {
                result.Warnings.Add("All view range planes are set to unlimited - no visualization can be created");
            }
            else if (unlimitedPlanes.Any())
            {
                result.Warnings.Add($"Some planes are unlimited: {string.Join(", ", unlimitedPlanes)}");
            }
        }

        private string CapitalizePlaneName(string planeName)
        {
            return planeName switch
            {
                "top" => "Top",
                "cut" => "Cut",
                "bottom" => "Bottom",
                "view_depth" => "View Depth",
                _ => planeName
            };
        }
    }
}