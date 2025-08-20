using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Utils.ParameterMappings;
using RevitMCPCommandSet.Utils.ParameterResolution;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Fallback parameter mapping for categories without specific mappings.
    /// Only uses SharedParameterMapping and generic parameter lookup.
    /// </summary>
    internal class UnmappedCategoryFallback : ParameterMappingBase
    {
        private readonly BuiltInCategory _category;

        public UnmappedCategoryFallback(BuiltInCategory category)
        {
            _category = category;
        }

        public override BuiltInCategory Category => _category;

        protected override Parameter GetCategorySpecificParameter(Element element, string parameterName)
        {
            // For unmapped categories, only use generic parameter lookup
            var genericParam = element.LookupParameter(parameterName);
            if (genericParam != null) return genericParam;

            // Try type parameter
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
            return elementType?.LookupParameter(parameterName);
        }

        public override object ConvertValue(string parameterName, object inputValue)
        {
            // No specific conversions for unmapped categories
            return inputValue;
        }

        public override List<string> GetCommonParameterNames()
        {
            // Return only shared parameter names for unmapped categories
            return SharedParameterMapping.GetCommonParameterNames();
        }

        public override Dictionary<string, string> GetParameterAliases()
        {
            // Return only shared parameter aliases for unmapped categories
            return SharedParameterMapping.CommonAliases;
        }

        public override bool HasParameter(string parameterName)
        {
            // Check if it's a shared parameter or alias
            string actualParamName = SharedParameterMapping.CommonAliases.ContainsKey(parameterName) 
                ? SharedParameterMapping.CommonAliases[parameterName] 
                : parameterName;
            
            return SharedParameterMapping.IsSharedParameter(actualParamName);
        }
    }

    public class ResolveParameterNamesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        
        public object Results { get; private set; }
        
        // Parameters
        private string _filterCategory;
        private List<string> _userParameterNames;
        private string _elementId;

        public void Reset()
        {
            _resetEvent.Reset();
            Results = null;
        }

        public void SetParameters(string filterCategory, List<string> userParameterNames, string elementId)
        {
            _filterCategory = filterCategory;
            _userParameterNames = userParameterNames;
            _elementId = elementId;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                System.Diagnostics.Trace.WriteLine($"ResolveParameterNames Execute: Category={_filterCategory}");

                // Parse category
                if (!Enum.TryParse<BuiltInCategory>(_filterCategory, true, out var category))
                {
                    Results = new
                    {
                        success = false,
                        message = $"Invalid category: {_filterCategory}. Please use valid Revit category like OST_StructuralFraming, OST_Walls, etc."
                    };
                    return;
                }

                // Get parameter mapping for this category (if available)
                var mapping = ParameterMappingManager.GetMapping(category);
                
                // For unmapped categories, we'll create a fallback mapping that only uses SharedParameterMapping
                bool isUnmappedCategory = mapping == null;
                if (isUnmappedCategory)
                {
                    System.Diagnostics.Trace.WriteLine($"ResolveParameterNames: No specific mapping for {_filterCategory}, using SharedParameterMapping only");
                    mapping = new UnmappedCategoryFallback(category);
                }
                var resolvedParameters = new List<object>();
                var resolvedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                System.Diagnostics.Trace.WriteLine($"ResolveParameterNames: Processing {_userParameterNames.Count} parameter names");

                foreach (var userTerm in _userParameterNames)
                {
                    // Check for special cases first (one-to-many mappings)
                    var specialCases = GetSpecialCaseMatches(userTerm.ToLower());
                    if (specialCases.Any())
                    {
                        // Expand special cases into multiple results
                        System.Diagnostics.Trace.WriteLine($"ResolveParameterNames: Special case '{userTerm}' → [{string.Join(", ", specialCases)}]");
                        
                        foreach (var specialCase in specialCases)
                        {
                            if (!resolvedNames.Contains(specialCase))
                            {
                                var specialResolution = CreateResolutionResult(userTerm, specialCase, 0.9f, "special_case", new List<string>());
                                resolvedParameters.Add(specialResolution);
                                resolvedNames.Add(specialCase);
                                System.Diagnostics.Trace.WriteLine($"ResolveParameterNames: '{userTerm}' → '{specialCase}' (special case)");
                            }
                            else
                            {
                                System.Diagnostics.Trace.WriteLine($"ResolveParameterNames: Skipping duplicate '{specialCase}' from '{userTerm}'");
                            }
                        }
                    }
                    else
                    {
                        // Regular single parameter resolution
                        var resolution = ResolveParameterName(mapping, userTerm);
                        var resolvedName = resolution.GetType().GetProperty("resolvedName")?.GetValue(resolution) as string;
                        
                        if (!string.IsNullOrEmpty(resolvedName) && !resolvedNames.Contains(resolvedName))
                        {
                            resolvedParameters.Add(resolution);
                            resolvedNames.Add(resolvedName);
                            System.Diagnostics.Trace.WriteLine($"ResolveParameterNames: '{userTerm}' → '{resolvedName}' (confidence: {resolution.GetType().GetProperty("confidence")?.GetValue(resolution)})");
                        }
                        else if (!string.IsNullOrEmpty(resolvedName))
                        {
                            System.Diagnostics.Trace.WriteLine($"ResolveParameterNames: Skipping duplicate '{resolvedName}' from '{userTerm}'");
                        }
                        else
                        {
                            // Still add failed resolutions
                            resolvedParameters.Add(resolution);
                            System.Diagnostics.Trace.WriteLine($"ResolveParameterNames: '{userTerm}' → failed to resolve");
                        }
                    }
                }

                Results = new
                {
                    success = true,
                    message = $"Resolved {resolvedParameters.Count} parameter names for category {_filterCategory}",
                    category = _filterCategory,
                    resolvedParameters = resolvedParameters
                };

                System.Diagnostics.Trace.WriteLine($"ResolveParameterNames: Successfully resolved {resolvedParameters.Count} parameters");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"ResolveParameterNames Execute Error: {ex.Message}");
                Results = new
                {
                    success = false,
                    message = $"Parameter resolution failed: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private object ResolveParameterName(ParameterMappingBase mapping, string userTerm)
        {
            try
            {
                string resolvedName = null;
                float confidence = 0f;
                string method = "none";
                List<string> suggestions = new List<string>();

                // Clean up user term
                string cleanTerm = userTerm?.Trim();
                if (string.IsNullOrEmpty(cleanTerm))
                {
                    return CreateResolutionResult(userTerm, null, 0f, "error", new List<string>(), "Empty parameter name");
                }

                // 1. Try category-specific aliases first
                var aliases = mapping.GetParameterAliases();
                if (aliases.ContainsKey(cleanTerm.ToLower()))
                {
                    resolvedName = aliases[cleanTerm.ToLower()];
                    confidence = 0.95f;
                    method = "category_alias";
                }
                // 2. Try shared parameter aliases
                else if (SharedParameterMapping.CommonAliases.ContainsKey(cleanTerm.ToLower()))
                {
                    resolvedName = SharedParameterMapping.CommonAliases[cleanTerm.ToLower()];
                    confidence = 0.9f;
                    method = "shared_alias";
                }
                // 3. Try exact match in mapping dictionaries (for actual parameter names)
                else if (mapping.HasParameter(cleanTerm))
                {
                    resolvedName = cleanTerm;
                    confidence = 1.0f;
                    method = "exact";
                }
                // 4. Try fuzzy matching
                else
                {
                    var fuzzyResults = FindFuzzyMatches(mapping, cleanTerm);
                    if (fuzzyResults.Any())
                    {
                        var bestMatch = fuzzyResults.First();
                        resolvedName = bestMatch.parameterName;
                        confidence = bestMatch.confidence;
                        method = "fuzzy";
                        
                        // Add other matches as suggestions
                        suggestions = fuzzyResults.Skip(1).Take(3).Select(m => m.parameterName).ToList();
                    }
                }

                return CreateResolutionResult(userTerm, resolvedName, confidence, method, suggestions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"ResolveParameterName Error for '{userTerm}': {ex.Message}");
                return CreateResolutionResult(userTerm, null, 0f, "error", new List<string>(), ex.Message);
            }
        }

        private object CreateResolutionResult(string userTerm, string resolvedName, float confidence, string method, List<string> suggestions, string error = null)
        {
            return new
            {
                userTerm = userTerm,
                resolvedName = resolvedName,
                confidence = confidence,
                available = resolvedName != null,
                method = method,
                suggestions = suggestions,
                error = error
            };
        }

        private List<(string parameterName, float confidence)> FindFuzzyMatches(ParameterMappingBase mapping, string userTerm)
        {
            var matches = new List<(string parameterName, float confidence)>();
            var commonParams = mapping.GetCommonParameterNames();
            var lowerUserTerm = userTerm.ToLower();

            // Special case handling for cross-category terms
            var specialCases = GetSpecialCaseMatches(lowerUserTerm);
            if (specialCases.Any())
            {
                foreach (var specialCase in specialCases)
                {
                    if (commonParams.Any(p => p.Equals(specialCase, StringComparison.OrdinalIgnoreCase)))
                    {
                        matches.Add((specialCase, 0.9f));
                    }
                }
            }

            // Exact contains match (parameter name contains user term)
            foreach (var param in commonParams)
            {
                var lowerParam = param.ToLower();
                
                if (lowerParam.Contains(lowerUserTerm))
                {
                    float confidence = lowerUserTerm.Length / (float)lowerParam.Length;
                    matches.Add((param, Math.Min(confidence * 0.8f, 0.85f)));
                }
            }

            // Reverse contains (user term contains parameter name)
            foreach (var param in commonParams)
            {
                var lowerParam = param.ToLower();
                
                if (lowerUserTerm.Contains(lowerParam) && lowerParam.Length > 2)
                {
                    float confidence = lowerParam.Length / (float)lowerUserTerm.Length;
                    matches.Add((param, Math.Min(confidence * 0.7f, 0.75f)));
                }
            }

            // Word boundary matching
            var userWords = lowerUserTerm.Split(new char[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var param in commonParams)
            {
                var paramWords = param.ToLower().Split(new char[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                
                int matchingWords = userWords.Count(uw => paramWords.Any(pw => pw.Contains(uw) || uw.Contains(pw)));
                if (matchingWords > 0)
                {
                    float confidence = (float)matchingWords / Math.Max(userWords.Length, paramWords.Length);
                    matches.Add((param, Math.Min(confidence * 0.6f, 0.7f)));
                }
            }

            // Remove duplicates and sort by confidence
            return matches
                .GroupBy(m => m.parameterName)
                .Select(g => g.OrderByDescending(m => m.confidence).First())
                .Where(m => m.confidence > 0.3f) // Minimum confidence threshold
                .OrderByDescending(m => m.confidence)
                .Take(5) // Top 5 matches
                .ToList();
        }

        private List<string> GetSpecialCaseMatches(string userTerm)
        {
            // Get cross-category expansion rules
            var expansionRules = ParameterExpansionRules.GetExpansionRules();

            if (expansionRules.ContainsKey(userTerm))
            {
                System.Diagnostics.Trace.WriteLine($"GetSpecialCaseMatches: Found expansion rule for '{userTerm}' → [{string.Join(", ", expansionRules[userTerm])}]");
                return expansionRules[userTerm];
            }

            return new List<string>();
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Resolve Parameter Names";
        }
    }
}