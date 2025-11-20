using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using System.Numerics;

namespace OpenCAD.Geometry
{
    public class Line : GeometryBase, ILinearGeometry
    {
        private readonly object _propertyLock = new();

        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public Line() : base()
        {
        }

        public Line(OpenCADDocument doc, Point3D start, Point3D end) : base(doc)
        {
            StartPoint = start;
            EndPoint = end;
        }

        [JsonIgnore, XmlIgnore]
        public Point3D StartPoint
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(StartPoint));
            set => SetPropertyValue(PropertyType.Point, nameof(StartPoint), OpenCADStrings.StartPoint, value);
        }

        [JsonIgnore, XmlIgnore]
        public Point3D EndPoint
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(EndPoint));
            set => SetPropertyValue(PropertyType.Point, nameof(EndPoint), OpenCADStrings.EndPoint, value);
        }

        [JsonIgnore, XmlIgnore]
        public override double Length => StartPoint.DistanceTo(EndPoint);

        [JsonIgnore, XmlIgnore]
        public override double Angle => StartPoint.AngleTo(EndPoint);

        #region Editing

        public override bool Move(Vector3D translation)
        {
            StartPoint += translation;
            EndPoint += translation;
            return true;
        }

        public override bool Transform(Matrix4x4 transformation)
        {
            // Convert to System.Numerics.Vector3 (float precision is OK for rendering/transforms)
            var s = Vector3.Transform(new Vector3((float)StartPoint.X, (float)StartPoint.Y, (float)StartPoint.Z), transformation);
            var e = Vector3.Transform(new Vector3((float)EndPoint.X, (float)EndPoint.Y, (float)EndPoint.Z), transformation);

            StartPoint = new Point3D(s.X, s.Y, s.Z);
            EndPoint = new Point3D(e.X, e.Y, e.Z);
            return true;
        }

        #endregion  
    }
}
