using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;

namespace RevitMCPCommandSet.Commands.Graphics
{
    public class ClearGraphicsOverridesCommand : ExternalEventCommandBase
    {
        private ClearGraphicsOverridesEventHandler _handler => (ClearGraphicsOverridesEventHandler)Handler;

        public override string CommandName => "clear_graphics_overrides";

        public ClearGraphicsOverridesCommand(UIApplication uiApp)
            : base(new ClearGraphicsOverridesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parse parameters from JSON
                var dataToken = parameters?["data"];
                var requestData = dataToken?.ToObject<ClearOverridesRequest>() ?? new ClearOverridesRequest();

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
                        Message = "Clear graphics overrides operation timed out",
                        ProcessedCount = 0,
                        ErrorCount = 0,
                        ViewName = ""
                    };
                }
            }
            catch (Exception ex)
            {
                return new
                {
                    Success = false,
                    Message = $"Error clearing graphics overrides: {ex.Message}",
                    ProcessedCount = 0,
                    ErrorCount = 0,
                    ViewName = ""
                };
            }
        }
    }

    // Data models for parameter parsing
    public class ClearOverridesRequest
    {
        [JsonProperty("scope")]
        public string Scope { get; set; } = "all";

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("parameter")]
        public ParameterFilterRequest Parameter { get; set; }

        [JsonProperty("elementIds")]
        public List<long> ElementIds { get; set; }
    }

    public class ParameterFilterRequest
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("operator")]
        public string Operator { get; set; }

        [JsonProperty("value")]
        public double Value { get; set; }
    }
}