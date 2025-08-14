using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.ElementInfos;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Services.ElementInfoFactories
{
    /// <summary>
    /// Factory for creating ViewInfo objects
    /// </summary>
    public class ViewInfoFactory : IElementInfoFactory
    {
        public bool CanHandle(Element element)
        {
            return element is View;
        }

        public object CreateInfo(Document doc, Element element)
        {
            return CreateInfo(doc, element, "basic", null);
        }

        public object CreateInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters)
        {
            try
            {
                if (element == null || !(element is View))
                    return null;
                View view = element as View;

                ViewInfo info = new ViewInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue) : null,
                    ElementClass = element.GetType().Name,
                    ViewType = view.ViewType.ToString(),
                    Scale = view.Scale,
                    IsTemplate = view.IsTemplate,
                    DetailLevel = view.DetailLevel.ToString(),
                    BoundingBox = ElementInfoUtility.GetBoundingBoxInfo(element)
                };

                // Get the level associated with the view
                if (view is ViewPlan viewPlan && viewPlan.GenLevel != null)
                {
                    Level level = viewPlan.GenLevel;
                    info.AssociatedLevel = new LevelInfo
                    {
                        Id = level.Id.IntegerValue,
                        Name = level.Name,
                        Height = level.Elevation * 304.8 // Convert to mm
                    };
                }

                // Determine if the view is open and active
                UIDocument uidoc = new UIDocument(doc);

                // Get all open views
                IList<UIView> openViews = uidoc.GetOpenUIViews();

                foreach (UIView uiView in openViews)
                {
                    // Check if the view is open
                    if (uiView.ViewId.IntegerValue == view.Id.IntegerValue)
                    {
                        info.IsOpen = true;

                        // Check if the view is the currently active view
                        if (uidoc.ActiveView.Id.IntegerValue == view.Id.IntegerValue)
                        {
                            info.IsActive = true;
                        }
                        break;
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error creating view element information: {ex.Message}");
                return null;
            }
        }
    }
} 