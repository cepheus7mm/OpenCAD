using System.Windows;
using System.Windows.Controls;

namespace UI.Controls.MainWindow
{
    /// <summary>
    /// Interaction logic for MenuBarControl.xaml
  /// </summary>
    public partial class MenuBarControl : UserControl
    {
        // Events to notify the main window of menu actions
        public event EventHandler? NewFileRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler? LightThemeRequested;
        public event EventHandler? DarkThemeRequested;
        public event EventHandler? NewViewportRequested;
        public event EventHandler<bool>? LayersVisibilityChanged;
        public event EventHandler? SaveAsRequested;

        public MenuBarControl()
        {
            InitializeComponent();
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            NewFileRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LightTheme_Click(object sender, RoutedEventArgs e)
        {
            LightThemeMenuItem.IsChecked = true;
            DarkThemeMenuItem.IsChecked = false;
            LightThemeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void DarkTheme_Click(object sender, RoutedEventArgs e)
        {
            LightThemeMenuItem.IsChecked = false;
            DarkThemeMenuItem.IsChecked = true;
            DarkThemeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Layers_Click(object sender, RoutedEventArgs e)
        {
            LayersVisibilityChanged?.Invoke(this, LayersMenuItem.IsChecked);
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveAsRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Updates the theme menu items to reflect the current theme
        /// </summary>
        public void UpdateThemeSelection(bool isLightTheme)
        {
            LightThemeMenuItem.IsChecked = isLightTheme;
            DarkThemeMenuItem.IsChecked = !isLightTheme;
        }

        /// <summary>
        /// Updates the layers menu item to reflect visibility
        /// </summary>
        public void UpdateLayersVisibility(bool isVisible)
        {
            LayersMenuItem.IsChecked = isVisible;
        }
    }
}
