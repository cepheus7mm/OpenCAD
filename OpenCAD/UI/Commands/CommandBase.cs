using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using OpenCAD.Undo;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Shapes;
using UI.Commands.Editing;
using UI.Commands.InputHelpers;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands
{
    /// <summary>
    /// Base class for all commands
    /// </summary>
    public abstract class CommandBase : IInputCommand
    {
        protected CancellationTokenSource? _cancellationTokenSource;
        protected UndoTransaction? _undoTransaction;
        protected IPreviewManager? PreviewManager => Context?.GetActiveViewportViewModel()?.PreviewManager;

        protected ICommandContext? Context { get; private set; }
        private string _currentPrompt = string.Empty;

        // Shared PointInputHelper instance so ProcessInput can route keyboard input correctly.
        protected IInputHelper? _inputHelper;

        public virtual bool IsMultiStep => false;

        public virtual bool IsCommandCompleted { get; protected set; } = false;

        /// <summary>
        /// Gets whether this command requires selection mode to be enabled.
        /// Override in derived classes for editing commands like Erase, Move, Copy, etc.
        /// </summary>
        public virtual bool RequiresSelection => false;

        public virtual string CurrentPrompt 
        { 
            get => _currentPrompt;
            protected set 
            { 
                if (_currentPrompt != value)
                {
                    _currentPrompt = value;
                    PromptChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? PromptChanged;
        public event EventHandler? CommandCompletedEvent;

        public virtual async Task Initialize(ICommandContext context)
        {
            Context = context;
            if (Context != null && Context.GetActiveViewportViewModel() != null)
            {
                Context!.GetActiveViewportViewModel()!.PropertyChanged += _previewHandler = (s, e) =>
                {
                    if (e.PropertyName == nameof(ViewportViewModel.PreviewPoint))
                    {
                        var point = context?.GetActiveViewportViewModel()?.PreviewPoint;
                        if (point.HasValue)
                        {
                            TargetPoint = point.Value;
                            UpdatePreview();
                        }
                    }
                };
            }
        }

        public abstract Task Execute();

        public virtual bool ProcessInput(string input)
        {
            // Route keyboard input to shared point helper if present
            if (_inputHelper != null)
            {
                _inputHelper.ProcessKeyboardInput(input);

                return IsCommandCompleted;
            }

            return true; // Single-step commands complete immediately
        }

        public virtual void Cancel()
        {
            Context?.OutputMessage("Command cancelled.");
            CurrentPrompt = string.Empty;

            // Ensure preview stopped if any command cancels
            CancelPreview();
        }

        /// <summary>
        /// Raise the CommandCompleted event
        /// </summary>
        protected void RaiseCommandCompleted()
        {
            CommandCompletedEvent?.Invoke(this, EventArgs.Empty);
        }

        ///// <summary>
        ///// Helper method to parse a point from input
        ///// </summary>
        //protected OpenCAD.Geometry.Point3D? ParsePoint(string input)
        //{
        //    string[] parts = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

        //    if (parts.Length != 3)
        //        return null;

        //    try
        //    {
        //        double x = double.Parse(parts[0]);
        //        double y = double.Parse(parts[1]);
        //        double z = double.Parse(parts[2]);
        //        return new OpenCAD.Geometry.Point3D(x, y, z);
        //    }
        //    catch (FormatException)
        //    {
        //        return null;
        //    }
        //}

        /// <summary>
        /// Asynchronously prompt the user for a point. Returns the selected point, or null if the
        /// operation was cancelled, a keyword was entered (keywordHandler will be invoked), or an invalid value was provided.
        /// Uses PointInputHelper.GetPointOrKeywordAsync under the hood and respects this command's
        /// cancellation token source (_cancellationTokenSource).
        /// </summary>
        /// <param name="prompt">The prompt to display to the user</param>
        /// <param name="defaultValue">Optional default point value (shown in angle brackets, accepted with Enter/Space)</param>
        /// <param name="keyWords">Optional array of keywords to recognize</param>
        /// <param name="allowLastPoint">Whether to allow using the last entered point</param>
        protected async Task<InputResult> GetPoint(
            string prompt,
            OpenCAD.Geometry.Point3D? defaultValue = null, 
            string[]? keyWords = null, 
            bool allowLastPoint = false)
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            var badResult = new InputResult() { ResultType = InputResult.InputResultType.None, Point = null };
            if (viewModel == null || Context == null)
                return badResult;

            // Ensure we have a cancellation token source for this command
            _cancellationTokenSource ??= new CancellationTokenSource();

            // Use shared helper if available, otherwise create a temporary one
            _inputHelper = new GetPointInput(Context, viewModel);

            CurrentPrompt = prompt;
            try
            {
                return await ((GetPointInput)_inputHelper).GetPointOrKeywordAsync(new InputParams
                {
                    Prompt = prompt,
                    DefaultValue = defaultValue,
                    AllowLastPoint = allowLastPoint,
                    AllowArbitraryInput = BasePoint.IsValid, //if the base point is valid, allow arbitrary input for relative coordinates
                    BasePoint = BasePoint,
                    Keywords = keyWords,
                    CancellationToken = _cancellationTokenSource.Token
                });
            }
            catch (OperationCanceledException)
            {
                return badResult;
            }
            finally
            {
                CurrentPrompt = string.Empty;
            }
        }

        protected async Task<InputResult> GetPoint(InputParams inputParams)
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            var badResult = new InputResult() { ResultType = InputResult.InputResultType.None, Point = null };
            if (viewModel == null || Context == null)
                return badResult;
            // Ensure we have a cancellation token source for this command
            _cancellationTokenSource ??= new CancellationTokenSource();
            // Use shared helper if available, otherwise create a temporary one
            _inputHelper = new GetPointInput(Context, viewModel);
            CurrentPrompt = inputParams.Prompt;
            try
            {
                return await ((GetPointInput)_inputHelper).GetPointOrKeywordAsync(inputParams);
            }
            catch (OperationCanceledException)
            {
                return badResult;
            }
            finally
            {
                CurrentPrompt = string.Empty;
            }
        }

        /// <summary>
        /// Get a distance value from the user.
        /// Supports numeric input, unit-aware parsing via document settings, or a two-point entry.
        /// Returns double.NaN on cancel/invalid input.
        /// </summary>
        /// <param name="prompt">The prompt to display to the user</param>
        /// <param name="defaultValue">Optional default distance value (shown in angle brackets, accepted with Enter/Space)</param>
        /// <param name="keyWords">Optional array of keywords to recognize</param>
        /// <param name="allowLastPoint">Whether to allow using the last entered point</param>
        protected async Task<InputResult> GetDistance(
            string prompt, 
            double? defaultValue = null, 
            string[]? keyWords = null, 
            bool allowLastPoint = false)
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            var badResult = new InputResult() { ResultType = InputResult.InputResultType.None, DoubleValue = double.NaN };
            if (viewModel == null || Context == null)
                return badResult;

            _cancellationTokenSource ??= new CancellationTokenSource();

            _inputHelper = new GetDistanceInput(Context, viewModel);

            try
            {
                return await ((GetDistanceInput)_inputHelper).GetDistance(new InputParams
                {
                    Prompt = prompt,
                    DefaultValue = defaultValue,
                    AllowLastPoint = allowLastPoint,
                    AllowArbitraryInput = true,
                    BasePoint = BasePoint,
                    Keywords = keyWords,
                    CancellationToken = _cancellationTokenSource.Token
                });
            }
            catch (OperationCanceledException)
            {
                return badResult;
            }
            finally
            {
                CurrentPrompt = string.Empty;
            }
        }

        /// <summary>
        /// Get an angle value. Returns angle in radians, or double.NaN on cancel/invalid input.
        /// Supports numeric/keyword input (unit-aware) or a single point pick (angle from BasePoint to picked point).
        /// Caller should set BasePoint before calling if using point picks.
        /// </summary>
        /// <param name="prompt">The prompt to display to the user</param>
        /// <param name="defaultValue">Optional default angle value in radians (shown in angle brackets, accepted with Enter/Space)</param>
        /// <param name="keyWords">Optional array of keywords to recognize</param>
        /// <param name="allowLastPoint">Whether to allow using the last entered point</param>
        protected async Task<InputResult> GetAngle(
            string prompt, 
            double? defaultValue = null, 
            string[]? keyWords = null, 
            bool allowLastPoint = false)
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            var badResult = new InputResult() { ResultType = InputResult.InputResultType.None, DoubleValue = double.NaN };
            if (viewModel == null || Context == null)
                return badResult;

            _cancellationTokenSource ??= new CancellationTokenSource();

            _inputHelper = new GetAngleInput(Context, viewModel);

            try
            {
                return await ((GetAngleInput)_inputHelper).GetAngle(new InputParams
                {
                    Prompt = prompt,
                    DefaultValue = defaultValue,
                    AllowLastPoint = allowLastPoint,
                    AllowArbitraryInput = true,
                    BasePoint = BasePoint,
                    Keywords = keyWords,
                    CancellationToken = _cancellationTokenSource.Token
                });
            }
            catch (OperationCanceledException)
            {
                return badResult;
            }
            finally
            {
                CurrentPrompt = string.Empty;
            }
        }

        /// <summary>
        /// Prompt for a string (keyword or arbitrary text). Returns an InputResult with Keyword set
        /// and ResultType indicating Keyword or Arbitrary. Returns a ResultType of None on cancel/failure.
        /// </summary>
        /// <param name="prompt">The prompt to display to the user</param>
        /// <param name="defaultValue">Optional default string value (shown in angle brackets, accepted with Enter/Space)</param>
        /// <param name="keyWords">Optional array of keywords to recognize</param>
        /// <param name="allowArbitrary">Whether to allow arbitrary text input (not just keywords)</param>
        protected async Task<InputResult> GetString(
            string prompt, 
            string? defaultValue = null, 
            string[]? keyWords = null, 
            bool allowArbitrary = true)
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            var badResult = new InputResult() { ResultType = InputResult.InputResultType.None, Keyword = null };
            if (viewModel == null || Context == null)
                return badResult;

            _cancellationTokenSource ??= new CancellationTokenSource();

            _inputHelper = new GetStringInput(Context, viewModel);

            CurrentPrompt = prompt;
            try
            {
                var res = await ((GetStringInput)_inputHelper).GetStringAsync(
                    prompt,
                    allowArbitrary: allowArbitrary,
                    keywords: keyWords,
                    cancellationToken: _cancellationTokenSource.Token);

                if (res == null)
                    return badResult;

                return res;
            }
            catch (OperationCanceledException)
            {
                return badResult;
            }
            finally
            {
                CurrentPrompt = string.Empty;
            }
        }

        /// <summary>
        /// Prompt for a string with preview callback support.
        /// </summary>
        protected async Task<InputResult?> GetString(
            string prompt,
            bool allowArbitrary = true,
            string[]? keywords = null,
            Action<string>? previewCallback = null,
            string? defaultValue = null)
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            var badResult = new InputResult() { ResultType = InputResult.InputResultType.None, Keyword = null };
            if (viewModel == null || Context == null)
                return badResult;

            _cancellationTokenSource ??= new CancellationTokenSource();
            _inputHelper = new GetStringInput(Context, viewModel);
            if (_inputHelper is GetStringInput stringInput)
            {
                CurrentPrompt = prompt;
                return await stringInput.GetStringAsync(
                    prompt,
                    allowArbitrary,
                    keywords,
                    previewCallback,
                    _cancellationTokenSource?.Token ?? default);
            }

            throw new InvalidOperationException("Current input helper is not GetStringInput");
        }

        protected Task<InputResult> GetEntity(string prompt, bool allowHover = false)
        {
            if (Context == null)
                throw new InvalidOperationException("No command context available.");

            var viewport = Context.GetActiveViewportViewModel()
                ?? throw new InvalidOperationException("No active viewport viewmodel available.");

            Context.SetCommandPrompt(prompt);

            var tcs = new TaskCompletionSource<InputResult>();
            InputResult result = new();

            EventHandler<ObjectSelectedEventArgs>? clickHandler = null;
            EventHandler<ObjectHoverEventArgs>? hoverHandler = null;

            // --- CLICK HANDLER ----------------------------------------------------
            clickHandler = (s, e) =>
            {
                viewport.ObjectSelected -= clickHandler;
                if (allowHover)
                    viewport.ObjectHovered -= hoverHandler;

                result.ResultType = InputResult.InputResultType.ObjectAndPoint;
                result.Object = e.Object;
                result.Point = e.PickedPoint;

                tcs.TrySetResult(result);
            };

            // --- HOVER HANDLER ----------------------------------------------------
            if (allowHover)
            {
                hoverHandler = (s, e) =>
                {
                    if (e.Object != null && e.PickedPoint != Point3D.NotAPoint)
                    {
                        result.ResultType = InputResult.InputResultType.Hover;
                        result.Object = e.Object;
                        result.Point = e.PickedPoint; 
                    }
                    else
                    {
                        result.ResultType = InputResult.InputResultType.None;
                    }

                        tcs.TrySetResult(result);
                };

                viewport.ObjectHovered += hoverHandler;
            }

            viewport.ObjectSelected += clickHandler;

            return tcs.Task;
        }

        #region Preview support (shared)

        // Objects that should be previewed (derived classes may populate this)
        protected List<OpenCADObject>? SelectedObjects;

        // Base/target points used by preview; derived classes should set BasePoint before BeginPreview.
        protected OpenCAD.Geometry.Point3D BasePoint { get; set; } = OpenCAD.Geometry.Point3D.Origin;
        protected OpenCAD.Geometry.Point3D TargetPoint { get; set; } = OpenCAD.Geometry.Point3D.Origin;

        // Preview fields
        private readonly List<OpenCADObject> _previewObjects = new();
        protected PropertyChangedEventHandler? _previewHandler;

        // Cached providers captured when preview starts (so awaits won't lose them)
        protected ViewportControl? viewport { get; private set; }
        protected OpenCADDocument? document { get; private set; }
        protected UndoRedoManager? CachedUndoManager { get; private set; }

        protected ViewportViewModel? CachedViewModel { get; private set; }

        /// <summary>
        /// Called by derived commands when they are ready to start showing preview
        /// (e.g. after BasePoint is set).
        /// </summary>
        public void BeginPreview()
        {
            PreviewManager?.Begin();
        }

        /// <summary>
        /// Called whenever the preview needs to be updated (e.g. PreviewPoint changed).
        /// Derived classes override ComputePreviewObjects to define what to show.
        /// </summary>
        protected void UpdatePreview()
        {
            var pm = PreviewManager;
            if (pm == null)
                return;

            pm.Clear();

            foreach (var obj in ComputePreviewObjects())
                pm.ShowPreview(obj);
        }

        /// <summary>
        /// Called when the command is canceled.
        /// Restores originals and removes preview objects.
        /// </summary>
        protected void CancelPreview()
        {
            PreviewManager?.Clear();
        }

        /// <summary>
        /// Called when the command successfully completes.
        /// Keeps preview objects visible; originals are expected to be
        /// replaced/removed by the undo actions in the commit phase.
        /// </summary>
        public void CommitPreview()
        {
            PreviewManager?.Commit();
        }

        /// <summary>
        /// Derived commands return the current preview geometry here,
        /// based on BasePoint/TargetPoint or other state.
        /// </summary>
        protected virtual IEnumerable<OpenCADObject> ComputePreviewObjects()
        {
            yield break;
        }
        ///// <summary>
        ///// Create a translated clone for a given source object.
        ///// Default implementation supports GeometryBase; override to support more types.
        ///// </summary>
        //protected virtual OpenCADObject? CreateTranslatedClone(OpenCADObject source, Matrix4D translation, OpenCADDocument document)
        //{
        //    try
        //    {
        //        if (source is ICurve curve)
        //        {
        //            return curve.Transform(translation) as OpenCADObject;
        //        }
        //    }
        //    catch (NotSupportedException ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"Preview clone failed for {source.GetType().Name}: {ex.Message}");
        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"Unexpected error creating preview clone: {ex.Message}");
        //        return null;
        //    }

        //    return null;
        //}

        /// <summary>
        /// Method used by the preview update to compute the transformation matrix.
        /// Default uses BasePoint and TargetPoint and attempts translation; derived classes that
        /// require different transforms should override this.
        /// </summary>
        //protected virtual Matrix4D GetPreviewTransformation()
        //{
        //    if (this is EditCommandBase editCommand)
        //    {
        //        if (editCommand.TryGetTransformation(out Matrix4D transformation))
        //        {
        //            return transformation;
        //        }
        //    }

        //    return Matrix4D.Identity; // Identity fallback
        //}

        /// <summary>
        /// Call this to finish the command: ensure point picking disabled and raise completion.
        /// </summary>
        protected void CommandCompleted()
        {
            CancelPreview();
            // Ensure point picking fully disabled
            var viewModel = Context?.GetActiveViewportViewModel();
            if (viewModel != null && viewModel.IsPointPickingMode)
            {
                // Disable on UI thread
                Context?.PostToUI(() => viewModel.DisablePointPickingMode());
            }
            RaiseCommandCompleted();
            System.Diagnostics.Debug.WriteLine("CommandBase: Raised Command Completed");
        }

        /// <summary>
        /// Notifies the input helper about text changes (for preview purposes).
        /// Used by the command input system to support live previews while typing.
        /// </summary>
        public void NotifyInputHelperTextChanged(string currentText)
        {
            if (_inputHelper is GetStringInput stringInput)
            {
                stringInput.OnTextChanged(currentText);
            }
        }
        #endregion

        public virtual void HandleObjectClick(OpenCADObject obj, Point3D worldPoint) { }

        protected virtual string GetUndoCreateString(OpenCADObject obj)
        {
            return string.Format(
                "UndoCreateObject",
                obj.GetType().Name);
        }

        protected void CreateObject(OpenCADObject obj)
        {
            var document = Context?.GetDocument() ?? throw new InvalidOperationException("No active document.");

            var undoManager = Context?.GetUndoRedoManager();
            var createString = GetUndoCreateString(obj);
            // If undo available, the action may need a UI-thread viewport reference; capture + execute on UI thread
            if (undoManager != null)
            {
                Context?.PostToUI(() =>
                {
                    var viewport = Context.GetActiveViewport();
                    var action = new OpenCAD.Undo.AddGeometryAction(
                        obj,
                        createString
                    );
                    undoManager.ExecuteAction(action);
                });
            }
            else
            {
                // CommandContext.RaiseGeometryCreated already posts to UI (our implementation does), so safe to call directly
                Context?.RaiseGeometryCreated(obj);
            }

            Context?.OutputMessage(createString);
        }
    }
}