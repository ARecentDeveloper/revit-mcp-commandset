using System;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Utils.ParameterResolution
{
    /// <summary>
    /// Parameter expansion rules for resolving user terms into multiple specific parameters
    /// These are ONLY terms that apply across ALL categories and expand to multiple parameters
    /// Category-specific terms should be handled in individual parameter mapping classes
    /// </summary>
    public static class ParameterExpansionRules
    {
        /// <summary>
        /// Special cases where a single user term expands to multiple parameter names
        /// ONLY include truly cross-category terms that don't belong in specific parameter mappings
        /// </summary>
        public static Dictionary<string, List<string>> GetExpansionRules()
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                // Phase-related terms (one-to-many expansion)
                {"phase", new List<string> {"phase created", "phase demolished"}},
                {"phases", new List<string> {"phase created", "phase demolished"}},
                
                // Assembly terms (one-to-many expansion)
                {"assembly", new List<string> {"assembly code", "assembly description"}},
                {"assemblies", new List<string> {"assembly code", "assembly description"}},
                
                // Common dimension terms (one-to-many expansion)
                {"dimensions", new List<string> {"length", "width", "height"}},
                {"size", new List<string> {"length", "width", "height"}},
                
                // Material terms (one-to-many expansion for different material types)
                {"materials", new List<string> {"material", "structural material"}},
                
                // Name terms (one-to-many expansion for different name types)
                {"names", new List<string> {"type name", "family name"}},
            };
        }
    }
}