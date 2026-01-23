using OpenCAD.SegmentSource;
using OpenCAD.Settings;
using OpenCAD.Styles.LineTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace OpenCAD.Containers
{
    /// <summary>
    /// Specialized container for managing line types in an OpenCAD document.
    /// Provides line type-specific operations like add, remove, and lookup by name.
    /// </summary>
    public class OpenCADLineTypes : OpenCADObject
    {
        // Cache for quick line type lookup by name
        private readonly ConcurrentDictionary<string, Guid> _lineTypeNameToId = new();
        private uint _nextId = 1; // Start from 1 since 0 is reserved for "Continuous"

        public OpenCADLineTypes(OpenCADDocument? document = null) : base(document)
        {
            Name = "LineTypes Container";
            LoadLineTypes();
        }

        private void LoadLineTypes()
        {
            ClearLineTypes();
            
            var continuous = new OpenCADLineType
            {
                Name = "Continuous",
                Description = "Default continuous line type",
                ID = Guid.Empty // Reserved ID for Continuous
            };

            _lineTypeNameToId.TryAdd(continuous.Name, continuous.ID);
            Add(continuous);
            try
            {
                string[] lines = File.ReadAllLines("Support\\OpenCAD.lin");
                var lineTypes = LinParser.Parse(lines);
                foreach (var lt in lineTypes)
                {
                    AddLineType(lt);
                }
            }
            catch (Exception)
            {
            }
        }

        private void ClearLineTypes()
        {
            foreach (var lineType in GetLineTypes())
            {
                Remove(lineType.ID);
            }
            _lineTypeNameToId.Clear();
        }


        /// <summary>
        /// Gets all layers in the container.
        /// </summary>
        public IEnumerable<OpenCADLineType> GetLineTypes()
        {
            return GetChildren().OfType<OpenCADLineType>();
        }

        /// <summary>
        /// Adds a new line type to the container.
        /// </summary>
        /// <param name="lineType">The line type to add.</param>
        /// <returns>True if the line type was added successfully, false if a line type with the same name already exists.</returns>
        public bool AddLineType(OpenCADLineType lineType)
        {
            if (lineType == null)
                throw new ArgumentNullException(nameof(lineType));

            // Check if line type name already exists
            if (_lineTypeNameToId.ContainsKey(lineType.Name))
                return false;

            if (Add(lineType))
            {
                lineType.LineTypeID = _nextId++; // Assign a new unique ID
                _lineTypeNameToId.TryAdd(lineType.Name, lineType.ID);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a line type by name.
        /// </summary>
        /// <param name="name">The name of the line type to retrieve.</param>
        /// <returns>The line type with the specified name, or null if not found.</returns>
        public OpenCADLineType? GetLineType(string name)
        {
            if (_lineTypeNameToId.TryGetValue(name, out var lineTypeId))
            {
                var lineType = GetChild(lineTypeId);
                return lineType as OpenCADLineType;
            }
            return null;
        }

        public OpenCADLineType? GetLineTypeByID(uint lineTypeID)
        {
            return GetLineTypes().FirstOrDefault(lt => lt.LineTypeID == lineTypeID);
        }

        /// <summary>
        /// Gets a line type by ID.
        /// </summary>
        /// <param name="lineTypeId">The ID of the line type to retrieve.</param>
        /// <returns>The line type with the specified ID, or null if not found.</returns>
        public OpenCADLineType? GetLineType(Guid lineTypeId)
        {
            var lineType = GetChild(lineTypeId);
            return lineType as OpenCADLineType;
        }

        /// <summary>
        /// Removes a line type from the container.
        /// "Continuous" line type cannot be removed.
        /// </summary>
        /// <param name="name">The name of the line type to remove.</param>
        /// <returns>True if the line type was removed successfully, false otherwise.</returns>
        public bool RemoveLineType(string name)
        {
            if (name == "Continuous")
                return false; // Cannot remove Continuous line type

            if (_lineTypeNameToId.TryRemove(name, out var lineTypeId))
            {
                return Remove(lineTypeId);
            }

            return false;
        }

        /// <summary>
        /// Gets all line types in the container.
        /// </summary>
        /// <returns>Collection of all line types.</returns>
        public IEnumerable<OpenCADLineType> GetAllLineTypes()
        {
            foreach (var child in GetChildren())
            {
                if (child is OpenCADLineType lineType)
                    yield return lineType;
            }
        }

        public void OnDeserialized()
        {
            // Rebuild the name-to-ID cache after deserialization
            _lineTypeNameToId.Clear();
            foreach (var lineType in GetAllLineTypes())
            {
                _lineTypeNameToId.TryAdd(lineType.Name, lineType.ID);
            }
        }

        public LinetypeGpuData GetScaledLineType (uint lineTypeID, float scale)
        {
            var lineType = GetLineTypeByID(lineTypeID);
            var linetypeGpuData = new LinetypeGpuData(); // Constructor initializes Pattern array
            
            if (lineType == null)
            {
                linetypeGpuData.PatternLength = 1.0f;
                linetypeGpuData.PatternCount = 1;
                linetypeGpuData.Pattern[0] = 1f;
                return linetypeGpuData;
            }

            var scaledPattern = new List<float>();
            float totalLength = 0;
            foreach (var segment in lineType.Pattern)
            {
                float scaledSegment = segment * scale;
                scaledPattern.Add(scaledSegment);
                totalLength += Math.Abs(scaledSegment);
            }
            
            if (totalLength <= 0 || scaledPattern.Count == 0)
            {
                linetypeGpuData.PatternLength = 1.0f;
                linetypeGpuData.PatternCount = 1;
                linetypeGpuData.Pattern[0] = 1f;
                return linetypeGpuData;
            }
            
            linetypeGpuData.PatternLength = totalLength;
            linetypeGpuData.PatternCount = scaledPattern.Count;
            
            for (int i = 0; i < scaledPattern.Count && i < 64; i++)
            {
                linetypeGpuData.Pattern[i] = scaledPattern[i];
            }
            
            return linetypeGpuData;
        }
    }
    public static class LinParser
    {
        public static List<OpenCADLineType> Parse(string[] lines)
        {
            var result = new List<OpenCADLineType>();
            OpenCADLineType? current = null;

            foreach (var raw in lines)
            {
                string line = raw.Trim();

                // Skip empty or comment lines
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";"))
                    continue;

                // Start of a new linetype
                if (line.StartsWith("*"))
                {
                    // Commit previous one
                    if (current != null)
                        result.Add(current);

                    current = ParseHeader(line);
                    continue;
                }

                // Pattern line (must start with A,)
                if (current != null && line.StartsWith("A", StringComparison.OrdinalIgnoreCase))
                {
                    current.Pattern = ParsePattern(line);
                    continue;
                }

                // Ignore anything else for now
            }

            // Add last one
            if (current != null)
                result.Add(current);

            return result;
        }

        private static OpenCADLineType ParseHeader(string line)
        {
            // Format: *NAME, Description
            // Remove leading *
            string content = line.Substring(1);

            int comma = content.IndexOf(',');
            if (comma < 0)
            {
                return new OpenCADLineType
                {
                    Name = content.Trim(),
                    Description = ""
                };
            }

            string name = content.Substring(0, comma).Trim();
            string desc = content.Substring(comma + 1).Trim();

            return new OpenCADLineType
            {
                Name = name,
                Description = desc
            };
        }

        private static List<float> ParsePattern(string line)
        {
            // Remove leading A,
            int comma = line.IndexOf(',');
            if (comma < 0)
                return new List<float>();

            string patternPart = line.Substring(comma + 1);

            var parts = patternPart.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var values = new List<float>();

            foreach (var p in parts)
            {
                if (float.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    values.Add(v);
            }

            return values;
        }
    }
}