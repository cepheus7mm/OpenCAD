using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System.Threading.Tasks;
using UI.Commands.InputHelpers;
using UI.Commands.Interfaces;

namespace UI.Commands.View
{
    [InputCommand("zoom", "Zoom the viewport", "z")]
    public class ZoomCommand : CommandBase
    {
        private enum ZoomMode
        {
            Window,
            Extents,
            Center,
            Scale,
            Previous
        }

        public override async Task Execute()
        {
            var camera = Context?.GetCamera();
            if (camera == null)
            {
                Context?.OutputMessage("No active viewport camera.");
                RaiseCommandCompleted();
                return;
            }

            var mode = await ResolveModeAsync();
            if (mode == null)
            {
                RaiseCommandCompleted();
                return;
            }

            switch (mode.Value)
            {
                case ZoomMode.Window:
                    await ExecuteWindowAsync(camera);
                    break;
                case ZoomMode.Extents:
                    ExecuteExtents(camera);
                    break;
                case ZoomMode.Center:
                    await ExecuteCenterAsync(camera);
                    break;
                case ZoomMode.Scale:
                    await ExecuteScaleAsync(camera);
                    break;
                case ZoomMode.Previous:
                    ExecutePrevious(camera);
                    break;
            }

            Context?.PostToUI(() => Context.GetActiveViewport()?.Refresh());
            RaiseCommandCompleted();
        }

        // ---------------------------------------------------------------
        // Mode resolution
        // ---------------------------------------------------------------

        private async Task<ZoomMode?> ResolveModeAsync()
        {
            // Try to map the entry point (alias or keyword) to a mode
            var entryPoint = _commandArgs?.EntryPoint?.Trim();
            if (!string.IsNullOrEmpty(entryPoint))
            {
                var matched = TryParseMode(entryPoint);
                if (matched.HasValue)
                    return matched;
            }

            // Prompt the user to choose
            _cancellationTokenSource ??= new System.Threading.CancellationTokenSource();

            var result = await GetEnum<ZoomMode>(new InputParams
            {
                Prompt = "Specify zoom option",
                CancellationToken = _cancellationTokenSource.Token
            });

            if (result.IsCancel || !result.IsKeyword)
                return null;

            return TryParseMode(result.Keyword!);
        }

        private static ZoomMode? TryParseMode(string input)
        {
            return input.ToUpperInvariant() switch
            {
                "W" or "WIN" or "WINDOW"   => ZoomMode.Window,
                "E" or "EXT" or "EXTENTS"  => ZoomMode.Extents,
                "C" or "CEN" or "CENTER"   => ZoomMode.Center,
                "S" or "SC"  or "SCALE"    => ZoomMode.Scale,
                "P" or "PRE" or "PREVIOUS" => ZoomMode.Previous,
                _ => null
            };
        }

        // ---------------------------------------------------------------
        // Window
        // ---------------------------------------------------------------

        private async Task ExecuteWindowAsync(ICamera camera)
        {
            var firstResult = await GetPoint(new InputParams
            {
                Prompt = "Specify first corner",
                CancellationToken = (_cancellationTokenSource ??= new System.Threading.CancellationTokenSource()).Token
            });

            if (firstResult.IsCancel || !firstResult.IsPoint)
                return;

            BasePoint = firstResult.Point!.Value;

            var edgeColor = Context?.GetDocument()?.GetViewportSettings()?.Crosshair?.Color
                            ?? System.Drawing.Color.LightBlue;
            var fillColor = System.Drawing.Color.FromArgb(64, 0, 0, 139); // dark blue, 25%

            var secondResult = await GetRectangle(
                new InputParams
                {
                    Prompt            = "Specify opposite corner",
                    BasePoint         = BasePoint,
                    CancellationToken = _cancellationTokenSource.Token
                },
                edgeColor,
                fillColor);

            if (secondResult.IsCancel || !secondResult.IsPoint)
                return;

            var extents = new Extents(BasePoint, secondResult.Point!.Value);
            camera.ZoomToExtents(extents);
        }

        // ---------------------------------------------------------------
        // Extents
        // ---------------------------------------------------------------

        private void ExecuteExtents(ICamera camera)
        {
            var document = Context?.GetDocument();
            if (document == null)
                return;

            Extents? combined = null;

            foreach (var child in document.GetChildren())
            {
                if (child is not GeometryBase geometry)
                    continue;

                try
                {
                    var ext = geometry.GetExtents();
                    if (combined == null)
                        combined = ext;
                    else
                    {
                        combined.Value.Union(ext);
                    }
                }
                catch
                {
                    // Skip objects whose extents cannot be computed
                }
            }

            if (combined == null)
            {
                Context?.OutputMessage("No geometry found in document.");
                return;
            }

            camera.ZoomToExtents(combined.Value);
        }

        // ---------------------------------------------------------------
        // Center
        // ---------------------------------------------------------------

        private async Task ExecuteCenterAsync(ICamera camera)
        {
            var centerResult = await GetPoint(new InputParams
            {
                Prompt = "Specify center point",
                CancellationToken = (_cancellationTokenSource ??= new System.Threading.CancellationTokenSource()).Token
            });

            if (centerResult.IsCancel || !centerResult.IsPoint)
                return;

            var center = centerResult.Point!.Value;

            var scaleResult = await GetDouble(new InputParams
            {
                Prompt = "Enter height",
                DefaultValue = (double)camera.WorldHeight,
                CancellationToken = _cancellationTokenSource.Token
            });

            float worldWidth;
            if (scaleResult.IsCancel || double.IsNaN(scaleResult.DoubleValue))
            {
                worldWidth = camera.WorldWidth;
            }
            else
            {
                float aspect = camera.ViewportWidthPx / camera.ViewportHeightPx;
                worldWidth = (float)scaleResult.DoubleValue * aspect;
            }

            camera.ZoomToCenter(center, worldWidth);
        }

        // ---------------------------------------------------------------
        // Scale
        // ---------------------------------------------------------------

        private async Task ExecuteScaleAsync(ICamera camera)
        {
            var result = await GetDouble(new InputParams
            {
                Prompt = "Enter a scale factor",
                CancellationToken = (_cancellationTokenSource ??= new System.Threading.CancellationTokenSource()).Token
            });

            if (result.IsCancel || double.IsNaN(result.DoubleValue) || result.DoubleValue <= 0)
                return;

            camera.Zoom((float)result.DoubleValue);
        }

        // ---------------------------------------------------------------
        // Previous
        // ---------------------------------------------------------------

        private void ExecutePrevious(ICamera camera)
        {
            if (!camera.CanZoomPrevious)
                Context?.OutputMessage("No previous zoom.");
            else
                camera.PopState();
        }
    }
}
