using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;
using RevitMCPCommandSet.Utils.ParameterMappings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Commands
{
    public class ResolveParameterNamesCommand : ExternalEventCommandBase
    {
        private ResolveParameterNamesEventHandler _handler => (ResolveParameterNamesEventHandler)Handler;

        /// <summary>
        /// Command name
        /// </summary>
        public override string CommandName => "resolve_parameter_names";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public ResolveParameterNamesCommand(UIApplication uiApp)
            : base(new ResolveParameterNamesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parse parameters
                string filterCategory = parameters["filterCategory"]?.ToString();
                var userParameterNames = parameters["userParameterNames"]?.ToObject<List<string>>();
                string elementId = parameters["elementId"]?.ToString(); // Optional for future use

                // Validate required parameters
                if (string.IsNullOrEmpty(filterCategory))
                    throw new ArgumentException("filterCategory is required for parameter resolution");
                
                if (userParameterNames == null || !userParameterNames.Any())
                    throw new ArgumentException("userParameterNames is required and must not be empty");

                System.Diagnostics.Trace.WriteLine($"ResolveParameterNames: Category={filterCategory}, Parameters=[{string.Join(", ", userParameterNames)}]");

                // Set parameters for the event handler
                _handler.SetParameters(filterCategory, userParameterNames, elementId);

                // Reset the event handler before triggering
                _handler.Reset();
                
                // Trigger external event and wait for completion
                if (RaiseAndWaitForCompletion(10000)) // 10 second timeout
                {
                    return _handler.Results;
                }
                else
                {
                    throw new TimeoutException("Parameter resolution operation timed out");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"ResolveParameterNames Error: {ex.Message}");
                throw new Exception($"Parameter resolution failed: {ex.Message}");
            }
        }
    }
}