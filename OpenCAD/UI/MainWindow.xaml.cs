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
using UI.Controls.Viewport;
using OpenCAD.Geometry;

using OCad = OpenCAD.Geometry;

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
            
            // Hook up menu bar events
            menuBar.NewFileRequested += (s, e) => NewFile_Click(s!, new RoutedEventArgs());
            menuBar.ExitRequested += (s, e) => Exit_Click(s!, new RoutedEventArgs());
            menuBar.LightThemeRequested += (s, e) => LightTheme_Click(s!, new RoutedEventArgs());
            menuBar.DarkThemeRequested += (s, e) => DarkTheme_Click(s!, new RoutedEventArgs());
            
            // Hook up toolbar events
            toolBar.NewFileRequested += (s, e) => NewFile_Click(s!, new RoutedEventArgs());
            toolBar.OpenRequested += (s, e) => Open_Click(s!, new RoutedEventArgs());
            toolBar.SaveRequested += (s, e) => Save_Click(s!, new RoutedEventArgs());
            toolBar.CutRequested += (s, e) => Cut_Click(s!, new RoutedEventArgs());
            toolBar.CopyRequested += (s, e) => Copy_Click(s!, new RoutedEventArgs());
            toolBar.PasteRequested += (s, e) => Paste_Click(s!, new RoutedEventArgs());
            
            // Hook up titlebar button events when the window is loaded
            this.Loaded += MainWindow_Loaded;
            
            // Hook up menu bar viewport event
            menuBar.NewViewportRequested += (s, e) => CreateViewport_Click(s!, new RoutedEventArgs());
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
            // The docking system is now initialized in the DockingAreaControl
            // You can add additional initialization here if needed
        }

        #region Menu Event Handlers

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            // Create new document content
            var newTextBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Text = "// New document"
            };

            // Apply current theme to the new document
            ApplyThemeToTextBox(newTextBox);

            // Get the document pane and add the new document
            var docPane = dockingArea.GetDocumentPane();
            if (docPane != null)
            {
                var newDoc = dockingArea.AddDocument(
                    $"Document{docPane.Children.Count + 1}",
                    $"doc{docPane.Children.Count + 1}",
                    newTextBox
                );

                if (newDoc != null)
                {
                    // Update status bar
                    statusBar.UpdateStatus($"Created new document: {newDoc.Title}");
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion

        #region Toolbar Event Handlers

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement file open functionality
            statusBar.UpdateStatus("Opening file...");
            MessageBox.Show("Open file functionality not yet implemented.", "Open File", 
            MessageBoxButton.OK, MessageBoxImage.Information);
            statusBar.UpdateStatus("Ready");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement file save functionality
            statusBar.UpdateStatus("Saving file...");
            MessageBox.Show("Save file functionality not yet implemented.", "Save File", 
       MessageBoxButton.OK, MessageBoxImage.Information);
    statusBar.UpdateStatus("Ready");
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement cut functionality
            statusBar.UpdateStatus("Cut operation");
   MessageBox.Show("Cut functionality not yet implemented.", "Cut", 
      MessageBoxButton.OK, MessageBoxImage.Information);
   statusBar.UpdateStatus("Ready");
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
 {
            // TODO: Implement copy functionality
     statusBar.UpdateStatus("Copy operation");
     MessageBox.Show("Copy functionality not yet implemented.", "Copy", 
            MessageBoxButton.OK, MessageBoxImage.Information);
            statusBar.UpdateStatus("Ready");
 }

        private void Paste_Click(object sender, RoutedEventArgs e)
   {
            // TODO: Implement paste functionality
       statusBar.UpdateStatus("Paste operation");
     MessageBox.Show("Paste functionality not yet implemented.", "Paste", 
   MessageBoxButton.OK, MessageBoxImage.Information);
      statusBar.UpdateStatus("Ready");
        }

        #endregion

        #region Theme Event Handlers

        private void LightTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Themes/LightTheme.xaml");
            menuBar.UpdateThemeSelection(isLightTheme: true);
            statusBar.UpdateStatus("Light theme applied");
        }

        private void DarkTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Themes/DarkTheme.xaml");
            menuBar.UpdateThemeSelection(isLightTheme: false);
            statusBar.UpdateStatus("Dark theme applied");
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
            // Use the DockingAreaControl's method to apply theme
            dockingArea.ApplyThemeToDocuments();
        }

        private void ApplyThemeToTextBox(TextBox? textBox)
        {
            if (textBox == null) return;
            
            // Apply theme resources to TextBox
            textBox.SetResourceReference(Control.BackgroundProperty, "PrimaryBackgroundBrush");
            textBox.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
        }

        #endregion

        #region Viewport Event Handlers

        private void CreateViewport_Click(object sender, RoutedEventArgs e)
        {
            // Create viewport control
            var viewport = new ViewportControl();
            
            // Wire up the status bar
            viewport.SetStatusBar(statusBar);

            // Add some sample geometry for testing
            var line1 = new OCad.Line(
                new Point3D(0, 0, 0),
                new Point3D(5, 5, 5)
            );
            viewport.AddObject(line1);

            var line2 = new OCad.Line(
                new Point3D(-3, 0, 0),
                new Point3D(3, 0, 0)
            );
            viewport.AddObject(line2);

            var line3 = new OCad.Line(
                new Point3D(0, -3, 0),
                new Point3D(0, 3, 0)
            );
            viewport.AddObject(line3);

            var line4 = new OCad.Line(
                new Point3D(0, 0, -3),
                new Point3D(0, 0, 3)
            );
            viewport.AddObject(line4);

            // Get the document pane and add the viewport
            var docPane = dockingArea.GetDocumentPane();
            if (docPane != null)
            {
                var newDoc = dockingArea.AddDocument(
                    $"Viewport {docPane.Children.Count + 1}",
                    $"viewport{docPane.Children.Count + 1}",
                    viewport
                );

                if (newDoc != null)
                {
                    statusBar.UpdateStatus($"Created new viewport: {newDoc.Title}");
                }
            }
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