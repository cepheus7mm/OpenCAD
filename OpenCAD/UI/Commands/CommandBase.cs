using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCAD;
using OpenCAD.Geometry;
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
        protected PointInputHelper? _pointInputHelper;

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

        public virtual void Initialize(ICommandContext context)
        {
            Context = context;

            // Create a helper tied to the active viewport/viewmodel so ProcessInput
            // can forward keyboard input to the same helper used by GetPoint/GetDistance/GetAngle.
            var viewport = context.GetActiveViewport();
            var viewModel = viewport?.DataContext as ViewportViewModel;
            if (viewModel != null)
            {
                _pointInputHelper = new PointInputHelper(context, viewModel);
            }
        }

        public abstract Task Execute();

        public virtual bool ProcessInput(string input)
        {
            // Route keyboard input to shared point helper if present
            if (_pointInputHelper != null)
                return _pointInputHelper.ProcessKeyboardInput(input);

            return true; // Single-step commands complete immediately
        }

        public virtual void Cancel()
        {
            Context?.OutputMessage("Command cancelled.");
            CurrentPrompt = string.Empty;

            // Ensure preview stopped if any command cancels
            StopPreview(false);

            // Cancel any pending point input
            _pointInputHelper?.Cancel();
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
        protected Point3D? ParsePoint(string input)
        {
            string[] parts = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 3)
                return null;

            try
            {
                double x = double.Parse(parts[0]);
                double y = double.Parse(parts[1]);
                double z = double.Parse(parts[2]);
                return new Point3D(x, y, z);
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
        protected async Task<Point3D?> GetPoint(string prompt, string[]? keyWords = null, Action<string>? keywordHandler = null, bool allowLastPoint = false)
        {
            var viewport = Context?.GetActiveViewport();
            var viewModel = viewport?.DataContext as ViewportViewModel;
            if (viewModel == null || Context == null)
                return null;

            // Ensure we have a cancellation token source for this command
            _cancellationTokenSource ??= new CancellationTokenSource();

            // Use shared helper if available, otherwise create a temporary one
            var helper = _pointInputHelper ?? new PointInputHelper(Context, viewModel);
            bool helperOwned = helper != _pointInputHelper;

            CurrentPrompt = prompt;
            try
            {
                var result = await helper.GetPointOrKeywordAsync(
                    prompt,
                    allowLastPoint: allowLastPoint,
                    basePoint: BasePoint,
                    keywords: keyWords,
                    cancellationToken: _cancellationTokenSource.Token);

                if (result == null || result.IsCancelled)
                    return null;

                if (result.IsPoint)
                    return result.Point;

                if (result.IsKeyword && keywordHandler != null && result.Keyword != null)
                {
                    // let caller handle keyword input synchronously
                    try
                    {
                        keywordHandler(result.Keyword);
                    }
                    catch
                    {
                        // swallow exceptions from keyword handler to avoid breaking input flow
                    }
                }

                // keyword entered (handled or not) -> treat as non-point result
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                if (helperOwned)
                {
                    try { helper.Cancel(); } catch { }
                }
                CurrentPrompt = string.Empty;
            }
        }

        /// <summary>
        /// Get a distance value from the user.
        /// Supports numeric input, unit-aware parsing via document settings, or a two-point entry.
        /// Returns double.NaN on cancel/invalid input.
        /// </summary>
        protected async Task<double> GetDistance(string prompt, string[]? keyWords = null, Action<string>? keywordHandler = null, bool allowLastPoint = false)
        {
            var viewport = Context?.GetActiveViewport();
            var viewModel = viewport?.DataContext as ViewportViewModel;
            if (viewModel == null || Context == null)
                return double.NaN;

            _cancellationTokenSource ??= new CancellationTokenSource();

            var helper = _pointInputHelper ?? new PointInputHelper(Context, viewModel);
            bool helperOwned = helper != _pointInputHelper;

            CurrentPrompt = prompt;
            try
            {
                var first = await helper.GetPointOrKeywordAsync(
                    prompt,
                    allowLastPoint: allowLastPoint,
                    basePoint: BasePoint,
                    keywords: keyWords,
                    cancellationToken: _cancellationTokenSource.Token);

                if (first == null || first.IsCancelled)
                    return double.NaN;

                if (first.IsKeyword && first.Keyword != null)
                {
                    if (keywordHandler != null)
                    {
                        try { keywordHandler(first.Keyword); }
                        catch { }
                        return double.NaN;
                    }

                    if (double.TryParse(first.Keyword, out var parsed))
                        return parsed;

                    var doc = Context.GetDocument();
                    if (doc != null)
                    {
                        try
                        {
                            return doc.StringToValue(first.Keyword, OpenCADDocument.UnitFormatType.Linear);
                        }
                        catch
                        {
                            return double.NaN;
                        }
                    }

                    return double.NaN;
                }

                if (first.IsPoint && first.Point != null)
                {
                    // two-point distance: set base, prompt for second pick
                    BasePoint = first.Point;
                    StartPreview();

                    try
                    {
                        var secondPrompt = OpenCADStrings.SecondPointPrompt ?? "Specify second point:";
                        CurrentPrompt = secondPrompt;

                        var second = await helper.GetPointOrKeywordAsync(
                            secondPrompt,
                            allowLastPoint: allowLastPoint,
                            basePoint: BasePoint,
                            keywords: keyWords,
                            cancellationToken: _cancellationTokenSource.Token);

                        if (second == null || second.IsCancelled)
                            return double.NaN;

                        if (second.IsPoint && second.Point != null)
                        {
                            return BasePoint.DistanceTo(second.Point);
                        }

                        if (second.IsKeyword && second.Keyword != null && keywordHandler != null)
                        {
                            try { keywordHandler(second.Keyword); } catch { }
                        }

                        return double.NaN;
                    }
                    finally
                    {
                        StopPreview(false);
                        BasePoint = null;
                        TargetPoint = null;
                    }
                }

                return double.NaN;
            }
            catch (OperationCanceledException)
            {
                return double.NaN;
            }
            finally
            {
                if (helperOwned)
                {
                    try { helper.Cancel(); } catch { }
                }
                CurrentPrompt = string.Empty;
            }
        }

        /// <summary>
        /// Get an angle value. Returns angle in radians, or double.NaN on cancel/invalid input.
        /// Supports numeric/keyword input (unit-aware) or a single point pick (angle from BasePoint to picked point).
        /// Caller should set BasePoint before calling if using point picks.
        /// </summary>
        protected async Task<double> GetAngle(string prompt, string[]? keyWords = null, Action<string>? keywordHandler = null, bool allowLastPoint = false)
        {
            var viewport = Context?.GetActiveViewport();
            var viewModel = viewport?.DataContext as ViewportViewModel;
            if (viewModel == null || Context == null)
                return double.NaN;

            _cancellationTokenSource ??= new CancellationTokenSource();

            var helper = _pointInputHelper ?? new PointInputHelper(Context, viewModel);
            bool helperOwned = helper != _pointInputHelper;

            CurrentPrompt = prompt;
            try
            {
                var result = await helper.GetPointOrKeywordAsync(
                    prompt,
                    allowLastPoint: allowLastPoint,
                    basePoint: BasePoint,
                    keywords: keyWords,
                    cancellationToken: _cancellationTokenSource.Token);

                if (result == null || result.IsCancelled)
                    return double.NaN;

                if (result.IsKeyword && result.Keyword != null)
                {
                    if (keywordHandler != null)
                    {
                        try { keywordHandler(result.Keyword); } catch { }
                        return double.NaN;
                    }

                    if (double.TryParse(result.Keyword, out var parsed))
                        return parsed;

                    var doc = Context.GetDocument();
                    if (doc != null)
                    {
                        try
                        {
                            // StringToValue used for linear values; angles use StringToAngle
                            return doc.StringToValue(result.Keyword, OpenCADDocument.UnitFormatType.Angular);
                        }
                        catch
                        {
                            return double.NaN;
                        }
                    }

                    return double.NaN;
                }

                if (result.IsPoint && result.Point != null)
                {
                    if (BasePoint == null)
                        return double.NaN;

                    // Compute angle from BasePoint to picked point
                    return BasePoint.AngleTo(result.Point);
                }

                return double.NaN;
            }
            catch (OperationCanceledException)
            {
                return double.NaN;
            }
            finally
            {
                if (helperOwned)
                {
                    try { helper.Cancel(); } catch { }
                }
                CurrentPrompt = string.Empty;
            }
        }

        #region Preview support (shared)

        // Objects that should be previewed (derived classes may populate this)
        protected List<OpenCADObject>? SelectedObjects;

        // Base/target points used by preview; derived classes should set BasePoint before StartPreview.
        protected Point3D? BasePoint { get; set; }
        protected Point3D? TargetPoint { get; set; }

        // Preview fields
        private readonly List<OpenCADObject> _previewObjects = new();
        protected PropertyChangedEventHandler? _previewHandler;
        private ViewportControl? _previewViewport;

        // Cached providers captured when preview starts (so awaits won't lose them)
        protected ViewportControl? CachedViewport { get; private set; }
        protected OpenCADDocument? CachedDocument { get; private set; }
        protected UndoRedoManager? CachedUndoManager { get; private set; }

        protected ViewportViewModel? CachedViewModel { get; private set; }

        /// <summary>
        /// Start preview mode using the current BasePoint.
        /// Subscribes to ViewportViewModel.PreviewPoint changes and shows translated clones.
        /// Captures providers (viewport/document/undo manager) to avoid them becoming null after awaits.
        /// </summary>
        protected void StartPreview()
        {
            var viewport = Context?.GetActiveViewport();
            if (viewport == null) return;

            var viewModel = viewport.DataContext as ViewportViewModel;
            if (viewModel == null) return;

            // Cache providers immediately so awaiting user input cannot lose them
            CachedViewport = viewport;
            CachedDocument = Context?.GetDocument();
            CachedUndoManager = Context?.GetUndoRedoManager();
            CachedViewModel = viewModel;

            // Remember viewport used for preview so we can remove clones later
            _previewViewport = viewport;

            // Attach handler to respond to PreviewPoint changes
            _previewHandler = (sender, e) =>
            {
                if (e.PropertyName == nameof(ViewportViewModel.PreviewPoint))
                {
                    var previewPoint = viewModel.PreviewPoint;
                    if (previewPoint != null && BasePoint != null)
                    {
                        TargetPoint = previewPoint;
                        UpdatePreviewObjects(viewport);
                    }
                    else
                    {
                        ClearPreviewObjects(viewport);
                    }
                }
            };

            viewModel.PropertyChanged += _previewHandler;
        }

        /// <summary>
        /// Stop preview mode and remove any preview clones.
        /// Clears cached providers.
        /// If completeCommand is true, CommandCompleted() is called at the end (preserves previous behavior).
        /// </summary>
        protected void StopPreview(bool completeCommand = true)
        {
            try
            {
                if (_previewViewport != null)
                {
                    var vm = _previewViewport.DataContext as ViewportViewModel;
                    if (vm != null && _previewHandler != null)
                    {
                        vm.PropertyChanged -= _previewHandler;
                    }
                }
            }
            catch
            {
                // ignore cleanup errors
            }

            if (_previewViewport != null)
                ClearPreviewObjects(_previewViewport);

            _previewHandler = null;
            _previewViewport = null;

            // Clear cached provider references
            CachedViewport = null;
            CachedDocument = null;
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
            // Clear any existing preview clones first
            ClearPreviewObjects(viewport);

            var document = viewport.Document;
            if (document == null || SelectedObjects == null)
                return;

            Matrix4D transformation = GetPreviewTransformation();

            foreach (var obj in SelectedObjects)
            {
                var clone = CreateTranslatedClone(obj, transformation, document);
                if (clone != null)
                {
                    _previewObjects.Add(clone);
                    viewport.AddObject(clone);
                }
            }

            viewport.Refresh();
        }

        /// <summary>
        /// Remove preview clones previously added to viewport/document.
        /// </summary>
        protected void ClearPreviewObjects(ViewportControl viewport)
        {
            if (_previewObjects.Count == 0)
                return;

            foreach (var p in _previewObjects.ToList())
            {
                try
                {
                    viewport.RemoveObject(p);
                }
                catch
                {
                    // Swallow errors during preview removal
                }
            }
            _previewObjects.Clear();
            viewport.Refresh();
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
                if (_previewViewport != null)
                    ClearPreviewObjects(_previewViewport);
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
            if (source is GeometryBase geom)
            {
                var clone = geom.Clone(document) as GeometryBase;
                clone?.Transform(translation);
                return clone;
            }

            // Default: unsupported geometry -> no preview clone
            return null;
        }

        /// <summary>
        /// Method used by the preview update to compute the transformation matrix.
        /// Default uses BasePoint and TargetPoint and attempts translation; derived classes that
        /// require different transforms should override this.
        /// </summary>
        protected virtual Matrix4D GetPreviewTransformation()
        {
            if (BasePoint == null || TargetPoint == null)
                return Matrix4D.CreateRotationZ(0); // Identity-like fallback

            if (Matrix4D.TryCreateTranslation(BasePoint, TargetPoint, out var t))
                return t;

            return Matrix4D.CreateRotationZ(0); // Identity fallback
        }

        /// <summary>
        /// Call this to finish the command: ensure point picking disabled and raise completion.
        /// </summary>
        protected void CommandCompleted()
        {
            // Ensure point picking fully disabled
            var viewport = Context?.GetActiveViewport();
            var viewModel = viewport?.DataContext as ViewportViewModel;
            if (viewModel != null && viewModel.IsPointPickingMode)
            {
                viewModel.DisablePointPickingMode();
            }
            RaiseCommandCompleted();
        }

        #endregion
    }
}