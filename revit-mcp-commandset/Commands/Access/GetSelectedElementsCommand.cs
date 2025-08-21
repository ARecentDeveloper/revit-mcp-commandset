using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Commands.Access
{
    public class GetSelectedElementsCommand : ExternalEventCommandBase
    {
        private GetSelectedElementsEventHandler _handler => (GetSelectedElementsEventHandler)Handler;
        private readonly UIApplication _uiApp;

        public override string CommandName => "get_selected_elements";

        public GetSelectedElementsCommand(UIApplication uiApp)
            : base(new GetSelectedElementsEventHandler(), uiApp)
        {
            _uiApp = uiApp;
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parse parameters
                int? limit = parameters?["limit"]?.Value<int>();

                // Direct execution - bypass external event for real-time accuracy
                var uiDoc = _uiApp.ActiveUIDocument;
                var doc = uiDoc.Document;

                // Get currently selected elements immediately
                var selectedIds = uiDoc.Selection.GetElementIds();
                
                // Convert to elements immediately to avoid any delays
                var selectedElements = new List<Element>();
                foreach (var id in selectedIds)
                {
                    var element = doc.GetElement(id);
                    if (element != null)
                    {
                        selectedElements.Add(element);
                    }
                }

                // Apply quantity limit
                if (limit.HasValue && limit.Value > 0)
                {
                    selectedElements = selectedElements.Take(limit.Value).ToList();
                }

                // Convert to ElementInfo list
                var resultElements = selectedElements.Select(element => new ElementInfo
                {
#if REVIT2024_OR_GREATER
                    Id = element.Id.Value,
#else
                    Id = element.Id.IntegerValue,
#endif
                    // UniqueId = element.UniqueId,
                    Name = element.Name,
                    Category = element.Category?.Name
                }).ToList();

                return resultElements;
            }
            catch (Exception ex)
            {
                throw new Exception($"Get selected elements failed: {ex.Message}");
            }
        }
    }
}
