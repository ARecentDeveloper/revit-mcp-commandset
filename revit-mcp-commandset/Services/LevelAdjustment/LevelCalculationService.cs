using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Services.LevelAdjustment
{
    /// <summary>
    /// Service for level-related calculations and operations
    /// </summary>
    public class LevelCalculationService
    {
        private readonly Document _document;
        private readonly List<Level> _sortedLevels;
        private readonly Dictionary<string, Level> _levelsByName;

        public LevelCalculationService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            
            // Collect all levels in the project
            var levels = new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            if (!levels.Any())
            {
                throw new InvalidOperationException("No levels found in the project");
            }

            // Create sorted list by elevation
            _sortedLevels = levels.OrderBy(l => l.Elevation).ToList();
            
            // Create lookup dictionary by name
            _levelsByName = levels.ToDictionary(l => l.Name, l => l);
        }

        /// <summary>
        /// Find the closest level to a given elevation
        /// </summary>
        /// <param name="absoluteElevation">The absolute elevation to find the closest level for</param>
        /// <returns>The closest level</returns>
        public Level FindClosestLevel(double absoluteElevation)
        {
            if (!_sortedLevels.Any())
                throw new InvalidOperationException("No levels available");

            Level closestLevel = _sortedLevels[0];
            double minDistance = Math.Abs(closestLevel.Elevation - absoluteElevation);

            foreach (var level in _sortedLevels)
            {
                double distance = Math.Abs(level.Elevation - absoluteElevation);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestLevel = level;
                }
            }

            return closestLevel;
        }

        /// <summary>
        /// Find a level by its name
        /// </summary>
        /// <param name="levelName">Name of the level to find</param>
        /// <returns>The level with the specified name, or null if not found</returns>
        public Level FindLevelByName(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return null;

            return _levelsByName.TryGetValue(levelName, out Level level) ? level : null;
        }

        /// <summary>
        /// Calculate the absolute elevation from a level and offset
        /// </summary>
        /// <param name="level">The reference level</param>
        /// <param name="offset">The offset from the level</param>
        /// <returns>The absolute elevation</returns>
        public double CalculateAbsoluteElevation(Level level, double offset)
        {
            if (level == null)
                throw new ArgumentNullException(nameof(level));

            return level.Elevation + offset;
        }

        /// <summary>
        /// Calculate the required offset to maintain absolute elevation when changing to a new level
        /// </summary>
        /// <param name="currentLevel">Current level</param>
        /// <param name="currentOffset">Current offset</param>
        /// <param name="targetLevel">Target level</param>
        /// <returns>The new offset required</returns>
        public double CalculateNewOffset(Level currentLevel, double currentOffset, Level targetLevel)
        {
            if (currentLevel == null)
                throw new ArgumentNullException(nameof(currentLevel));
            if (targetLevel == null)
                throw new ArgumentNullException(nameof(targetLevel));

            double absoluteElevation = CalculateAbsoluteElevation(currentLevel, currentOffset);
            return absoluteElevation - targetLevel.Elevation;
        }

        /// <summary>
        /// Get all levels sorted by elevation
        /// </summary>
        /// <returns>List of levels sorted by elevation</returns>
        public IReadOnlyList<Level> GetSortedLevels()
        {
            return _sortedLevels.AsReadOnly();
        }

        /// <summary>
        /// Get all level names
        /// </summary>
        /// <returns>Collection of all level names</returns>
        public IEnumerable<string> GetLevelNames()
        {
            return _levelsByName.Keys;
        }

        /// <summary>
        /// Check if a level exists by name
        /// </summary>
        /// <param name="levelName">Name of the level to check</param>
        /// <returns>True if the level exists, false otherwise</returns>
        public bool LevelExists(string levelName)
        {
            return !string.IsNullOrEmpty(levelName) && _levelsByName.ContainsKey(levelName);
        }
    }
}