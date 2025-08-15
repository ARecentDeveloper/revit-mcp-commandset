using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services.ElementInfoFactories;
using RevitMCPCommandSet.Services.Filtering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class ElementFilterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;
        /// <summary>
        /// Event wait object
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        /// <summary>
        /// Create data (incoming data)
        /// </summary>
        public FilterSetting FilterSetting { get; private set; }
        /// <summary>
        /// Execution result (outgoing data)
        /// </summary>
        public AIResult<List<object>> Result { get; private set; }

        /// <summary>
        /// Set creation parameters
        /// </summary>
        public void SetParameters(FilterSetting data)
        {
            FilterSetting = data;
            _resetEvent.Reset();
        }
        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementInfoList = new List<object>();
                // Check if the filter settings are valid
                if (!FilterSetting.Validate(out string errorMessage))
                    throw new Exception(errorMessage);
                // Get the Id of the element that meets the specified conditions
                var elementList = ElementFilterService.GetFilteredElements(doc, FilterSetting);
                if (elementList == null || !elementList.Any())
                    throw new Exception("The specified element was not found in the project. Please check if the filter settings are correct");
                // Maximum number of filters limit
                string message = "";
                if (FilterSetting.MaxElements > 0)
                {
                    if (elementList.Count > FilterSetting.MaxElements)
                    {
                        elementList = elementList.Take(FilterSetting.MaxElements).ToList();
                        message = $". In addition, there are {elementList.Count} elements that meet the filter criteria, only the first {FilterSetting.MaxElements} are displayed";
                    }
                }

                // Get information of the specified Id element using the factory registry with selective extraction
                var registry = ElementInfoFactoryRegistry.Instance;
                
                // Determine what parameters to extract based on the filter settings
                var requestedParameters = GetRequestedParameters(FilterSetting);
                var detailLevel = FilterSetting.DetailLevel ?? "basic";
                
                foreach (var element in elementList)
                {
                    var info = registry.CreateInfo(doc, element, detailLevel, requestedParameters);
                    if (info != null)
                    {
                        elementInfoList.Add(info);
                    }
                }

                Result = new AIResult<List<object>>
                {
                    Success = true,
                    Message = $"Successfully obtained {elementInfoList.Count} element information. The detailed information is stored in the Response property"+ message,
                    Response = elementInfoList,
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<object>>
                {
                    Success = false,
                    Message = $"Error getting element information: {ex.Message}",
                };
            }
            finally
            {
                _resetEvent.Set(); // Notify the waiting thread that the operation is complete
            }
        }

        /// <summary>
        /// Wait for creation to complete
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout (milliseconds)</param>
        /// <returns>Whether the operation was completed before the timeout</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// Determine what parameters to extract based on filter settings
        /// </summary>
        private List<string> GetRequestedParameters(FilterSetting filterSetting)
        {
            var requestedParams = new List<string>();
            
            // Add explicitly requested parameters
            if (filterSetting.RequestedParameters != null && filterSetting.RequestedParameters.Any())
            {
                requestedParams.AddRange(filterSetting.RequestedParameters);
            }
            
            // Add parameters that are being filtered on (so they appear in results)
            if (filterSetting.ParameterFilters != null && filterSetting.ParameterFilters.Any())
            {
                foreach (var paramFilter in filterSetting.ParameterFilters)
                {
                    if (!string.IsNullOrWhiteSpace(paramFilter.Name) && !requestedParams.Contains(paramFilter.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        requestedParams.Add(paramFilter.Name);
                    }
                }
            }
            
            return requestedParams.Any() ? requestedParams : null;
        }

        /// <summary>
        /// IExternalEventHandler.GetName implementation
        /// </summary>
        public string GetName()
        {
            return "Get Element Information";
        }
    }
}