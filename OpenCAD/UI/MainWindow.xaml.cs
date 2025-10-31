﻿using System.Text;
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
using OpenCAD;

using OCad = OpenCAD.Geometry;

namespace UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int _documentCounter = 0;

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
            
            // Hook up command input event
            dockingArea.CommandInput.GeometryCreated += CommandInput_GeometryCreated;
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

        #region Command Input Handlers

        private void CommandInput_GeometryCreated(object? sender, UI.Controls.MainWindow.GeometryCreatedEventArgs e)
        {
            // Get the currently selected document
            var docPane = dockingArea.GetDocumentPane();
            if (docPane != null)
            {
                var selectedDoc = docPane.SelectedContent as LayoutDocument;
                if (selectedDoc?.Content is ViewportControl viewport)
                {
                    // Add the geometry to the current document via the viewport
                    viewport.AddObject(e.Geometry);
                    
                    statusBar.UpdateStatus($"Geometry added to {selectedDoc.Title}");
                }
                else
                {
                    statusBar.UpdateStatus("No active document. Create a new document first.");
                }
            }
        }

        #endregion

        #region Menu Event Handlers

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            _documentCounter++;
            
            // Create a new OpenCAD document
            var document = new OpenCADDocument(
                $"Document{_documentCounter}.cad",
                $"New document created {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            );

            // Create viewport control for the document, passing the document to the constructor
            var viewport = new ViewportControl(document);
            
            // Wire up the status bar
            viewport.SetStatusBar(statusBar);

            // **ADD THIS: Set the document in the command input control**
            dockingArea.CommandInput.SetDocument(document);

            // Get the document pane and add the viewport with the document
            var docPane = dockingArea.GetDocumentPane();
            if (docPane != null)
            {
                var newDoc = dockingArea.AddDocument(
                    document.Filename,
                    $"doc{_documentCounter}",
                    viewport
                );

                if (newDoc != null)
                {
                    statusBar.UpdateStatus($"Created new document: {document.Filename}");
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
            
            // Get the currently selected document
            var docPane = dockingArea.GetDocumentPane();
            if (docPane != null)
            {
                var selectedDoc = docPane.SelectedContent as LayoutDocument;
                if (selectedDoc?.Content is ViewportControl viewport)
                {
                    // Access the OpenCADDocument through the viewport
                    var openCADDoc = viewport.ObjectToDisplay as OpenCADDocument;
                    if (openCADDoc != null)
                    {
                        // TODO: Implement actual save logic here
                        MessageBox.Show(
                            $"Saving: {openCADDoc.Filename}\nDescription: {openCADDoc.Description}", 
                            "Save File", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No document to save.", "Save File", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("No document selected.", "Save File", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            
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
            // Create a temporary OpenCADObject to hold test geometry
            var testContainer = new OpenCADObject();

            // Add some sample geometry for testing
            var line1 = new OCad.Line(
                new Point3D(0, 0, 0),
                new Point3D(5, 5, 5)
            );
            testContainer.Add(line1);

            var line2 = new OCad.Line(
                new Point3D(-3, 0, 0),
                new Point3D(3, 0, 0)
            );
            testContainer.Add(line2);

            var line3 = new OCad.Line(
                new Point3D(0, -3, 0),
                new Point3D(0, 3, 0)
            );
            testContainer.Add(line3);

            var line4 = new OCad.Line(
                new Point3D(0, 0, -3),
                new Point3D(0, 0, 3)
            );
            testContainer.Add(line4);

            // Create viewport control with the test container
            var viewport = new ViewportControl(testContainer);
            
            // Wire up the status bar
            viewport.SetStatusBar(statusBar);

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