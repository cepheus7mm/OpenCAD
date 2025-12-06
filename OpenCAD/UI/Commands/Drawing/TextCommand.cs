using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.NonGeometric;
using OpenCAD.TextRendering;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace UI.Commands.Drawing
{
    /// <summary>
    /// Command to create text with live preview while typing
    /// </summary>
    [InputCommand("text", "Create text (prompts for base point, rotation angle, and text string)", "dt")]
    public class TextCommand : CommandBase
    {
        private OpenCADText? _previewText;
        private Point3D _basePoint;
        private double _rotation;

        public override bool IsMultiStep => true;

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
        }

        public override async Task Execute()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            BasePoint = Point3D.NotAPoint;

            try
            {
                // Get base point
                var result = await GetPoint(
                    "Specify base point",
                    allowLastPoint: true);

                if (result == null || result.Point == null || result.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                {
                    Cancel();
                    return;
                }

                _basePoint = result.Point;

                Context?.OutputMessage(
                    string.Format(
                        "Base point: ({0:F3}, {1:F3}, {2:F3})",
                        _basePoint.X,
                        _basePoint.Y,
                        _basePoint.Z));

                // Get rotation angle
                BasePoint = _basePoint;
                var angleResult = await GetAngle(
                    "Specify rotation angle",
                    allowLastPoint: false);

                if (angleResult == null || double.IsNaN(angleResult.DoubleValue) || angleResult.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                {
                    Cancel();
                    return;
                }

                _rotation = angleResult.DoubleValue;

                Context?.OutputMessage(
                    string.Format(
                        "Rotation angle: {0:F3} radians",
                        _rotation));

                // Enable preview mode for text input
                var viewport = Context?.GetActiveViewportViewModel();
                if (viewport != null)
                {
                    // Create initial preview text object (empty)
                    CreatePreviewText(string.Empty);

                    // Enable preview mode with callback to update text as user types
                    viewport.EnablePreviewMode((point) => 
                    {
                        // Not used for text - we update via UpdatePreviewText instead
                    });
                }

                // Get text string with preview callback
                var textResult = await GetString(
                    "Enter text string",
                    allowArbitrary: true,
                    previewCallback: UpdatePreviewText);

                // Disable preview mode and clear preview object
                if (viewport != null)
                {
                    viewport.DisablePreviewMode();
                    ClearPreviewText();
                }

                if (textResult == null || string.IsNullOrEmpty(textResult.Keyword) || textResult.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                {
                    Cancel();
                    return;
                }

                var textString = textResult.Keyword;

                // Create the final text object
                CreateText(_basePoint, _rotation, textString);

            }
            catch (OperationCanceledException)
            {
                Cancel();
            }
            finally
            {
                // Ensure preview is cleared
                ClearPreviewText();
                
                Context?.OutputMessage("Text command completed.");
                RaiseCommandCompleted();
            }
        }

        public override bool ProcessInput(string input)
        {
            if (_inputHelper != null)
            {
                return _inputHelper.ProcessKeyboardInput(input);
            }
            
            return false;
        }

        /// <summary>
        /// Creates or updates the preview text object
        /// </summary>
        private void CreatePreviewText(string text)
        {
            var document = Context?.GetDocument();
            if (document == null) return;

            // Ensure document has access to services
            if (document.ServiceProvider == null && Application.Current is UI.App app)
            {
                document.ServiceProvider = app.Services;
            }

            // Create preview text object
            _previewText = new OpenCADText(document, text, _basePoint, _rotation);

            // Add to viewport's temp objects for rendering
            var viewport = Context?.GetActiveViewportViewModel();
            if (viewport != null && _previewText != null)
            {
                // Clear any existing preview
                viewport.ClearTempPoints();
                
                // Add preview text to viewport (it will be rendered as part of overlay)
                Context?.PostToUI(() =>
                {
                    var vpControl = Context.GetActiveViewport();
                    if (vpControl != null)
                    {
                        // Temporarily add to document for rendering
                        // (will be removed when preview is cleared)
                        vpControl.AddObject(_previewText);
                    }
                });
            }
        }

        /// <summary>
        /// Updates the preview text as the user types
        /// </summary>
        private void UpdatePreviewText(string currentText)
        {
            if (_previewText != null)
            {
                // Update the text property
                _previewText.Text = currentText;

                // Refresh viewport to show updated preview
                Context?.PostToUI(() =>
                {
                    var vpControl = Context.GetActiveViewport();
                    vpControl?.Refresh();
                });
            }
            else
            {
                // Create preview if it doesn't exist yet
                CreatePreviewText(currentText);
            }
        }

        /// <summary>
        /// Clears the preview text from the viewport
        /// </summary>
        private void ClearPreviewText()
        {
            if (_previewText != null)
            {
                Context?.PostToUI(() =>
                {
                    var vpControl = Context.GetActiveViewport();
                    if (vpControl != null)
                    {
                        vpControl.RemoveObject(_previewText);
                        vpControl.Refresh();
                    }
                });

                _previewText = null;
            }
        }

        private void CreateText(Point3D basePoint, double rotation, string textString)
        {
            var document = Context?.GetDocument();
            if (document == null)
                throw new InvalidOperationException("No active document to create text in.");

            // Ensure document has access to services
            if (document.ServiceProvider == null && Application.Current is UI.App app)
            {
                document.ServiceProvider = app.Services;
            }

            var text = new OpenCADText(document, textString, basePoint, rotation);

            var undoManager = Context?.GetUndoRedoManager();

            if (undoManager != null)
            {
                Context?.PostToUI(() =>
                {
                    var viewport = Context.GetActiveViewport();
                    var action = new Undo.AddGeometryAction(
                        text,
                        document,
                        viewport,
                        string.Format(
                            "Create Text at ({0:F3}, {1:F3}, {2:F3}): \"{3}\"",
                            basePoint.X, basePoint.Y, basePoint.Z,
                            textString)
                    );
                    undoManager.ExecuteAction(action);
                });
            }
            else
            {
                Context?.RaiseGeometryCreated(text);
            }

            Context?.OutputMessage(
                string.Format(
                    "Text created at ({0:F3}, {1:F3}, {2:F3}): \"{3}\"",
                    basePoint.X, basePoint.Y, basePoint.Z,
                    textString));
        }

        public override void Cancel()
        {
            base.Cancel();
            _cancellationTokenSource?.Cancel();
            ClearPreviewText();
            CurrentPrompt = string.Empty;
        }
    }
}