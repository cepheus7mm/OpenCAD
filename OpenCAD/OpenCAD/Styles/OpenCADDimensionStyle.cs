using OpenCAD.Dimensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenCAD.Styles
{
    /// <summary>
    /// Represents a dimension style in the CAD document.
    /// </summary>
    public class OpenCADDimensionStyle : OpenCADObject
    {
        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public OpenCADDimensionStyle() : base()
        {
            _isDrawable = false;
        }

        /// <summary>
        /// Creates a new dimension style with specified properties.
        /// </summary>
        /// <param name="name">The name of the dimension style. Must be unique within the document.</param>
        /// <param name="arrow">The arrow type used for dimension lines.</param>
        /// <param name="arrowSize">The size of the arrows.</param>
        /// <param name="textStyleId">The ID of the text style used for dimension text.</param>
        /// <param name="textHeight">The height of the dimension text.</param>
        /// <param name="offset">The offset distance from the measured points.</param>
        /// <param name="extensionLength">The length of extension lines.</param>
        /// <param name="extensionOffset">The offset of extension lines from the measured points.</param>
        /// <param name="extensionBeyond">The distance extension lines extend beyond the dimension line.</param>
        /// <param name="scale">The overall scale factor applied to all visual sizes. Defaults to 1.</param>
        /// <param name="document">The document this style belongs to.</param>
        public OpenCADDimensionStyle(
            string name,
            ArrowType arrow,
            float arrowSize,
            Guid textStyleId,
            float textHeight,
            float offset,
            float extensionLength,
            float extensionOffset,
            float extensionBeyond,
            float scale,
            OpenCADDocument document) : base(document)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Dimension style name cannot be null or empty.", nameof(name));

            _isDrawable = false;

            Name = name;
            ArrowType = arrow;
            ArrowSize = arrowSize;
            TextStyleID = textStyleId;
            TextHeight = textHeight;
            Offset = offset;
            ExtensionLength = extensionLength;
            ExtensionOffset = extensionOffset;
            ExtensionBeyond = extensionBeyond;
            Scale = scale;
        }

        /// <summary>
        /// Gets or sets the arrow type used for dimension lines.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public ArrowType ArrowType
        {
            get => (ArrowType)GetPropertyValue<int>(PropertyType.Integer, nameof(ArrowType));
            set => SetPropertyValue(PropertyType.Integer, nameof(ArrowType), OpenCADStrings.DimArrow, (int)value);
        }

        /// <summary>
        /// Gets or sets the size of the arrows.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public float ArrowSize
        {
            get => (float)GetPropertyValue<double>(PropertyType.DoubleUnitLess, nameof(ArrowSize));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(ArrowSize), OpenCADStrings.DimArrowSize, (double)value);
        }

        /// <summary>
        /// Gets or sets the text style ID for dimension text.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Guid TextStyleID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(TextStyleID));
            set => SetPropertyValue(PropertyType.ID, nameof(TextStyleID), OpenCADStrings.DimTextStyleID, value);
        }

        /// <summary>
        /// Gets or sets the text style for dimension text.
        /// This is a convenience property that resolves the text style from the document.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public OpenCADTextStyle? TextStyle
        {
            get
            {
                if (_document == null)
                    return null;

                return _document.GetTextStyles().FirstOrDefault(x => x.ID == TextStyleID);
            }
            set => TextStyleID = value?.ID ?? Guid.Empty;
        }

        /// <summary>
        /// Gets or sets the height of the dimension text.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public float TextHeight
        {
            get => (float)GetPropertyValue<double>(PropertyType.DoubleUnitLess, nameof(TextHeight));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(TextHeight), OpenCADStrings.DimTextHeight, (double)value);
        }

        /// <summary>
        /// Gets or sets the offset distance from the measured points.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public float Offset
        {
            get => (float)GetPropertyValue<double>(PropertyType.DoubleUnitLess, nameof(Offset));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(Offset), OpenCADStrings.DimOffset, (double)value);
        }

        /// <summary>
        /// Gets or sets the length of extension lines.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public float ExtensionLength
        {
            get => (float)GetPropertyValue<double>(PropertyType.DoubleUnitLess, nameof(ExtensionLength));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(ExtensionLength), OpenCADStrings.DimExtensionLength, (double)value);
        }

        /// <summary>
        /// Gets or sets the offset of extension lines from the measured points.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public float ExtensionOffset
        {
            get => (float)GetPropertyValue<double>(PropertyType.DoubleUnitLess, nameof(ExtensionOffset));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(ExtensionOffset), OpenCADStrings.DimExtensionOffset, (double)value);
        }

        /// <summary>
        /// Gets or sets the distance extension lines extend beyond the dimension line.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public float ExtensionBeyond
        {
            get => (float)GetPropertyValue<double>(PropertyType.DoubleUnitLess, nameof(ExtensionBeyond));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(ExtensionBeyond), OpenCADStrings.DimExtensionBeyond, (double)value);
        }

        /// <summary>
        /// Gets or sets the overall scale factor applied to all visual dimension sizes.
        /// A value of 1.0 means no scaling. This does not affect the measured value.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public float Scale
        {
            get => (float)GetPropertyValue<double>(PropertyType.DoubleUnitLess, nameof(Scale));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(Scale), OpenCADStrings.DimScale, (double)value);
        }

        // -------------------------------------------------
        // Scaled accessors – rendering code should use these
        // -------------------------------------------------

        /// <summary>Arrow size multiplied by <see cref="Scale"/>.</summary>
        public float ScaledArrowSize => ArrowSize * Scale;

        /// <summary>Text height multiplied by <see cref="Scale"/>.</summary>
        public float ScaledTextHeight => TextHeight * Scale;

        /// <summary>Extension line offset multiplied by <see cref="Scale"/>.</summary>
        public float ScaledExtensionOffset => ExtensionOffset * Scale;

        /// <summary>Extension beyond distance multiplied by <see cref="Scale"/>.</summary>
        public float ScaledExtensionBeyond => ExtensionBeyond * Scale;

        /// <summary>Extension line length multiplied by <see cref="Scale"/>.</summary>
        public float ScaledExtensionLength => ExtensionLength * Scale;

        public override string ToString()
        {
            return $"Dimension Style: {Name} (Arrow: {ArrowType}, TextHeight: {TextHeight})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is OpenCADDimensionStyle other)
                return ID == other.ID;
            return false;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }
    }
}
