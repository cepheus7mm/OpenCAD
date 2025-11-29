using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using OpenCAD;
using UI.Controls.Viewport;

namespace UI.Commands.Undo
{
    /// <summary>
    /// Undoable action for adding multiple geometry objects
    /// </summary>
    public class AddMultipleGeometryAction : IUndoableAction
    {
        private readonly IReadOnlyList<OpenCADObject> _geometries;
        private readonly OpenCADDocument _document;
        private readonly ViewportControl? _viewport;

        public string Description { get; }

        public AddMultipleGeometryAction(IEnumerable<OpenCADObject> geometries, OpenCADDocument document, ViewportControl? viewport, string description)
        {
            _geometries = new List<OpenCADObject>(geometries);
            _document = document;
            _viewport = viewport;
            Description = description;
        }

        public void Execute()
        {
            foreach (var g in _geometries)
                _document.Add(g);

            if (_viewport != null)
            {
                PostToUI(() =>
                {
                    try
                    {
                        foreach (var g in _geometries)
                            _viewport.AddObject(g);
                        _viewport.Refresh();
                    }
                    catch
                    {
                        // swallow UI errors
                    }
                });
            }
        }

        public void Undo()
        {
            foreach (var g in _geometries)
                _document.Remove(g);

            if (_viewport != null)
            {
                PostToUI(() =>
                {
                    try
                    {
                        foreach (var g in _geometries)
                            _viewport.RemoveObject(g);
                        _viewport.Refresh();
                    }
                    catch
                    {
                        // swallow UI errors
                    }
                });
            }
        }

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