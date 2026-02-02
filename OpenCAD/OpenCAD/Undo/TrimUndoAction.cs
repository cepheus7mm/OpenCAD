using System;
using System.Collections.Generic;
using OpenCAD;

namespace OpenCAD.Undo
{
    public class TrimUndoAction : IUndoableAction
    {
        private readonly List<TrimOperation> _operations;

        public string Description => $"Trim {_operations.Count} object(s)";

        public TrimUndoAction(IEnumerable<TrimOperation> operations)
        {
            if (operations == null) throw new ArgumentNullException(nameof(operations));
            _operations = new List<TrimOperation>(operations);
        }

        public void Execute(OpenCADDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            foreach (var op in _operations)
            {
                document.Remove(op.OriginalObject);
                foreach (var result in op.ResultObjects)
                    document.Add(result);
            }

            document.MarkAsModified();
        }

        public void Undo(OpenCADDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            foreach (var op in _operations)
            {
                foreach (var result in op.ResultObjects)
                    document.Remove(result);

                document.Add(op.OriginalObject);
            }

            document.MarkAsModified();
        }
    }
}
