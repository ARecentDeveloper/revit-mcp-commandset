using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitMCPCommandSet.Models.PACompliance
{
    /// <summary>
    /// PA naming convention rules and validation logic
    /// </summary>
    public static class PANamingRules
    {
        #region Constants

        private const string PA_PREFIX = "PA";
        private const string GENERIC_MANUFACTURER = "Generic";
        private const string NAME_SEPARATOR = "-";

        // Regex patterns for validation
        private static readonly Regex AnnotationFamilyPattern = new Regex(@"^PA-[A-Z0-9_]+-[A-Z0-9_\s]+$", RegexOptions.Compiled);
        private static readonly Regex ModelFamilyPattern = new Regex(@"^[A-Z0-9_]+-[A-Z0-9_]+-[A-Z0-9_\s]+$", RegexOptions.Compiled);

        #endregion

        #region Annotation Family Naming

        /// <summary>
        /// Generates PA-compliant annotation family name
        /// Format: PA-CATEGORY-DESCRIPTION
        /// </summary>
        /// <param name="category">Element category</param>
        /// <param name="description">Family description</param>
        /// <returns>PA-compliant annotation family name</returns>
        public static string GenerateAnnotationFamilyName(string category, string description)
        {
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("Category cannot be null or empty", nameof(category));
            
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description cannot be null or empty", nameof(description));

            var cleanCategory = CleanNameComponent(category);
            var cleanDescription = CleanNameComponent(description);

            return $"{PA_PREFIX}{NAME_SEPARATOR}{cleanCategory}{NAME_SEPARATOR}{cleanDescription}";
        }

        /// <summary>
        /// Validates if annotation family name follows PA conventions
        /// </summary>
        /// <param name="familyName">Family name to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidAnnotationFamilyName(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                return false;

            return AnnotationFamilyPattern.IsMatch(familyName.ToUpper());
        }

        /// <summary>
        /// Extracts components from annotation family name
        /// </summary>
        /// <param name="familyName">Family name to parse</param>
        /// <returns>Tuple of (category, description) or null if invalid</returns>
        public static (string category, string description)? ParseAnnotationFamilyName(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                return null;

            var parts = familyName.Split(new[] { NAME_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 3 || !parts[0].Equals(PA_PREFIX, StringComparison.OrdinalIgnoreCase))
                return null;

            var category = parts[1];
            var description = string.Join(NAME_SEPARATOR, parts.Skip(2));

            return (category, description);
        }

        #endregion

        #region Model Family Naming

        /// <summary>
        /// Generates PA-compliant model family name
        /// Format: CATEGORY-MANUFACTURER-DESCRIPTION
        /// </summary>
        /// <param name="category">Element category</param>
        /// <param name="manufacturer">Manufacturer name (use Generic if unknown)</param>
        /// <param name="description">Family description</param>
        /// <returns>PA-compliant model family name</returns>
        public static string GenerateModelFamilyName(string category, string manufacturer, string description)
        {
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("Category cannot be null or empty", nameof(category));
            
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description cannot be null or empty", nameof(description));

            var cleanCategory = CleanNameComponent(category);
            var cleanManufacturer = CleanNameComponent(string.IsNullOrWhiteSpace(manufacturer) ? GENERIC_MANUFACTURER : manufacturer);
            var cleanDescription = CleanNameComponent(description);

            return $"{cleanCategory}{NAME_SEPARATOR}{cleanManufacturer}{NAME_SEPARATOR}{cleanDescription}";
        }

        /// <summary>
        /// Validates if model family name follows PA conventions
        /// </summary>
        /// <param name="familyName">Family name to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidModelFamilyName(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                return false;

            return ModelFamilyPattern.IsMatch(familyName.ToUpper());
        }

        /// <summary>
        /// Extracts components from model family name
        /// </summary>
        /// <param name="familyName">Family name to parse</param>
        /// <returns>Tuple of (category, manufacturer, description) or null if invalid</returns>
        public static (string category, string manufacturer, string description)? ParseModelFamilyName(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                return null;

            var parts = familyName.Split(new[] { NAME_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 3)
                return null;

            var category = parts[0];
            var manufacturer = parts[1];
            var description = string.Join(NAME_SEPARATOR, parts.Skip(2));

            return (category, manufacturer, description);
        }

        #endregion

        #region Manufacturer Detection

        /// <summary>
        /// Attempts to detect manufacturer from family name or description
        /// </summary>
        /// <param name="familyName">Current family name</param>
        /// <param name="description">Family description</param>
        /// <returns>Detected manufacturer or "Generic" if not found</returns>
        public static string DetectManufacturer(string familyName, string description = null)
        {
            var searchText = $"{familyName} {description}".ToLower();
            
            // Common manufacturer patterns
            var manufacturers = new Dictionary<string, string[]>
            {
                { "Kohler", new[] { "kohler", "k-" } },
                { "American Standard", new[] { "american standard", "americanstandard", "am std" } },
                { "Toto", new[] { "toto" } },
                { "Moen", new[] { "moen" } },
                { "Delta", new[] { "delta" } },
                { "Grohe", new[] { "grohe" } },
                { "Hansgrohe", new[] { "hansgrohe" } },
                { "Sloan", new[] { "sloan" } },
                { "Bradley", new[] { "bradley" } },
                { "Bobrick", new[] { "bobrick" } },
                { "ASI", new[] { "asi", "american specialties" } },
                { "Zurn", new[] { "zurn" } },
                { "Chicago Faucets", new[] { "chicago faucets", "chicago" } },
                { "Elkay", new[] { "elkay" } },
                { "Franke", new[] { "franke" } }
            };

            foreach (var manufacturer in manufacturers)
            {
                if (manufacturer.Value.Any(pattern => searchText.Contains(pattern)))
                {
                    return manufacturer.Key;
                }
            }

            return GENERIC_MANUFACTURER;
        }

        #endregion

        #region Name Cleaning and Validation

        /// <summary>
        /// Cleans and formats a name component according to PA standards
        /// </summary>
        /// <param name="component">Name component to clean</param>
        /// <returns>Cleaned name component</returns>
        public static string CleanNameComponent(string component)
        {
            if (string.IsNullOrWhiteSpace(component))
                return string.Empty;

            // Remove special characters except spaces, hyphens, and underscores
            var cleaned = Regex.Replace(component, @"[^\w\s\-]", "");
            
            // Replace multiple spaces with single space
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            
            // Convert to uppercase and trim
            cleaned = cleaned.ToUpper().Trim();
            
            // Replace spaces with underscores for consistency
            cleaned = cleaned.Replace(" ", "_");
            
            return cleaned;
        }

        /// <summary>
        /// Validates if a name component is acceptable for PA naming
        /// </summary>
        /// <param name="component">Name component to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidNameComponent(string component)
        {
            if (string.IsNullOrWhiteSpace(component))
                return false;

            // Check for valid characters only
            return Regex.IsMatch(component, @"^[A-Z0-9_\s]+$");
        }

        /// <summary>
        /// Generates suggested name based on current name and category
        /// </summary>
        /// <param name="currentName">Current element name</param>
        /// <param name="category">Element category</param>
        /// <param name="isAnnotationFamily">True for annotation families, false for model families</param>
        /// <returns>Suggested PA-compliant name</returns>
        public static string GenerateSuggestedName(string currentName, string category, bool isAnnotationFamily)
        {
            if (string.IsNullOrWhiteSpace(currentName) || string.IsNullOrWhiteSpace(category))
                return string.Empty;

            try
            {
                if (isAnnotationFamily)
                {
                    // Try to extract meaningful description from current name
                    var description = ExtractDescriptionFromName(currentName);
                    return GenerateAnnotationFamilyName(category, description);
                }
                else
                {
                    // Detect manufacturer and extract description
                    var manufacturer = DetectManufacturer(currentName);
                    var description = ExtractDescriptionFromName(currentName);
                    return GenerateModelFamilyName(category, manufacturer, description);
                }
            }
            catch
            {
                // If generation fails, return empty string for manual input
                return string.Empty;
            }
        }

        /// <summary>
        /// Extracts meaningful description from current name
        /// </summary>
        /// <param name="currentName">Current name to extract from</param>
        /// <returns>Extracted description</returns>
        private static string ExtractDescriptionFromName(string currentName)
        {
            if (string.IsNullOrWhiteSpace(currentName))
                return "DESCRIPTION";

            // Remove common prefixes and suffixes
            var description = currentName;
            
            // Remove file extensions
            description = Regex.Replace(description, @"\.(rfa|rvt)$", "", RegexOptions.IgnoreCase);
            
            // Remove common prefixes
            var prefixesToRemove = new[] { "PA-", "Generic", "Standard", "Default", "Basic" };
            foreach (var prefix in prefixesToRemove)
            {
                if (description.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    description = description.Substring(prefix.Length).TrimStart('-', '_', ' ');
                }
            }
            
            // If description is too short or generic, use a default
            if (string.IsNullOrWhiteSpace(description) || description.Length < 3)
            {
                description = "DESCRIPTION";
            }

            return CleanNameComponent(description);
        }

        #endregion
    }
}