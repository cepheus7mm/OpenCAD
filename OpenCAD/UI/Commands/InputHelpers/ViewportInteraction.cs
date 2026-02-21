using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    public sealed class ViewportInteraction : IDisposable
    {
        private readonly ViewportViewModel _viewModel;
        private readonly Action<Point3D> _onPick;
        private readonly Action _onCancel;
        private readonly Point3D? _basePoint;

        public ViewportInteraction(
            ViewportViewModel viewModel,
            Point3D? basePoint,
            Action<Point3D> onPick,
            Action onCancel,
            Action<Point3D>? onPreview = null)
        {
            _viewModel = viewModel;
            _onPick = onPick;
            _onCancel = onCancel;
            _basePoint = basePoint;

            _viewModel.PointPicked += View_PointPicked;
            _viewModel.PointPickingCancelled += View_PointPickingCancelled;

            _viewModel.EnablePointPickingMode();

            if (basePoint != null)
            {
                _viewModel.ClearTempPoints();
                _viewModel.AddTempPoint(basePoint.Value);
                if (onPreview != null)
                    _viewModel.EnablePreviewMode(onPreview);
            }
        }

        private void View_PointPicked(object? s, PointPickedEventArgs e)
            => _onPick(e.Point);

        private void View_PointPickingCancelled(object? s, EventArgs e)
            => _onCancel();

        public void Dispose()
        {
            try
            {
                _viewModel.DisablePointPickingMode();
                _viewModel.PointPicked -= View_PointPicked;
                _viewModel.PointPickingCancelled -= View_PointPickingCancelled;

                if (_basePoint != null)
                {
                    _viewModel.DisablePreviewMode();
                    _viewModel.ClearTempPoints();
                }
            }
            catch { }
        }
    }
}
