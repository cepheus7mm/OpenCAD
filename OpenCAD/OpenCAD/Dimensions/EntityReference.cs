using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Dimensions
{
    public sealed class EntityReference
    {
        public Vector2? Point { get; } 
        public Guid EntityId { get; }
        public string? SubEntity { get; } // e.g. "StartPoint", "EndPoint", "Center", "Edge:3"

        public EntityReference(Guid entityId, string? subEntity = null, Point3D? point = null)
        {
            EntityId = entityId;
            SubEntity = subEntity;
            Point = point?.ToVector2();
        }
        public Vector2 ResolvePoint(OpenCADDocument? document = null)
        {
            if (Point.HasValue)
                return Point.Value;
            if (document is null || EntityId == Guid.Empty)
                return Vector2.Zero; // non-associative fallback
            // 1. Lookup entity
            var curve = document.GetChild(EntityId) as ICurve;

            if (curve is null)
                return Vector2.Zero; // non-associative fallback

            // 2. No sub-entity? Try default point
            if (SubEntity is null)
                return ResolveDefaultPoint(curve);

            // 3. Parse sub-entity
            var parts = SubEntity.Split(':');

            return parts[0] switch
            {
                "Start" => ResolveStartPoint(curve),
                "End" => ResolveEndPoint(curve),
                "Center" => ResolveCenterPoint(curve),
                "Point" => ResolvePointEntity(curve),
                "Vertex" => ResolveVertex(curve, int.Parse(parts[1])),
                "Param" => ResolveCurveParam(curve, double.Parse(parts[1])),

                _ => Vector2.Zero
            };
        }

        private Vector2 ResolveCurveParam(ICurve curve, double v)
        {
            return curve.GetPointAtParameter(v).ToVector2();
        }

        private Vector2 ResolveVertex(ICurve curve, int v)
        {
            return curve.GetPointAtParameter(v).ToVector2();
        }

        private Vector2 ResolvePointEntity(ICurve curve)
        {
            throw new NotImplementedException();
        }

        private Vector2 ResolveCenterPoint(ICurve curve)
        {
            if (curve is ICircularGeometry circular)
                return circular.Center.ToVector2();

            return ResolveDefaultPoint(curve);
        }

        private Vector2 ResolveEndPoint(ICurve curve)
        {
            return curve.GetPointAtParameter(curve.DomainEnd).ToVector2();
        }

        private Vector2 ResolveStartPoint(ICurve curve)
        {
            return curve.GetPointAtParameter(curve.DomainStart).ToVector2();
        }

        private Vector2 ResolveDefaultPoint(ICurve curve)
        {
            return ResolveStartPoint(curve);
        }
    }
}
