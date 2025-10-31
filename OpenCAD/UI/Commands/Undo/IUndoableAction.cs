namespace UI.Commands.Undo
{
    /// <summary>
    /// Interface for actions that can be undone and redone
    /// </summary>
    public interface IUndoableAction
    {
        /// <summary>
        /// Execute the action (used for redo)
        /// </summary>
        void Execute();

        /// <summary>
        /// Undo the action
        /// </summary>
        void Undo();

        /// <summary>
        /// Description of the action for display purposes
        /// </summary>
        string Description { get; }
    }
}