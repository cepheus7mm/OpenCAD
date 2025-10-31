using OpenCAD;
using OpenCAD.Geometry;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands
{
    /// <summary>
    /// Implementation of command context
    /// </summary>
    public class CommandContext : ICommandContext
    {
        private readonly Action<string> _outputMessage;
        private readonly Func<Point3D?> _getLastPoint;
        private readonly Action<Point3D> _setLastPoint;
        private readonly Action<OpenCADObject> _raiseGeometryCreated;
        private readonly Func<ViewportControl?> _getActiveViewport;
        private readonly Func<UndoRedoManager?> _getUndoRedoManager;
        private readonly Func<OpenCADDocument?> _getDocument;

        public CommandContext(
            Action<string> outputMessage,
            Func<Point3D?> getLastPoint,
            Action<Point3D> setLastPoint,
            Action<OpenCADObject> raiseGeometryCreated,
            Func<ViewportControl?>? getActiveViewport = null,
            Func<UndoRedoManager?>? getUndoRedoManager = null,
            Func<OpenCADDocument?>? getDocument = null)
        {
            _outputMessage = outputMessage;
            _getLastPoint = getLastPoint;
            _setLastPoint = setLastPoint;
            _raiseGeometryCreated = raiseGeometryCreated;
            _getActiveViewport = getActiveViewport ?? (() => null);
            _getUndoRedoManager = getUndoRedoManager ?? (() => null);
            _getDocument = getDocument ?? (() => null);
        }

        public void OutputMessage(string message) => _outputMessage(message);
        public Point3D? GetLastPoint() => _getLastPoint();
        public void SetLastPoint(Point3D point) => _setLastPoint(point);
        public void RaiseGeometryCreated(OpenCADObject geometry) => _raiseGeometryCreated(geometry);
        public ViewportControl? GetActiveViewport() => _getActiveViewport();
        public UndoRedoManager? GetUndoRedoManager() => _getUndoRedoManager();
        public OpenCADDocument? GetDocument() => _getDocument();
    }
}