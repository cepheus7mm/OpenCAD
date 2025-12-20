using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

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
        private ICommandContext _commandContext = null;

        public ICommandContext Context
        {
            get => _commandContext;
            set
            {
                if (_commandContext == null)
                    _commandContext = value;
            }
        }

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
        /// Execute and record an action. Execution is marshalled to the UI thread to keep model + view updates consistent.
        /// </summary>
        public void ExecuteAction(IUndoableAction action)
        {
            InvokeOnUI(() => action.Execute(Context));
            _undoStack.Push(action);
            _redoStack.Clear(); // Clear redo stack when new action is performed
            ShrinkCache(_undoStack);

            OnUndoRedoStateChanged();
        }

        public void AddActionWithoutExecute(IUndoableAction action)
        {
            _undoStack.Push(action);
            _redoStack.Clear(); // Clear redo stack when new action is performed
            ShrinkCache(_undoStack);
            OnUndoRedoStateChanged();
        }

        private void ShrinkCache(Stack<IUndoableAction> stack)
        {
            if (stack.Count <= _maxUndoLevels)
                return;

            var items = stack.ToArray(); // top element at index 0
            stack.Clear();

            int keep = Math.Min(items.Length, _maxUndoLevels);

            // Push kept items back preserving original LIFO order (top stays top)
            for (int i = keep - 1; i >= 0; i--)
            {
                stack.Push(items[i]);
            }
        }

        /// <summary>
        /// Undo the last action (marshalled to UI thread)
        /// </summary>
        public void Undo()
        {
            if (!CanUndo)
                return;

            var action = _undoStack.Pop();
            InvokeOnUI(() => action.Undo(Context));
            _redoStack.Push(action);

            OnUndoRedoStateChanged();
        }

        /// <summary>
        /// Redo the last undone action (marshalled to UI thread)
        /// </summary>
        public void Redo()
        {
            if (!CanRedo)
                return;

            var action = _redoStack.Pop();
            InvokeOnUI(() => action.Execute(Context));
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

        /// <summary>
        /// Helper to invoke an action on the WPF UI thread. Falls back to direct call when dispatcher is unavailable.
        /// </summary>
        private static void InvokeOnUI(Action action)
        {
            if (action == null) return;

            try
            {
                var app = Application.Current;
                if (app?.Dispatcher != null)
                {
                    var dispatcher = app.Dispatcher;
                    if (dispatcher.CheckAccess())
                    {
                        action();
                    }
                    else
                    {
                        dispatcher.Invoke(action, DispatcherPriority.Send);
                    }
                    return;
                }
            }
            catch
            {
                // If dispatcher invocation fails, fall back to direct call below.
            }

            // Fallback when no Application/Dispatcher is present (unit tests, headless).
            action();
        }
    }
}