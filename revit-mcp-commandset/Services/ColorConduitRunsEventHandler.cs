using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class ColorConduitRunsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        /// <summary>
        /// Event wait object
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        /// <summary>
        /// Results data
        /// </summary>
        public object Results { get; private set; }

        /// <summary>
        /// Reset the event handler for a new operation
        /// </summary>
        public void Reset()
        {
            _resetEvent.Reset();
            Results = null;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;
                View view = doc.ActiveView;
                
                if (view == null)
                {
                    Results = new
                    {
                        success = false,
                        message = "No active view found"
                    };
                    return;
                }

                // Collect conduits and fittings from the current view
                var inView = new FilteredElementCollector(doc, view.Id);
                var conduits = inView
                    .OfCategory(BuiltInCategory.OST_Conduit)
                    .WhereElementIsNotElementType()
                    .ToElements();

                var fittings = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_ConduitFitting)
                    .WhereElementIsNotElementType()
                    .ToElements();

                var allElements = new List<Element>();
                allElements.AddRange(conduits);
                allElements.AddRange(fittings);

                if (allElements.Count == 0)
                {
                    Results = new
                    {
                        success = false,
                        message = "No conduits or fittings found in the current view"
                    };
                    return;
                }

                // Build adjacency graph and find connected components
                var adjacency = BuildAdjacency(allElements);
                var runs = FindConnectedComponents(adjacency);
                var random = new Random();

                // Get solid fill pattern ID for surface overrides
                var solidFillPatternId = GetSolidFillPatternId(doc);

                var runDetails = new List<object>();

                using (Transaction trans = new Transaction(doc, "Color Conduit Runs"))
                {
                    trans.Start();
                    
                    foreach (var run in runs)
                    {
                        // Generate random color
                        var color = new Color(
                            (byte)random.Next(40, 226),
                            (byte)random.Next(40, 226),
                            (byte)random.Next(40, 226));
                        
                        // Create graphics override settings
                        var overrideSettings = new OverrideGraphicSettings();
                        overrideSettings.SetProjectionLineColor(color);
                        overrideSettings.SetCutLineColor(color);
                        overrideSettings.SetProjectionLineWeight(6);
                        overrideSettings.SetCutLineWeight(6);
                        overrideSettings.SetSurfaceForegroundPatternColor(color);
                        overrideSettings.SetSurfaceBackgroundPatternColor(color);
                        
                        // Set solid fill pattern if available
                        if (solidFillPatternId != null && solidFillPatternId != ElementId.InvalidElementId)
                        {
                            overrideSettings.SetSurfaceForegroundPatternId(solidFillPatternId);
                            overrideSettings.SetSurfaceBackgroundPatternId(solidFillPatternId);
                        }
                        
                        // Apply overrides to all elements in this run
                        foreach (var elementId in run)
                        {
                            try 
                            { 
                                view.SetElementOverrides(elementId, overrideSettings); 
                            } 
                            catch 
                            { 
                                // Handle exception silently - some elements might not support overrides
                            }
                        }

                        // Store run details for response
                        runDetails.Add(new
                        {
                            elementCount = run.Count,
                            color = new { r = color.Red, g = color.Green, b = color.Blue }
                        });
                    }
                    
                    trans.Commit();
                }

                Results = new
                {
                    success = true,
                    message = $"Successfully colored {runs.Count} conduit run(s)",
                    totalRuns = runs.Count,
                    totalElements = allElements.Count,
                    runs = runDetails
                };
            }
            catch (Exception ex)
            {
                Results = new
                {
                    success = false,
                    message = ex.Message
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
            return "Color Conduit Runs";
        }

        private static Dictionary<ElementId, HashSet<ElementId>> BuildAdjacency(IEnumerable<Element> elements)
        {
            var adjacency = new Dictionary<ElementId, HashSet<ElementId>>();
            var validElements = new HashSet<ElementId>(elements.Select(e => e.Id));

            foreach (var element in elements)
            {
                var elementId = element.Id;
                if (!adjacency.ContainsKey(elementId)) 
                    adjacency[elementId] = new HashSet<ElementId>();

                foreach (var connectedId in GetConnectedElements(element))
                {
                    if (connectedId == elementId || !validElements.Contains(connectedId)) 
                        continue;
                    
                    if (!adjacency.ContainsKey(connectedId)) 
                        adjacency[connectedId] = new HashSet<ElementId>();
                    
                    adjacency[elementId].Add(connectedId);
                    adjacency[connectedId].Add(elementId);
                }
            }
            
            return adjacency;
        }

        private static IEnumerable<ElementId> GetConnectedElements(Element element)
        {
            var connectedIds = new HashSet<ElementId>();
            
            // Handle MEP curves (conduits)
            if (element is MEPCurve mepCurve && mepCurve.ConnectorManager != null)
            {
                foreach (Connector connector in mepCurve.ConnectorManager.Connectors)
                {
                    foreach (Connector connectedConnector in connector.AllRefs)
                    {
                        if (connectedConnector.Owner != null) 
                            connectedIds.Add(connectedConnector.Owner.Id);
                    }
                }
                return connectedIds;
            }

            // Handle family instances (fittings)
            if (element is FamilyInstance familyInstance && 
                familyInstance.MEPModel != null && 
                familyInstance.MEPModel.ConnectorManager != null)
            {
                foreach (Connector connector in familyInstance.MEPModel.ConnectorManager.Connectors)
                {
                    foreach (Connector connectedConnector in connector.AllRefs)
                    {
                        if (connectedConnector.Owner != null) 
                            connectedIds.Add(connectedConnector.Owner.Id);
                    }
                }
            }
            
            return connectedIds;
        }

        private static List<List<ElementId>> FindConnectedComponents(Dictionary<ElementId, HashSet<ElementId>> adjacency)
        {
            var components = new List<List<ElementId>>();
            var visited = new HashSet<ElementId>();

            foreach (var kvp in adjacency)
            {
                if (visited.Contains(kvp.Key)) 
                    continue;

                var component = new List<ElementId>();
                var queue = new Queue<ElementId>();
                queue.Enqueue(kvp.Key);
                visited.Add(kvp.Key);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);
                    
                    if (adjacency.TryGetValue(current, out HashSet<ElementId> neighbors))
                    {
                        foreach (var neighbor in neighbors)
                        {
                            if (visited.Add(neighbor)) 
                                queue.Enqueue(neighbor);
                        }
                    }
                }
                
                components.Add(component);
            }
            
            return components;
        }

        /// <summary>
        /// Get the solid fill pattern ID from the document
        /// </summary>
        /// <param name="document">The Revit document</param>
        /// <returns>ElementId of solid fill pattern, or InvalidElementId if not found</returns>
        private static ElementId GetSolidFillPatternId(Document document)
        {
            var collector = new FilteredElementCollector(document)
                .OfClass(typeof(FillPatternElement));
            
            foreach (FillPatternElement pattern in collector)
            {
                if (pattern.GetFillPattern().IsSolidFill)
                {
                    return pattern.Id;
                }
            }
            
            return ElementId.InvalidElementId;
        }
    }
}