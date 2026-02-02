using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using static System.Collections.Specialized.BitVector32;

namespace OpenCAD.Undo
{
    /// <summary>
    /// Manages undo and redo operations for the document.
    /// </summary>
    public class UndoRedoManager : OpenCADObject
    {
        private readonly OpenCADDocument _document;
        private readonly Stack<UndoTransaction> _undoStack = new();
        private readonly Stack<UndoTransaction> _redoStack = new();
        private int _maxUndoLevels = 100;

        public UndoRedoManager(OpenCADDocument doc) : base(doc)
        {
            _document = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public IDispatcher Dispatcher { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of undo levels.
        /// </summary>
        public int MaxUndoLevels
        {
            get => _maxUndoLevels;
            set => _maxUndoLevels = value > 0 ? value : 100;
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public string? UndoDescription => CanUndo ? _undoStack.Peek().Description : null;
        public string? RedoDescription => CanRedo ? _redoStack.Peek().Description : null;

        /// <summary>
        /// Execute and record an action.
        /// </summary>
        public void ExecuteAction(IUndoableAction action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            Dispatcher.Invoke(() =>
            {
                action.Execute(_document);

                if (_activeTransaction != null)
                {
                    _activeTransaction.AddAction(action);
                    return;
                }

                _activeTransaction = BeginTransaction(action.Description);
                _activeTransaction.AddAction(action);
                CommitTransaction();
            });
        }

        private void ShrinkCache(Stack<UndoTransaction> stack)
        {
            if (stack.Count <= _maxUndoLevels)
                return;

            var items = stack.ToArray(); // top element at index 0
            stack.Clear();

            int keep = Math.Min(items.Length, _maxUndoLevels);

            // Push kept items back preserving original LIFO order (top stays top)
            for (int i = keep - 1; i >= 0; i--)
                stack.Push(items[i]);
        }

        public void Undo()
        {
            if (!CanUndo)
                return;

            var action = _undoStack.Pop();

            Dispatcher.Invoke(() => { action.Undo(_document); }); // Ensure we are on the UI thread

            _redoStack.Push(action);
            OnUndoRedoStateChanged();
        }

        public void Redo()
        {
            if (!CanRedo)
                return;

            var transaction = _redoStack.Pop();

            Dispatcher.Invoke(() => { transaction.Redo(_document); }); // Ensure we are on the UI thread
            
            _undoStack.Push(transaction);
            OnUndoRedoStateChanged();
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnUndoRedoStateChanged();
        }

        public event EventHandler? UndoRedoStateChanged;

        private void OnUndoRedoStateChanged()
        {
            UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private UndoTransaction? _activeTransaction;

        public UndoTransaction BeginTransaction(string description)
        {
            if (_activeTransaction != null)
                throw new InvalidOperationException("Nested transactions not supported yet.");

            _activeTransaction = new UndoTransaction(description);
            return _activeTransaction;
        }

        public void CommitTransaction()
        {
            if (_activeTransaction == null)
                return;

            // Push the whole transaction as ONE undo step
            _undoStack.Push(_activeTransaction);
            _redoStack.Clear();
            ShrinkCache(_undoStack);

            _activeTransaction = null;
            OnUndoRedoStateChanged();
        }

        public void AbortTransaction()
        {
            // Do nothing — preview never touched the document
            _activeTransaction = null;
        }
    }
}