using System.Windows;
using System.Windows.Controls;
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
        /// Sets the viewport settings to enable toggling grid and snap
        /// </summary>
        /// <param name="settings">The viewport settings object</param>
        public void SetViewportSettings(ViewportSettings settings)
        {
            _viewportSettings = settings;
            
            // Initialize toggle button states from settings
            if (_viewportSettings.Grid != null)
            {
                gridToggleButton.IsChecked = _viewportSettings.Grid.ShowGrid;
            }
            
            if (_viewportSettings.Snap != null)
            {
                snapToggleButton.IsChecked = _viewportSettings.Snap.SnapEnabled;
            }
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
    }
}
