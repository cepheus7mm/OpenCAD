using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Xceed.Wpf.AvalonDock.Layout;

namespace UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitializeDockingSystem();
            
            // Hook up titlebar button events when the window is loaded
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Find and hook up titlebar button events
            HookupTitleBarButtons();
        }

        private void HookupTitleBarButtons()
        {
            try
            {
                // Find the buttons in the window template
                var minimizeButton = FindVisualChild<Button>(this, "PART_MinimizeButton");
                var maximizeRestoreButton = FindVisualChild<Button>(this, "PART_MaximizeRestoreButton");
                var closeButton = FindVisualChild<Button>(this, "PART_CloseButton");

                // Hook up event handlers
                if (minimizeButton != null)
                    minimizeButton.Click += (s, e) => this.WindowState = WindowState.Minimized;

                if (maximizeRestoreButton != null)
                    maximizeRestoreButton.Click += (s, e) => 
                    {
                        this.WindowState = this.WindowState == WindowState.Maximized 
                            ? WindowState.Normal 
                            : WindowState.Maximized;
                    };

                if (closeButton != null)
                    closeButton.Click += (s, e) => this.Close();
            }
            catch (Exception ex)
            {
                // If titlebar button hookup fails, continue without custom buttons
                System.Diagnostics.Debug.WriteLine($"Failed to hook up titlebar buttons: {ex.Message}");
            }
        }

        /// <summary>
        /// Find a visual child control by name in the visual tree
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild && typedChild.Name == name)
                    return typedChild;

                var foundChild = FindVisualChild<T>(child, name);
                if (foundChild != null)
                    return foundChild;
            }

            return null;
        }

        private void InitializeDockingSystem()
        {
            // You can programmatically add more dock panels here
            // For example, add event handlers for opening/closing panels
        }

        #region Menu Event Handlers

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            // Add a new document tab
            var documentPane = dockingManager.Layout.RootPanel.Children[0] as LayoutPanel;
            var docPaneGroup = documentPane?.Children[1] as LayoutPanel;
            var docPane = docPaneGroup?.Children[0] as LayoutDocumentPane;
            
            if (docPane != null)
            {
                var newDoc = new LayoutDocument
                {
                    Title = $"Document{docPane.Children.Count + 1}",
                    ContentId = $"doc{docPane.Children.Count + 1}",
                    Content = new TextBox 
                    { 
                        AcceptsReturn = true, 
                        AcceptsTab = true,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        Text = "// New document"
                    }
                };
                
                // Apply current theme to the new document
                ApplyThemeToTextBox(newDoc.Content as TextBox);
                
                docPane.Children.Add(newDoc);
                newDoc.IsSelected = true;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion

        #region Theme Event Handlers

        private void LightTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Themes/LightTheme.xaml");
            LightThemeMenuItem.IsChecked = true;
            DarkThemeMenuItem.IsChecked = false;
        }

        private void DarkTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Themes/DarkTheme.xaml");
            LightThemeMenuItem.IsChecked = false;
            DarkThemeMenuItem.IsChecked = true;
        }

        #endregion

        #region Theme Management

        private void ApplyTheme(string themeUri)
        {
            try
            {
                // Work with application resources instead of window resources
                var app = Application.Current;
                
                // Find and remove the current theme
                ResourceDictionary? themeToRemove = null;
                foreach (var resource in app.Resources.MergedDictionaries)
                {
                    if (resource.Source?.OriginalString?.Contains("Theme.xaml") == true)
                    {
                        themeToRemove = resource;
                        break;
                    }
                }

                if (themeToRemove != null)
                {
                    app.Resources.MergedDictionaries.Remove(themeToRemove);
                }

                // Load and apply the new theme
                var newTheme = new ResourceDictionary
                {
                    Source = new Uri(themeUri, UriKind.Relative)
                };
                
                // Insert at the beginning so it has precedence
                app.Resources.MergedDictionaries.Insert(0, newTheme);
                
                // Apply theme to existing dynamic document TextBoxes
                ApplyThemeToExistingDocuments();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying theme: {ex.Message}", "Theme Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplyThemeToExistingDocuments()
        {
            // Find all LayoutDocuments and apply theme to their TextBox content
            var layoutRoot = dockingManager.Layout;
            if (layoutRoot?.RootPanel?.Children[0] is LayoutPanel mainPanel &&
                mainPanel.Children[1] is LayoutPanel centerPanel &&
                centerPanel.Children[0] is LayoutDocumentPane docPane)
            {
                foreach (var document in docPane.Children.OfType<LayoutDocument>())
                {
                    if (document.Content is TextBox textBox)
                    {
                        ApplyThemeToTextBox(textBox);
                    }
                }
            }
        }

        private void ApplyThemeToTextBox(TextBox? textBox)
        {
            if (textBox == null) return;
            
            // Apply theme resources to TextBox
            textBox.SetResourceReference(Control.BackgroundProperty, "PrimaryBackgroundBrush");
            textBox.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
        }

        #endregion
    }

    /// <summary>
    /// Simple class to represent property items in the properties grid
    /// </summary>
    public class PropertyItem
    {
        public string Property { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}