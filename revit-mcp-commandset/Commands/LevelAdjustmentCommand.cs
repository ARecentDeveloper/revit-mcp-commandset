using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services.LevelAdjustment;

namespace RevitMCPCommandSet.Commands
{
    public class LevelAdjustmentCommand : ExternalEventCommandBase
    {
        private LevelAdjustmentEventHandler _handler => (LevelAdjustmentEventHandler)Handler;

        public override string CommandName => "element_level_adjustment";

        public LevelAdjustmentCommand(UIApplication uiApp)
            : base(new LevelAdjustmentEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parse parameters from JObject
                string mode = parameters["mode"]?.ToString();
                string targetLevelName = parameters["targetLevelName"]?.ToString();
                bool maintainElevation = parameters["maintainElevation"]?.ToObject<bool>() ?? true;

                // Validate required parameters
                if (string.IsNullOrEmpty(mode))
                {
                    throw new ArgumentException("Mode parameter is required (auto or manual)");
                }

                if (mode.ToLower() != "auto" && mode.ToLower() != "manual")
                {
                    throw new ArgumentException("Mode must be either 'auto' or 'manual'");
                }

                if (mode.ToLower() == "manual" && string.IsNullOrEmpty(targetLevelName))
                {
                    throw new ArgumentException("Target level name is required when mode is 'manual'");
                }

                // Set parameters for the event handler
                _handler.SetParameters(mode.ToLower(), targetLevelName, maintainElevation);

                // Reset the event handler before triggering
                _handler.Reset();
                
                // Trigger external event and wait for completion
                if (RaiseAndWaitForCompletion(20000)) // 20 second timeout
                {
                    return _handler.Results;
                }
                else
                {
                    throw new TimeoutException("Level adjustment operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Level adjustment operation failed: {ex.Message}");
            }
        }
    }
}