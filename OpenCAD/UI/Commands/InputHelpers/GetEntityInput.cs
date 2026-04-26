using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    /// <summary>
    /// Helper class to get entity selection input from user via mouse click.
    /// Follows the same pattern as GetPointInput, GetDistanceInput, etc.
    /// Supports optional entity filtering and hover preview.
    /// </summary>
    public class GetEntityInput : InputHelperBase
    {
        private TaskCompletionSource<InputResult>? _taskSource;

        public GetEntityInput(ICommandContext context, ViewportViewModel? viewModel)
            : base(context, viewModel)
        {
        }

        /// <summary>
        /// Prompt the user to select an entity. Supports optional filtering, hover preview,
        /// and Enter-to-skip (returns InputResult.Default).
        /// </summary>
        /// <param name="inputParams">Standard input parameters (Prompt, Keywords, CancellationToken, etc.)</param>
        /// <param name="filter">Optional predicate to accept/reject entities. Return (true, null) to accept,
        /// or (false, "reason") to reject with a message.</param>
        /// <param name="allowHover">If true, hover events resolve the task with InputResult.Hover.</param>
        public async Task<InputResult> GetEntityAsync(
            InputParams inputParams,
            Func<OpenCADObject, (bool accepted, string? rejection)>? filter = null,
            bool allowHover = false)
        {
            _inputParams = inputParams;

            string formattedPrompt = PromptBuilder.Build(inputParams, _context.GetLastPoint());
            _context.PostToUI(() => _context.SetCommandPrompt(formattedPrompt));

            if (_viewModel == null)
                return InputResult.Cancel;

            var controller = new InputTaskController();
            controller.AttachCancellation(_inputParams.CancellationToken);

            _taskSource = controller.TaskCompletionSource;
            KeyWordHandler = kw => controller.CompleteWithKeyword(kw);

            EventHandler<ObjectSelectedEventArgs>? clickHandler = null;
            EventHandler<ObjectHoverEventArgs>? hoverHandler = null;

            // --- CLICK HANDLER ------------------------------------------------
            clickHandler = (s, e) =>
            {
                if (e.Object != null && filter != null)
                {
                    var (accepted, rejection) = filter(e.Object);
                    if (!accepted)
                    {
                        _context.OutputMessage(rejection ?? "Object is not a valid selection.");
                        return; // stay listening
                    }
                }

                _viewModel.ObjectSelected -= clickHandler;
                if (allowHover)
                    _viewModel.ObjectHovered -= hoverHandler;

                if (e.Object != null)
                {
                    _taskSource.TrySetResult(InputResult.FromObjectAndPoint(e.Object, e.PickedPoint ?? Point3D.NotAPoint));
                }
                else
                {
                    _taskSource.TrySetResult(InputResult.Cancel);
                }
            };

            // --- HOVER HANDLER ------------------------------------------------
            if (allowHover)
            {
                hoverHandler = (s, e) =>
                {
                    if (e.Object != null && e.PickedPoint.HasValue && e.PickedPoint != Point3D.NotAPoint)
                    {
                        // Apply filter to hover as well
                        if (filter != null)
                        {
                            var (accepted, _) = filter(e.Object);
                            if (!accepted)
                                return;
                        }

                        _taskSource.TrySetResult(new InputResult
                        {
                            ResultType = InputResult.InputResultType.Hover,
                            Object = e.Object,
                            Point = e.PickedPoint
                        });
                    }
                    else
                    {
                        _taskSource.TrySetResult(new InputResult
                        {
                            ResultType = InputResult.InputResultType.None
                        });
                    }
                };

                _viewModel.ObjectHovered += hoverHandler;
            }

            _viewModel.ObjectSelected += clickHandler;

            try
            {
                return await controller.Task.ConfigureAwait(false);
            }
            finally
            {
                _viewModel.ObjectSelected -= clickHandler;
                if (allowHover)
                    _viewModel.ObjectHovered -= hoverHandler;
            }
        }

        /// <summary>
        /// Process keyboard input — supports Enter for default/skip and keyword matching.
        /// </summary>
        public override bool ProcessKeyboardInput(string input)
        {
            if (_taskSource == null)
                return false;

            // Empty input with default → skip (Enter to continue without selection)
            if (string.IsNullOrWhiteSpace(input) && _inputParams.DefaultValue != null)
            {
                _taskSource.TrySetResult(InputResult.Default);
                return true;
            }

            // Keyword matching via base
            base.ProcessKeyboardInput(input);
            if (LastInputHandled)
            {
                _taskSource.TrySetResult(InputResult.FromKeyword(input));
                return true;
            }

            return false;
        }
    }
}