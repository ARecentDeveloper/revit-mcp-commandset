using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.ElementInfos;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Services.ElementInfoFactories
{
    /// <summary>
    /// Factory for creating ElementTypeInfo objects
    /// </summary>
    public class ElementTypeInfoFactory : IElementInfoFactory
    {
        public bool CanHandle(Element element)
        {
            return element is ElementType;
        }

        public object CreateInfo(Document doc, Element element)
        {
            return CreateInfo(doc, element, "basic", null);
        }

        public object CreateInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters)
        {
            try
            {
                if (!(element is ElementType elementType))
                    return null;

                ElementTypeInfo typeInfo = new ElementTypeInfo();
                // Id
                typeInfo.Id = (int)elementType.Id.Value;
                // UniqueId
                typeInfo.UniqueId = elementType.UniqueId;
                // Type name
                typeInfo.Name = elementType.Name;
                // Family name
                typeInfo.FamilyName = elementType.FamilyName;
                // Category
                typeInfo.Category = elementType.Category?.Name;
                // Built-in category
                if (elementType.Category != null)
                {
                    typeInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), elementType.Category.Id.Value);
                }
                // Parameter dictionary - for type elements, we typically want more detail
                typeInfo.Parameters = ParameterUtility.GetDimensionParameters(elementType);
                ParameterInfo thicknessParam = ElementInfoUtility.GetThicknessInfo(element);      //Thickness parameter
                if (thicknessParam != null)
                {
                    typeInfo.Parameters.Add(thicknessParam);
                }
                return typeInfo;
            }
            catch
            {
                return null;
            }
        }
    }
} 