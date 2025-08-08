using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Commands.Graphics;
using RevitMCPCommandSet.Models;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services
{
    public class ClearGraphicsOverridesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private ClearOverridesRequest _parameters;
        private object _result;
        
        // Status synchronization
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        // Implement IWaitableExternalEventHandler interface
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Clear Graphics Overrides Event Handler";
        }

        public void SetParameters(ClearOverridesRequest parameters)
        {
            _parameters = parameters;
            TaskCompleted = false;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    _result = new
                    {
                        Success = false,
                        Message = "No active document found.",
                        ProcessedCount = 0,
                        ErrorCount = 0,
                        ViewName = ""
                    };
                    TaskCompleted = true;
                    _resetEvent.Set();
                    return;
                }

                View view = doc.ActiveView;
                if (view == null)
                {
                    _result = new
                    {
                        Success = false,
                        Message = "No active view found.",
                        ProcessedCount = 0,
                        ErrorCount = 0,
                        ViewName = ""
                    };
                    TaskCompleted = true;
                    _resetEvent.Set();
                    return;
                }

                // Convert request to filter criteria
                var filterCriteria = ConvertToFilterCriteria(_parameters);
                
                // Use shared filtering utility
                var filterResult = ElementFilterUtility.FilterElements(doc, view, filterCriteria);
                
                if (!filterResult.Success)
                {
                    _result = new
                    {
                        Success = false,
                        Message = filterResult.Message,
                        ProcessedCount = 0,
                        ErrorCount = filterResult.ErrorCount,
                        ViewName = view.Name
                    };
                    TaskCompleted = true;
                    _resetEvent.Set();
                    return;
                }

                if (filterResult.Elements.Count == 0)
                {
                    _result = new
                    {
                        Success = true,
                        Message = "No elements found matching the criteria.",
                        ProcessedCount = 0,
                        ErrorCount = 0,
                        ViewName = view.Name
                    };
                    TaskCompleted = true;
                    _resetEvent.Set();
                    return;
                }

                // Clear overrides
                var result = ClearOverridesForElements(doc, view, filterResult.Elements);

                _result = new
                {
                    Success = true,
                    Message = $"Successfully processed {result.ProcessedCount} elements.",
                    ProcessedCount = result.ProcessedCount,
                    ErrorCount = result.ErrorCount,
                    ViewName = view.Name
                };
            }
            catch (Exception ex)
            {
                _result = new
                {
                    Success = false,
                    Message = $"Error executing clear overrides: {ex.Message}",
                    ProcessedCount = 0,
                    ErrorCount = 0,
                    ViewName = ""
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        /// <summary>
        /// Convert clear overrides request to filter criteria
        /// </summary>
        private FilterCriteria ConvertToFilterCriteria(ClearOverridesRequest request)
        {
            var criteria = new FilterCriteria();
            
            // Parse scope
            if (Enum.TryParse<FilterScope>(request.Scope, true, out var scope))
            {
                criteria.Scope = scope;
            }
            else
            {
                criteria.Scope = FilterScope.All;
            }
            
            // Set category
            criteria.Category = request.Category;
            
            // Convert parameter filter
            if (request.Parameter != null)
            {
                criteria.Parameter = new ParameterFilter
                {
                    Name = request.Parameter.Name,
                    Operator = request.Parameter.Operator,
                    Value = request.Parameter.Value,
                    ValueType = ParameterValueType.Double // Default to double for backward compatibility
                };
            }
            
            // Set element IDs
            criteria.ElementIds = request.ElementIds;
            
            return criteria;
        }

        private ClearOverridesResult ClearOverridesForElements(Document doc, View view, List<ElementId> elementIds)
        {
            OverrideGraphicSettings blankOverride = new OverrideGraphicSettings(); // Reset to default

            using (Transaction t = new Transaction(doc, "Clear Graphics Overrides"))
            {
                t.Start();

                int processedCount = 0;
                int errorCount = 0;

                foreach (ElementId elementId in elementIds)
                {
                    try
                    {
                        view.SetElementOverrides(elementId, blankOverride);
                        processedCount++;
                    }
                    catch
                    {
                        errorCount++;
                        // Continue processing other elements
                    }
                }

                t.Commit();

                return new ClearOverridesResult
                {
                    ProcessedCount = processedCount,
                    ErrorCount = errorCount
                };
            }
        }

        public object GetResult()
        {
            return _result;
        }
    }

    public class ClearOverridesResult
    {
        public int ProcessedCount { get; set; }
        public int ErrorCount { get; set; }
    }
}