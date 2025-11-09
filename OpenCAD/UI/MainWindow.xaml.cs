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
using OpenCAD;
using UI.Controls.MainWindow;
using OpenCAD.Serialization;
using Microsoft.Win32; // For file dialogs
using System.Windows.Threading;

using OCad = OpenCAD.Geometry;
using System.ComponentModel;

namespace UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int _documentCounter = 0;
        private DispatcherTimer? _autoSaveTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeDockingSystem();
            
            // **ADD THIS: Set up the viewport provider for command input**
            dockingArea.CommandInput.SetActiveViewportProvider(() => dockingArea.GetActiveViewport());
            
            // Hook up menu bar events
            menuBar.NewFileRequested += (s, e) => NewFile_Click(s!, new RoutedEventArgs());
            menuBar.ExitRequested += (s, e) => Exit_Click(s!, new RoutedEventArgs());
            menuBar.LightThemeRequested += (s, e) => LightTheme_Click(s!, new RoutedEventArgs());
            menuBar.DarkThemeRequested += (s, e) => DarkTheme_Click(s!, new RoutedEventArgs());
            menuBar.LayersVisibilityChanged += MenuBar_LayersVisibilityChanged;
            menuBar.SaveAsRequested += (s, e) => SaveAs_Click(s!, new RoutedEventArgs());
            
            // Hook up toolbar events
            toolBar.NewFileRequested += (s, e) => NewFile_Click(s!, new RoutedEventArgs());
            toolBar.OpenRequested += (s, e) => Open_Click(s!, new RoutedEventArgs());
            toolBar.SaveRequested += (s, e) => Save_Click(s!, new RoutedEventArgs());
            toolBar.CutRequested += (s, e) => Cut_Click(s!, new RoutedEventArgs());
            toolBar.CopyRequested += (s, e) => Copy_Click(s!, new RoutedEventArgs());
            toolBar.PasteRequested += (s, e) => Paste_Click(s!, new RoutedEventArgs());
            
            // Hook up titlebar button events when the window is loaded
            this.Loaded += MainWindow_Loaded;
            
            // Hook up command input event
            dockingArea.CommandInput.GeometryCreated += CommandInput_GeometryCreated;
            
            // ADD THIS: Hook up PreviewKeyDown for global keyboard routing
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            
            // Setup auto-save timer (every 5 minutes)
            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _autoSaveTimer.Tick += AutoSave_Tick;
            _autoSaveTimer.Start();
        }

        private void AutoSave_Tick(object? sender, EventArgs e)
        {
            var docPane = dockingArea.GetDocumentPane();
            if (docPane != null)
            {
                foreach (var doc in docPane.Children.OfType<LayoutDocument>())
                {
                    if (doc.Content is ViewportControl viewport)
                    {
                        var openCADDoc = viewport.Document;
                        if (openCADDoc?.HasUnsavedChanges == true)
                        {
                            try
                            {
                                DocumentSerializer.SaveToJson(openCADDoc, openCADDoc.Filename);
                                openCADDoc.MarkAsSaved();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Auto-save failed: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handle keyboard input at the window level to route ESC and other keys
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow.PreviewKeyDown: Key={e.Key}, Modifiers={Keyboard.Modifiers}");
            
            // Handle Ctrl+S for Save
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Save_Click(this, new RoutedEventArgs());
                e.Handled = true;
                System.Diagnostics.Debug.WriteLine("Ctrl+S - Save executed");
                return;
            }
            
            // Handle Ctrl+Shift+S for Save As
            if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                SaveAs_Click(this, new RoutedEventArgs());
                e.Handled = true;
                System.Diagnostics.Debug.WriteLine("Ctrl+Shift+S - Save As executed");
                return;
            }
            
            // Handle Ctrl+O for Open
            if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Open_Click(this, new RoutedEventArgs());
                e.Handled = true;
                System.Diagnostics.Debug.WriteLine("Ctrl+O - Open executed");
                return;
            }
            
            // Handle ESC key - route to active viewport
            if (e.Key == Key.Escape)
            {
                var viewport = dockingArea.GetActiveViewport();
                if (viewport != null)
                {
                    // Try to handle ESC in the viewport
                    if (viewport.HandleEscapeKey())
                    {
                        e.Handled = true;
                        System.Diagnostics.Debug.WriteLine("ESC handled by viewport");
                        return;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("ESC not handled by viewport - allowing default behavior");
            }
            
            // Handle Delete key - execute the Erase command
            if (e.Key == Key.Delete)
            {
                var viewport = dockingArea.GetActiveViewport();
                if (viewport != null)
                {
                    var viewModel = viewport.DataContext as ViewportViewModel;
                    
                    // Only execute erase if there are selected objects
                    if (viewModel != null && viewModel.SelectedObjects.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Delete key pressed - executing Erase command for {viewModel.SelectedObjects.Count} selected object(s)");
                        
                        // Execute the erase command programmatically
                        dockingArea.CommandInput.ExecuteCommandProgrammatically("erase");
                        
                        e.Handled = true;
                        System.Diagnostics.Debug.WriteLine("Delete handled - Erase command executed");
                        return;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("Delete not handled - no selection");
            }
            
            // Handle Ctrl+Z for Undo
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var undoManager = dockingArea.CommandInput.GetUndoRedoManager();
                if (undoManager.CanUndo)
                {
                    undoManager.Undo();
                    statusBar.UpdateStatus($"Undo: {undoManager.UndoDescription ?? "action"}");
                    e.Handled = true;
                    System.Diagnostics.Debug.WriteLine("Ctrl+Z - Undo executed");
                }
                return;
            }
            
            // Handle Ctrl+Y for Redo
            if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var undoManager = dockingArea.CommandInput.GetUndoRedoManager();
                if (undoManager.CanRedo)
                {
                    undoManager.Redo();
                    statusBar.UpdateStatus($"Redo: {undoManager.RedoDescription ?? "action"}");
                    e.Handled = true;
                    System.Diagnostics.Debug.WriteLine("Ctrl+Y - Redo executed");
                }
                return;
            }
            
            // Add other global keyboard shortcuts here as needed
            // Example: F1 for help, Ctrl+S for save, etc.
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Find and hook up titlebar button events
            HookupTitleBarButtons();
            
            // Update menu to reflect initial layers visibility
            menuBar.UpdateLayersVisibility(dockingArea.IsLayersPanelVisible());
        }

        private void MenuBar_LayersVisibilityChanged(object? sender, bool isVisible)
        {
            dockingArea.ShowLayersPanel(isVisible);
            statusBar.UpdateStatus(isVisible ? "Layers panel shown" : "Layers panel hidden");
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
            
            // Wire up the status bar - the viewport has its own ViewportSettings instance
            viewport.SetStatusBar(statusBar);
            
            // Get the ViewportSettings from the viewport
            var viewportSettings = viewport.GetViewportSettings();
            statusBar.SetViewportSettings(viewportSettings);
            statusBar.ViewportSettingsChanged += (s, e) => 
            {
                // Update snapping state from settings
                viewport.UpdateSnappingFromSettings();
                
                // Force viewport to refresh when settings change
                viewport.Refresh();
            };

            // Set the document in the command input control
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
                    
                    // Focus the command input after the UI has finished updating
                    // Use Dispatcher to ensure the docking layout is complete first
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        dockingArea.CommandInput.FocusCommandInput();
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
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
            statusBar.UpdateStatus("Opening file...");
            
            // Show Open File Dialog
            var openFileDialog = new OpenFileDialog
            {
                Filter = "OpenCAD Files (*.ocad)|*.ocad|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".ocad",
                Title = "Open CAD Document",
                CheckFileExists = true
            };
            
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Load the document
                    var loadedDocument = DocumentSerializer.LoadFromJson(openFileDialog.FileName);
                    
                    if (loadedDocument != null)
                    {
                        _documentCounter++;
                        
                        // Update filename to match loaded file
                        loadedDocument.Filename = openFileDialog.FileName;
                        
                        // Create viewport for the loaded document
                        var viewport = new ViewportControl(loadedDocument);
                        
                        // Wire up the status bar
                        viewport.SetStatusBar(statusBar);
                        
                        // Get ViewportSettings and wire up events
                        var viewportSettings = viewport.GetViewportSettings();
                        statusBar.SetViewportSettings(viewportSettings);
                        statusBar.ViewportSettingsChanged += (s, args) => 
                        {
                            viewport.UpdateSnappingFromSettings();
                            viewport.Refresh();
                        };

                        // Set the document in the command input control
                        dockingArea.CommandInput.SetDocument(loadedDocument);

                        // Add to document pane
                        var docPane = dockingArea.GetDocumentPane();
                        if (docPane != null)
                        {
                            var newDoc = dockingArea.AddDocument(
                                System.IO.Path.GetFileName(openFileDialog.FileName),
                                $"doc{_documentCounter}",
                                viewport
                            );

                            if (newDoc != null)
                            {
                                statusBar.UpdateStatus($"Opened: {openFileDialog.FileName}");
                                System.Diagnostics.Debug.WriteLine($"Document opened: {openFileDialog.FileName}");
                                
                                // Focus command input
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    dockingArea.CommandInput.FocusCommandInput();
                                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to load document. The file may be corrupted or in an incompatible format.", 
                            "Open File Error", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
                        statusBar.UpdateStatus("Open failed");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error opening file:\n{ex.Message}", 
                        "Open File Error", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                    statusBar.UpdateStatus("Open failed");
                    System.Diagnostics.Debug.WriteLine($"Open error: {ex}");
                }
            }
            else
            {
                statusBar.UpdateStatus("Open cancelled");
            }
            
            statusBar.UpdateStatus("Ready");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            statusBar.UpdateStatus("Saving file...");
            
            // Get the currently selected document
            var docPane = dockingArea.GetDocumentPane();
            if (docPane != null)
            {
                var selectedDoc = docPane.SelectedContent as LayoutDocument;
                if (selectedDoc?.Content is ViewportControl viewport)
                {
                    // Access the OpenCADDocument through the viewport
                    var openCADDoc = viewport.Document;
                    if (openCADDoc != null)
                    {
                        try
                        {
                            // If filename is empty or default, prompt for Save As
                            if (string.IsNullOrEmpty(openCADDoc.Filename) || 
                                openCADDoc.Filename.StartsWith("Document"))
                            {
                                SaveAs_Click(sender, e);
                                return;
                            }
                            
                            // Save to existing filename
                            DocumentSerializer.SaveToJson(openCADDoc, openCADDoc.Filename);
                            statusBar.UpdateStatus($"Saved: {openCADDoc.Filename}");
                            System.Diagnostics.Debug.WriteLine($"Document saved to: {openCADDoc.Filename}");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Error saving file:\n{ex.Message}", 
                                "Save Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
                            statusBar.UpdateStatus("Save failed");
                            System.Diagnostics.Debug.WriteLine($"Save error: {ex}");
                        }
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

        // Add a Save As handler
        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            statusBar.UpdateStatus("Save As...");
            
            var docPane = dockingArea.GetDocumentPane();
            if (docPane != null)
            {
                var selectedDoc = docPane.SelectedContent as LayoutDocument;
                if (selectedDoc?.Content is ViewportControl viewport)
                {
                    var openCADDoc = viewport.Document;
                    if (openCADDoc != null)
                    {
                        // Show Save File Dialog
                        var saveFileDialog = new SaveFileDialog
                        {
                            Filter = "OpenCAD Files (*.ocad)|*.ocad|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                            DefaultExt = ".ocad",
                            FileName = openCADDoc.Filename,
                            Title = "Save CAD Document"
                        };
                        
                        if (saveFileDialog.ShowDialog() == true)
                        {
                            try
                            {
                                // Update document filename
                                openCADDoc.Filename = saveFileDialog.FileName;
                                
                                // Save the document
                                DocumentSerializer.SaveToJson(openCADDoc, saveFileDialog.FileName);
                                
                                // Update tab title
                                selectedDoc.Title = System.IO.Path.GetFileName(saveFileDialog.FileName);
                                
                                statusBar.UpdateStatus($"Saved: {saveFileDialog.FileName}");
                                System.Diagnostics.Debug.WriteLine($"Document saved to: {saveFileDialog.FileName}");
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(
                                    $"Error saving file:\n{ex.Message}", 
                                    "Save Error", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Error);
                                statusBar.UpdateStatus("Save failed");
                                System.Diagnostics.Debug.WriteLine($"Save error: {ex}");
                            }
                        }
                        else
                        {
                            statusBar.UpdateStatus("Save cancelled");
                        }
                    }
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
        }

        #endregion

        protected override void OnClosing(CancelEventArgs e)
        {
            var docPane = dockingArea.GetDocumentPane();
            if (docPane != null)
            {
                var unsavedDocs = new List<string>();
                
                foreach (var doc in docPane.Children.OfType<LayoutDocument>())
                {
                    if (doc.Content is ViewportControl viewport)
                    {
                        var openCADDoc = viewport.Document;
                        if (openCADDoc?.HasUnsavedChanges == true)
                        {
                            unsavedDocs.Add(openCADDoc.Filename);
                        }
                    }
                }
                
                if (unsavedDocs.Count > 0)
                {
                    var result = MessageBox.Show(
                        $"You have {unsavedDocs.Count} unsaved document(s). Save before closing?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        // Save all documents
                        foreach (var doc in docPane.Children.OfType<LayoutDocument>())
                        {
                            if (doc.Content is ViewportControl vp && vp.Document?.HasUnsavedChanges == true)
                            {
                                try
                                {
                                    DocumentSerializer.SaveToJson(vp.Document, vp.Document.Filename);
                                    vp.Document.MarkAsSaved();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Save failed: {ex.Message}");
                                }
                            }
                        }
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
            
            base.OnClosing(e);
        }
    }
}