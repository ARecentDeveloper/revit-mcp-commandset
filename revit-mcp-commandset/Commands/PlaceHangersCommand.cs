using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;

namespace RevitMCPCommandSet.Commands
{
    public class PlaceHangersCommand : ExternalEventCommandBase
    {
        private PlaceHangersEventHandler _handler => (PlaceHangersEventHandler)Handler;

        public override string CommandName => "place_hangers";

        public PlaceHangersCommand(UIApplication uiApp)
            : base(new PlaceHangersEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parse parameters from JObject
                string placementMode = parameters["placement_mode"]?.ToString();
                string hangerType = parameters["hanger_type"]?.ToString();
                string targetCategory = parameters["target_category"]?.ToString();
                string spacing = parameters["spacing"]?.ToString() ?? "6'";
                string startOffset = parameters["start_offset"]?.ToString() ?? "0";
                string endOffset = parameters["end_offset"]?.ToString() ?? "0";

                // Validate required parameters
                if (string.IsNullOrEmpty(placementMode))
                {
                    throw new ArgumentException("Placement mode is required");
                }

                if (string.IsNullOrEmpty(hangerType))
                {
                    throw new ArgumentException("Hanger type is required");
                }

                if (placementMode != "click" && placementMode != "parametric")
                {
                    throw new ArgumentException("Placement mode must be either 'click' or 'parametric'");
                }

                if (hangerType != "Clevis Hanger" && hangerType != "Trapeze Hanger")
                {
                    throw new ArgumentException("Hanger type must be either 'Clevis Hanger' or 'Trapeze Hanger'");
                }

                // Set parameters for the event handler
                _handler.SetParameters(placementMode, hangerType, targetCategory, spacing, startOffset, endOffset);

                // Reset the event handler before triggering
                _handler.Reset();
                
                // Trigger external event and wait for completion
                int timeout = placementMode == "click" ? 300000 : 60000; // 5 minutes for click mode, 1 minute for parametric
                if (RaiseAndWaitForCompletion(timeout))
                {
                    return _handler.Results;
                }
                else
                {
                    throw new TimeoutException($"Hanger placement operation timed out after {timeout/1000} seconds");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Hanger placement operation failed: {ex.Message}");
            }
        }
    }
}