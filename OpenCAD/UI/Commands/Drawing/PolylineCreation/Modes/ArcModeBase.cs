using Newtonsoft.Json.Linq;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.PolylineCreation.Modes
{
    public abstract class ArcModeBase : PolylineCreationModeBase
    {
        protected readonly Point3D StartPoint;
        protected Point3D? _center1;
        protected Point3D? _center2;
        protected readonly Vector3D _tangent;
        protected Point3D? _endPoint;

        protected ArcModeBase(Point3D startPoint, Vector3D tangent)
        {
            StartPoint = startPoint;
            _tangent = tangent;
        }

        // Most arc modes use the start point as the base point for the first input
        public override Point3D? GetBasePoint() => StartPoint;

        public virtual bool CanPreview => false;

        // All arc modes end the same way:
        //   - compute bulge
        //   - add vertex
        //   - return to NextPointMode
        public override IPolylineCreationMode Apply(PolylineCommand command)
        {
            _endPoint = InputPoint!.Value;
            var bulge = ComputeBulge(_endPoint.Value);

            command.AddVertex(_endPoint.Value, bulge);
            return new NextPointMode(_endPoint.Value);
        }

        // Derived classes implement the actual bulge math
        protected abstract double ComputeBulge(Point3D endPoint);

        // Shared preview logic for all arc modes
        public override IEnumerable<IDrawable> GetPreview(PolylineCommand command)
        {
            if (!CanPreview)
                return Enumerable.Empty<IDrawable>();  

            if (!InputPoint.HasValue)
            {
                // Preview using target point
                _endPoint = command.GetTarget();
                var bulge = ComputeBulge(_endPoint.Value);
                command.SetPreviewVertex(_endPoint.Value, bulge);
                return Enumerable.Empty<IDrawable>();
            }

            // Preview using actual input point
            _endPoint = InputPoint.Value;
            var finalBulge = ComputeBulge(_endPoint.Value);
            command.SetPreviewVertex(_endPoint.Value, finalBulge);
            return Enumerable.Empty<IDrawable>();
        }

        public static double GetCursorSideOfTangent(
            Point3D start,
            Vector3D tangent,
            Point3D cursor)
        {
            // Vector from start point to cursor
            var v = cursor - start;

            // 2D cross product (z‑component)
            // side > 0 → cursor is left of tangent
            // side < 0 → cursor is right of tangent
            // side = 0 → cursor is exactly on tangent
            return tangent.X * v.Y - tangent.Y * v.X;
        }
    }
}
