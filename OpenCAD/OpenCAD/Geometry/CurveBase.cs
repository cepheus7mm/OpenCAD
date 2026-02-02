using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry
{
    internal abstract class CurveBase : ICurve
    {
        public abstract bool IsClosed { get; }
        public abstract bool IsPeriodic { get; }

        public abstract double DomainStart { get; }
        public abstract double DomainEnd { get; }

        public abstract Point3D Start { get; }
        public abstract Point3D End { get; }

        public abstract Point3D GetPointAtParameter(double t);
        public abstract Vector3D GetFirstDerivativeAtParameter(double t);
        public abstract Vector3D GetSecondDerivativeAtParameter(double t);

        public abstract double GetClosestParameter(Point3D point, bool extend = false);
        public abstract Point3D GetClosestPoint(Point3D point, bool extend = false);
        public abstract double GetParameterAtPoint(Point3D point);

        public abstract double GetLength();
        public abstract double GetLength(double t0, double t1);

        public abstract ICurve Trim(double t0, double t1);
        public abstract ICurve Transform(Matrix4D transform);

    }
}
