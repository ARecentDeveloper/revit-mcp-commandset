using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.ViewRange;

namespace RevitMCPCommandSet.Commands
{
    public class ViewRangeVisualizationCommand : ExternalEventCommandBase
    {
        private ViewRangeVisualizationEventHandler _handler => (ViewRangeVisualizationEventHandler)Handler;

        public override string CommandName => "view_range_visualization";

        public ViewRangeVisualizationCommand(UIApplication uiApp)
            : base(new ViewRangeVisualizationEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parse parameters from JSON
                var dataToken = parameters?["data"];
                var requestData = dataToken?.ToObject<ViewRangeVisualizationRequest>() ?? new ViewRangeVisualizationRequest();

                // Set parameters for the event handler
                _handler.SetParameters(requestData);

                // Execute the external event and wait for completion
                if (RaiseAndWaitForCompletion(30000)) // 30 second timeout
                {
                    return _handler.GetResult();
                }
                else
                {
                    return new
                    {
                        Success = false,
                        Message = "View range visualization operation timed out"
                    };
                }
            }
            catch (Exception ex)
            {
                return new
                {
                    Success = false,
                    Message = $"Error in view range visualization: {ex.Message}"
                };
            }
        }
    }

    // Data models for parameter parsing
    public class ViewRangeVisualizationRequest
    {
        [JsonProperty("action")]
        public string Action { get; set; } = "visualize";

        [JsonProperty("viewId")]
        public string ViewId { get; set; }

        [JsonProperty("viewName")]
        public string ViewName { get; set; }

        [JsonProperty("removeExisting")]
        public bool RemoveExisting { get; set; } = false;

        [JsonProperty("validateOnly")]
        public bool ValidateOnly { get; set; } = false;
    }
}