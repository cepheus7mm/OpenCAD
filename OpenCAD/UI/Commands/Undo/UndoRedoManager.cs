using System.Collections.Generic;

namespace UI.Commands.Undo
{
    /// <summary>
    /// Manages undo and redo operations for the application
    /// </summary>
    public class UndoRedoManager
    {
        private readonly Stack<IUndoableAction> _undoStack = new();
        private readonly Stack<IUndoableAction> _redoStack = new();
        private int _maxUndoLevels = 100;

        /// <summary>
        /// Gets or sets the maximum number of undo levels
        /// </summary>
        public int MaxUndoLevels
        {
            get => _maxUndoLevels;
            set => _maxUndoLevels = value > 0 ? value : 100;
        }

        /// <summary>
        /// Gets whether undo is available
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Gets whether redo is available
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Gets the description of the next undo action
        /// </summary>
        public string? UndoDescription => CanUndo ? _undoStack.Peek().Description : null;

        /// <summary>
        /// Gets the description of the next redo action
        /// </summary>
        public string? RedoDescription => CanRedo ? _redoStack.Peek().Description : null;

        /// <summary>
        /// Execute and record an action
        /// </summary>
        public void ExecuteAction(IUndoableAction action)
        {
            action.Execute();
            _undoStack.Push(action);
            _redoStack.Clear(); // Clear redo stack when new action is performed

            // Limit undo stack size
            while (_undoStack.Count > _maxUndoLevels)
            {
                var items = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = 0; i < _maxUndoLevels; i++)
                {
                    _undoStack.Push(items[i]);
                }
            }

            OnUndoRedoStateChanged();
        }

        /// <summary>
        /// Undo the last action
        /// </summary>
        public void Undo()
        {
            if (!CanUndo)
                return;

            var action = _undoStack.Pop();
            action.Undo();
            _redoStack.Push(action);

            OnUndoRedoStateChanged();
        }

        /// <summary>
        /// Redo the last undone action
        /// </summary>
        public void Redo()
        {
            if (!CanRedo)
                return;

            var action = _redoStack.Pop();
            action.Execute();
            _undoStack.Push(action);

            OnUndoRedoStateChanged();
        }

        /// <summary>
        /// Clear all undo and redo history
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnUndoRedoStateChanged();
        }

        /// <summary>
        /// Event raised when undo/redo state changes
        /// </summary>
        public event EventHandler? UndoRedoStateChanged;

        private void OnUndoRedoStateChanged()
        {
            UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}