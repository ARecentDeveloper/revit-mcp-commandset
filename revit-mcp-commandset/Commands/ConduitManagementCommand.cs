using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;

namespace RevitMCPCommandSet.Commands
{
    public class ConduitManagementCommand : ExternalEventCommandBase
    {
        private ConduitManagementEventHandler _handler => (ConduitManagementEventHandler)Handler;

        public override string CommandName => "conduit_management";

        public ConduitManagementCommand(UIApplication uiApp)
            : base(new ConduitManagementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parse parameters from JObject
                string action = parameters["action"]?.ToString();
                double proximityDistance = parameters["proximityDistance"]?.ToObject<double>() ?? 0.5;

                // Validate required parameters
                if (string.IsNullOrEmpty(action))
                {
                    throw new ArgumentException("Action parameter is required");
                }

                if (action != "highlight" && action != "connect")
                {
                    throw new ArgumentException("Action must be either 'highlight' or 'connect'");
                }

                // Set parameters for the event handler
                _handler.SetParameters(action, proximityDistance);

                // Reset the event handler before triggering
                _handler.Reset();
                
                // Trigger external event and wait for completion
                if (RaiseAndWaitForCompletion(20000)) // 20 second timeout
                {
                    return _handler.Results;
                }
                else
                {
                    throw new TimeoutException("Conduit management operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Conduit management operation failed: {ex.Message}");
            }
        }
    }
}