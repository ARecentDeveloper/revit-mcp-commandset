using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Services.ElementInfoFactories
{
    /// <summary>
    /// Interface for element info factories
    /// </summary>
    public interface IElementInfoFactory
    {
        /// <summary>
        /// Determines if this factory can handle the given element
        /// </summary>
        bool CanHandle(Element element);

        /// <summary>
        /// Creates the appropriate info object for the element
        /// </summary>
        object CreateInfo(Document doc, Element element);

        /// <summary>
        /// Creates the appropriate info object for the element with selective parameter extraction
        /// </summary>
        object CreateInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters);
    }
} 