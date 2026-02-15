using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Undo
{
    public sealed class UndoTransaction
    {
        public string Description { get; }
        private readonly List<IUndoableAction> _actions = new();

        public UndoTransaction(string description)
        {
            Description = description;
        }

        public void AddAction(IUndoableAction action)
            => _actions.Add(action);

        public void Undo(OpenCADDocument doc)
        {
            // Undo in reverse order
            for (int i = _actions.Count - 1; i >= 0; i--)
                _actions[i].Undo(doc);
        }

        public void Redo(OpenCADDocument doc)
        {
            foreach (var action in _actions)
                action.Execute(doc);
        }

        public void Execute(OpenCADDocument doc)
        {
            foreach (var action in _actions)
                action.Execute(doc);
        }
    }

}
