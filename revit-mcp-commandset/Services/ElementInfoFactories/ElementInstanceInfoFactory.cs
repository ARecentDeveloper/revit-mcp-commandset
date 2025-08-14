using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMCPCommandSet.Models.ElementInfos;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

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
            return CreateInfo(doc, element, "basic", null);
        }

        public object CreateInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters)
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
                
                // Parameters: selective extraction based on detail level and requested parameters
                List<ParameterInfo> parameters = new List<ParameterInfo>();
                
                if (requestedParameters != null && requestedParameters.Any())
                {
                    // Extract specific requested parameters
                    parameters = ElementInfoUtility.GetSpecificParameters(element, requestedParameters);
                }
                else
                {
                    // Extract based on detail level
                    switch (detailLevel?.ToLower())
                    {
                        case "detailed":
                        case "all":
                            parameters = ElementInfoUtility.GetMappedParameters(element);
                            break;
                        case "standard":
                            parameters = ElementInfoUtility.GetMappedParameters(element);
                            break;
                        case "basic":
                        default:
                            parameters = ElementInfoUtility.GetBasicParameters(element);
                            break;
                    }
                }
                
                elementInfo.Parameters.AddRange(parameters);

                return elementInfo;
            }
            catch
            {
                return null;
            }
        }
    }
} 