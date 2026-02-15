using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using OpenCAD.Undo;
using OpenCAD.Undo.OpenCAD.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Editing.Fillet
{
    [InputCommand("fillet", "connect objects with an arc", "fi")]
    public sealed class FilletCommand : EditCommandBase
    {
        private FilletState _state = new();
        private readonly FilletSolver _solver = new();
        private UndoRedoManager? _undoRedoManager;

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
            _commandName = OpenCADStrings.FilletCommandName;
            var doc = context.GetDocument();
            if (doc != null)
            {
                _state.Trim = doc.GetViewportSettings()?.TrimMode ?? true;
                _state.Radius = doc.GetViewportSettings()?.FilletRadius ?? 1.0;
                _solver.Document = doc;
            }
            _undoRedoManager = context.GetUndoRedoManager() ?? throw new InvalidOperationException("UndoRedoManager is required for FilletCommand.");
        }

        public override async Task Execute()
        {
            try
            {
                // --- STEP 1: Prompt for radius ------------------------------------
                await PromptRadius();

                // --- STEP 2: Select first object ----------------------------------
                await SelectFirstObject();

                // --- STEP 3: Hover second object (dynamic preview) ----------------
                await HoverSecondObject();

                // --- STEP 4: Select second object ---------------------------------
                await SelectSecondObject();

                // --- STEP 5: Commit fillet ----------------------------------------
                CommitFillet();

                RaiseCommandCompleted();
            }
            catch (OperationCanceledException)
            {
                Cancel();
            }
        }

        // -------------------------------------------------------------------------
        // STEP 1: RADIUS
        // -------------------------------------------------------------------------
        private async Task PromptRadius()
        {
            _state.Step = FilletStep.PromptRadius;
            BasePoint = Point3D.NotAPoint;

            var input = await GetDistance(
                prompt: $"Specify fillet radius",
                _state.Radius);

            if (input.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                throw new OperationCanceledException();

            if (input.ResultType == InputHelpers.InputResult.InputResultType.Double)
            {
                _state.Radius = input.DoubleValue;
                Context.GetDocument().GetViewportSettings().FilletRadius = _state.Radius;
            }

            // If user presses Enter, keep existing radius
        }

        // -------------------------------------------------------------------------
        // STEP 2: SELECT FIRST OBJECT
        // -------------------------------------------------------------------------
        private async Task SelectFirstObject()
        {
            _state.Step = FilletStep.SelectFirstObject;

            var input = await GetEntity("Select first object:");

            if (input.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                throw new OperationCanceledException();

            _state.FirstObject = input.Object;
            _state.FirstPickPoint = input.Point;
        }

        // -------------------------------------------------------------------------
        // STEP 3: HOVER SECOND OBJECT (dynamic preview)
        // -------------------------------------------------------------------------
        private async Task HoverSecondObject()
        {
            _state.Step = FilletStep.HoverSecondObject;

            while (true)
            {
                var input = await GetEntity(
                    prompt: "Select second object:",
                    allowHover: true);

                if (input.ResultType == InputHelpers.InputResult.InputResultType.None)
                {
                    // This can happen if user moves mouse without hovering over anything
                    _state.HoverObject = null;
                    UpdatePreview();
                    continue;
                }

                if (input.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                    throw new OperationCanceledException();

                if (input.ResultType == InputHelpers.InputResult.InputResultType.Hover)
                {
                    _state.HoverObject = input.Object;
                    _state.SecondPickPoint = input.Point;

                    UpdatePreview();
                    continue;
                }

                // If it's a real selection, break and handle in next step
                _state.SecondObject = input.Object;
                _state.SecondPickPoint = input.Point;
                break;
            }
        }

        // -------------------------------------------------------------------------
        // STEP 4: SELECT SECOND OBJECT (final)
        // -------------------------------------------------------------------------
        private async Task SelectSecondObject()
        {
            _state.Step = FilletStep.SelectSecondObject;

            if (_state.SecondObject == null)
            {
                var input = await GetEntity("Select second object:");

                if (input.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                    throw new OperationCanceledException();

                _state.SecondObject = input.Object;
                _state.SecondPickPoint = input.Point;
            }

            // Final preview update before commit
            UpdatePreview();
        }

        // -------------------------------------------------------------------------
        // PREVIEW
        // -------------------------------------------------------------------------
        private void UpdatePreview()
        {
            if (_state.FirstObject == null || _state.HoverObject == null)
            {
                PreviewManager.Clear();
                return;
            }

            if (_solver.TrySolve(
                    _state.FirstObject as ICurve,
                    _state.HoverObject as ICurve,
                    _state.FirstPickPoint.Value,
                    _state.SecondPickPoint.Value,
                    _state.Radius,
                    out var solution))
            {
                _state.HasPreview = true;

                PreviewManager.ShowPreview(solution.FilletArc);
                PreviewManager.ShowPreview(solution.TrimmedA as OpenCADObject);
                PreviewManager.ShowPreview(solution.TrimmedB as OpenCADObject);
            }
            else
            {
                _state.HasPreview = false;
                PreviewManager.Clear();
            }
        }

        // -------------------------------------------------------------------------
        // STEP 5: COMMIT
        // -------------------------------------------------------------------------
        private void CommitFillet()
        {
            if (_state.FirstObject == null ||
                _state.SecondObject == null ||
                !_state.HasPreview)
            {
                Context.OutputMessage("No valid fillet solution.");
                return;
            }

            if (!_solver.TrySolve(
                    _state.FirstObject as ICurve,
                    _state.SecondObject as ICurve,
                    _state.FirstPickPoint.Value,
                    _state.SecondPickPoint.Value,
                    _state.Radius,
                    out var solution))
            {
                Context.OutputMessage("Unable to compute fillet.");
                return;
            }

            var tx = _undoRedoManager.BeginTransaction("Fillet");

            // Add trimmed curves
            if (_state.Trim)
            {
                var trimmedAAction = new ReplaceGeometryAction(_state.FirstObject, solution.TrimmedA as OpenCADObject, OpenCADStrings.FilletCommandName);
                var trimmedBAction = new ReplaceGeometryAction(_state.SecondObject, solution.TrimmedB as OpenCADObject, OpenCADStrings.FilletCommandName);
                tx.AddAction(trimmedAAction);
                tx.AddAction(trimmedBAction);
            }
            else
            {
                // NoTrim: keep originals, add arc only
                var firstObjectAction = new AddGeometryAction(_state.FirstObject, OpenCADStrings.FilletCommandName);
                var secondObjectAction = new AddGeometryAction(_state.SecondObject, OpenCADStrings.FilletCommandName);
                tx.AddAction(firstObjectAction);
                tx.AddAction(secondObjectAction);
            }

            // Add fillet arc
            var filletArcAction = new AddGeometryAction(solution.FilletArc, OpenCADStrings.FilletCommandName);
            tx.AddAction(filletArcAction);

            _undoRedoManager.CommitTransaction(true);

            PreviewManager.Clear();
        }

        protected override Matrix4D GetTransformation()
        {
            throw new NotImplementedException();
        }
    }
}
