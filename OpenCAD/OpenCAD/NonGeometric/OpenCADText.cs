using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using OpenCAD.TextRendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.NonGeometric
{
    public class OpenCADText : NonGeometricBase
    {
        // Thread-safe cache access
        private readonly object _cacheLock = new object();
        private TextBounds? _cachedBounds;
        private string? _cachedText;
        private double _cachedFontSize;
        private string? _cachedFontFamily;
        private bool _cachedIsBold;
        private bool _cachedIsItalic;

        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public OpenCADText() : base()
        {
        }

        public OpenCADText(OpenCADDocument doc, string text, Point3D basePoint, double rotation) : base(doc)
        {
            Text = text;
            BasePoint = basePoint;
            Rotation = rotation;
            TextStyleID = doc.CurrentTextStyleID;
        }

        [JsonIgnore, XmlIgnore]
        public string Text
        {
            get => GetPropertyValue<string>(PropertyType.String, nameof(Text)) ?? string.Empty;
            set
            {
                SetPropertyValue(PropertyType.String, nameof(Text), OpenCADStrings.Text, value);
                lock (_cacheLock)
                {
                    _cachedBounds = null;
                }
            }
        }

        [JsonIgnore, XmlIgnore]
        public Point3D BasePoint
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(BasePoint)) ?? new Point3D();
            set => SetPropertyValue(PropertyType.Point, nameof(BasePoint), OpenCADStrings.BasePoint, value);
        }

        [JsonIgnore, XmlIgnore]
        public double Rotation
        {
            get => GetPropertyValue<double>(PropertyType.DoubleUnitLess, nameof(Rotation));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(Rotation), OpenCADStrings.Rotation, value);
        }

        [JsonIgnore, XmlIgnore]
        public Guid TextStyleID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(TextStyleID));
            set => SetPropertyValue(PropertyType.ID, nameof(TextStyleID), OpenCADStrings.TextStyleID, value);
        }

        [JsonIgnore, XmlIgnore]
        public double FontSize
        {
            get
            {
                var fs = GetPropertyValue<double?>(PropertyType.DoubleUnitLess, nameof(FontSize));
                if (fs.HasValue)
                    return fs.Value;
                var style = _document?.GetChild(TextStyleID);
                if (style is OpenCADTextStyle textStyle)
                    return textStyle.FontSize;
                style = _document?.CurrentTextStyle;
                if (style is OpenCADTextStyle currentTextStyle)
                    return currentTextStyle.FontSize;
                return 0.1;
            }
            set
            {
                SetPropertyValue(PropertyType.DoubleUnitLess, nameof(FontSize), OpenCADStrings.FontSize, value);
                lock (_cacheLock)
                {
                    _cachedBounds = null;
                }
            }
        }

        [JsonIgnore, XmlIgnore]
        public string FontFamily
        {
            get
            {
                var ff = GetPropertyValue<string>(PropertyType.String, nameof(FontFamily));
                if (!string.IsNullOrEmpty(ff))
                    return ff;
                var style = _document?.GetChild(TextStyleID);
                if (style is OpenCADTextStyle textStyle)
                    return textStyle.FontFamily;
                style = _document?.CurrentTextStyle;
                if (style is OpenCADTextStyle currentTextStyle)
                    return currentTextStyle.FontFamily;
                return "Arial";
            }
            set
            {
                SetPropertyValue(PropertyType.String, nameof(FontFamily), OpenCADStrings.FontFamily, value);
                lock (_cacheLock)
                {
                    _cachedBounds = null;
                }
            }
        }

        [JsonIgnore, XmlIgnore]
        public bool IsBold
        {
            get
            {
                var b = GetPropertyValue<bool?>(PropertyType.Boolean, nameof(IsBold));
                if (b.HasValue)
                    return b.Value;
                var style = _document?.GetChild(TextStyleID);
                if (style is OpenCADTextStyle textStyle)
                    return textStyle.IsBold;
                style = _document?.CurrentTextStyle;
                if (style is OpenCADTextStyle currentTextStyle)
                    return currentTextStyle.IsBold;
                return false;
            }
            set
            {
                SetPropertyValue(PropertyType.Boolean, nameof(IsBold), OpenCADStrings.Bold, value);
                lock (_cacheLock)
                {
                    _cachedBounds = null;
                }
            }
        }

        [JsonIgnore, XmlIgnore]
        public bool IsUnderlined
        {
            get
            {
                var u = GetPropertyValue<bool?>(PropertyType.Boolean, nameof(IsUnderlined));
                if (u.HasValue)
                    return u.Value;
                var style = _document?.GetChild(TextStyleID);
                if (style is OpenCADTextStyle textStyle)
                    return textStyle.IsUnderlined;
                style = _document?.CurrentTextStyle;
                if (style is OpenCADTextStyle currentTextStyle)
                    return currentTextStyle.IsUnderlined;
                return false;
            }
            set => SetPropertyValue(PropertyType.Boolean, nameof(IsUnderlined), OpenCADStrings.Underline, value);
        }

        [JsonIgnore, XmlIgnore]
        public bool IsItalic
        {
            get
            {
                var u = GetPropertyValue<bool?>(PropertyType.Boolean, nameof(IsItalic));
                if (u.HasValue)
                    return u.Value;
                var style = _document?.GetChild(TextStyleID);
                if (style is OpenCADTextStyle textStyle)
                    return textStyle.IsItalic;
                style = _document?.CurrentTextStyle;
                if (style is OpenCADTextStyle currentTextStyle)
                    return currentTextStyle.IsItalic;
                return false;
            }
            set
            {
                SetPropertyValue(PropertyType.Boolean, nameof(IsItalic), OpenCADStrings.Italic, value);
                lock (_cacheLock)
                {
                    _cachedBounds = null;
                }
            }
        }

        /// <summary>
        /// Invalidates the cached bounding box when text properties change.
        /// </summary>
        private void InvalidateBoundsCache()
        {
            _cachedBounds = null;
        }

        /// <summary>
        /// Gets the text bounding box in local space (unrotated, relative to origin).
        /// Returns cached value if properties haven't changed.
        /// </summary>
        private TextBounds? GetTextBounds()
        {
            // Read cache with lock
            lock (_cacheLock)
            {
                if (_cachedBounds.HasValue &&
                    _cachedText == Text &&
                    Math.Abs(_cachedFontSize - FontSize) < double.Epsilon &&
                    _cachedFontFamily == FontFamily &&
                    _cachedIsBold == IsBold &&
                    _cachedIsItalic == IsItalic)
                {
                    return _cachedBounds;
                }
            }

            // Compute bounds (outside lock to avoid holding lock during expensive operation)
            if (string.IsNullOrEmpty(Text) || Document == null)
                return null;

            var metricsProvider = Document.GetService<ITextMetricsProvider>();
            if (metricsProvider == null)
            {
                System.Diagnostics.Debug.WriteLine("Warning: ITextMetricsProvider not available");
                return null;
            }

            try
            {
                var fontDescriptor = new FontDescriptor(FontFamily, IsBold, IsItalic);
                var bounds = metricsProvider.GetTextBounds(Text, fontDescriptor, FontSize);

                // Update cache with lock
                lock (_cacheLock)
                {
                    _cachedBounds = bounds;
                    _cachedText = Text;
                    _cachedFontSize = FontSize;
                    _cachedFontFamily = FontFamily;
                    _cachedIsBold = IsBold;
                    _cachedIsItalic = IsItalic;
                }

                return bounds;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error computing text bounds: {ex.Message}");
                return null;
            }
        }

        public override Point3D GetClosestPointTo(Point3D point, bool extend = false)
        {
            var bounds = GetTextBounds();
            if (!bounds.HasValue || bounds.Value.Width < double.Epsilon || bounds.Value.Height < double.Epsilon)
            {
                return BasePoint;
            }

            var localPoint = TransformToLocal(point);

            bool isInside = localPoint.X >= bounds.Value.MinX && localPoint.X <= bounds.Value.MaxX &&
                            localPoint.Y >= bounds.Value.MinY && localPoint.Y <= bounds.Value.MaxY;

            if (isInside)
            {
                return point;
            }

            double clampedX = Math.Clamp(localPoint.X, bounds.Value.MinX, bounds.Value.MaxX);
            double clampedY = Math.Clamp(localPoint.Y, bounds.Value.MinY, bounds.Value.MaxY);

            var clampedLocal = new Point3D(clampedX, clampedY, 0);

            return TransformToWorld(clampedLocal);
        }

        private Point3D TransformToLocal(Point3D world)
        {
            // Read Rotation once to avoid torn reads
            double rotation = Rotation;
            var basePoint = BasePoint;

            double translatedX = world.X - basePoint.X;
            double translatedY = world.Y - basePoint.Y;

            double cosRot = Math.Cos(-rotation);
            double sinRot = Math.Sin(-rotation);

            double localX = translatedX * cosRot - translatedY * sinRot;
            double localY = translatedX * sinRot + translatedY * cosRot;

            return new Point3D(localX, localY, 0);
        }

        private Point3D TransformToWorld(Point3D local)
        {
            // Read Rotation once to avoid torn reads
            double rotation = Rotation;
            var basePoint = BasePoint;

            double cosRot = Math.Cos(rotation);
            double sinRot = Math.Sin(rotation);

            double rotatedX = local.X * cosRot - local.Y * sinRot;
            double rotatedY = local.X * sinRot + local.Y * cosRot;

            return new Point3D(
                basePoint.X + rotatedX,
                basePoint.Y + rotatedY,
                basePoint.Z
            );
        }
    }
}
