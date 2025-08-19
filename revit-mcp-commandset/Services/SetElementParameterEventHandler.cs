using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Utils;
using RevitMCPCommandSet.Utils.ParameterMappings;
using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Services
{
    public class SetElementParameterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        /// <summary>
        /// Event wait object
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        /// <summary>
        /// Results data
        /// </summary>
        public object Results { get; private set; }

        // Parameters
        private List<int> _elementIds;
        private string _parameterName;
        private List<object> _parameterValues;
        private string _parameterValueType;

        /// <summary>
        /// Reset the event handler for a new operation
        /// </summary>
        public void Reset()
        {
            _resetEvent.Reset();
            Results = null;
        }

        /// <summary>
        /// Set parameters
        /// </summary>
        public void SetParameters(List<int> elementIds, string parameterName, List<object> parameterValues, string parameterValueType)
        {
            _elementIds = elementIds ?? new List<int>();
            _parameterName = parameterName ?? string.Empty;
            _parameterValues = parameterValues ?? new List<object>();
            _parameterValueType = parameterValueType;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;
                
                using (Transaction trans = new Transaction(doc, "Set Element Parameter"))
                {
                    trans.Start();
                    
                    int successCount = 0;
                    int failureCount = 0;
                    var failureDetails = new List<string>();
                    
                    // Process each element with its corresponding parameter value
                    for (int i = 0; i < _elementIds.Count; i++)
                    {
                        int elementId = _elementIds[i];
                        object parameterValue = i < _parameterValues.Count ? _parameterValues[i] : null;
                        
                        try
                        {
                            ElementId elemId = new ElementId((long)elementId);
                            Element element = doc.GetElement(elemId);
                            
                            if (element == null)
                            {
                                failureCount++;
                                failureDetails.Add($"Element {elementId} not found");
                                continue;
                            }
                            
                            // Try to find and set the parameter
                            bool success = SetElementParameter(element, _parameterName, parameterValue, _parameterValueType);
                            
                            if (success)
                            {
                                successCount++;
                            }
                            else
                            {
                                failureCount++;
                                failureDetails.Add($"Failed to set parameter '{_parameterName}' on element {elementId}");
                            }
                        }
                        catch (Exception elemEx)
                        {
                            failureCount++;
                            failureDetails.Add($"Error processing element {elementId}: {elemEx.Message}");
                        }
                    }
                    
                    trans.Commit();

                    // Set results
                    string batchType = _parameterValues.Distinct().Count() == 1 ? "single value" : "individual values";
                    Results = new SetElementParameterResult
                    {
                        Success = true,
                        Message = $"Batch operation completed. Set parameter '{_parameterName}' with {batchType} on {_elementIds.Count} elements. {successCount} successful, {failureCount} failed.",
                        SuccessCount = successCount,
                        FailureCount = failureCount,
                        Failures = failureDetails
                    };
                }
            }
            catch (Exception ex)
            {
                Results = new SetElementParameterResult
                {
                    Success = false,
                    Message = $"Operation failed: {ex.Message}",
                    Error = ex.ToString()
                };
            }
            finally
            {
                _resetEvent.Set(); // Signal that operation is complete
            }
        }

        /// <summary>
        /// Set a parameter on an element
        /// </summary>
        private bool SetElementParameter(Element element, string parameterName, object parameterValue, string parameterValueType)
        {
            try
            {
                // Validate inputs
                if (element == null)
                    throw new ArgumentNullException(nameof(element));
                    
                if (string.IsNullOrEmpty(parameterName))
                    throw new ArgumentException("Parameter name cannot be null or empty");
                
                // First try to get the parameter using our parameter mapping system
                Parameter param = null;
                
                // Try to determine the element's category
                BuiltInCategory category = BuiltInCategory.INVALID;
                try
                {
                    Category elemCategory = element.Category;
                    if (elemCategory != null)
                    {
                        category = (BuiltInCategory)elemCategory.Id.Value;
                    }
                }
                catch
                {
                    // If we can't determine the category, we'll use generic lookup
                }
                
                // Try to get parameter using parameter mapping if we have a valid category
                if (category != BuiltInCategory.INVALID && ParameterMappingManager.HasMapping(category))
                {
                    param = ParameterMappingManager.GetParameter(element, parameterName, category);
                }
                
                // Fallback to generic parameter lookup
                if (param == null)
                {
                    param = element.LookupParameter(parameterName);
                    
                    // Also try looking in the element type
                    if (param == null)
                    {
                        ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
                        if (elementType != null)
                        {
                            param = elementType.LookupParameter(parameterName);
                        }
                    }
                }
                
                // Try common aliases if still not found
                if (param == null)
                {
                    var aliases = ParameterMappingManager.GetParameterAliases(category);
                    if (aliases != null && aliases.ContainsKey(parameterName))
                    {
                        string actualParamName = aliases[parameterName];
                        param = element.LookupParameter(actualParamName);
                        
                        if (param == null)
                        {
                            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
                            if (elementType != null)
                            {
                                param = elementType.LookupParameter(actualParamName);
                            }
                        }
                    }
                }
                
                if (param == null)
                {
                    throw new Exception($"Parameter '{parameterName}' not found on element");
                }
                
                // Check if parameter is read-only
                if (param.IsReadOnly)
                {
                    throw new Exception($"Parameter '{parameterName}' is read-only and cannot be modified");
                }
                
                // Handle null/undefined parameter values
                if (parameterValue == null)
                {
                    // For string parameters, we can set to empty string
                    if (param.StorageType == StorageType.String)
                    {
                        return param.Set(string.Empty);
                    }
                    else
                    {
                        throw new Exception($"Cannot set null value for non-string parameter '{parameterName}'");
                    }
                }
                
                // Always use the parameter's actual storage type - this is critical for ElementId parameters
                StorageType storageType = param.StorageType;
                
                // Only override storage type for specific cases where the user explicitly specifies a different type
                if (!string.IsNullOrEmpty(_parameterValueType))
                {
                    switch (_parameterValueType.ToLower())
                    {
                        case "boolean":
                            // Boolean values are stored as integers in Revit
                            if (storageType == StorageType.Integer)
                            {
                                storageType = StorageType.Integer;
                            }
                            break;
                        case "elementid":
                            storageType = StorageType.ElementId;
                            break;
                        // For other types, trust the parameter's actual storage type
                        // This prevents issues where Claude specifies "Integer" for ElementId parameters
                    }
                }
                
                // Set parameter value based on type
                switch (storageType)
                {
                    case StorageType.String:
                        if (parameterValue is string stringValue)
                        {
                            return param.Set(stringValue);
                        }
                        else
                        {
                            return param.Set(parameterValue?.ToString() ?? string.Empty);
                        }
                        
                    case StorageType.Double:
                        if (double.TryParse(parameterValue?.ToString() ?? "0", out double doubleValue))
                        {
                            // Convert value if needed using parameter mapping
                            object convertedValue = ParameterMappingManager.ConvertValue(parameterName, doubleValue, category);
                            if (convertedValue is double convertedDouble)
                            {
                                return param.Set(convertedDouble);
                            }
                            else
                            {
                                return param.Set(doubleValue);
                            }
                        }
                        break;
                        
                    case StorageType.Integer:
                        // Handle boolean values (stored as integers in Revit)
                        if (parameterValue is bool boolValue)
                        {
                            return param.Set(boolValue ? 1 : 0);
                        }
                        else if (int.TryParse(parameterValue?.ToString() ?? "0", out int intValue))
                        {
                            return param.Set(intValue);
                        }
                        else if (bool.TryParse(parameterValue?.ToString() ?? "false", out boolValue))
                        {
                            return param.Set(boolValue ? 1 : 0);
                        }
                        break;
                        
                    case StorageType.ElementId:
                        // Handle ElementId parameters (like Level, Type, etc.)
                        if (parameterValue is int elementIdInt)
                        {
                            ElementId elementId = new ElementId((long)elementIdInt);
                            return param.Set(elementId);
                        }
                        else if (parameterValue is long elementIdLong)
                        {
                            ElementId elementId = new ElementId(elementIdLong);
                            return param.Set(elementId);
                        }
                        else if (int.TryParse(parameterValue?.ToString() ?? "0", out int elementIdValue))
                        {
                            ElementId elementId = new ElementId((long)elementIdValue);
                            return param.Set(elementId);
                        }
                        else if (long.TryParse(parameterValue?.ToString() ?? "0", out long elementIdValueLong))
                        {
                            ElementId elementId = new ElementId(elementIdValueLong);
                            return param.Set(elementId);
                        }
                        break;
                }
                
                throw new Exception($"Unable to set parameter value. Parameter type: {storageType}, Value: {parameterValue} ({parameterValue.GetType()})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error setting parameter '{parameterName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Wait for operation to complete
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds</param>
        /// <returns>Whether operation completed within timeout</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// IExternalEventHandler.GetName implementation
        /// </summary>
        public string GetName()
        {
            return "Set Element Parameter";
        }
    }
}