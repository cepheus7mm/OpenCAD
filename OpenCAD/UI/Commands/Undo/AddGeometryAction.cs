using System;
using System.Windows;
using System.Windows.Threading;
using OpenCAD;
using UI.Controls.Viewport;

namespace UI.Commands.Undo
{
    /// <summary>
    /// Undoable action for adding geometry to the document
    /// </summary>
    public class AddGeometryAction : IUndoableAction
    {
        private readonly OpenCADObject _geometry;
        private readonly OpenCADDocument _document;
        private readonly ViewportControl? _viewport;

        public string Description { get; }

        public AddGeometryAction(OpenCADObject geometry, OpenCADDocument document, ViewportControl? viewport, string description)
        {
            _geometry = geometry;
            _document = document;
            _viewport = viewport;
            Description = description;
        }

        public void Execute()
        {
            // Document change is non-UI and can be applied on caller thread
            _document.Add(_geometry);

            // Viewport is a WPF control - update it on UI thread (best-effort, non-blocking)
            if (_viewport != null)
            {
                PostToUI(() =>
                {
                    try
                    {
                        _viewport.AddObject(_geometry);
                        _viewport.Refresh();
                    }
                    catch
                    {
                        // swallow UI errors - document already updated
                    }
                });
            }
        }

        public void Undo()
        {
            // Remove from document (non-UI)
            _document.Remove(_geometry);

            // Ensure UI removal happens on UI thread
            if (_viewport != null)
            {
                PostToUI(() =>
                {
                    try
                    {
                        _viewport.RemoveObject(_geometry);
                        _viewport.Refresh();
                    }
                    catch
                    {
                        // swallow UI errors
                    }
                });
            }
        }

        // Best-effort UI dispatcher helper (mirrors CommandContext PostToUI semantics)
        private static void PostToUI(Action action)
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
    }
}