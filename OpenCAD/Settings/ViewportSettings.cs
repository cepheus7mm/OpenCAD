using OpenCAD;

namespace OpenCAD.Settings // CHANGED
{
    /// <summary>
    /// Contains user-definable settings for viewport display elements.
    /// Settings are organized into groups (crosshair, grid, snap, etc.) stored as child objects.
    /// </summary>
    public class ViewportSettings : OpenCADObject
    {
        public ViewportSettings() : this(null!)
        {
        }

        public ViewportSettings(OpenCADDocument document)
        {
            // Create and add the crosshair settings group
            var crosshairSettings = new CrosshairSettings(document);
            Add(crosshairSettings);

            // Create and add the grid settings group
            var gridSettings = new GridSettings(document);
            Add(gridSettings);

            // Create and add the snap settings group
            var snapSettings = new SnapSettings(document);
            Add(snapSettings);
            _document = document;
        }

        /// <summary>
        /// Gets the crosshair display settings.
        /// </summary>
        public CrosshairSettings? Crosshair
        {
            get => GetChildren().OfType<CrosshairSettings>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the grid display settings.
        /// </summary>
        public GridSettings? Grid
        {
            get => GetChildren().OfType<GridSettings>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the snap settings.
        /// </summary>
        public SnapSettings? Snap
        {
            get => GetChildren().OfType<SnapSettings>().FirstOrDefault();
        }
    }
}