using OpenCAD;
using OpenCAD.Geometry;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands
{
    /// <summary>
    /// Context interface for commands to interact with the UI
    /// </summary>
    public interface ICommandContext
    {
        /// <summary>
        /// Output a message to the command history
        /// </summary>
        void OutputMessage(string message);

        /// <summary>
        /// Get the last entered point (for point continuation)
        /// </summary>
        Point3D? GetLastPoint();

        /// <summary>
        /// Set the last entered point
        /// </summary>
        void SetLastPoint(Point3D point);

        /// <summary>
        /// Raise geometry created event
        /// </summary>
        void RaiseGeometryCreated(OpenCADObject geometry);

        /// <summary>
        /// Get the current active viewport
        /// </summary>
        ViewportControl? GetActiveViewport();

        /// <summary>
        /// Get the undo/redo manager
        /// </summary>
        UndoRedoManager? GetUndoRedoManager();

        /// <summary>
        /// Get the current document
        /// </summary>
        OpenCADDocument? GetDocument();
    }
}