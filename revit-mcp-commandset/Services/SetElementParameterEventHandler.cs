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
        private object _parameterValue;
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
        public void SetParameters(List<int> elementIds, string parameterName, object parameterValue, string parameterValueType)
        {
            _elementIds = elementIds ?? new List<int>();
            _parameterName = parameterName ?? string.Empty;
            _parameterValue = parameterValue;
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
                    
                    // Process each element
                    foreach (int elementId in _elementIds)
                    {
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
                            bool success = SetElementParameter(element, _parameterName, _parameterValue, _parameterValueType);
                            
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
                    Results = new SetElementParameterResult
                    {
                        Success = true,
                        Message = $"Operation completed. {successCount} elements updated successfully, {failureCount} failed.",
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
                
                // Determine value type if not specified
                StorageType storageType = param.StorageType;
                if (!string.IsNullOrEmpty(_parameterValueType))
                {
                    // Use specified type hint
                    switch (_parameterValueType.ToLower())
                    {
                        case "string":
                            storageType = StorageType.String;
                            break;
                        case "double":
                            storageType = StorageType.Double;
                            break;
                        case "integer":
                            storageType = StorageType.Integer;
                            break;
                        case "boolean":
                            storageType = StorageType.Integer; // Boolean is stored as integer in Revit
                            break;
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