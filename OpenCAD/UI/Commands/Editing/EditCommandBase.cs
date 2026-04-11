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
        protected enum InputMode
        {
            ObjectSelection,
            PointInput
        }
        protected InputMode _currentInputMode = InputMode.ObjectSelection;

        private bool _needsSelection = false;
        private int _initialSelectionCount = 0;
        protected string _commandName = string.Empty;
        protected bool _preserveOriginal = false;
        protected bool _isRepeatable = false;

        public string SelectObjectsPrompt => string.Format(OpenCADStrings.SelectObjectsToActOnPrompt, _commandName);

        public string SelectObjectsMessage => string.Format(OpenCADStrings.SelectObjectsToActOnMessage, _commandName);

        public string NoObjectsMessage => OpenCADStrings.NoObjectsSelectedToActOnCancelled;

        public string UnableToActOnObjectsMissingContext => OpenCADStrings.UnableToActOnObjectsMissingContext;

        public string NoObjectsSelectedToActOnCancelled => OpenCADStrings.NoObjectsSelectedToActOnCancelled;

        public string BasePointPrompt => OpenCADStrings.BasePointPrompt;

        public string TargetPointPrompt => OpenCADStrings.TargetPointPrompt;

        public string InvalidPointInput => OpenCADStrings.InvalidPointInput;

        public override bool IsMultiStep => _needsSelection;

        public override bool RequiresSelection => true;

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
        }

        public override async Task Execute()
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            if (viewModel == null)
            {
                Context?.OutputMessage(OpenCADStrings.UnableToAccessViewport);
                return;
            }

            if (viewModel.SelectedObjects.Count > 0)
            {
                // Pre-selected objects - proceed immediately
                SelectedObjects = new List<OpenCADObject>(viewModel.SelectedObjects);
                _needsSelection = false;
                await OnObjectsSelected();
                // Derived class will call RaiseCommandCompleted when done
            }
            else
            {
                // No selection - prompt user
                _needsSelection = true;
                _initialSelectionCount = 0;
                //CurrentPrompt = SelectObjectsPrompt;
                Context?.OutputMessage(SelectObjectsMessage);

                // Ensure subscription happens on UI thread
                Context?.PostToUI(() => viewModel.SelectionChanged += OnSelectionChanged);
            }
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            if (viewModel == null) 
                return;


            int count = viewModel.SelectedObjects.Count;
            if (count != _initialSelectionCount)
            {
                Context?.OutputMessage($"Selected {count} object(s). Press ENTER to continue, or continue selecting objects.");
                _initialSelectionCount = count;
            }
        }

        public override bool ProcessInput(string input)
        {
            if (!_needsSelection)
                return base.ProcessInput(input);  // Let derived class handle it

            // User pressed ENTER to confirm selection
            if (string.IsNullOrWhiteSpace(input))
            {

                var viewmodel = Context?.GetActiveViewportViewModel();
                if (viewmodel == null || viewmodel.SelectedObjects.Count == 0)
                {
                    Context?.OutputMessage(NoObjectsMessage);
                    return false;
                }

                _currentInputMode = InputMode.PointInput;

                // Unsubscribe on UI thread and capture selected objects
                Context?.PostToUI(() => viewmodel.SelectionChanged -= OnSelectionChanged);
                SelectedObjects = new List<OpenCADObject>(viewmodel.SelectedObjects);
                _needsSelection = false;

                Task.Run(async () => { await OnObjectsSelected(); });
                return true;
            }

            return false;
        }

        protected override void PostCommandCleanup()
        {
            base.PostCommandCleanup();
            Context?.PostToUI(() =>
            {
                var viewmodel = Context.GetActiveViewportViewModel();
                if (viewmodel != null)
                {
                    viewmodel.SelectionChanged -= OnSelectionChanged;
                    viewmodel.SelectionManager.ClearSelection();
                }
            });
            _needsSelection = false;
            _initialSelectionCount = 0;
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
                // Ensure preview is cleaned up if something goes wrong
                CommitPreview();
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

            // Determine which objects we are transforming
            List<OpenCADObject> objectsToTransform = SelectedObjects!;

            if (_preserveOriginal)
            {
                var clones = new List<OpenCADObject>();
                foreach (var obj in SelectedObjects!)
                {
                    var clone = obj.Clone(Document);
                    if (clone != null)
                    {
                        Document.Add(clone);
                        clones.Add(clone);
                    }
                }
                objectsToTransform = clones;
            }

            // Apply transform using ReplaceGeometryAction
            foreach (var obj in objectsToTransform)
            {
                if (obj is ICurve curve)
                {
                    var transformed = curve.Transform(matrix) as OpenCADObject; // returns new object
                    if (transformed != null)
                    {
                        var action = new ReplaceGeometryAction(obj, transformed,
                            $"Transform {GetCommandName(true)}");

                        undo.ExecuteAction(action);
                    }
                }
                else
                {
                    // Non-curve objects may need their own transform logic
                    Context?.OutputMessage($"Cannot transform object of type {obj.GetType().Name}");
                }
            }

            Context?.OutputMessage(
                $"{objectsToTransform.Count} object(s) transformed by {GetCommandName(true, true)}");
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
