using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class ConduitManagementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
        private string _action;
        private double _proximityDistance;

        /// <summary>
        /// Reset the event handler for a new operation
        /// </summary>
        public void Reset()
        {
            _resetEvent.Reset();
            Results = null;
        }

        /// <summary>
        /// Set parameters
        /// </summary>
        public void SetParameters(string action, double proximityDistance)
        {
            _action = action;
            _proximityDistance = proximityDistance;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;
                View activeView = app.ActiveUIDocument.ActiveView;
                
                if (_action == "highlight")
                {
                    Results = HighlightUnconnectedConduits(doc, activeView);
                }
                else if (_action == "connect")
                {
                    Results = ConnectUnconnectedFittings(doc, activeView, _proximityDistance);
                }
                else
                {
                    throw new ArgumentException("Invalid action specified");
                }
            }
            catch (Exception ex)
            {
                Results = new
                {
                    success = false,
                    message = ex.Message,
                    action = _action
                };
            }
            finally
            {
                _resetEvent.Set(); // Signal that operation is complete
            }
        }

        private object HighlightUnconnectedConduits(Document doc, View activeView)
        {
            using (Transaction trans = new Transaction(doc, "Highlight Unconnected Conduits"))
            {
                trans.Start();

                // Get conduits in the active view
                FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id);
                var conduits = collector.OfCategory(BuiltInCategory.OST_Conduit)
                    .WhereElementIsNotElementType()
                    .ToElements();

                // Get solid fill pattern ID for surface overrides
                var solidFillPatternId = GetSolidFillPatternId(doc);

                // Create OverrideGraphicSettings for red color
                OverrideGraphicSettings redOgs = new OverrideGraphicSettings();
                Color redColor = new Color(255, 0, 0); // RGB for red
                redOgs.SetProjectionLineColor(redColor);
                redOgs.SetCutLineColor(redColor);
                redOgs.SetSurfaceForegroundPatternColor(redColor);
                redOgs.SetSurfaceBackgroundPatternColor(redColor);

                // Set solid fill pattern if available
                if (solidFillPatternId != null && solidFillPatternId != ElementId.InvalidElementId)
                {
                    redOgs.SetSurfaceForegroundPatternId(solidFillPatternId);
                    redOgs.SetSurfaceBackgroundPatternId(solidFillPatternId);
                }

                int highlightedCount = 0;
                int totalConduits = conduits.Count;
                List<string> highlightedConduits = new List<string>();

                foreach (Element conduit in conduits)
                {
                    // Get the ConnectorManager
                    ConnectorManager connectorManager = GetConnectorManager(conduit);

                    if (connectorManager != null)
                    {
                        ConnectorSet unusedConnectors = connectorManager.UnusedConnectors;
                        int numUnused = unusedConnectors.Size;

                        if (numUnused > 0)
                        {
                            // Override the graphics of the conduit to red
                            activeView.SetElementOverrides(conduit.Id, redOgs);
                            highlightedCount++;
                            highlightedConduits.Add($"Conduit ID: {conduit.Id.Value} ({numUnused} unused connectors)");
                        }
                    }
                }

                trans.Commit();

                return new
                {
                    success = true,
                    action = "highlight",
                    message = $"Highlighted {highlightedCount} unconnected conduits out of {totalConduits} total conduits",
                    highlightedCount = highlightedCount,
                    totalConduits = totalConduits,
                    highlightedConduits = highlightedConduits
                };
            }
        }

        private object ConnectUnconnectedFittings(Document doc, View activeView, double proximityDistanceInches)
        {
            using (Transaction trans = new Transaction(doc, "Connect Fittings to Nearby MEPCurves"))
            {
                trans.Start();

                try
                {
                    // Get fittings and MEP curves
                    var fittings = GetFittings(doc, activeView);
                    var mepCurves = GetMEPCurves(doc, activeView);

                    // Collect unused connectors on fittings
                    List<Connector> fittingUnusedConnectors = new List<Connector>();
                    foreach (Element fitting in fittings)
                    {
                        ConnectorManager connectorManager = GetConnectorManager(fitting);
                        if (connectorManager != null)
                        {
                            foreach (Connector connector in connectorManager.UnusedConnectors)
                            {
                                fittingUnusedConnectors.Add(connector);
                            }
                        }
                    }

                    // Collect all connectors on MEP curves
                    List<Connector> mepCurveConnectors = new List<Connector>();
                    foreach (Element mepCurve in mepCurves)
                    {
                        ConnectorManager connectorManager = GetConnectorManager(mepCurve);
                        if (connectorManager != null)
                        {
                            foreach (Connector connector in connectorManager.Connectors)
                            {
                                mepCurveConnectors.Add(connector);
                            }
                        }
                    }

                    // Get solid fill pattern ID for surface overrides
                    var solidFillPatternId = GetSolidFillPatternId(doc);

                    // Graphics setup for connected elements
                    OverrideGraphicSettings greenOgs = new OverrideGraphicSettings();
                    Color greenColor = new Color(0, 255, 0);
                    greenOgs.SetProjectionLineColor(greenColor);
                    greenOgs.SetCutLineColor(greenColor);
                    greenOgs.SetSurfaceForegroundPatternColor(greenColor);
                    greenOgs.SetSurfaceBackgroundPatternColor(greenColor);

                    // Set solid fill pattern if available
                    if (solidFillPatternId != null && solidFillPatternId != ElementId.InvalidElementId)
                    {
                        greenOgs.SetSurfaceForegroundPatternId(solidFillPatternId);
                        greenOgs.SetSurfaceBackgroundPatternId(solidFillPatternId);
                    }

                    // Convert proximity distance to internal units
                    double proximityDistanceInternal = UnitUtils.ConvertToInternalUnits(proximityDistanceInches, UnitTypeId.Inches);

                    List<Element> connectedElements = new List<Element>();
                    List<string> connectionDetails = new List<string>();
                    int connectionsAttempted = 0;
                    int connectionsSuccessful = 0;

                    // Iterate and connect
                    foreach (Connector fittingConnector in fittingUnusedConnectors)
                    {
                        if (!fittingConnector.IsConnected)
                        {
                            foreach (Connector mepCurveConnector in mepCurveConnectors)
                            {
                                // Exclude same element and check distance
                                if (mepCurveConnector.Owner.Id != fittingConnector.Owner.Id &&
                                    fittingConnector.Origin.DistanceTo(mepCurveConnector.Origin) <= proximityDistanceInternal &&
                                    !mepCurveConnector.IsConnected)
                                {
                                    try
                                    {
                                        connectionsAttempted++;
                                        fittingConnector.ConnectTo(mepCurveConnector);
                                        
                                        if (fittingConnector.IsConnected)
                                        {
                                            connectionsSuccessful++;
                                            connectedElements.Add(fittingConnector.Owner);
                                            connectedElements.Add(mepCurveConnector.Owner);
                                            
                                            double distance = fittingConnector.Origin.DistanceTo(mepCurveConnector.Origin);
                                            connectionDetails.Add($"Connected fitting {fittingConnector.Owner.Id.Value} to MEP curve {mepCurveConnector.Owner.Id.Value} (Distance: {distance:F4})");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        connectionDetails.Add($"Connection failed between {fittingConnector.Owner.Id.Value} and {mepCurveConnector.Owner.Id.Value}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }

                    // Apply green override to unique connected elements
                    var uniqueConnectedElements = connectedElements.Distinct().ToList();
                    foreach (Element element in uniqueConnectedElements)
                    {
                        activeView.SetElementOverrides(element.Id, greenOgs);
                    }

                    trans.Commit();

                    return new
                    {
                        success = true,
                        action = "connect",
                        message = $"Successfully connected {connectionsSuccessful} out of {connectionsAttempted} attempted connections",
                        connectionsAttempted = connectionsAttempted,
                        connectionsSuccessful = connectionsSuccessful,
                        connectedElementsCount = uniqueConnectedElements.Count,
                        proximityDistance = proximityDistanceInches,
                        connectionDetails = connectionDetails
                    };
                }
                catch (Exception)
                {
                    trans.RollBack();
                    throw;
                }
            }
        }

        private ConnectorManager GetConnectorManager(Element element)
        {
            if (element is MEPCurve mepCurve)
            {
                return mepCurve.ConnectorManager;
            }
            else if (element is FamilyInstance familyInstance && familyInstance.MEPModel != null)
            {
                return familyInstance.MEPModel.ConnectorManager;
            }
            return null;
        }

        private List<Element> GetFittings(Document doc, View activeView)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id);
            var fittings = collector.OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .ToElements();

            var validFittings = fittings.Where(f => f.Category != null && 
                (f.Category.BuiltInCategory == BuiltInCategory.OST_ElectricalFixtures ||
                 f.Category.BuiltInCategory == BuiltInCategory.OST_ConduitFitting ||
                 f.Category.BuiltInCategory == BuiltInCategory.OST_PipeFitting ||
                 f.Category.BuiltInCategory == BuiltInCategory.OST_DuctFitting ||
                 f.Category.BuiltInCategory == BuiltInCategory.OST_CableTrayFitting))
                .ToList();

            return validFittings;
        }

        private List<Element> GetMEPCurves(Document doc, View activeView)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id);
            var mepCurves = collector.OfClass(typeof(MEPCurve))
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();

            return mepCurves;
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

        /// <summary>
        /// IExternalEventHandler.GetName implementation
        /// </summary>
        public string GetName()
        {
            return "Conduit Management";
        }
    }
}