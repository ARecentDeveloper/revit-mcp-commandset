using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// Filter settings - supports combined condition filtering
    /// </summary>
    public class FilterSetting
    {
        /// <summary>
        /// Gets or sets the Revit built-in category name to filter (e.g., "OST_Walls").
        /// If null or empty, no category filtering is performed.
        /// </summary>
        [JsonProperty("filterCategory")]
        public string FilterCategory { get; set; } = null;
        /// <summary>
        /// Gets or sets the Revit element type name to filter (e.g., "Wall" or "Autodesk.Revit.DB.Wall").
        /// If null or empty, no type filtering is performed.
        /// </summary>
        [JsonProperty("filterElementType")]
        public string FilterElementType { get; set; } = null;
        /// <summary>
        /// Gets or sets the ElementId value of the family type to filter (FamilySymbol).
        /// If 0 or negative, no family filtering is performed.
        /// Note: This filter only applies to element instances, not type elements.
        /// </summary>
        [JsonProperty("filterFamilySymbolId")]
        public int FilterFamilySymbolId { get; set; } = -1;
        /// <summary>
        /// Gets or sets whether to include element types (such as wall types, door types, etc.)
        /// </summary>
        [JsonProperty("includeTypes")]
        public bool IncludeTypes { get; set; } = false;
        /// <summary>
        /// Gets or sets whether to include element instances (such as placed walls, doors, etc.)
        /// </summary>
        [JsonProperty("includeInstances")]
        public bool IncludeInstances { get; set; } = true;
        /// <summary>
        /// Gets or sets whether to return only elements visible in the current view.
        /// Note: This filter only applies to element instances, not type elements.
        /// </summary>
        [JsonProperty("filterVisibleInCurrentView")]
        public bool FilterVisibleInCurrentView { get; set; }
        /// <summary>
        /// Gets or sets the minimum point coordinates for spatial range filtering (unit: mm)
        /// If this value and BoundingBoxMax are set, elements intersecting with this bounding box will be filtered
        /// </summary>
        [JsonProperty("boundingBoxMin")]
        public JZPoint BoundingBoxMin { get; set; } = null;
        /// <summary>
        /// Gets or sets the maximum point coordinates for spatial range filtering (unit: mm)
        /// If this value and BoundingBoxMin are set, elements intersecting with this bounding box will be filtered
        /// </summary>
        [JsonProperty("boundingBoxMax")]
        public JZPoint BoundingBoxMax { get; set; } = null;
        /// <summary>
        /// Maximum element count limit
        /// </summary>
        [JsonProperty("maxElements")]
        public int MaxElements { get; set; } = 50; 
        /// <summary>
        /// Validates the validity of filter settings and checks for potential conflicts
        /// </summary>
        /// <returns>Returns true if settings are valid, otherwise returns false</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = null;

            // Check if at least one element type is selected
            if (!IncludeTypes && !IncludeInstances)
            {
                errorMessage = "Filter settings invalid: Must include at least one of element types or element instances";
                return false;
            }

            // Check if at least one filter condition is specified
            if (string.IsNullOrWhiteSpace(FilterCategory) &&
                string.IsNullOrWhiteSpace(FilterElementType) &&
                FilterFamilySymbolId <= 0)
            {
                errorMessage = "Filter settings invalid: Must specify at least one filter condition (category, element type, or family type)";
                return false;
            }

            // Check for conflicts between type elements and certain filters
            if (IncludeTypes && !IncludeInstances)
            {
                List<string> invalidFilters = new List<string>();
                if (FilterFamilySymbolId > 0)
                    invalidFilters.Add("family instance filtering");
                if (FilterVisibleInCurrentView)
                    invalidFilters.Add("view visibility filtering");
                if (invalidFilters.Count > 0)
                {
                    errorMessage = $"When filtering only type elements, the following filters are not applicable: {string.Join(", ", invalidFilters)}";
                    return false;
                }
            }
            // Check validity of spatial range filter
            if (BoundingBoxMin != null && BoundingBoxMax != null)
            {
                // Ensure minimum point is less than or equal to maximum point
                if (BoundingBoxMin.X > BoundingBoxMax.X ||
                    BoundingBoxMin.Y > BoundingBoxMax.Y ||
                    BoundingBoxMin.Z > BoundingBoxMax.Z)
                {
                    errorMessage = "Spatial range filter settings invalid: Minimum point coordinates must be less than or equal to maximum point coordinates";
                    return false;
                }
            }
            else if (BoundingBoxMin != null || BoundingBoxMax != null)
            {
                errorMessage = "Spatial range filter settings invalid: Both minimum and maximum point coordinates must be set";
                return false;
            }
            return true;
        }
    }
}
