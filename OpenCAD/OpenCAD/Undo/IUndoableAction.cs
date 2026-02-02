namespace OpenCAD.Undo
{
    /// <summary>
    /// Interface for actions that can be undone and redone (document-owned).
    /// </summary>
    public interface IUndoableAction
    {
        /// <summary>
        /// Execute the action (used for redo).
        /// </summary>
        void Execute(OpenCADDocument document);

        /// <summary>
        /// Undo the action.
        /// </summary>
        void Undo(OpenCADDocument document);

        /// <summary>
        /// Description of the action for display purposes.
        /// </summary>
        string Description { get; }
    }
}