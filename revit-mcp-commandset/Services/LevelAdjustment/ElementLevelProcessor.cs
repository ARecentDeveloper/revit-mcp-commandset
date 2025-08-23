using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Services.LevelAdjustment
{
    /// <summary>
    /// Result of a level adjustment operation
    /// </summary>
    public class LevelAdjustmentResult
    {
        public bool Success { get; set; }
        public string AssignedLevel { get; set; }
        public string ErrorMessage { get; set; }

        public static LevelAdjustmentResult CreateSuccess(string assignedLevel)
        {
            return new LevelAdjustmentResult
            {
                Success = true,
                AssignedLevel = assignedLevel
            };
        }

        public static LevelAdjustmentResult CreateFailure(string errorMessage)
        {
            return new LevelAdjustmentResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Service for processing element-specific level adjustments
    /// </summary>
    public class ElementLevelProcessor
    {
        private readonly Document _document;
        private readonly LevelCalculationService _levelCalculationService;

        public ElementLevelProcessor(Document document, LevelCalculationService levelCalculationService)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _levelCalculationService = levelCalculationService ?? throw new ArgumentNullException(nameof(levelCalculationService));
        }

        /// <summary>
        /// Assign an element to its closest level based on current elevation
        /// </summary>
        /// <param name="element">The element to process</param>
        /// <returns>Result of the operation</returns>
        public LevelAdjustmentResult AssignToClosestLevel(Element element)
        {
            try
            {
                // Handle different element types
                if (element is Wall wall)
                {
                    return ProcessWallAutoAssignment(wall);
                }
                else if (element is FamilyInstance familyInstance && 
                         familyInstance.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns)
                {
                    return ProcessColumnAutoAssignment(familyInstance);
                }
                else
                {
                    return ProcessGeneralElementAutoAssignment(element);
                }
            }
            catch (Exception ex)
            {
                return LevelAdjustmentResult.CreateFailure($"Error processing element: {ex.Message}");
            }
        }

        /// <summary>
        /// Assign an element to a specific level while optionally maintaining elevation
        /// </summary>
        /// <param name="element">The element to process</param>
        /// <param name="targetLevel">The target level</param>
        /// <param name="maintainElevation">Whether to maintain absolute elevation</param>
        /// <returns>Result of the operation</returns>
        public LevelAdjustmentResult AssignToSpecificLevel(Element element, Level targetLevel, bool maintainElevation)
        {
            try
            {
                // Handle different element types
                if (element is Wall wall)
                {
                    return ProcessWallSpecificAssignment(wall, targetLevel, maintainElevation);
                }
                else if (element is FamilyInstance familyInstance && 
                         familyInstance.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns)
                {
                    return ProcessColumnSpecificAssignment(familyInstance, targetLevel, maintainElevation);
                }
                else
                {
                    return ProcessGeneralElementSpecificAssignment(element, targetLevel, maintainElevation);
                }
            }
            catch (Exception ex)
            {
                return LevelAdjustmentResult.CreateFailure($"Error processing element: {ex.Message}");
            }
        }

        #region Wall Processing

        private LevelAdjustmentResult ProcessWallAutoAssignment(Wall wall)
        {
            var baseResult = AdjustLevelAndOffsetToClosest(
                wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT),
                wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET));

            string assignedLevel = baseResult.Success ? baseResult.AssignedLevel : "";

            // Handle top constraint if it's set to a level
            var topConstraintParam = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
            if (topConstraintParam?.AsInteger() == 1) // Level constraint
            {
                // For walls with level-based top constraint, we would need to handle this differently
                // For now, we'll just use the base level assignment
                // Note: Wall top constraint parameters are more complex in Revit API
            }

            return baseResult.Success ? LevelAdjustmentResult.CreateSuccess(assignedLevel) : baseResult;
        }

        private LevelAdjustmentResult ProcessWallSpecificAssignment(Wall wall, Level targetLevel, bool maintainElevation)
        {
            var baseResult = AdjustLevelAndOffsetToSpecific(
                wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT),
                wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET),
                targetLevel, maintainElevation);

            return baseResult.Success ? LevelAdjustmentResult.CreateSuccess(targetLevel.Name) : baseResult;
        }

        #endregion

        #region Column Processing

        private LevelAdjustmentResult ProcessColumnAutoAssignment(FamilyInstance column)
        {
            var baseResult = AdjustLevelAndOffsetToClosest(
                column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM),
                column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM));

            var topResult = AdjustLevelAndOffsetToClosest(
                column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM),
                column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM));

            if (baseResult.Success && topResult.Success)
            {
                return LevelAdjustmentResult.CreateSuccess($"Base: {baseResult.AssignedLevel}, Top: {topResult.AssignedLevel}");
            }
            else if (baseResult.Success)
            {
                return LevelAdjustmentResult.CreateSuccess($"Base: {baseResult.AssignedLevel}");
            }
            else if (topResult.Success)
            {
                return LevelAdjustmentResult.CreateSuccess($"Top: {topResult.AssignedLevel}");
            }
            else
            {
                return LevelAdjustmentResult.CreateFailure("Could not find appropriate level parameters for column");
            }
        }

        private LevelAdjustmentResult ProcessColumnSpecificAssignment(FamilyInstance column, Level targetLevel, bool maintainElevation)
        {
            // For columns, only adjust the base level in manual mode
            var baseResult = AdjustLevelAndOffsetToSpecific(
                column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM),
                column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM),
                targetLevel, maintainElevation);

            return baseResult.Success ? LevelAdjustmentResult.CreateSuccess($"Base: {targetLevel.Name}") : baseResult;
        }

        #endregion

        #region General Element Processing

        private LevelAdjustmentResult ProcessGeneralElementAutoAssignment(Element element)
        {
            // Try different parameter combinations
            var parameterCombinations = new[]
            {
                new { Level = BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM, Offset = BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM },
                new { Level = BuiltInParameter.SCHEDULE_LEVEL_PARAM, Offset = BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM },
                new { Level = BuiltInParameter.LEVEL_PARAM, Offset = BuiltInParameter.INSTANCE_ELEVATION_PARAM },
                new { Level = BuiltInParameter.FAMILY_LEVEL_PARAM, Offset = BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM },
                new { Level = BuiltInParameter.FAMILY_BASE_LEVEL_PARAM, Offset = BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM },
                new { Level = BuiltInParameter.FAMILY_TOP_LEVEL_PARAM, Offset = BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM }
            };

            foreach (var combo in parameterCombinations)
            {
                var levelParam = element.get_Parameter(combo.Level);
                var offsetParam = element.get_Parameter(combo.Offset);

                if (levelParam != null && !levelParam.IsReadOnly && offsetParam != null && !offsetParam.IsReadOnly)
                {
                    var result = AdjustLevelAndOffsetToClosest(levelParam, offsetParam);
                    if (result.Success)
                    {
                        return result;
                    }
                }
            }

            return LevelAdjustmentResult.CreateFailure("Could not find appropriate level parameters");
        }

        private LevelAdjustmentResult ProcessGeneralElementSpecificAssignment(Element element, Level targetLevel, bool maintainElevation)
        {
            // Try different parameter combinations
            var parameterCombinations = new[]
            {
                new { Level = BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM, Offset = BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM },
                new { Level = BuiltInParameter.SCHEDULE_LEVEL_PARAM, Offset = BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM },
                new { Level = BuiltInParameter.LEVEL_PARAM, Offset = BuiltInParameter.INSTANCE_ELEVATION_PARAM },
                new { Level = BuiltInParameter.FAMILY_LEVEL_PARAM, Offset = BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM },
                new { Level = BuiltInParameter.FAMILY_BASE_LEVEL_PARAM, Offset = BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM }
            };

            foreach (var combo in parameterCombinations)
            {
                var levelParam = element.get_Parameter(combo.Level);
                var offsetParam = element.get_Parameter(combo.Offset);

                if (levelParam != null && !levelParam.IsReadOnly && offsetParam != null && !offsetParam.IsReadOnly)
                {
                    var result = AdjustLevelAndOffsetToSpecific(levelParam, offsetParam, targetLevel, maintainElevation);
                    if (result.Success)
                    {
                        return result;
                    }
                }
            }

            return LevelAdjustmentResult.CreateFailure("Could not find appropriate level parameters");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Adjust level and offset parameters to the closest level
        /// </summary>
        private LevelAdjustmentResult AdjustLevelAndOffsetToClosest(Parameter levelParam, Parameter offsetParam)
        {
            if (levelParam == null || offsetParam == null || levelParam.IsReadOnly || offsetParam.IsReadOnly)
            {
                return LevelAdjustmentResult.CreateFailure("Missing or read-only level/offset parameters");
            }

            try
            {
                // Get current level and offset
                var currentLevelId = levelParam.AsElementId();
                var currentLevel = _document.GetElement(currentLevelId) as Level;
                
                if (currentLevel == null)
                {
                    return LevelAdjustmentResult.CreateFailure("Could not retrieve current level");
                }

                var currentOffset = offsetParam.AsDouble();
                var absoluteElevation = _levelCalculationService.CalculateAbsoluteElevation(currentLevel, currentOffset);

                // Find closest level
                var targetLevel = _levelCalculationService.FindClosestLevel(absoluteElevation);
                var newOffset = _levelCalculationService.CalculateNewOffset(currentLevel, currentOffset, targetLevel);

                // Update parameters
                levelParam.Set(targetLevel.Id);
                offsetParam.Set(newOffset);

                return LevelAdjustmentResult.CreateSuccess(targetLevel.Name);
            }
            catch (Exception ex)
            {
                return LevelAdjustmentResult.CreateFailure($"Failed to adjust level/offset: {ex.Message}");
            }
        }

        /// <summary>
        /// Adjust level and offset parameters to a specific level
        /// </summary>
        private LevelAdjustmentResult AdjustLevelAndOffsetToSpecific(Parameter levelParam, Parameter offsetParam, Level targetLevel, bool maintainElevation)
        {
            if (levelParam == null || offsetParam == null || levelParam.IsReadOnly || offsetParam.IsReadOnly)
            {
                return LevelAdjustmentResult.CreateFailure("Missing or read-only level/offset parameters");
            }

            try
            {
                if (maintainElevation)
                {
                    // Get current level and offset
                    var currentLevelId = levelParam.AsElementId();
                    var currentLevel = _document.GetElement(currentLevelId) as Level;
                    var currentOffset = offsetParam.AsDouble();

                    if (currentLevel != null)
                    {
                        var newOffset = _levelCalculationService.CalculateNewOffset(currentLevel, currentOffset, targetLevel);
                        levelParam.Set(targetLevel.Id);
                        offsetParam.Set(newOffset);
                    }
                    else
                    {
                        // If we can't get current level, just set to target level with zero offset
                        levelParam.Set(targetLevel.Id);
                        offsetParam.Set(0.0);
                    }
                }
                else
                {
                    // Just change the level, keep existing offset
                    levelParam.Set(targetLevel.Id);
                }

                return LevelAdjustmentResult.CreateSuccess(targetLevel.Name);
            }
            catch (Exception ex)
            {
                return LevelAdjustmentResult.CreateFailure($"Failed to adjust level/offset: {ex.Message}");
            }
        }

        #endregion
    }
}