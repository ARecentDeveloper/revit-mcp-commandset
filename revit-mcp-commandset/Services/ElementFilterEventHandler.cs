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
                if (elementList == null)
                    elementList = new List<Element>();
                
                DebugLogger.Log("HANDLER", $"ElementFilterEventHandler received {elementList.Count} elements from ElementFilterService");
                
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
                DebugLogger.Log("HANDLER", $"Starting to create element info for {elementList.Count} elements");
                
                foreach (var element in elementList)
                {
                    DebugLogger.Log("HANDLER", $"Processing element {element.Id} ({element.Name ?? "null"})");
                    
                    var info = registry.CreateInfo(doc, element, detailLevel, requestedParameters);
                    if (info is ElementMinimalInfo minimalInfo)
                    {
                        DebugLogger.Log("HANDLER", $"Created ElementMinimalInfo for element {element.Id}");
                        elementInfoList.Add(minimalInfo);
                    }
                    else if (info != null)
                    {
                        DebugLogger.Log("HANDLER", $"Converting {info.GetType().Name} to ElementMinimalInfo for element {element.Id}");
                        // Convert other types to ElementMinimalInfo for consistency
                        var converted = ConvertToElementMinimalInfo(info);
                        if (converted != null)
                        {
                            DebugLogger.Log("HANDLER", $"Successfully converted to ElementMinimalInfo for element {element.Id}");
                            elementInfoList.Add(converted);
                        }
                        else
                        {
                            DebugLogger.Log("HANDLER", $"Failed to convert to ElementMinimalInfo for element {element.Id}");
                        }
                    }
                    else
                    {
                        DebugLogger.Log("HANDLER", $"registry.CreateInfo returned null for element {element.Id}");
                    }
                }
                
                DebugLogger.Log("HANDLER", $"Final elementInfoList has {elementInfoList.Count} items");

                // Determine response format
                string responseFormat = FilterSetting.ResponseFormat ?? "tabular";
                DebugLogger.Log("HANDLER", $"Response format: {responseFormat}");
                
                if (responseFormat.Equals("standard", StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.Log("HANDLER", "Creating standard format response");
                    // Return standard format (backward compatibility)
                    Result = new AIResult<List<object>>
                    {
                        Success = true,
                        Message = $"Successfully obtained {elementInfoList.Count} element information. The detailed information is stored in the Response property" + limitMessage,
                        Response = elementInfoList.Cast<object>().ToList(),
                    };
                    DebugLogger.Log("HANDLER", $"Standard response created with {elementInfoList.Count} elements");
                }
                else
                {
                    DebugLogger.Log("HANDLER", "Creating tabular format response");
                    // Return tabular format (default)
                    string message = elementInfoList.Count > 0 
                        ? $"Successfully obtained {elementInfoList.Count} element information in optimized tabular format. Elements grouped by parameter values for efficient processing" + limitMessage
                        : $"No elements found matching the specified filter criteria. Filter applied: {GetFilterDescription(FilterSetting)}";
                    
                    DebugLogger.Log("HANDLER", $"Calling ElementFilterResponse.CreateTabular with {elementInfoList.Count} elements");
                    Result = ElementFilterResponse.CreateTabular(elementInfoList, message);
                    DebugLogger.Log("HANDLER", "Tabular response created");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogException("HANDLER", ex);
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
        /// Get a description of the applied filters for debugging
        /// </summary>
        private string GetFilterDescription(FilterSetting filterSetting)
        {
            var filters = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(filterSetting.FilterCategory))
                filters.Add($"Category={filterSetting.FilterCategory}");
            
            if (!string.IsNullOrWhiteSpace(filterSetting.FilterElementType))
                filters.Add($"ElementType={filterSetting.FilterElementType}");
            
            if (filterSetting.FilterFamilySymbolId > 0)
                filters.Add($"FamilySymbolId={filterSetting.FilterFamilySymbolId}");
            
            if (filterSetting.ParameterFilters?.Count > 0)
                filters.Add($"ParameterFilters={filterSetting.ParameterFilters.Count}");
            
            filters.Add($"IncludeTypes={filterSetting.IncludeTypes}");
            filters.Add($"IncludeInstances={filterSetting.IncludeInstances}");
            
            if (filterSetting.FilterVisibleInCurrentView)
                filters.Add("VisibleInCurrentView=true");
            
            return string.Join(", ", filters);
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
                
                case PositioningElementInfo positioningInfo:
                    DebugLogger.Log("CONVERT", $"Converting legacy PositioningElementInfo for element {positioningInfo.Id}");
                    return new ElementMinimalInfo
                    {
                        Id = positioningInfo.Id,
                        Name = positioningInfo.Name,
                        Parameters = new List<ParameterInfo>()
                    };
                
                default:
                    DebugLogger.Log("CONVERT", $"Unknown element info type: {elementInfo?.GetType().Name}");
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