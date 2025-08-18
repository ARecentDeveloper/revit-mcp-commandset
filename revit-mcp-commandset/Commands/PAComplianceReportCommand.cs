using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;
using System;

namespace RevitMCPCommandSet.Commands
{
    public class PAComplianceReportCommand : ExternalEventCommandBase
    {
        private PAComplianceReportEventHandler _handler => (PAComplianceReportEventHandler)Handler;

        /// <summary>
        /// Command name
        /// </summary>
        public override string CommandName => "pa_compliance_report";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public PAComplianceReportCommand(UIApplication uiApp)
            : base(new PAComplianceReportEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parse parameters
                var reportSettings = new PAComplianceReportSettings();
                
                if (parameters.ContainsKey("outputPath"))
                {
                    reportSettings.OutputPath = parameters["outputPath"].ToString();
                }
                
                if (parameters.ContainsKey("step"))
                {
                    reportSettings.Step = parameters["step"].ToString();
                }
                
                if (parameters.ContainsKey("includeAnnotationFamilies"))
                {
                    reportSettings.IncludeAnnotationFamilies = parameters["includeAnnotationFamilies"].ToObject<bool>();
                }
                
                if (parameters.ContainsKey("includeModelFamilies"))
                {
                    reportSettings.IncludeModelFamilies = parameters["includeModelFamilies"].ToObject<bool>();
                }
                
                if (parameters.ContainsKey("includeWorksets"))
                {
                    reportSettings.IncludeWorksets = parameters["includeWorksets"].ToObject<bool>();
                }
                
                if (parameters.ContainsKey("includeSheets"))
                {
                    reportSettings.IncludeSheets = parameters["includeSheets"].ToObject<bool>();
                }
                
                if (parameters.ContainsKey("includeModelIntegrity"))
                {
                    reportSettings.IncludeModelIntegrity = parameters["includeModelIntegrity"].ToObject<bool>();
                }

                // Log the full request for debugging
                System.Diagnostics.Trace.WriteLine($"PA Compliance Report Request: {parameters.ToString()}");

                // Set parameters for the handler
                _handler.SetParameters(reportSettings);

                // Trigger external event and wait for completion
                if (RaiseAndWaitForCompletion(30000)) // 30 second timeout for report generation
                {
                    var result = _handler.Result as PAComplianceReportResult;
                    if (result != null)
                    {
                        System.Diagnostics.Trace.WriteLine($"PA Compliance Report Result: Success={result.Success}, Message={result.Message}");
                        System.Diagnostics.Trace.WriteLine($"PA Compliance Report OutputPath: {result.OutputPath}");
                        System.Diagnostics.Trace.WriteLine($"PA Compliance Report StepExecuted: {result.StepExecuted}");
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine("PA Compliance Report Result is null!");
                    }
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("PA compliance report generation operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"PA compliance report generation failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Settings for PA compliance report generation
    /// </summary>
    public class PAComplianceReportSettings
    {
        public string OutputPath { get; set; } = "";
        public string Step { get; set; } = "all";
        public bool IncludeAnnotationFamilies { get; set; } = true;
        public bool IncludeModelFamilies { get; set; } = true;
        public bool IncludeWorksets { get; set; } = true;
        public bool IncludeSheets { get; set; } = true;
        public bool IncludeModelIntegrity { get; set; } = true;
    }
}