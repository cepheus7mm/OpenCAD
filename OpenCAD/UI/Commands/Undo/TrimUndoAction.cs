using System;
using System.Collections.Generic;
using OpenCAD;
using UI.Commands.Editing;

namespace UI.Commands.Undo
{
    internal class TrimUndoAction : IUndoableAction
    {
        internal readonly List<TrimCommand.TrimOperation> _operations;
        private OpenCADDocument? _document;

        public string Description => $"Trim {_operations.Count} object(s)";

        public TrimUndoAction(List<TrimCommand.TrimOperation> operations, OpenCADDocument document)
        {
            _operations = new List<TrimCommand.TrimOperation>(operations ?? throw new ArgumentNullException(nameof(operations)));
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public void Execute(ICommandContext context)
        {
            _document = context?.GetDocument() ?? _document;
            if (_document == null) return;

            foreach (var op in _operations)
            {
                _document.Remove(op.OriginalObject);
                foreach (var result in op.ResultObjects)
                {
                    _document.Add(result);
                }
            }

            _document.MarkAsModified();

            // UI updates will happen via document events; post a best-effort refresh.
            try
            {
                context?.PostToUI(() => context?.GetActiveViewport()?.Refresh());
            }
            catch { }
        }

        public void Undo(ICommandContext context)
        {
            _document = context?.GetDocument() ?? _document;
            if (_document == null) return;

            foreach (var op in _operations)
            {
                foreach (var result in op.ResultObjects)
                {
                    _document.Remove(result);
                }

                _document.Add(op.OriginalObject);
            }

            _document.MarkAsModified();

            try
            {
                context?.PostToUI(() => context?.GetActiveViewport()?.Refresh());
            }
            catch { }
        }
    }
}
