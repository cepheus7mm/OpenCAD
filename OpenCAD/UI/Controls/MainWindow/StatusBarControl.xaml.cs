using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Settings;

namespace UI.Controls.MainWindow
{
    /// <summary>
    /// Interaction logic for StatusBarControl.xaml
    /// </summary>
    public partial class StatusBarControl : UserControl
    {
        private ViewportSettings? _viewportSettings;

        public StatusBarControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Updates the status message displayed in the status bar
        /// </summary>
        /// <param name="message">The status message to display</param>
        public void UpdateStatus(string message)
        {
            statusTextBlock.Text = message;
        }

        /// <summary>
        /// Updates the cursor position (line and column) displayed in the status bar
        /// </summary>
        /// <param name="line">The current line number</param>
        /// <param name="column">The current column number</param>
        public void UpdatePosition(int line, int column)
        {
            positionTextBlock.Text = $"Line {line}, Col {column}";
        }

        /// <summary>
        /// Updates the cursor position with a custom text format
        /// </summary>
        /// <param name="positionText">The position text to display</param>
        public void UpdatePositionText(string positionText)
        {
            positionTextBlock.Text = positionText;
        }

        /// <summary>
        /// Updates the camera position and target displayed in the status bar
        /// </summary>
        /// <param name="cameraPosition">The camera position</param>
        /// <param name="cameraTarget">The camera target</param>
        public void UpdateCameraInfo(Vector3 cameraPosition, Vector3 cameraTarget)
        {
            string cameraInfo = $"Cam: ({cameraPosition.X:F2}, {cameraPosition.Y:F2}, {cameraPosition.Z:F2}) → Target: ({cameraTarget.X:F2}, {cameraTarget.Y:F2}, {cameraTarget.Z:F2})";
            statusTextBlock.Text = cameraInfo;
        }

        /// <summary>
        /// Sets the viewport settings to enable toggling grid and snap
        /// </summary>
        /// <param name="settings">The viewport settings object</param>
        public void SetViewportSettings(ViewportSettings settings)
        {
            if (_viewportSettings?.Snap != null)
                _viewportSettings.Snap.PropertyChanged -= OnSnapSettingsChanged;
            if (_viewportSettings?.Grid != null)
                _viewportSettings.Grid.PropertyChanged -= OnGridSettingsChanged;

            _viewportSettings = settings;

            if (_viewportSettings?.Snap != null)
                _viewportSettings.Snap.PropertyChanged += OnSnapSettingsChanged;
            if (_viewportSettings?.Grid != null)
                _viewportSettings.Grid.PropertyChanged += OnGridSettingsChanged;

            UpdateSnapButton();
            UpdateGridButton();
        }

        public void UpdateButtons()
        {
            UpdateSnapButton();
            UpdateGridButton();
        }

        private void OnSnapSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SnapSettings.SnapEnabled))
                UpdateSnapButton();
        }

        private void OnGridSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GridSettings.ShowGrid))
                UpdateGridButton();
        }

        private void UpdateSnapButton()
        {
            // Update the snap button's IsChecked or visual state based on _viewportSettings.Snap.SnapEnabled
            if (_viewportSettings?.Snap != null)
            {
                snapToggleButton.IsChecked = _viewportSettings.Snap.SnapEnabled;
            }
        }

        private void UpdateGridButton()
        {
            // Update the grid button's IsChecked or visual state based on _viewportSettings.Grid.ShowGrid
            if (_viewportSettings?.Grid != null)
            {
                gridToggleButton.IsChecked = _viewportSettings.Grid.ShowGrid;
                // Update status message
                UpdateStatus(_viewportSettings.Grid.ShowGrid ? "Grid ON" : "Grid OFF");
            }

            // Raise the event to trigger viewport refresh
            ViewportSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Event raised when grid or snap settings are changed
        /// </summary>
        public event EventHandler? ViewportSettingsChanged;

        private void GridToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewportSettings?.Grid != null)
            {
                bool newState = gridToggleButton.IsChecked ?? false;
                _viewportSettings.Grid.ShowGrid = newState;
                
                // Update status message
                UpdateStatus(newState ? "Grid ON" : "Grid OFF");
                
                // Raise the event to trigger viewport refresh
                ViewportSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SnapToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewportSettings?.Snap != null)
            {
                bool newState = snapToggleButton.IsChecked ?? false;
                _viewportSettings.Snap.SnapEnabled = newState;
                
                // Update status message
                UpdateStatus(newState ? "Snap ON" : "Snap OFF");
                
                // Raise the event to trigger viewport refresh
                ViewportSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private void GeoSnapToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // Show the GeoPointModesDialog
            var initialSelection = _viewportSettings?.GeoPointModes ?? 0;
            var dialog = new GeoPointModesDialog(initialSelection);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                // Apply selected modes to SnapSettings (assuming SnapSettings.EnabledModes exists)
                if (_viewportSettings?.Snap != null)
                {
                    GeoPointModes geoPointModes = 0;
                    foreach (var mode in dialog.SelectedModes)
                    {
                        geoPointModes |= mode;
                    }
                    _viewportSettings.GeoPointModes = geoPointModes;
                    UpdateStatus("GeoPoint modes updated");
                    ViewportSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                UpdateStatus("GeoPoint modes unchanged");
            }
        }
    }
}
