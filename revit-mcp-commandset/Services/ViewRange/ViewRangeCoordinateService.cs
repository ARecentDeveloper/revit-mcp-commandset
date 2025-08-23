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

            double baseElevation;
            Level levelForCoordinateBase;

            // Handle "Level Below" by finding the next lower level dynamically
            if (level.Name.Contains("Level Below"))
            {
                var levelBelow = GetLevelBelow(doc, level);
                if (levelBelow != null)
                {
                    baseElevation = levelBelow.Elevation + offset;
                    levelForCoordinateBase = levelBelow;
                }
                else
                {
                    // No level below, fallback to offset only
                    baseElevation = offset;
                    levelForCoordinateBase = null;
                }
            }
            else
            {
                baseElevation = level.Elevation + offset;
                levelForCoordinateBase = level;
            }

            // Apply coordinate base conversion
            if (levelForCoordinateBase != null)
            {
                baseElevation = ApplyCoordinateBaseConversion(doc, levelForCoordinateBase, baseElevation);
            }

            return baseElevation;
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

            var baseElevation = level.Elevation + offset;
            return ApplyCoordinateBaseConversion(doc, level, baseElevation);
        }

        public double ConvertInternalToLevelOffset(Document doc, ElementId levelId, double internalElevation)
        {
            if (levelId.Value < 0)
            {
                return internalElevation;
            }

            var level = doc.GetElement(levelId) as Level;
            if (level == null)
            {
                return internalElevation;
            }

            // Convert internal elevation back to base point coordinate system
            var basePointElevation = ReverseCoordinateBaseConversion(doc, level, internalElevation);
            
            // Calculate offset from level
            return basePointElevation - level.Elevation;
        }

        private Level GetLevelBelow(Document doc, Level currentLevel)
        {
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            var currentIndex = levels.FindIndex(l => l.Id == currentLevel.Id);
            return currentIndex > 0 ? levels[currentIndex - 1] : null;
        }

        private double ApplyCoordinateBaseConversion(Document doc, Level level, double baseElevation)
        {
            var basePoints = GetBasePoints(doc);
            var coordinateBase = GetLevelCoordinateBase(level);

            switch (coordinateBase)
            {
                case 1: // Survey Point
                    if (basePoints.SurveyPoint != null)
                    {
                        var spSharedZ = basePoints.SurveyPoint.SharedPosition.Z;
                        var spInternalZ = basePoints.SurveyPoint.Position.Z;
                        var zOffset = spSharedZ - spInternalZ;
                        return baseElevation - zOffset;
                    }
                    break;

                case 0: // Project Base Point
                    if (basePoints.ProjectBasePoint != null)
                    {
                        var pbpSharedZ = basePoints.ProjectBasePoint.SharedPosition.Z;
                        var pbpInternalZ = basePoints.ProjectBasePoint.Position.Z;
                        var zOffset = pbpSharedZ - pbpInternalZ;
                        return baseElevation - zOffset;
                    }
                    break;
            }

            return baseElevation;
        }

        private double ReverseCoordinateBaseConversion(Document doc, Level level, double internalElevation)
        {
            var basePoints = GetBasePoints(doc);
            var coordinateBase = GetLevelCoordinateBase(level);

            switch (coordinateBase)
            {
                case 1: // Survey Point
                    if (basePoints.SurveyPoint != null)
                    {
                        var spSharedZ = basePoints.SurveyPoint.SharedPosition.Z;
                        var spInternalZ = basePoints.SurveyPoint.Position.Z;
                        var zOffset = spSharedZ - spInternalZ;
                        return internalElevation + zOffset;
                    }
                    break;

                case 0: // Project Base Point
                    if (basePoints.ProjectBasePoint != null)
                    {
                        var pbpSharedZ = basePoints.ProjectBasePoint.SharedPosition.Z;
                        var pbpInternalZ = basePoints.ProjectBasePoint.Position.Z;
                        var zOffset = pbpSharedZ - pbpInternalZ;
                        return internalElevation + zOffset;
                    }
                    break;
            }

            return internalElevation;
        }

        private BasePointInfo GetBasePoints(Document doc)
        {
            var basePointCollector = new FilteredElementCollector(doc).OfClass(typeof(BasePoint));
            var info = new BasePointInfo();

            foreach (BasePoint bp in basePointCollector)
            {
                if (!bp.IsShared) // Project Base Point
                {
                    info.ProjectBasePoint = bp;
                }
                else // Survey Point
                {
                    info.SurveyPoint = bp;
                }
            }

            return info;
        }

        private int GetLevelCoordinateBase(Level level)
        {
            var levelTypeId = level.GetTypeId();
            var levelType = level.Document.GetElement(levelTypeId);

            if (levelType != null)
            {
                var coordinateBaseParam = levelType.get_Parameter(BuiltInParameter.LEVEL_RELATIVE_BASE_TYPE);
                if (coordinateBaseParam != null)
                {
                    return coordinateBaseParam.AsInteger();
                }
            }

            return 0; // Default to Project Base Point
        }

        private class BasePointInfo
        {
            public BasePoint ProjectBasePoint { get; set; }
            public BasePoint SurveyPoint { get; set; }
        }
    }
}