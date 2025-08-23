using System;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Services.LevelAdjustment
{
    public class LevelAdjustmentEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        /// <summary>
        /// Event wait object
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        /// <summary>
        /// Results data
        /// </summary>
        public object Results { get; private set; }

        // Parameters
        private string _mode;
        private string _targetLevelName;
        private bool _maintainElevation;

        /// <summary>
        /// Reset the event handler for a new operation
        /// </summary>
        public void Reset()
        {
            _resetEvent.Reset();
            Results = null;
        }

        /// <summary>
        /// Set parameters for the level adjustment operation
        /// </summary>
        public void SetParameters(string mode, string targetLevelName, bool maintainElevation)
        {
            _mode = mode;
            _targetLevelName = targetLevelName;
            _maintainElevation = maintainElevation;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;
                UIDocument uidoc = app.ActiveUIDocument;
                
                // Get selected elements
                var selection = uidoc.Selection.GetElementIds();
                if (!selection.Any())
                {
                    Results = new
                    {
                        success = false,
                        message = "No elements selected. Please select elements and try again.",
                        summary = new { successful = 0, failed = 0 }
                    };
                    return;
                }

                // Initialize services
                var levelCalculationService = new LevelCalculationService(doc);
                var elementProcessor = new ElementLevelProcessor(doc, levelCalculationService);

                Level targetLevel = null;
                
                // For manual mode, find and validate the target level
                if (_mode == "manual")
                {
                    targetLevel = levelCalculationService.FindLevelByName(_targetLevelName);
                    if (targetLevel == null)
                    {
                        Results = new
                        {
                            success = false,
                            message = $"Target level '{_targetLevelName}' not found in the project.",
                            summary = new { successful = 0, failed = 0 }
                        };
                        return;
                    }
                }

                var successfulElements = new List<object>();
                var failedElements = new List<object>();
                var levelAssignments = new Dictionary<string, string>();

                using (Transaction trans = new Transaction(doc, $"Level Adjustment - {(_mode == "auto" ? "Auto Assignment" : $"Change to {_targetLevelName}")}"))
                {
                    trans.Start();
                    
                    foreach (ElementId elemId in selection)
                    {
                        Element element = doc.GetElement(elemId);
                        try
                        {
                            var elementInfo = GetElementInfo(element);
                            bool success = false;
                            string assignedLevel = "";

                            if (_mode == "auto")
                            {
                                // Auto mode: assign to closest level
                                var result = elementProcessor.AssignToClosestLevel(element);
                                success = result.Success;
                                assignedLevel = result.AssignedLevel;
                                
                                if (!success && !string.IsNullOrEmpty(result.ErrorMessage))
                                {
                                    elementInfo["failureReason"] = result.ErrorMessage;
                                }
                            }
                            else
                            {
                                // Manual mode: assign to specific level
                                var result = elementProcessor.AssignToSpecificLevel(element, targetLevel, _maintainElevation);
                                success = result.Success;
                                assignedLevel = result.AssignedLevel;
                                
                                if (!success && !string.IsNullOrEmpty(result.ErrorMessage))
                                {
                                    elementInfo["failureReason"] = result.ErrorMessage;
                                }
                            }

                            if (success)
                            {
                                successfulElements.Add(elementInfo);
                                if (!string.IsNullOrEmpty(assignedLevel))
                                {
                                    levelAssignments[elementInfo["id"].ToString()] = assignedLevel;
                                }
                            }
                            else
                            {
                                failedElements.Add(elementInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            var elementInfo = GetElementInfo(element);
                            elementInfo["failureReason"] = ex.Message;
                            failedElements.Add(elementInfo);
                        }
                    }
                    
                    trans.Commit();
                }

                // Generate level assignment summary for auto mode
                var levelCounts = new Dictionary<string, int>();
                if (_mode == "auto")
                {
                    foreach (var assignment in levelAssignments.Values)
                    {
                        // Handle combined level names (base and top)
                        if (assignment.Contains(","))
                        {
                            var parts = assignment.Split(',');
                            foreach (var part in parts)
                            {
                                var level = part.Split(':')[1].Trim();
                                if (levelCounts.ContainsKey(level))
                                    levelCounts[level]++;
                                else
                                    levelCounts[level] = 1;
                            }
                        }
                        else
                        {
                            if (levelCounts.ContainsKey(assignment))
                                levelCounts[assignment]++;
                            else
                                levelCounts[assignment] = 1;
                        }
                    }
                }

                // Update selection to show only failed elements if any exist
                if (failedElements.Any())
                {
                    var failedElementIds = failedElements
                        .Select(elem => new ElementId((long)Convert.ToInt32(elem.GetType().GetProperty("id").GetValue(elem))))
                        .ToList();
                    uidoc.Selection.SetElementIds(failedElementIds);
                }
                else
                {
                    // Clear selection if all elements were successful
                    uidoc.Selection.SetElementIds(new List<ElementId>());
                }

                // Set results
                Results = new
                {
                    success = true,
                    message = _mode == "auto" 
                        ? $"Auto level assignment completed. {successfulElements.Count} elements successfully assigned, {failedElements.Count} failed."
                        : $"Level change completed. {successfulElements.Count} elements successfully moved to '{_targetLevelName}', {failedElements.Count} failed.",
                    summary = new 
                    { 
                        successful = successfulElements.Count, 
                        failed = failedElements.Count,
                        mode = _mode,
                        targetLevel = _targetLevelName,
                        maintainElevation = _maintainElevation
                    },
                    levelAssignments = _mode == "auto" ? levelCounts : null,
                    successfulElements = successfulElements,
                    failedElements = failedElements.Any() ? failedElements : null,
                    selectionUpdated = failedElements.Any() ? "Failed elements selected for review" : "Selection cleared"
                };
            }
            catch (Exception ex)
            {
                Results = new
                {
                    success = false,
                    message = ex.Message,
                    summary = new { successful = 0, failed = 0 }
                };
            }
            finally
            {
                _resetEvent.Set(); // Signal that operation is complete
            }
        }

        /// <summary>
        /// Wait for operation to complete
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds</param>
        /// <returns>Whether operation completed within timeout</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// IExternalEventHandler.GetName implementation
        /// </summary>
        public string GetName()
        {
            return "Level Adjustment Handler";
        }

        /// <summary>
        /// Helper function to get element information
        /// </summary>
        private Dictionary<string, object> GetElementInfo(Element element)
        {
            try
            {
                string category = element.Category?.Name ?? "No Category";
                string name = "Unnamed";

                // Try to get element name
                if (!string.IsNullOrEmpty(element.Name))
                {
                    name = element.Name;
                }
                else
                {
                    // Try to get name from parameters
                    var nameParam = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) ??
                                   element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) ??
                                   element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM);
                    if (nameParam != null)
                    {
                        name = nameParam.AsString() ?? "Unnamed";
                    }
                }

                return new Dictionary<string, object>
                {
                    ["id"] = (long)element.Id.Value,
                    ["category"] = category,
                    ["name"] = name
                };
            }
            catch
            {
                return new Dictionary<string, object>
                {
                    ["id"] = (long)element.Id.Value,
                    ["category"] = "Unknown Category",
                    ["name"] = "Unknown"
                };
            }
        }
    }
}