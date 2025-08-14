using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMCPCommandSet.Models.ElementInfos;
using RevitMCPCommandSet.Utils;
using System;

namespace RevitMCPCommandSet.Services.ElementInfoFactories
{
    /// <summary>
    /// Factory for creating ElementInstanceInfo objects
    /// </summary>
    public class ElementInstanceInfoFactory : IElementInfoFactory
    {
        public bool CanHandle(Element element)
        {
            // This factory handles solid model elements (elements with material quantities)
            return element?.Category?.HasMaterialQuantities ?? false;
        }

        public object CreateInfo(Document doc, Element element)
        {
            try
            {
                if (element?.Category == null)
                    return null;

                ElementInstanceInfo elementInfo = new ElementInstanceInfo();
                // ID
                elementInfo.Id = element.Id.IntegerValue;
                // UniqueId
                elementInfo.UniqueId = element.UniqueId;
                // Type name
                elementInfo.Name = element.Name;
                // Family name
                elementInfo.FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString();
                // Category
                elementInfo.Category = element.Category.Name;
                // Built-in category
                elementInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue);
                // Type Id
                elementInfo.TypeId = element.GetTypeId().IntegerValue;
                //Room Id  
                if (element is FamilyInstance instance)
                    elementInfo.RoomId = instance.Room?.Id.IntegerValue ?? -1;
                // Level
                elementInfo.Level = ElementInfoUtility.GetElementLevel(doc, element);
                // Maximum bounding box
                elementInfo.BoundingBox = ElementInfoUtility.GetBoundingBoxInfo(element);
                // Parameters
                ParameterInfo thicknessParam = ElementInfoUtility.GetThicknessInfo(element);      //Thickness parameter
                if (thicknessParam != null)
                {
                    elementInfo.Parameters.Add(thicknessParam);
                }
                ParameterInfo heightParam = ElementInfoUtility.GetBoundingBoxHeight(elementInfo.BoundingBox);      //Height parameter
                if (heightParam != null)
                {
                    elementInfo.Parameters.Add(heightParam);
                }

                return elementInfo;
            }
            catch
            {
                return null;
            }
        }
    }
} 