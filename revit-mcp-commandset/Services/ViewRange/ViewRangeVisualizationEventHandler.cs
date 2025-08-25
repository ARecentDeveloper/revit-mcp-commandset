using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Commands;

namespace RevitMCPCommandSet.Services.ViewRange
{
    public class ViewRangeVisualizationEventArgs
    {
        public string Action { get; set; } = "visualize";
        public string ViewId { get; set; }
        public string ViewName { get; set; }
        public Document Document { get; set; }
        public UIDocument UIDocument { get; set; }
    }

    public class ViewRangeVisualizationResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("viewName")]
        public string ViewName { get; set; }

        [JsonProperty("planesCreated")]
        public List<ViewRangePlaneInfo> PlanesCreated { get; set; } = new List<ViewRangePlaneInfo>();

        [JsonProperty("planesSkipped")]
        public List<string> PlanesSkipped { get; set; } = new List<string>();

        [JsonProperty("movementInfo")]
        public List<PlaneMovementInfo> MovementInfo { get; set; } = new List<PlaneMovementInfo>();

        [JsonProperty("error")]
        public string Error { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    public class ViewRangePlaneInfo
    {
        [JsonProperty("planeName")]
        public string PlaneName { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("elevation")]
        public double Elevation { get; set; }

        [JsonProperty("elementId")]
        public int ElementId { get; set; }
    }

    public class PlaneMovementInfo
    {
        [JsonProperty("planeName")]
        public string PlaneName { get; set; }

        [JsonProperty("direction")]
        public string Direction { get; set; }

        [JsonProperty("distance")]
        public double Distance { get; set; }
    }

    public class ViewRangeVisualizationEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private const string VISUALIZATION_GROUP = "ViewRangeVisualization";

        private readonly ViewRangeCoordinateService _coordinateService;
        private readonly ViewRangePlaneService _planeService;
        private readonly ViewRangeUpdateService _updateService;
        private readonly ViewRangeValidationService _validationService;

        private ViewRangeVisualizationRequest _parameters;
        private object _result;
        
        // Status synchronization
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public ViewRangeVisualizationEventHandler()
        {
            _coordinateService = new ViewRangeCoordinateService();
            _planeService = new ViewRangePlaneService();
            _updateService = new ViewRangeUpdateService();
            _validationService = new ViewRangeValidationService();
        }

        // Implement IWaitableExternalEventHandler interface
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "View Range Visualization Event Handler";
        }

        public void SetParameters(ViewRangeVisualizationRequest parameters)
        {
            _parameters = parameters;
            TaskCompleted = false;
            _resetEvent.Reset();
        }

        public object GetResult()
        {
            return _result;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    _result = new
                    {
                        Success = false,
                        Message = "No active document found",
                        Action = _parameters?.Action ?? "unknown"
                    };
                    CompleteTask();
                    return;
                }

                var args = new ViewRangeVisualizationEventArgs
                {
                    Action = _parameters?.Action ?? "visualize",
                    ViewId = _parameters?.ViewId,
                    ViewName = _parameters?.ViewName,
                    Document = doc,
                    UIDocument = app.ActiveUIDocument
                };

                _result = ExecuteInternal(args);
                CompleteTask();
            }
            catch (Exception ex)
            {
                _result = new
                {
                    Success = false,
                    Message = $"Error in view range visualization: {ex.Message}",
                    Action = _parameters?.Action ?? "unknown"
                };
                CompleteTask();
            }
        }

        private void CompleteTask()
        {
            TaskCompleted = true;
            _resetEvent.Set();
        }

        private ViewRangeVisualizationResult ExecuteInternal(ViewRangeVisualizationEventArgs args)
        {
            var result = new ViewRangeVisualizationResult
            {
                Action = args.Action
            };

            try
            {
                // Check if visualization already exists in the active view
                var existingVisualizations = GetExistingVisualizations(args.Document);

                if (existingVisualizations.Any())
                {
                    // Second run: Update view range from adjusted planes, then remove them
                    result = HandleUpdateViewRange(args, existingVisualizations);
                }
                else
                {
                    // First run: Create visualization planes
                    result = HandleCreateVisualization(args);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                result.Message = $"Error in view range visualization: {ex.Message}";
            }

            return result;
        }

        private ViewRangeVisualizationResult HandleCreateVisualization(ViewRangeVisualizationEventArgs args)
        {
            var result = new ViewRangeVisualizationResult
            {
                Action = "visualize"
            };

            // Get available plan views if no specific view ID provided
            ViewPlan selectedView = null;
            if (!string.IsNullOrEmpty(args.ViewId))
            {
                var elementId = new ElementId(long.Parse(args.ViewId));
                selectedView = args.Document.GetElement(elementId) as ViewPlan;
            }
            else if (!string.IsNullOrEmpty(args.ViewName))
            {
                // Find view by name
                var planViews = GetSuitablePlanViews(args.Document);
                selectedView = planViews.FirstOrDefault(v => v.Name == args.ViewName);
                
                if (selectedView == null)
                {
                    result.Success = false;
                    result.Error = $"View with name '{args.ViewName}' not found or is not a suitable plan view";
                    return result;
                }
            }
            else
            {
                // Get suitable plan views
                var planViews = GetSuitablePlanViews(args.Document);
                if (!planViews.Any())
                {
                    result.Success = false;
                    result.Error = "No suitable plan views found in the project";
                    return result;
                }

                // For now, use the first available view (in a real implementation, this would be user-selected)
                selectedView = planViews.First();
            }

            if (selectedView == null)
            {
                result.Success = false;
                result.Error = "Selected view not found or is not a plan view";
                return result;
            }

            result.ViewName = selectedView.Name;

            // Check if active view is 3D
            if (!(args.Document.ActiveView is View3D))
            {
                result.Success = false;
                result.Error = "Please switch to a 3D view before running the visualization";
                return result;
            }

            // Get view range information
            var viewRangeInfo = _coordinateService.GetViewRangeInfo(selectedView);
            
            // Get crop box boundary
            var boundary = GetCropBoxBoundary(selectedView);
            if (boundary == null)
            {
                result.Success = false;
                result.Error = $"No crop box defined for the selected view: {selectedView.Name}";
                return result;
            }

            // Create visualization planes
            using (var transaction = new Transaction(args.Document, "Visualize Plan View Range"))
            {
                transaction.Start();

                try
                {
                    var solidFillId = GetSolidFillPatternId(args.Document);
                    var levelName = selectedView.GenLevel?.Name ?? selectedView.Name;

                    // Create planes for non-unlimited ranges
                    if (viewRangeInfo.TopHeight.HasValue)
                    {
                        var elementId = _planeService.CreateColoredPlane(
                            args.Document, boundary, viewRangeInfo.TopHeight.Value,
                            new Color(255, 0, 0), levelName, "Top", solidFillId, selectedView.Id);
                        
                        result.PlanesCreated.Add(new ViewRangePlaneInfo
                        {
                            PlaneName = "Top",
                            Color = "Red",
                            Elevation = viewRangeInfo.TopHeight.Value,
                            ElementId = (int)elementId.Value
                        });
                    }
                    else
                    {
                        result.PlanesSkipped.Add("Top (unlimited)");
                    }

                    if (viewRangeInfo.CutHeight.HasValue)
                    {
                        var elementId = _planeService.CreateColoredPlane(
                            args.Document, boundary, viewRangeInfo.CutHeight.Value,
                            new Color(0, 255, 0), levelName, "Cut", solidFillId, selectedView.Id);
                        
                        result.PlanesCreated.Add(new ViewRangePlaneInfo
                        {
                            PlaneName = "Cut",
                            Color = "Green",
                            Elevation = viewRangeInfo.CutHeight.Value,
                            ElementId = (int)elementId.Value
                        });
                    }
                    else
                    {
                        result.PlanesSkipped.Add("Cut (unlimited)");
                    }

                    if (viewRangeInfo.BottomHeight.HasValue)
                    {
                        var elementId = _planeService.CreateColoredPlane(
                            args.Document, boundary, viewRangeInfo.BottomHeight.Value,
                            new Color(0, 0, 255), levelName, "Bottom", solidFillId, selectedView.Id);
                        
                        result.PlanesCreated.Add(new ViewRangePlaneInfo
                        {
                            PlaneName = "Bottom",
                            Color = "Blue",
                            Elevation = viewRangeInfo.BottomHeight.Value,
                            ElementId = (int)elementId.Value
                        });
                    }
                    else
                    {
                        result.PlanesSkipped.Add("Bottom (unlimited)");
                    }

                    if (viewRangeInfo.ViewDepthHeight.HasValue)
                    {
                        var elementId = _planeService.CreateColoredPlane(
                            args.Document, boundary, viewRangeInfo.ViewDepthHeight.Value,
                            new Color(255, 165, 0), levelName, "View Depth", solidFillId, selectedView.Id);
                        
                        result.PlanesCreated.Add(new ViewRangePlaneInfo
                        {
                            PlaneName = "View Depth",
                            Color = "Orange",
                            Elevation = viewRangeInfo.ViewDepthHeight.Value,
                            ElementId = (int)elementId.Value
                        });
                    }
                    else
                    {
                        result.PlanesSkipped.Add("View Depth (unlimited)");
                    }

                    transaction.Commit();

                    // Build success message
                    var messageParts = new List<string>
                    {
                        $"View range visualization created for '{selectedView.Name}'"
                    };

                    if (result.PlanesCreated.Any())
                    {
                        messageParts.Add("Visualization planes created:");
                        foreach (var plane in result.PlanesCreated)
                        {
                            messageParts.Add($"• {plane.PlaneName} plane ({plane.Color})");
                        }
                    }

                    if (result.PlanesSkipped.Any())
                    {
                        messageParts.Add("Skipped planes:");
                        foreach (var skipped in result.PlanesSkipped)
                        {
                            messageParts.Add($"• {skipped}");
                        }
                    }

                    messageParts.Add("Run the tool again to update the view range after adjusting the planes.");
                    result.Message = string.Join("\n", messageParts);
                }
                catch (Exception)
                {
                    transaction.RollBack();
                    throw;
                }
            }

            return result;
        }

        private ViewRangeVisualizationResult HandleUpdateViewRange(ViewRangeVisualizationEventArgs args, List<Element> existingVisualizations)
        {
            var result = new ViewRangeVisualizationResult
            {
                Action = "update"
            };

            // Auto-detect the original plan view from stored comments
            var planViewId = GetOriginalPlanViewId(existingVisualizations);
            if (planViewId == null)
            {
                result.Success = false;
                result.Error = "Could not determine original plan view from visualization planes";
                return result;
            }

            var planView = args.Document.GetElement(planViewId) as ViewPlan;
            if (planView == null)
            {
                result.Success = false;
                result.Error = "Original plan view no longer exists";
                return result;
            }

            result.ViewName = planView.Name;

            using (var transaction = new Transaction(args.Document, "Update View Range from Planes"))
            {
                transaction.Start();

                try
                {
                    var updateResult = _updateService.UpdateViewRangeFromPlanes(
                        args.Document, planView, existingVisualizations);

                    if (updateResult.Success)
                    {
                        // Remove visualization planes after successful update
                        foreach (var element in existingVisualizations)
                        {
                            args.Document.Delete(element.Id);
                        }

                        transaction.Commit();

                        result.MovementInfo = updateResult.MovementInfo;
                        result.Message = updateResult.Message;
                    }
                    else
                    {
                        transaction.RollBack();
                        result.Success = false;
                        result.Error = updateResult.Error;
                        result.Message = $"Failed to update view range. Planes were not removed.\n\nReason: {updateResult.Error}";
                    }
                }
                catch (Exception)
                {
                    transaction.RollBack();
                    throw;
                }
            }

            return result;
        }

        private List<Element> GetExistingVisualizations(Document doc)
        {
            var collector = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfClass(typeof(DirectShape));
            
            return collector
                .Where(el => el.Name == VISUALIZATION_GROUP)
                .ToList();
        }

        private List<ViewPlan> GetSuitablePlanViews(Document doc)
        {
            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => !v.IsTemplate && 
                           (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan))
                .ToList();

            // Filter by discipline parameter
            var filteredViews = new List<ViewPlan>();
            foreach (var view in views)
            {
                var disciplineParam = view.get_Parameter(BuiltInParameter.VIEW_DISCIPLINE);
                if (disciplineParam?.AsInteger() != 0)
                {
                    filteredViews.Add(view);
                }
            }

            return filteredViews;
        }

        private List<XYZ> GetCropBoxBoundary(ViewPlan planView)
        {
            var cropBox = planView.CropBox;
            if (cropBox == null) return null;

            return new List<XYZ>
            {
                new XYZ(cropBox.Min.X, cropBox.Min.Y, 0),
                new XYZ(cropBox.Max.X, cropBox.Min.Y, 0),
                new XYZ(cropBox.Max.X, cropBox.Max.Y, 0),
                new XYZ(cropBox.Min.X, cropBox.Max.Y, 0)
            };
        }

        private ElementId GetSolidFillPatternId(Document doc)
        {
            var collector = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement));
            foreach (FillPatternElement pattern in collector)
            {
                if (pattern.GetFillPattern().IsSolidFill)
                {
                    return pattern.Id;
                }
            }
            throw new InvalidOperationException("Solid fill pattern not found in the project");
        }

        private ElementId GetOriginalPlanViewId(List<Element> visualizationElements)
        {
            foreach (var element in visualizationElements)
            {
                var commentsParam = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (commentsParam?.AsString() != null)
                {
                    var commentParts = commentsParam.AsString().Split('|');
                    if (commentParts.Length >= 3 && long.TryParse(commentParts[2], out long viewIdLong))
                    {
                        return new ElementId(viewIdLong);
                    }
                }
            }
            return null;
        }
    }
}