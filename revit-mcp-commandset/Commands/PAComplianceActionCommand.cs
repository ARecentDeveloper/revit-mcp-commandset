using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;
using System;

namespace RevitMCPCommandSet.Commands
{
    public class PAComplianceActionCommand : ExternalEventCommandBase
    {
        private PAComplianceActionEventHandler _handler => (PAComplianceActionEventHandler)Handler;

        /// <summary>
        /// Command name
        /// </summary>
        public override string CommandName => "pa_compliance_action";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public PAComplianceActionCommand(UIApplication uiApp)
            : base(new PAComplianceActionEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parse parameters
                var actionSettings = new PAComplianceActionSettings();
                
                if (parameters.ContainsKey("excelFilePath"))
                {
                    actionSettings.ExcelFilePath = parameters["excelFilePath"].ToString();
                }
                
                if (parameters.ContainsKey("step"))
                {
                    actionSettings.Step = parameters["step"].ToString();
                }
                
                if (parameters.ContainsKey("dryRun"))
                {
                    actionSettings.DryRun = parameters["dryRun"].ToObject<bool>();
                }
                
                if (parameters.ContainsKey("backupProject"))
                {
                    actionSettings.BackupProject = parameters["backupProject"].ToObject<bool>();
                }

                // Log the full request for debugging
                System.Diagnostics.Trace.WriteLine($"PA Compliance Action Request: {parameters.ToString()}");

                // Set parameters for the handler
                _handler.SetParameters(actionSettings);

                // Trigger external event and wait for completion
                if (RaiseAndWaitForCompletion(60000)) // 60 second timeout for action execution
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("PA compliance action execution operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"PA compliance action execution failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Settings for PA compliance action execution
    /// </summary>
    public class PAComplianceActionSettings
    {
        public string ExcelFilePath { get; set; } = "";
        public string Step { get; set; } = "all";
        public bool DryRun { get; set; } = false;
        public bool BackupProject { get; set; } = true;
    }
}