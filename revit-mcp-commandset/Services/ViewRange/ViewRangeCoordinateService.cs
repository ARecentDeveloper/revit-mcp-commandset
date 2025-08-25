using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Services
{
    public class ViewRangeInfo
    {
        public double? TopHeight { get; set; }
        public double? CutHeight { get; set; }
        public double? BottomHeight { get; set; }
        public double? ViewDepthHeight { get; set; }
        
        public ElementId TopLevelId { get; set; }
        public ElementId CutLevelId { get; set; }
        public ElementId BottomLevelId { get; set; }
        public ElementId ViewDepthLevelId { get; set; }
        
        public double TopOffset { get; set; }
        public double CutOffset { get; set; }
        public double BottomOffset { get; set; }
        public double ViewDepthOffset { get; set; }
    }

    public class ViewRangeCoordinateService
    {
        public ViewRangeInfo GetViewRangeInfo(ViewPlan planView)
        {
            var viewRange = planView.GetViewRange();
            var doc = planView.Document;

            var info = new ViewRangeInfo
            {
                TopLevelId = viewRange.GetLevelId(PlanViewPlane.TopClipPlane),
                CutLevelId = viewRange.GetLevelId(PlanViewPlane.CutPlane),
                BottomLevelId = viewRange.GetLevelId(PlanViewPlane.BottomClipPlane),
                ViewDepthLevelId = viewRange.GetLevelId(PlanViewPlane.ViewDepthPlane),
                
                TopOffset = viewRange.GetOffset(PlanViewPlane.TopClipPlane),
                CutOffset = viewRange.GetOffset(PlanViewPlane.CutPlane),
                BottomOffset = viewRange.GetOffset(PlanViewPlane.BottomClipPlane),
                ViewDepthOffset = viewRange.GetOffset(PlanViewPlane.ViewDepthPlane)
            };

            // Calculate absolute heights
            info.TopHeight = GetAbsoluteHeight(doc, info.TopLevelId, info.TopOffset);
            info.CutHeight = GetAbsoluteHeight(doc, info.CutLevelId, info.CutOffset);
            info.BottomHeight = GetAbsoluteHeight(doc, info.BottomLevelId, info.BottomOffset);
            info.ViewDepthHeight = GetAbsoluteHeight(doc, info.ViewDepthLevelId, info.ViewDepthOffset);

            return info;
        }

        public double? GetAbsoluteHeight(Document doc, ElementId levelId, double offset)
        {
            // Check for unlimited range
            if (levelId.Value < 0)
            {
                return null; // Unlimited range
            }

            var level = doc.GetElement(levelId) as Level;
            if (level == null)
            {
                throw new InvalidOperationException($"Level with ID {levelId} not found");
            }

            // Handle "Level Below" by finding the next lower level dynamically
            if (level.Name.Contains("Level Below"))
            {
                var levelBelow = GetLevelBelow(doc, level);
                if (levelBelow != null)
                {
                    var levelBelowAbsoluteElevation = GetLevelAbsoluteElevation(doc, levelBelow);
                    return levelBelowAbsoluteElevation + offset;
                }
                else
                {
                    // No level below, fallback to offset only
                    return offset;
                }
            }
            else
            {
                var levelAbsoluteElevation = GetLevelAbsoluteElevation(doc, level);
                return levelAbsoluteElevation + offset;
            }
        }

        public double ConvertLevelOffsetToInternal(Document doc, ElementId levelId, double offset)
        {
            if (levelId.Value < 0)
            {
                return offset;
            }

            var level = doc.GetElement(levelId) as Level;
            if (level == null)
            {
                return offset;
            }

            var levelAbsoluteElevation = GetLevelAbsoluteElevation(doc, level);
            return levelAbsoluteElevation + offset;
        }

        public double ConvertInternalToLevelOffset(Document doc, ElementId levelId, double absoluteElevation)
        {
            if (levelId.Value < 0)
            {
                return absoluteElevation;
            }

            var level = doc.GetElement(levelId) as Level;
            if (level == null)
            {
                return absoluteElevation;
            }

            var levelAbsoluteElevation = GetLevelAbsoluteElevation(doc, level);
            return absoluteElevation - levelAbsoluteElevation;
        }

        private Level GetLevelBelow(Document doc, Level currentLevel)
        {
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => GetLevelAbsoluteElevation(doc, l))
                .ToList();

            var currentIndex = levels.FindIndex(l => l.Id == currentLevel.Id);
            return currentIndex > 0 ? levels[currentIndex - 1] : null;
        }

        /// <summary>
        /// Gets the absolute elevation of a level using ProjectElevation + Project Base Point Z position
        /// This provides consistent elevation values regardless of level elevation base settings
        /// </summary>
        private double GetLevelAbsoluteElevation(Document doc, Level level)
        {
            // Use ProjectElevation instead of Elevation for consistency
            var projectElevation = level.ProjectElevation;
            
            // Get Project Base Point Z position
            var projectBasePoint = GetProjectBasePoint(doc);
            var basePointZ = projectBasePoint?.Position.Z ?? 0.0;
            
            // Calculate absolute elevation
            return projectElevation + basePointZ;
        }

        /// <summary>
        /// Gets the Project Base Point element from the document
        /// </summary>
        private BasePoint GetProjectBasePoint(Document doc)
        {
            var basePointCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_ProjectBasePoint)
                .OfClass(typeof(BasePoint));

            return basePointCollector.FirstOrDefault() as BasePoint;
        }

        /// <summary>
        /// Convert a level + offset to internal elevation using simplified approach
        /// This matches the Python convert_level_offset_to_internal function
        /// </summary>
        public double ConvertAbsoluteToLevelOffset(double absoluteElevation, double levelAbsoluteElevation)
        {
            return absoluteElevation - levelAbsoluteElevation;
        }


    }
}