using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using UI.Commands.Editing;
using UI.Commands.InputHelpers;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands
{
    /// <summary>
    /// Base class for all commands
    /// </summary>
    public abstract class CommandBase : IInputCommand
    {
        protected CancellationTokenSource? _cancellationTokenSource;

        protected ICommandContext? Context { get; private set; }
        private string _currentPrompt = string.Empty;

        // Shared PointInputHelper instance so ProcessInput can route keyboard input correctly.
        protected IInputHelper? _inputHelper;

        public virtual bool IsMultiStep => false;
        
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
        }

        public abstract Task Execute();

        public virtual bool ProcessInput(string input)
        {
            // Route keyboard input to shared point helper if present
            if (_inputHelper != null)
                return _inputHelper.ProcessKeyboardInput(input);

            return true; // Single-step commands complete immediately
        }

        public virtual void Cancel()
        {
            Context?.OutputMessage("Command cancelled.");
            CurrentPrompt = string.Empty;

            // Ensure preview stopped if any command cancels
            StopPreview(false);
        }

        /// <summary>
        /// Raise the CommandCompleted event
        /// </summary>
        protected void RaiseCommandCompleted()
        {
            CommandCompletedEvent?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Helper method to parse a point from input
        /// </summary>
        protected OpenCAD.Geometry.Point3D? ParsePoint(string input)
        {
            string[] parts = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 3)
                return null;

            try
            {
                double x = double.Parse(parts[0]);
                double y = double.Parse(parts[1]);
                double z = double.Parse(parts[2]);
                return new OpenCAD.Geometry.Point3D(x, y, z);
            }
            catch (FormatException)
            {
                return null;
            }
        }

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
                return await ((GetPointInput)_inputHelper).GetPointOrKeywordAsync(
                    prompt,
                    defaultValue: defaultValue,
                    allowLastPoint: allowLastPoint,
                    basePoint: BasePoint,
                    keywords: keyWords,
                    cancellationToken: _cancellationTokenSource.Token);
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

            _inputHelper = new GetDistanceInput(Context, viewModel, this);

            try
            {
                return await ((GetDistanceInput)_inputHelper).GetDistance(
                    prompt,
                    defaultValue: defaultValue,
                    allowLastPoint: allowLastPoint,
                    basePoint: BasePoint,
                    keyWords: keyWords,
                    cancellationToken: _cancellationTokenSource.Token);
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
                return await ((GetAngleInput)_inputHelper).GetAngle(
                    prompt,
                    defaultValue: defaultValue,
                    allowLastPoint: allowLastPoint,
                    basePoint: BasePoint,
                    keyWords: keyWords,
                    cancellationToken: _cancellationTokenSource.Token);
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

        #region Preview support (shared)

        // Objects that should be previewed (derived classes may populate this)
        protected List<OpenCADObject>? SelectedObjects;

        // Base/target points used by preview; derived classes should set BasePoint before StartPreview.
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
        /// Start preview mode using the current BasePoint.
        /// Subscribes to ViewportViewModel.PreviewPoint changes and shows translated clones.
        /// Captures providers (viewport/document/undo manager) to avoid them becoming null after awaits.
        /// </summary>
        public void StartPreview()
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            if (viewModel == null || Context == null) return;

            // Capture providers and subscribe on UI thread
            Context.PostToUI(() =>
            {
                var viewport = Context.GetActiveViewport();
                if (viewport == null) return;

                // Cache providers immediately so awaiting user input cannot lose them
                this.viewport = viewport;
                document = Context.GetDocument();
                CachedUndoManager = Context.GetUndoRedoManager();
                CachedViewModel = viewModel;

                // Attach handler to respond to PreviewPoint changes
                _previewHandler = (sender, e) =>
                {
                    if (e.PropertyName == nameof(ViewportViewModel.PreviewPoint))
                    {
                        var previewPoint = CachedViewModel?.PreviewPoint;
                        if (previewPoint != null && BasePoint != null)
                        {
                            TargetPoint = previewPoint;
                            UpdatePreviewObjects(this.viewport!);
                        }
                        else
                        {
                            ClearPreviewObjects(this.viewport!);
                        }
                    }
                };

                CachedViewModel.PropertyChanged += _previewHandler;
            });
        }

        /// <summary>
        /// Stop preview mode and remove any preview clones.
        /// Clears cached providers.
        /// If completeCommand is true, CommandCompleted() is called at the end (preserves previous behavior).
        /// </summary>
        public void StopPreview(bool completeCommand = true)
        {
            try
            {
                var viewModel = Context?.GetActiveViewportViewModel();
                // Unsubscribe on UI thread using cached view model (safer than re-reading DataContext)
                if (viewModel != null && _previewHandler != null)
                {
                    Context?.PostToUI(() => viewModel.PropertyChanged -= _previewHandler);
                }
            }
            catch
            {
                // ignore cleanup errors
            }

            if (viewport != null)
                ClearPreviewObjects(viewport);

            _previewHandler = null;

            // Clear cached provider references
            viewport = null;
            document = null;
            CachedUndoManager = null;
            CachedViewModel = null;

            BasePoint = null;
            TargetPoint = null;

            if (completeCommand)
                CommandCompleted();
        }

        /// <summary>
        /// Update preview clones using the given translation matrix computed by derived class.
        /// Existing preview clones are removed and replaced.
        /// Derived classes should implement ComputePreviewTransformation() (or set TargetPoint and rely on CreateTranslatedClone/override).
        /// </summary>
        private void UpdatePreviewObjects(ViewportControl viewport)
        {
            if (viewport == null) return;

            // Run on UI thread
            Context?.PostToUI(() =>
            {
                // Clear existing preview objects
                viewport.ClearPreviewObjects();

                var document = this.document ?? viewport.Document;
                if (document == null || SelectedObjects == null)
                    return;

                Matrix4D transformation = GetPreviewTransformation();

                foreach (var obj in SelectedObjects)
                {
                    var clone = CreateTranslatedClone(obj, transformation, document);
                    if (clone != null)
                    {
                        viewport.AddPreviewObject(clone);
                    }
                }
            });
        }

        protected void ClearPreviewObjects(ViewportControl viewport)
        {
            if (_previewObjects.Count == 0)
                return;

            Context?.PostToUI(() =>
            {
                viewport?.ClearPreviewObjects();
            });
        }

        /// <summary>
        /// Remove preview clones previously added to the current preview viewport,
        /// but keep preview mode active (do not unsubscribe handlers or clear cached providers).
        /// Useful when a command wants to commit a copy/move while continuing point picking.
        /// </summary>
        protected void ClearPreviewClones()
        {
            try
            {
                if (viewport != null)
                    ClearPreviewObjects(viewport);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Create a translated clone for a given source object.
        /// Default implementation supports GeometryBase; override to support more types.
        /// </summary>
        protected virtual OpenCADObject? CreateTranslatedClone(OpenCADObject source, Matrix4D translation, OpenCADDocument document)
        {
            try
            {
                if (source is GeometryBase geom)
                {
                    var clone = geom.Clone(document) as GeometryBase;
                    if (clone != null)
                    {
                        clone.Transform(translation);
                        return clone;
                    }
                }
            }
            catch (NotSupportedException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Preview clone failed for {source.GetType().Name}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error creating preview clone: {ex.Message}");
                return null;
            }

            return null;
        }

        /// <summary>
        /// Method used by the preview update to compute the transformation matrix.
        /// Default uses BasePoint and TargetPoint and attempts translation; derived classes that
        /// require different transforms should override this.
        /// </summary>
        protected virtual Matrix4D GetPreviewTransformation()
        {
            if (this is EditCommandBase editCommand)
            {
                if (editCommand.TryGetTransformation(out Matrix4D transformation))
                {
                    return transformation;
                }
            }

            return Matrix4D.Identity; // Identity fallback
        }

        /// <summary>
        /// Call this to finish the command: ensure point picking disabled and raise completion.
        /// </summary>
        protected void CommandCompleted()
        {
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
    }
}