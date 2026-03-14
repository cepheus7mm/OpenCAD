using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using OpenCAD.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("offset", "creates an offset of an object", "of")]
    public sealed class OffsetCommand : EditCommandBase
    {
        private double _distance;
        private double _offset;
        private ICurve? _sourceCurve;
        private Point3D _sidePoint;

        public override async Task Execute()
        {
            var document = Context?.GetDocument();
            if (document == null)
                throw new InvalidOperationException("No active document");

            var viewModel = Context?.GetActiveViewportViewModel();
            if (viewModel == null)
                throw new InvalidOperationException("No active view model");

            // 1. Ask for distance
            BasePoint = Point3D.NotAPoint;
            var result = await GetDistance("Offset distance", document.LastDistance);
            if (!result.IsDouble)
                return;
            _distance = result.DoubleValue;
            document.LastDistance = _distance;

            // 2. Ask for curve
            result = await GetEntity("Select object to offset");
            _sourceCurve = result.Object as ICurve;
            _sidePoint = result.Point.Value;

            if (_sourceCurve == null || !_sidePoint.IsValid)
                return;

            // 3. Compute which side based on pick point
            viewModel.PreviewPointChanged += OnPreviewPointChanged;
            try
            {
                _sidePoint = await GetSide(_sourceCurve);
            }
            finally
            {
                viewModel.PreviewPointChanged -= OnPreviewPointChanged;
            }

            _offset = ComputeSignedDistance(_sourceCurve, _sidePoint, _distance);

            // 4. Generate offset curves
            var offsets = _sourceCurve.GetOffsetCurves(_offset);

            // 5. Add them to the document in a single undo transaction
            var tx = document.GetUndoRedoManager()?.BeginTransaction("Offset");
            foreach (var offsetCurve in offsets)
            {
                var action = new AddGeometryAction(offsetCurve as OpenCADObject, "Offset");
                tx.AddAction(action);
            }
            document.GetUndoRedoManager()?.CommitTransaction(true);
        }

        protected override Matrix4D GetTransformation()
        {
            throw new NotImplementedException();
        }

        private double ComputeSignedDistance(ICurve curve, Point3D sidePoint, double distance)
        {
            double t = curve.GetClosestParameter(sidePoint, extend: true);
            Point3D p = curve.GetPointAtParameter(t);
            Vector3D tan = curve.GetFirstDerivativeAtParameter(t).Normalized;

            Vector3D v = sidePoint - p;

            // 2D cross product (scalar)
            double cross = tan.X * v.Y - tan.Y * v.X;

            return cross >= 0 ? distance : -distance;
        }

        private async Task<Point3D> GetSide(ICurve curve)
        {
            var point = Point3D.NotAPoint;

            while (true)
            {
                var ptResult = await GetPoint("Specify side to offset");

                if (ptResult == null)
                    continue;

                if (ptResult.IsPoint)
                {
                    CancelPreview();
                    point = ptResult.Point!.Value;
                    break;
                }
                else
                {
                    // cancel
                    CancelPreview();
                    break;
                }
            }

            return point;
        }

        private void OnPreviewPointChanged(object? sender, PointPickedEventArgs e)
        {
            _sidePoint = e.Point;
            _offset = ComputeSignedDistance(_sourceCurve!, _sidePoint, _distance);
            UpdatePreview();
        }

        protected override IEnumerable<OpenCADObject> ComputePreviewObjects()
        {
            if (_sourceCurve == null)
                return Enumerable.Empty<OpenCADObject>();

            return _sourceCurve.GetOffsetCurves(_offset).Select(x => x as OpenCADObject);
        }
    }
}
