using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.Drawing
{
    /// <summary>
    /// Command to create a donut (filled ring) by specifying inner diameter, outer diameter, and center point(s)
    /// </summary>
    [InputCommand("donut", "Create a donut (prompts for inner diameter, outer diameter, and center)", "do")]
    public class DonutCommand : CommandBase
    {
        private enum DonutInputStep
        {
            InnerDiameter,
            OuterDiameter,
            CenterPoint
        }

        private DonutInputStep _step;
        private double _innerDiameter = double.NaN;
        private double _outerDiameter = double.NaN;
        private Point3D _center = Point3D.NotAPoint;

        // preview donut instance (removed/added during preview)
        private Polyline? _previewDonut;
        private PropertyChangedEventHandler? _donutPreviewHandler;

        public override bool IsMultiStep => true;

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
            _donutPreviewHandler = OnDonutPreview;
        }

        public override async Task Execute()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Step 1: Get inner diameter
                _step = DonutInputStep.InnerDiameter;
                var innerResult = await GetDistance(OpenCADStrings.DonutInnerDiameterPrompt);
                if (innerResult == null || innerResult.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                {
                    Cancel();
                    return;
                }

                _innerDiameter = innerResult.DoubleValue;

                // Validate inner diameter (must be >= 0)
                if (_innerDiameter < 0)
                {
                    Context?.OutputMessage("Inner diameter must be greater than or equal to zero.");
                    Cancel();
                    return;
                }

                // Step 2: Get outer diameter
                _step = DonutInputStep.OuterDiameter;
                var outerResult = await GetDistance(OpenCADStrings.DonutOuterDiameterPrompt);
                if (outerResult == null || outerResult.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                {
                    Cancel();
                    return;
                }

                _outerDiameter = outerResult.DoubleValue;

                // Validate outer diameter (must be >= inner diameter)
                if (_outerDiameter < _innerDiameter)
                {
                    Context?.OutputMessage("Outer diameter must be greater than or equal to the inner diameter.");
                    Cancel();
                    return;
                }

                // Step 3: Get center point(s) - this step repeats until user cancels
                _step = DonutInputStep.CenterPoint;
                
                // Start preview for center point selection
                BeginPreview();

                // Attach the donut preview handler on the UI thread
                if (Context != null && _donutPreviewHandler != null)
                {
                    Context.PostToUI(() =>
                    {
                        if (CachedViewModel != null)
                            CachedViewModel.PropertyChanged += _donutPreviewHandler;
                    });
                }

                try
                {
                    bool firstDonut = true;
                    while (true)
                    {
                        var centerResult = await GetPoint(OpenCADStrings.DonutCenterPointPrompt);
                        
                        // User pressed Enter or ESC - exit the loop
                        if (centerResult == null || 
                            centerResult.ResultType == InputHelpers.InputResult.InputResultType.Cancel ||
                            centerResult.Point == null)
                        {
                            break;
                        }

                        _center = centerResult.Point.Value;

                        // Create the donut
                        CreateDonut();
                        firstDonut = false;
                    }

                    // Command completed successfully
                    if (!firstDonut)
                    {
                        Context?.OutputMessage("Donut command completed.");
                        RaiseCommandCompleted();
                    }
                    else
                    {
                        Cancel();
                    }
                }
                finally
                {
                    // Cleanup preview handler and preview donut
                    CleanUpPreview();
                }
            }
            catch (OperationCanceledException)
            {
                Cancel();
            }
        }

        private void CleanUpPreview()
        {
            try
            {
                if (Context != null && _donutPreviewHandler != null)
                {
                    Context.PostToUI(() =>
                    {
                        if (CachedViewModel != null)
                            CachedViewModel.PropertyChanged -= _donutPreviewHandler;
                    });
                }
            }
            catch { }

            try
            {
                if (_previewDonut != null && Context != null)
                {
                    Context.PostToUI(() =>
                    {
                        try
                        {
                            if (_previewDonut != null && viewport != null)
                            {
                                viewport.RemoveObject(_previewDonut);
                                _previewDonut = null;
                            }
                        }
                        catch { }
                    });
                }
            }
            catch { }

            CommitPreview();
        }

        public override bool ProcessInput(string input)
        {
            // Route keyboard input to shared helper in base (created during Initialize)
            if (_inputHelper != null)
            {
                _inputHelper.ProcessKeyboardInput(input);
                return IsCommandCompleted;
            }

            return false;
        }

        private void CreateDonut()
        {
            Polyline donut = null;

            // Get the document to apply current properties
            var document = Context?.GetDocument();
            if (document == null)
            {
                throw new InvalidOperationException("No active document to create donut in.");
            }

            if (!_center.IsValid || double.IsNaN(_innerDiameter) || double.IsNaN(_outerDiameter))
            {
                throw new InvalidOperationException("Invalid donut parameters.");
            }

            // Calculate width from the difference between outer and inner radii
            double outerRadius = _outerDiameter / 2.0;
            double innerRadius = _innerDiameter / 2.0;
            double width = outerRadius - innerRadius;

            // Create a closed polyline with 2 vertices
            // Each vertex has bulge = 1 (represents a semicircle)
            // Position vertices at opposite ends of a diameter
            var vertex1Position = new Point3D(_center.X - innerRadius, _center.Y, _center.Z);
            var vertex2Position = new Point3D(_center.X + innerRadius, _center.Y, _center.Z);

            donut = new Polyline(document);
            
            // Add two vertices with bulge = 1 (semicircle for each segment)
            var v1 = donut.AddVertex(vertex1Position, bulge: 1.0);
            var v2 = donut.AddVertex(vertex2Position, bulge: 1.0);

            // Set start and end widths for both vertices
            v1.StartWidth = width;
            v1.EndWidth = width;
            v2.StartWidth = width;
            v2.EndWidth = width;

            // Close the polyline
            donut.Close();

            // Use undo/redo system if available
            var undoManager = Context?.GetUndoRedoManager();

            if (undoManager != null)
            {
                // Execute undo action creation/execution on UI thread so any viewport access is safe
                Context?.PostToUI(() =>
                {
                    var action = new OpenCAD.Undo.AddGeometryAction(
                        donut,
                        $"Create Donut at ({_center.X:F3}, {_center.Y:F3}, {_center.Z:F3}), " +
                        $"Inner Diameter: {_innerDiameter:F3}, Outer Diameter: {_outerDiameter:F3}"
                    );
                    undoManager.ExecuteAction(action);
                });
            }
            else
            {
                // Fallback to direct creation (CommandContext.RaiseGeometryCreated posts to UI)
                Context?.RaiseGeometryCreated(donut);
            }

            Context?.OutputMessage(
                $"Donut created: Center=({_center.X:F3}, {_center.Y:F3}, {_center.Z:F3}), " +
                $"Inner Diameter={_innerDiameter:F3}, Outer Diameter={_outerDiameter:F3}");
        }

        public override void Cancel()
        {
            base.Cancel();
            _cancellationTokenSource?.Cancel();
            CurrentPrompt = string.Empty;
        }

        private void OnDonutPreview(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViewportViewModel.PreviewPoint))
                return;

            var previewPoint = CachedViewModel?.PreviewPoint;
            try
            {
                // Remove previous preview donut
                if (_previewDonut != null && viewport != null)
                {
                    try { viewport.RemoveObject(_previewDonut); }
                    catch { }
                    _previewDonut = null;
                }

                if (previewPoint.HasValue && viewport != null && Context != null)
                {
                    var doc = Context.GetDocument();
                    if (doc != null)
                    {
                        // Calculate width from the difference between outer and inner radii
                        double outerRadius = _outerDiameter / 2.0;
                        double innerRadius = _innerDiameter / 2.0;
                        double width = outerRadius - innerRadius;

                        // Create preview donut at the preview point
                        var previewCenter = previewPoint.Value;
                        var vertex1Position = new Point3D(previewCenter.X - innerRadius, previewCenter.Y, previewCenter.Z);
                        var vertex2Position = new Point3D(previewCenter.X + innerRadius, previewCenter.Y, previewCenter.Z);

                        var previewDonut = new Polyline(doc);
                        
                        var v1 = previewDonut.AddVertex(vertex1Position, bulge: 1.0);
                        var v2 = previewDonut.AddVertex(vertex2Position, bulge: 1.0);

                        v1.StartWidth = width;
                        v1.EndWidth = width;
                        v2.StartWidth = width;
                        v2.EndWidth = width;

                        previewDonut.IsPreviewGeometry = true;

                        viewport.AddObject(previewDonut);
                        _previewDonut = previewDonut;
                        viewport.Refresh();
                    }
                }
                else
                {
                    if (viewport != null)
                        viewport.Refresh();
                }
            }
            catch
            {
                // Ignore preview errors
            }
        }
    }
}