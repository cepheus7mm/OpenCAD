using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using OpenCAD.Styles.LineTypes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OpenCAD.NonGeometric
{
    public abstract class NonGeometricBase : OpenCADObject
    {
        protected Vector3D _normal = new(0, 0, 1);

        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public NonGeometricBase() : base()
        {
            _isDrawable = true;
        }

        public NonGeometricBase(OpenCADDocument? doc) : base(doc)
        {
            _document = doc;
            _parent = doc;

            // Assign to the document's current layer if document exists
            if (_document != null)
            {
                Layer = _document.CurrentLayer;
                Color = _document.CurrentColor;
                LineTypeID = _document.CurrentLineTypeID ?? OpenCADDocument.ContinuousLineTypeID;
                LineWeight = _document.CurrentLineWeight ?? LineWeight.Default;
            }
        }

        [JsonIgnore]
        public Color Color
        {
            get
            {
                // Try to get the object's own color property
                var color = GetPropertyValue<Color?>(PropertyType.Color, nameof(Color));
                if (color.HasValue && color.Value.A > 0)
                    return color.Value;

                // If not set, try to get the layer's color
                if (Layer != null)
                    return Layer.Color;

                // Fallback to white
                return Color.FromArgb(255, 255, 255, 255);
            }
            set => SetPropertyValue<Color?>(PropertyType.Color, nameof(Color), OpenCADStrings.Color, value);
        }

        [JsonIgnore]
        public uint LineTypeID
        {
            get
            {
                // Try to get the object's own line type property
                var lineType = GetPropertyValue<uint?>(PropertyType.UInt, nameof(LineTypeID));
                if (lineType.HasValue && lineType.Value != uint.MaxValue)
                    return lineType.Value;

                // If not set or ByLayer, try to get the layer's line type
                if (Layer != null)
                    return Layer.LineTypeID;

                if (Document != null)
                {
                    // If the document has a default line type, use it
                    var docDefaultLineType = Document.CurrentLineTypeID;
                    if (docDefaultLineType.HasValue && docDefaultLineType != OpenCADDocument.LineTypeByLayer)
                        return docDefaultLineType.Value;
                }

                // Fallback to Continuous
                return OpenCADDocument.ContinuousLineTypeID;
            }
            set => SetPropertyValue<uint?>(PropertyType.UInt, nameof(LineTypeID), OpenCADStrings.LineType, value);
        }

        [JsonIgnore]
        public LineWeight LineWeight
        {
            get
            {
                // Try to get the object's own line weight property
                var lineWeight = GetPropertyValue<LineWeight?>(PropertyType.LineWeight, nameof(LineWeight));
                if (lineWeight.HasValue && lineWeight.Value != LineWeight.ByLayer)
                    return lineWeight.Value;

                // If not set or ByLayer, try to get the layer's line weight
                if (Layer != null)
                    return Layer.LineWeight;

                if (Document != null)
                {
                    // If the document has a default line weight, use it
                    var docDefaultLineWeight = Document.CurrentLineWeight;
                    if (docDefaultLineWeight != null && docDefaultLineWeight != LineWeight.ByLayer)
                        return (LineWeight)docDefaultLineWeight;
                }

                // Fallback to Default
                return LineWeight.Default;
            }
            set => SetPropertyValue<LineWeight?>(PropertyType.LineWeight, nameof(LineWeight), OpenCADStrings.LineWeight, value);
        }

        public virtual Point3D GetClosestPointTo(Point3D point, bool extend = false)
        {
            throw new NotImplementedException();
        }

        public Vector3D? GetFirstDerivate(Point3D point)
        {
            throw new NotImplementedException();
        }

        public double GetParameterAtPoint(Point3D point)
        {
            throw new NotImplementedException();
        }

        public Point3D GetPointAtParameter(double parameter)
        {
            throw new NotImplementedException();
        }

        public Vector3D? GetSecondDerivate(Point3D point)
        {
            throw new NotImplementedException();
        }
    }
}
