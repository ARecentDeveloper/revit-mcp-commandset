using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Service for generating PA (Port Authority) compliant naming conventions
    /// </summary>
    public static class PANamingConventionService
    {
        /// <summary>
        /// Generate PA-compliant annotation family name (CI-CATEGORY-DESCRIPTION1-DESCRIPTION2 format)
        /// </summary>
        /// <param name="category">Family category</param>
        /// <param name="currentName">Current family name</param>
        /// <param name="companyInitial">Company initial (default: "EN")</param>
        /// <returns>PA-compliant suggested name</returns>
        public static string GenerateAnnotationFamilyName(string category, string currentName, string companyInitial = "EN")
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(currentName))
            {
                System.Diagnostics.Trace.WriteLine($"PA Naming: Empty category or name - Category: '{category}', Name: '{currentName}'");
                return "";
            }

            try
            {
                // Use provided company initial or default
                var ci = string.IsNullOrWhiteSpace(companyInitial) ? "EN" : companyInitial.ToUpper();
                
                // Map category to new abbreviations
                var categoryCode = MapAnnotationCategoryToCode(category);
                
                // Extract description parts from current name
                var (description1, description2) = ExtractAnnotationDescriptions(currentName, categoryCode);
                
                // Generate new format: CI-CATEGORY-DESCRIPTION1-DESCRIPTION2
                var suggestedName = string.IsNullOrWhiteSpace(description2) 
                    ? $"{ci}-{categoryCode}-{description1}" 
                    : $"{ci}-{categoryCode}-{description1}-{description2}";
                
                System.Diagnostics.Trace.WriteLine($"PA Naming: Annotation family '{currentName}' (Category: '{category}') -> '{suggestedName}'");
                
                return suggestedName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error generating annotation family name: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Generate PA-compliant model family name (CATEGORY-MANUFACTURER-DESCRIPTION format)
        /// </summary>
        /// <param name="category">Family category</param>
        /// <param name="currentName">Current family name</param>
        /// <param name="manufacturer">Detected or specified manufacturer</param>
        /// <returns>PA-compliant suggested name</returns>
        public static string GenerateModelFamilyName(string category, string currentName, string manufacturer = null)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(currentName))
            {
                System.Diagnostics.Trace.WriteLine($"PA Naming: Empty category or name - Category: '{category}', Name: '{currentName}'");
                return "";
            }

            try
            {
                // Clean and format category name
                var cleanCategory = CleanCategoryName(category);
                
                // Detect manufacturer if not provided
                if (string.IsNullOrWhiteSpace(manufacturer))
                {
                    manufacturer = DetectManufacturer(currentName);
                }
                
                // Extract meaningful description from current name
                var description = ExtractDescription(currentName, manufacturer);
                
                // Generate PA format: CATEGORY-MANUFACTURER-DESCRIPTION
                var suggestedName = $"{cleanCategory.ToUpper()}-{manufacturer.ToUpper()}-{description.ToUpper()}";
                
                System.Diagnostics.Trace.WriteLine($"PA Naming: Model family '{currentName}' (Category: '{category}', Manufacturer: '{manufacturer}') -> '{suggestedName}'");
                
                return suggestedName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error generating model family name: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Detect manufacturer from family name
        /// </summary>
        /// <param name="familyName">Family name to analyze</param>
        /// <returns>Detected manufacturer or "Generic" if not found</returns>
        public static string DetectManufacturer(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                return "Generic";

            try
            {
                // Common manufacturer patterns
                var manufacturers = new[]
                {
                    "Kohler", "American Standard", "Toto", "Moen", "Delta", "Grohe",
                    "Steelcase", "Herman Miller", "Knoll", "Haworth", "Teknion",
                    "Armstrong", "USG", "Owens Corning", "Johns Manville",
                    "Carrier", "Trane", "York", "Lennox", "Rheem", "Goodman",
                    "Square D", "Schneider", "GE", "Siemens", "Eaton", "ABB",
                    "Lutron", "Leviton", "Cooper", "Hubbell", "Pass & Seymour"
                };

                // Check for manufacturer names in the family name
                foreach (var manufacturer in manufacturers)
                {
                    if (familyName.IndexOf(manufacturer, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return manufacturer;
                    }
                }

                // Check for manufacturer patterns at the beginning of the name
                var words = familyName.Split(new char[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    var firstWord = words[0];
                    
                    // If first word looks like a manufacturer (capitalized, reasonable length)
                    if (firstWord.Length >= 3 && firstWord.Length <= 15 && 
                        char.IsUpper(firstWord[0]) && firstWord.All(c => char.IsLetter(c)))
                    {
                        return firstWord;
                    }
                }

                return "Generic";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error detecting manufacturer: {ex.Message}");
                return "Generic";
            }
        }

        /// <summary>
        /// Generate PA-compliant workset name
        /// </summary>
        /// <param name="currentName">Current workset name</param>
        /// <returns>PA-compliant suggested name</returns>
        public static string GenerateWorksetName(string currentName)
        {
            if (string.IsNullOrWhiteSpace(currentName))
                return "";

            try
            {
                // Clean the name and apply PA conventions
                var cleanName = CleanName(currentName);
                
                // Apply PA workset naming patterns
                if (cleanName.ToLower().Contains("arch"))
                    return "PA-ARCH-" + Regex.Replace(cleanName, "arch", "", RegexOptions.IgnoreCase).Trim().ToUpper();
                if (cleanName.ToLower().Contains("struct"))
                    return "PA-STRUCT-" + Regex.Replace(cleanName, "struct", "", RegexOptions.IgnoreCase).Trim().ToUpper();
                if (cleanName.ToLower().Contains("mep") || cleanName.ToLower().Contains("mechanical") || cleanName.ToLower().Contains("electrical") || cleanName.ToLower().Contains("plumbing"))
                    return "PA-MEP-" + cleanName.ToUpper();
                
                return "PA-" + cleanName.ToUpper();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error generating workset name: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Generate PA-compliant sheet name
        /// </summary>
        /// <param name="currentName">Current sheet name</param>
        /// <param name="sheetNumber">Sheet number</param>
        /// <returns>PA-compliant suggested name</returns>
        public static string GenerateSheetName(string currentName, string sheetNumber)
        {
            if (string.IsNullOrWhiteSpace(currentName))
                return "";

            try
            {
                // Clean the name
                var cleanName = CleanName(currentName);
                
                // Apply PA sheet naming conventions
                // Keep the original name but ensure proper formatting
                return cleanName.ToUpper();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error generating sheet name: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Suggest appropriate category for Generic Model elements
        /// </summary>
        /// <param name="elementName">Element name</param>
        /// <param name="currentCategory">Current category</param>
        /// <returns>Suggested category</returns>
        public static string SuggestCategory(string elementName, string currentCategory)
        {
            if (string.IsNullOrWhiteSpace(elementName))
                return currentCategory;

            try
            {
                var lowerName = elementName.ToLower();

                // Furniture patterns
                if (lowerName.Contains("chair") || lowerName.Contains("desk") || lowerName.Contains("table") || 
                    lowerName.Contains("cabinet") || lowerName.Contains("shelf"))
                    return "Furniture";

                // Equipment patterns
                if (lowerName.Contains("equipment") || lowerName.Contains("unit") || lowerName.Contains("machine"))
                    return "Mechanical Equipment";

                // Electrical patterns
                if (lowerName.Contains("panel") || lowerName.Contains("switch") || lowerName.Contains("outlet") || 
                    lowerName.Contains("light") || lowerName.Contains("fixture"))
                    return "Electrical Equipment";

                // Plumbing patterns
                if (lowerName.Contains("sink") || lowerName.Contains("toilet") || lowerName.Contains("faucet") || 
                    lowerName.Contains("valve") || lowerName.Contains("pipe"))
                    return "Plumbing Fixtures";

                // Structural patterns
                if (lowerName.Contains("beam") || lowerName.Contains("column") || lowerName.Contains("brace"))
                    return "Structural Framing";

                return "Specialty Equipment";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error suggesting category: {ex.Message}");
                return currentCategory;
            }
        }

        /// <summary>
        /// Clean category name for PA conventions
        /// </summary>
        private static string CleanCategoryName(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return "UNKNOWN";

            // Remove OST_ prefix if present
            var cleaned = category.StartsWith("OST_") ? category.Substring(4) : category;
            
            // Replace underscores with spaces and clean up
            cleaned = cleaned.Replace("_", " ");
            
            // Remove common prefixes/suffixes
            cleaned = Regex.Replace(cleaned, @"\b(Categories?|Category)\b", "", RegexOptions.IgnoreCase).Trim();
            
            // Convert to proper format
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            
            return string.IsNullOrWhiteSpace(cleaned) ? "UNKNOWN" : cleaned;
        }

        /// <summary>
        /// Extract meaningful description from name
        /// </summary>
        private static string ExtractDescription(string name, string manufacturerToRemove = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "UNNAMED";

            var description = name;

            // Remove manufacturer if specified
            if (!string.IsNullOrWhiteSpace(manufacturerToRemove))
            {
                description = Regex.Replace(description, Regex.Escape(manufacturerToRemove), "", RegexOptions.IgnoreCase).Trim();
            }

            // Remove common prefixes
            description = Regex.Replace(description, @"^(PA-|PA_)", "", RegexOptions.IgnoreCase);
            
            // Clean up the description
            description = CleanName(description);
            
            return string.IsNullOrWhiteSpace(description) ? "UNNAMED" : description;
        }

        /// <summary>
        /// Clean and format name for PA conventions
        /// </summary>
        private static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            // Replace various separators with spaces
            var cleaned = Regex.Replace(name, @"[_\-\.]", " ");
            
            // Remove extra whitespace
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            
            // Remove special characters but keep alphanumeric and spaces
            cleaned = Regex.Replace(cleaned, @"[^a-zA-Z0-9\s]", "");
            
            return cleaned;
        }

        /// <summary>
        /// Map annotation family category to the new category codes
        /// </summary>
        private static string MapAnnotationCategoryToCode(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return "SYM";

            var categoryUpper = category.ToUpper();
            
            // TitleBlocks -> TB
            if (categoryUpper.Contains("TITLEBLOCK") || categoryUpper.Contains("OST_TITLEBLOCKS"))
                return "TB";
            
            // Any Tags -> TAG
            if (categoryUpper.Contains("TAG") || categoryUpper.Contains("TAGS"))
                return "TAG";
            
            // All other categories -> SYM
            return "SYM";
        }

        /// <summary>
        /// Extract description parts from annotation family name
        /// </summary>
        private static (string description1, string description2) ExtractAnnotationDescriptions(string currentName, string categoryCode = "")
        {
            if (string.IsNullOrWhiteSpace(currentName))
                return ("UNNAMED", "");

            // Clean the name first
            var cleanName = CleanName(currentName);
            
            // Extract potential size information for DESCRIPTION2
            var description2 = ExtractSizeInformation(currentName);
            
            // Remove size information from the name if we extracted it to DESCRIPTION2
            var nameWithoutSize = cleanName;
            if (!string.IsNullOrWhiteSpace(description2))
            {
                // Remove various size patterns from the name
                nameWithoutSize = Regex.Replace(nameWithoutSize, @"\d+\s*[xX]\s*\d+", "", RegexOptions.IgnoreCase);
                nameWithoutSize = Regex.Replace(nameWithoutSize, @"\d+(?:\.\d+)?\s*['""]*\s*[xX]\s*\d+(?:\.\d+)?\s*['""]*", "", RegexOptions.IgnoreCase);
                nameWithoutSize = nameWithoutSize.Trim();
            }
            
            // Convert to uppercase and remove symbols/spaces for DESCRIPTION1
            var description1 = Regex.Replace(nameWithoutSize, @"[^a-zA-Z0-9]", "").ToUpper();
            
            // Remove "TAG" suffix if the category is TAG
            if (categoryCode.Equals("TAG", StringComparison.OrdinalIgnoreCase) && description1.EndsWith("TAG"))
            {
                description1 = description1.Substring(0, description1.Length - 3);
            }
            
            return (string.IsNullOrWhiteSpace(description1) ? "UNNAMED" : description1, description2);
        }

        /// <summary>
        /// Extract size information from name for DESCRIPTION2
        /// </summary>
        private static string ExtractSizeInformation(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            try
            {
                // Look for patterns like "34 x 44", "34x44", "34 X 44", etc.
                var sizePattern = @"(\d+)\s*[xX]\s*(\d+)";
                var match = Regex.Match(name, sizePattern);
                
                if (match.Success)
                {
                    var width = match.Groups[1].Value;
                    var height = match.Groups[2].Value;
                    return $"{width}x{height}";
                }
                
                // Look for other size patterns like dimensions followed by units
                var dimensionPattern = @"(\d+(?:\.\d+)?)\s*['""]*\s*[xX]\s*(\d+(?:\.\d+)?)\s*['""]*";
                match = Regex.Match(name, dimensionPattern);
                
                if (match.Success)
                {
                    var width = match.Groups[1].Value;
                    var height = match.Groups[2].Value;
                    return $"{width}x{height}";
                }
                
                return "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error extracting size information: {ex.Message}");
                return "";
            }
        }
    }
}