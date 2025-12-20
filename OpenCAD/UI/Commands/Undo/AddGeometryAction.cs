using System;
using OpenCAD;

namespace UI.Commands.Undo
{
    /// <summary>
    /// Undoable action for adding geometry to the document (model-first).
    /// UI updates occur via document events or via ICommandContext.PostToUI as a best-effort.
    /// </summary>
    public class AddGeometryAction : IUndoableAction
    {
        private readonly OpenCADObject _geometry;
        private readonly OpenCADDocument _document;

        public string Description { get; }

        public AddGeometryAction(OpenCADObject geometry, OpenCADDocument document, string description)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _document = document ?? throw new ArgumentNullException(nameof(document));
            Description = description;
        }

        public void Execute(ICommandContext context)
        {
            // Apply change to canonical model only
            _document.Add(_geometry);
            _document.MarkAsModified();

            // Notify model listeners; viewmodel subscribed to document events will refresh.
            // Post a best-effort UI refresh if context is available.
            try
            {
                context?.PostToUI(() => context?.GetActiveViewport()?.Refresh());
            }
            catch
            {
                // best-effort only
            }
        }

        public void Undo(ICommandContext context)
        {
            _document.Remove(_geometry);
            _document.MarkAsModified();

            try
            {
                context?.PostToUI(() => context?.GetActiveViewport()?.Refresh());
            }
            catch
            {
                // best-effort only
            }
        }
    }
}