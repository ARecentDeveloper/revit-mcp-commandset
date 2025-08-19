using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Commands
{
    public class SetElementParameterCommand : ExternalEventCommandBase
    {
        private SetElementParameterEventHandler _handler => (SetElementParameterEventHandler)Handler;

        /// <summary>
        /// Command name
        /// </summary>
        public override string CommandName => "set_element_parameter";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public SetElementParameterCommand(UIApplication uiApp)
            : base(new SetElementParameterEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Log the incoming parameters for debugging
                System.Diagnostics.Trace.WriteLine($"SetElementParameterCommand.Execute called with parameters: {parameters?.ToString() ?? "null"}");
                
                // Handle case where parameters might be wrapped in a "data" object (backward compatibility)
                JObject dataParams = parameters;
                if (parameters != null && parameters["data"] != null)
                {
                    dataParams = parameters["data"].ToObject<JObject>();
                }
                // If no data wrapper, use parameters directly
                if (dataParams == null)
                {
                    dataParams = parameters;
                }
                
                // Parse parameters
                List<int> elementIds = new List<int>();
                if (dataParams != null)
                {
                    if (dataParams["elementIds"] != null)
                    {
                        elementIds = dataParams["elementIds"].ToObject<List<int>>();
                    }
                    else if (dataParams["elementId"] != null)
                    {
                        // Support singular elementId as well
                        int singleId = dataParams["elementId"].ToObject<int>();
                        elementIds.Add(singleId);
                    }
                }
                
                string parameterName = dataParams?["parameterName"]?.ToString();
                var parameterValueToken = dataParams?["parameterValue"];
                string parameterValueType = dataParams?["parameterValueType"]?.ToString();

                // Validate required parameters
                if (elementIds == null || !elementIds.Any())
                    throw new ArgumentException("At least one element ID is required");
                
                if (string.IsNullOrEmpty(parameterName))
                    throw new ArgumentException("Parameter name is required");

                // Parse parameter values (single value or array)
                List<object> parameterValues = new List<object>();
                
                if (parameterValueToken != null && parameterValueToken.Type == JTokenType.Array)
                {
                    // Handle array of values
                    var valueArray = parameterValueToken.ToObject<List<object>>();
                    if (valueArray.Count != elementIds.Count)
                    {
                        throw new ArgumentException($"Parameter value array length ({valueArray.Count}) must match element ID array length ({elementIds.Count})");
                    }
                    parameterValues = valueArray;
                }
                else
                {
                    // Handle single value - apply to all elements
                    object singleValue = parameterValueToken?.ToObject<object>();
                    for (int i = 0; i < elementIds.Count; i++)
                    {
                        parameterValues.Add(singleValue);
                    }
                }

                // Set parameters for the event handler
                _handler.SetParameters(elementIds, parameterName, parameterValues, parameterValueType);

                // Reset the event handler before triggering
                _handler.Reset();

                // Trigger external event and wait for completion
                if (RaiseAndWaitForCompletion(20000)) // 20 second timeout
                {
                    return _handler.Results;
                }
                else
                {
                    throw new TimeoutException("Set parameter operation timed out");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"SetElementParameterCommand.Execute error: {ex}");
                throw new Exception($"Set parameter operation failed: {ex.Message}");
            }
        }
    }
}