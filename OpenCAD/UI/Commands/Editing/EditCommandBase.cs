using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using OpenCAD.Undo;
using OpenCAD.Undo.OpenCAD.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    /// <summary>
    /// Base class for editing commands that require object selection.
    /// Includes preview support (uses shared preview in CommandBase).
    /// </summary>
    public abstract class EditCommandBase : CommandBase
    {
        private TaskCompletionSource<List<OpenCADObject>>? _selectionTcs;

        protected string _commandName = string.Empty;
        protected bool _preserveOriginal = false;
        protected bool _isRepeatable = false;

        public string SelectObjectsMessage => string.Format(OpenCADStrings.SelectObjectsToActOnMessage, _commandName);

        public string NoObjectsMessage => OpenCADStrings.NoObjectsSelectedToActOnCancelled;

        public string UnableToActOnObjectsMissingContext => string.Format(OpenCADStrings.UnableToActOnObjectsMissingContext, _commandName);

        public string NoObjectsSelectedToActOnCancelled => OpenCADStrings.NoObjectsSelectedToActOnCancelled;

        public string BasePointPrompt => OpenCADStrings.BasePointPrompt;

        public string TargetPointPrompt => OpenCADStrings.TargetPointPrompt;

        public string InvalidPointInput => OpenCADStrings.InvalidPointInput;

        public override bool IsMultiStep => true;

        public override bool RequiresSelection => true;

        public override async Task Initialize(ICommandContext context, CommandArgs? args = null)
        {
            await base.Initialize(context, args);
        }

        public override async Task Execute()
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            if (viewModel == null)
            {
                Context?.OutputMessage(OpenCADStrings.UnableToAccessViewport);
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            SelectedObjects = await GetSelection(viewModel);

            if (SelectedObjects.Count == 0)
            {
                Context?.OutputMessage(NoObjectsMessage);
                return;
            }

            await OnObjectsSelected();
        }

        /// <summary>
        /// Awaits the user's object selection. Returns immediately with pre-selected objects,
        /// or suspends until the user presses Enter to confirm a new selection.
        /// </summary>
        private async Task<List<OpenCADObject>> GetSelection(ViewportViewModel viewModel)
        {
            if (viewModel.SelectedObjects.Count > 0)
                return new List<OpenCADObject>(viewModel.SelectedObjects);

            Context?.OutputMessage(SelectObjectsMessage);

            _selectionTcs = new TaskCompletionSource<List<OpenCADObject>>();
            var capturedTcs = _selectionTcs;
            _cancellationTokenSource!.Token.Register(() =>
                capturedTcs.TrySetResult(new List<OpenCADObject>()));

            int lastCount = 0;

            void onSelectionChanged(object? sender, EventArgs e)
            {
                var vm = Context?.GetActiveViewportViewModel();
                if (vm == null) return;
                int count = vm.SelectedObjects.Count;
                if (count != lastCount)
                {
                    Context?.OutputMessage($"Selected {count} object(s). Press ENTER to continue, or continue selecting objects.");
                    lastCount = count;
                }
            }

            Context?.PostToUI(() => viewModel.SelectionChanged += onSelectionChanged);

            try
            {
                return await _selectionTcs.Task.ConfigureAwait(false);
            }
            finally
            {
                Context?.PostToUI(() => viewModel.SelectionChanged -= onSelectionChanged);
                _selectionTcs = null;
            }
        }

        public override bool ProcessInput(string input)
        {
            // Selection phase: Enter confirms the current selection
            if (_selectionTcs != null)
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    var viewModel = Context?.GetActiveViewportViewModel();
                    if (viewModel == null || viewModel.SelectedObjects.Count == 0)
                    {
                        Context?.OutputMessage(NoObjectsMessage);
                        return false;
                    }
                    _selectionTcs.TrySetResult(new List<OpenCADObject>(viewModel.SelectedObjects));
                }
                return false;
            }

            // Point-picking phase: route keyboard input to the active input helper.
            // Do not forward the helper's return value — it signals "input was handled",
            // not "command is complete". Returning IsCommandCompleted matches the pattern
            // used by draw commands and prevents premature CompleteActiveCommand() calls.
            if (_inputHelper != null)
                _inputHelper.ProcessKeyboardInput(input);

            return IsCommandCompleted;
        }

        protected override void PostCommandCleanup()
        {
            base.PostCommandCleanup();
            _selectionTcs?.TrySetResult(new List<OpenCADObject>());
            _selectionTcs = null;
            Context?.PostToUI(() =>
            {
                var viewModel = Context.GetActiveViewportViewModel();
                viewModel?.SelectionManager.ClearSelection();
            });
        }

        /// <summary>
        /// Called when objects have been selected and the command should proceed.
        /// </summary>
        protected virtual async Task OnObjectsSelected()
        {
            if (SelectedObjects == null || SelectedObjects.Count == 0)
            {
                Context?.OutputMessage(NoObjectsMessage);
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();

            // Prompt for base point using shared GetPoint on CommandBase
            BasePoint = Point3D.NotAPoint;
            var result = await GetPoint(BasePointPrompt, null);

            if (result != null && result.Point is OpenCAD.Geometry.Point3D basePoint)
            {
                BasePoint = basePoint;
            }
            else
            {
                return;
            }

            // Start unified preview support (now provided by CommandBase)
            BeginPreview();

            try
            {
                do
                {
                    // Prompt for target point using shared GetPoint on CommandBase
                    result = await GetPoint(TargetPointPrompt);
                    if (result != null && result.Point is OpenCAD.Geometry.Point3D targetPoint)
                    {
                        TargetPoint = targetPoint;
                    }

                    else
                    {
                        return;
                    }

                    // Perform the transformation WHILE cached providers are still available
                    TransformSelectedObjects();
                } while (_isRepeatable);
            }
            finally
            {
                // Preview is no longer needed — the undo action committed the real objects
                CancelPreview();
            }
        }

        protected abstract Matrix4D GetTransformation();

        protected override IEnumerable<OpenCADObject> ComputePreviewObjects()
        {
            if (SelectedObjects == null || BasePoint == Point3D.NotAPoint || TargetPoint == Point3D.NotAPoint)
                yield break;

            var matrix = GetTransformation(); // based on BasePoint/TargetPoint

            foreach (var obj in SelectedObjects)
            {
                if (obj is ICurve curve)
                {
                    var preview = curve.Transform(matrix) as OpenCADObject;
                    if (preview != null)
                        yield return preview;
                }
            }
        }

        public bool TryGetTransformation(out Matrix4D transformation)
        {
            if (BasePoint == Point3D.NotAPoint || TargetPoint == Point3D.NotAPoint)
            {
                transformation = Matrix4D.Identity;
                return false;
            }

            transformation = GetTransformation();
            return true;
        }

        protected virtual void TransformSelectedObjects()
        {
            var matrix = GetTransformation();
            if (matrix == Matrix4D.Identity)
                return;

            if (Document == null || viewport == null)
            {
                Context?.OutputMessage(UnableToActOnObjectsMissingContext);
                return;
            }

            var undo = CachedUndoManager;
            if (undo == null)
                return;

            if (_preserveOriginal)
            {
                // Copy: add a transformed clone of each object as a new document object
                int copied = 0;
                foreach (var obj in SelectedObjects!)
                {
                    if (obj is ICurve curve)
                    {
                        var transformed = curve.Transform(matrix) as OpenCADObject;
                        if (transformed != null)
                        {
                            undo.ExecuteAction(new AddGeometryAction(transformed, $"{GetCommandName(true)}"));
                            copied++;
                        }
                    }
                    else
                    {
                        Context?.OutputMessage($"Cannot copy object of type {obj.GetType().Name}");
                    }
                }
                Context?.OutputMessage($"{copied} object(s) copied");
                return;
            }

            // Move/rotate/scale: replace each original with its transformed version
            int transformed_count = 0;
            foreach (var obj in SelectedObjects!)
            {
                if (obj is ICurve curve)
                {
                    var transformed = curve.Transform(matrix) as OpenCADObject;
                    if (transformed != null)
                    {
                        undo.ExecuteAction(new ReplaceGeometryAction(obj, transformed,
                            $"{GetCommandName(true)}"));
                        transformed_count++;
                    }
                }
                else
                {
                    Context?.OutputMessage($"Cannot transform object of type {obj.GetType().Name}");
                }
            }

            Context?.OutputMessage($"{transformed_count} object(s) {GetCommandName(pastTense: true)}");
        }

        private object? GetCommandName(bool capitized = false, bool pastTense = false)
        {
            var commandName = capitized ? char.ToUpper(_commandName[0]) + _commandName.Substring(1) : _commandName;
            if (pastTense)
            {
                // Simple past tense conversion (may not be accurate for all commands)
                if (commandName.EndsWith("e"))
                    commandName += "d";
                else
                    commandName += "ed";
                commandName = commandName.Replace("y", "i");
            }
            return commandName;
        }
    }
}
