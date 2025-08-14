using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Services.ElementInfoFactories
{
    /// <summary>
    /// Registry for managing element info factories
    /// </summary>
    public class ElementInfoFactoryRegistry
    {
        private readonly List<IElementInfoFactory> _factories;
        private static ElementInfoFactoryRegistry _instance;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static ElementInfoFactoryRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ElementInfoFactoryRegistry();
                }
                return _instance;
            }
        }

        private ElementInfoFactoryRegistry()
        {
            _factories = new List<IElementInfoFactory>();
            RegisterDefaultFactories();
        }

        /// <summary>
        /// Register default factories
        /// </summary>
        private void RegisterDefaultFactories()
        {
            // Register factories in order of priority (most specific first)
            Register(new ViewInfoFactory());
            Register(new PositioningElementInfoFactory());
            Register(new SpatialElementInfoFactory());
            Register(new AnnotationInfoFactory());
            Register(new GroupOrLinkInfoFactory());
            Register(new ElementTypeInfoFactory());
            Register(new ElementInstanceInfoFactory());
            Register(new ElementBasicInfoFactory()); // Fallback factory
        }

        /// <summary>
        /// Register a new factory
        /// </summary>
        public void Register(IElementInfoFactory factory)
        {
            if (factory != null && !_factories.Contains(factory))
            {
                _factories.Add(factory);
            }
        }

        /// <summary>
        /// Get the appropriate factory for an element
        /// </summary>
        public IElementInfoFactory GetFactory(Element element)
        {
            // Find the first factory that can handle this element
            return _factories.FirstOrDefault(f => f.CanHandle(element));
        }

        /// <summary>
        /// Create info for an element using the appropriate factory
        /// </summary>
        public object CreateInfo(Document doc, Element element)
        {
            var factory = GetFactory(element);
            if (factory != null)
            {
                return factory.CreateInfo(doc, element);
            }
            return null;
        }

        /// <summary>
        /// Create info for an element using the appropriate factory with selective parameter extraction
        /// </summary>
        public object CreateInfo(Document doc, Element element, string detailLevel, List<string> requestedParameters)
        {
            var factory = GetFactory(element);
            if (factory != null)
            {
                return factory.CreateInfo(doc, element, detailLevel, requestedParameters);
            }
            return null;
        }
    }
} 