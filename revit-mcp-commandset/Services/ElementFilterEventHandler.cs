using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.ElementInfos;
using RevitMCPCommandSet.Services.ElementInfoFactories;
using RevitMCPCommandSet.Services.Filtering;
using RevitMCPCommandSet.Utils;
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
        public object Result { get; private set; }

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
                // Check if the filter settings are valid
                if (!FilterSetting.Validate(out string errorMessage))
                    throw new Exception(errorMessage);
                
                // Get the Id of the element that meets the specified conditions
                var elementList = ElementFilterService.GetFilteredElements(doc, FilterSetting);
                if (elementList == null || !elementList.Any())
                    throw new Exception("The specified element was not found in the project. Please check if the filter settings are correct");
                
                // Maximum number of filters limit
                string limitMessage = "";
                if (FilterSetting.MaxElements > 0)
                {
                    if (elementList.Count > FilterSetting.MaxElements)
                    {
                        elementList = elementList.Take(FilterSetting.MaxElements).ToList();
                        limitMessage = $". Note: {elementList.Count} elements found, showing first {FilterSetting.MaxElements} due to limit";
                    }
                }

                // Get information of the specified Id element using the factory registry with selective extraction
                var registry = ElementInfoFactoryRegistry.Instance;
                
                // Determine what parameters to extract based on the filter settings
                var requestedParameters = GetRequestedParameters(FilterSetting);
                var detailLevel = FilterSetting.DetailLevel ?? "basic";
                
                // Create element info objects
                var elementInfoList = new List<ElementMinimalInfo>();
                foreach (var element in elementList)
                {
                    var info = registry.CreateInfo(doc, element, detailLevel, requestedParameters);
                    if (info is ElementMinimalInfo minimalInfo)
                    {
                        elementInfoList.Add(minimalInfo);
                    }
                    else if (info != null)
                    {
                        // Convert other types to ElementMinimalInfo for consistency
                        var converted = ConvertToElementMinimalInfo(info);
                        if (converted != null)
                        {
                            elementInfoList.Add(converted);
                        }
                    }
                }

                // Determine response format
                string responseFormat = FilterSetting.ResponseFormat ?? "tabular";
                
                if (responseFormat.Equals("standard", StringComparison.OrdinalIgnoreCase))
                {
                    // Return standard format (backward compatibility)
                    Result = new AIResult<List<object>>
                    {
                        Success = true,
                        Message = $"Successfully obtained {elementInfoList.Count} element information. The detailed information is stored in the Response property" + limitMessage,
                        Response = elementInfoList.Cast<object>().ToList(),
                    };
                }
                else
                {
                    // Return tabular format (default)
                    string message = $"Successfully obtained {elementInfoList.Count} element information in optimized tabular format. Elements grouped by parameter values for efficient processing" + limitMessage;
                    Result = ElementFilterResponse.CreateTabular(elementInfoList, message);
                }
            }
            catch (Exception ex)
            {
                Result = ElementFilterResponse.CreateError($"Error getting element information: {ex.Message}");
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
        /// Convert various element info types to ElementMinimalInfo for consistent processing
        /// </summary>
        private ElementMinimalInfo ConvertToElementMinimalInfo(object elementInfo)
        {
            switch (elementInfo)
            {
                case ElementBasicInfo basicInfo:
                    return new ElementMinimalInfo
                    {
                        Id = basicInfo.Id,
                        Name = basicInfo.Name,
                        Parameters = basicInfo.Parameters ?? new List<ParameterInfo>()
                    };
                
                case ElementInstanceInfo instanceInfo:
                    return new ElementMinimalInfo
                    {
                        Id = instanceInfo.Id,
                        Name = instanceInfo.Name,
                        Parameters = instanceInfo.Parameters ?? new List<ParameterInfo>()
                    };
                
                case ElementMinimalInfo minimalInfo:
                    return minimalInfo;
                
                default:
                    System.Diagnostics.Trace.WriteLine($"Warning: Unknown element info type: {elementInfo?.GetType().Name}");
                    return null;
            }
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