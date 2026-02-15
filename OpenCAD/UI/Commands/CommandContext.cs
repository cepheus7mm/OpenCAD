using System;
using System.Windows;
using System.Windows.Threading;
using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Undo;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands
{
    /// <summary>
    /// Implementation of command context
    /// </summary>
    public class CommandContext : ICommandContext
    {
        private readonly Action<string> _outputMessage;
        private readonly Func<Point3D?> _getLastPoint;
        private readonly Action<Point3D> _setLastPoint;
        private readonly Action<OpenCADObject> _raiseGeometryCreated;
        private readonly Func<ViewportControl?> _getActiveViewport;
        private readonly Func<UndoRedoManager?> _getUndoRedoManager;
        private readonly Func<OpenCADDocument?> _getDocument;
        private readonly Action<string> _setCommandPrompt; // <-- new

        public event EventHandler<ObjectClickedEventArgs>? ObjectClicked;

        public CommandContext(
            Action<string> outputMessage,
            Func<Point3D?> getLastPoint,
            Action<Point3D> setLastPoint,
            Action<OpenCADObject> raiseGeometryCreated,
            Func<ViewportControl?>? getActiveViewport = null,
            Func<UndoRedoManager?>? getUndoRedoManager = null,
            Func<OpenCADDocument?>? getDocument = null,
            Action<string>? setCommandPrompt = null) // optional delegate
        {
            _outputMessage = outputMessage;
            _getLastPoint = getLastPoint;
            _setLastPoint = setLastPoint;
            _raiseGeometryCreated = raiseGeometryCreated;
            _getActiveViewport = getActiveViewport ?? (() => null);
            _getUndoRedoManager = getUndoRedoManager ?? (() => null);
            _getDocument = getDocument ?? (() => null);
            _setCommandPrompt = setCommandPrompt ?? (_ => { });
        }

        public void OutputMessage(string message)
        {
            // UI-affecting operations should be marshalled to UI thread; don't block caller
            PostToUI(() => _outputMessage(message));
        }

        public Point3D? GetLastPoint()
        {
            return InvokeOnUI(() => _getLastPoint());
        }

        public void SetLastPoint(Point3D point)
        {
            // Ensure consistent behavior: set synchronously on UI thread when required
            InvokeOnUI(() =>
            {
                _setLastPoint(point);
                return true;
            });
        }

        public void RaiseGeometryCreated(OpenCADObject geometry)
        {
            // Raising geometry created is a UI concern (document/viewport) - post to UI
            PostToUI(() => _raiseGeometryCreated(geometry));
        }

        public ViewportControl? GetActiveViewport()
        {
            return InvokeOnUI(() => _getActiveViewport());
        }

        public UndoRedoManager? GetUndoRedoManager()
        {
            return InvokeOnUI(() => _getUndoRedoManager());
        }

        public OpenCADDocument? GetDocument()
        {
            return InvokeOnUI(() => _getDocument());
        }

        /// <summary>
        /// Set the command pane prompt (delegates to the UI layer).
        /// InputHelpers/commands can call this to update the prompt line without writing into history.
        /// Marshalled to UI thread (non-blocking).
        /// </summary>
        public void SetCommandPrompt(string prompt)
        {
            PostToUI(() => _setCommandPrompt(prompt));
        }

        /// <summary>
        /// Return the ViewportViewModel for the active viewport. Always marshals to the UI thread.
        /// </summary>
        public ViewportViewModel? GetActiveViewportViewModel()
        {
            try
            {
                var disp = Application.Current?.Dispatcher;
                if (disp != null && !disp.CheckAccess())
                {
                    return disp.Invoke(() =>
                    {
                        var vp = _getActiveViewport();
                        return vp?.DataContext as ViewportViewModel;
                    });
                }

                var viewport = _getActiveViewport();
                return viewport?.DataContext as ViewportViewModel;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Post an action to the UI thread without blocking the caller.
        /// </summary>
        public void PostToUI(Action action)
        {
            try
            {
                var disp = Application.Current?.Dispatcher;
                if (disp != null && !disp.CheckAccess())
                {
                    disp.BeginInvoke(action, DispatcherPriority.Normal);
                    return;
                }

                action();
            }
            catch
            {
                try { action(); } catch { /* swallow */ }
            }
        }

        // Single generic helper: execute a func on UI thread and return the result.
        // Using one overload avoids ambiguity with nullable/reference-returning lambdas.
        private T InvokeOnUI<T>(Func<T> func)
        {
            try
            {
                var disp = Application.Current?.Dispatcher;
                if (disp != null && !disp.CheckAccess())
                {
                    return disp.Invoke(func);
                }

                return func();
            }
            catch
            {
                // If dispatching fails (app shutting down etc.), fall back to direct invocation.
                return func();
            }
        }
    }
}