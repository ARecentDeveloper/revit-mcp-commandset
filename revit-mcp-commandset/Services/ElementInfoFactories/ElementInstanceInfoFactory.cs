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

                // Check if detailed info is specifically requested
                bool needsDetailedInfo = detailLevel?.ToLower() == "detailed" || 
                                        detailLevel?.ToLower() == "full" ||
                                        NeedsDetailedInfo(requestedParameters);

                if (needsDetailedInfo)
                {
                    // Return full ElementInstanceInfo for detailed requests
                    return CreateDetailedInfo(doc, element, detailLevel, requestedParameters);
                }
                else
                {
                    // Return minimal info by default (token-efficient)
                    return CreateMinimalInfo(doc, element, requestedParameters);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create minimal element info (default) - only Id, Name, and requested parameters
        /// </summary>
        private ElementMinimalInfo CreateMinimalInfo(Document doc, Element element, List<string> requestedParameters)
        {
            var minimalInfo = new ElementMinimalInfo
            {
                Id = (int)element.Id.Value,
                Name = element.Name
            };

            // Add only specifically requested parameters
            if (requestedParameters != null && requestedParameters.Any())
            {
                var parameters = ElementInfoUtility.GetSpecificParameters(element, requestedParameters);
                minimalInfo.Parameters.AddRange(parameters);
            }

            return minimalInfo;
        }

        /// <summary>
        /// Create detailed element info - includes all properties
        /// </summary>
        private ElementInstanceInfo CreateDetailedInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters)
        {
            var elementInfo = new ElementInstanceInfo();
            // ID
            elementInfo.Id = (int)element.Id.Value;
            // UniqueId
            elementInfo.UniqueId = element.UniqueId;
            // Type name
            elementInfo.Name = element.Name;
            // Family name
            elementInfo.FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString();
            // Category
            elementInfo.Category = element.Category.Name;
            // Built-in category
            elementInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), element.Category.Id.Value);
            // Type Id
            elementInfo.TypeId = (int)element.GetTypeId().Value;
            //Room Id  
            if (element is FamilyInstance instance)
                elementInfo.RoomId = (int)(instance.Room?.Id.Value ?? -1);
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

        /// <summary>
        /// Check if requested parameters require detailed element info (like family, category, etc.)
        /// </summary>
        private bool NeedsDetailedInfo(List<string> requestedParameters)
        {
            if (requestedParameters == null) return false;

            var detailedInfoParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "family", "family name", "category", "type", "level", "room", "bounding box", "unique id"
            };

            return requestedParameters.Any(param => detailedInfoParams.Contains(param));
        }
    }
} 