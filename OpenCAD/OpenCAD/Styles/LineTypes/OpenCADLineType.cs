using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Styles.LineTypes
{
    /// <summary>
    /// Represents a line type style in the CAD document.
    /// </summary>
    public class OpenCADLineType : OpenCADObject
    {
        private List<float> _pattern = new List<float> { 1.0f };
        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public OpenCADLineType() : base()
        {
            _isDrawable = false;
        }

        /// <summary>
        /// Creates a new line type with specified properties.
        /// </summary>
        /// <param name="name">The name of the line type. Must be unique within the document.</param>
        /// <param name="description">Description of the line type pattern.</param>
        /// <param name="pattern">The dash pattern for the line type.</param>
        /// <param name="document">The parent document.</param>
        public OpenCADLineType(string name, string description, List<double> pattern, OpenCADDocument document) 
            : base(document)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Line type name cannot be null or empty.", nameof(name));

            _isDrawable = false;
            
            // Initialize line type
            Name = name;
            Description = description;
        }

        /// <summary>
        /// Gets or sets the description of the line type.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public string Description
        {
            get => GetPropertyValue<string>(PropertyType.String, nameof(Description)) ?? string.Empty;
            set => SetPropertyValue(PropertyType.String, nameof(Description), OpenCADStrings.Description, value);
        }

        /// <summary>
        /// Gets or sets the description of the line type.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public uint LineTypeID
        {
            get => GetPropertyValue<uint>(PropertyType.UInt, nameof(LineTypeID));
            set => SetPropertyValue(PropertyType.UInt, nameof(LineTypeID), "LineTypeID", value);
        }

        /// <summary>
        /// Gets or sets the dash pattern for the line type.
        /// Positive values represent dashes, negative values represent spaces.
        /// A pattern of { 1.0 } represents a continuous line.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public List<float> Pattern
        {
            get => _pattern;
            set => _pattern = value;
        }

        /// <summary>
        /// Gets the total length of one complete pattern cycle.
        /// Automatically calculated from the pattern.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double PatternLength
        {
            get
            {
                var pattern = Pattern;
                if (pattern == null || pattern.Count == 0)
                    return 0;

                double length = 0;
                foreach (var segment in pattern)
                    length += Math.Abs(segment);

                return length;
            }
        }

        /// <summary>
        /// Gets whether this is a continuous line type.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsContinuous => Pattern.Count == 1 && Math.Abs(Pattern[0] - 1.0) < 0.0001;

        public override string ToString()
        {
            return $"Line Type: {Name} ({Description})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is OpenCADLineType other)
                return ID == other.ID;
            return false;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }
    }
}