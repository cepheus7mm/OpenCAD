using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Undo;
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
        /// Set the current command prompt text (displayed on the command input line).
        /// The string should be the complete prompt text the input helpers expect to show.
        /// </summary>
        void SetCommandPrompt(string prompt);

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

        /// <summary>
        /// Convenience: return the ViewportViewModel for the active viewport.
        /// Implementations should marshal to UI thread as required.
        /// </summary>
        ViewportViewModel? GetActiveViewportViewModel();

        /// <summary>
        /// Post an action to the UI thread without blocking the caller.
        /// Useful for UI operations such as AddObject/RemoveObject/Refresh.
        /// </summary>
        void PostToUI(System.Action action);

        event EventHandler<ObjectClickedEventArgs>? ObjectClicked;
    }
}