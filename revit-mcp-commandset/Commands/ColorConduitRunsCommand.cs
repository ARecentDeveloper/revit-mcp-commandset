using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;

namespace RevitMCPCommandSet.Commands
{
    public class ColorConduitRunsCommand : ExternalEventCommandBase
    {
        private ColorConduitRunsEventHandler _handler => (ColorConduitRunsEventHandler)Handler;

        public override string CommandName => "color_conduit_runs";

        public ColorConduitRunsCommand(UIApplication uiApp)
            : base(new ColorConduitRunsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Reset the event handler before triggering
                _handler.Reset();
                
                // Trigger external event and wait for completion
                if (RaiseAndWaitForCompletion(20000)) // 20 second timeout
                {
                    return _handler.Results;
                }
                else
                {
                    throw new TimeoutException("Color conduit runs operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Color conduit runs operation failed: {ex.Message}");
            }
        }
    }
}