using System.IO;
using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.AvalonDock.Layout.Serialization;
using UI.Controls.Viewport;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using OpenCAD;
using System.Linq;
using Xceed.Wpf.AvalonDock.Controls;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Interaction logic for DockingAreaControl.xaml
	/// </summary>
	public partial class DockingAreaControl : UserControl
	{
		private const string LayoutFileName = "layout.xml";
		private LayoutAnchorable? _layersAnchorable;

		public DockingAreaControl()
		{
			InitializeComponent();

			// Set up command state tracking for viewport selection mode
			this.Loaded += (s, e) => SetupCommandStateTracking();

			// Subscribe to active document change to update properties
			this.Loaded += (s, e) => SetupDocumentTracking();

			// Subscribe to layer changes to update properties panel
			this.Loaded += (s, e) => SetupLayerChangeTracking();

			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
		}

		/// <summary>
		/// Gets the DockingManager instance for programmatic access
		/// </summary>
		public DockingManager DockingManager => dockingManager;

		/// <summary>
		/// Gets the CommandInputControl for wiring up events
		/// </summary>
		public CommandInputControl CommandInput => commandInputControl;

		/// <summary>
		/// Gets the PropertiesControl for programmatic access
		/// </summary>
		public PropertiesControl PropertiesPanel => propertiesControl;

		/// <summary>
		/// Gets the LayersControl for programmatic access
		/// </summary>
		public LayersControl LayersPanel => layersControl;

		/// <summary>
		/// Shows or hides the layers panel
		/// </summary>
		public void ShowLayersPanel(bool show)
		{
			// First try to get cached reference
			if (_layersAnchorable == null)
			{
				_layersAnchorable = FindLayoutAnchorable("layers");
			}

			if (_layersAnchorable != null)
			{
				if (show)
				{
					_layersAnchorable.Show();
					System.Diagnostics.Debug.WriteLine("Layers panel shown");
				}
				else
				{
					_layersAnchorable.Hide();
					System.Diagnostics.Debug.WriteLine("Layers panel hidden");
				}
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("Layers panel LayoutAnchorable not found - searching entire layout tree");
				
				// Debug: Print entire layout structure
				PrintLayoutStructure(dockingManager.Layout?.RootPanel, 0);
				
				// Try to find it by control reference instead
				var parent = layersControl.Parent;
				System.Diagnostics.Debug.WriteLine($"LayersControl parent type: {parent?.GetType().Name ?? "null"}");
				
				if (parent is LayoutAnchorableControl anchorableControl)
				{
					System.Diagnostics.Debug.WriteLine("Found layers through control parent");
					_layersAnchorable = anchorableControl.Model as LayoutAnchorable;
					if (_layersAnchorable != null)
					{
						if (show)
							_layersAnchorable.Show();
						else
							_layersAnchorable.Hide();
					}
				}
			}
		}

		/// <summary>
		/// Debug helper to print layout structure
		/// </summary>
		private void PrintLayoutStructure(ILayoutElement? element, int indent)
		{
			if (element == null) return;

			string indentStr = new string(' ', indent * 2);
			
			if (element is LayoutAnchorable anchorable)
			{
				System.Diagnostics.Debug.WriteLine($"{indentStr}LayoutAnchorable: ContentId={anchorable.ContentId}, Title={anchorable.Title}, IsVisible={anchorable.IsVisible}");
			}
			else if (element is LayoutDocument document)
			{
				System.Diagnostics.Debug.WriteLine($"{indentStr}LayoutDocument: ContentId={document.ContentId}, Title={document.Title}");
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"{indentStr}{element.GetType().Name}");
			}

			if (element is ILayoutContainer container)
			{
				foreach (var child in container.Children)
				{
					PrintLayoutStructure(child, indent + 1);
				}
			}
		}

		/// <summary>
		/// Checks if the layers panel is currently visible
		/// </summary>
		public bool IsLayersPanelVisible()
		{
			if (_layersAnchorable == null)
			{
				_layersAnchorable = FindLayoutAnchorable("layers");
			}
			
			return _layersAnchorable?.IsVisible ?? true; // Default to true if not found
		}

		/// <summary>
		/// Finds a LayoutAnchorable by ContentId
		/// </summary>
		private LayoutAnchorable? FindLayoutAnchorable(string contentId)
		{
			return FindLayoutAnchorableRecursive(dockingManager.Layout?.RootPanel, contentId);
		}

		/// <summary>
		/// Recursively searches for a LayoutAnchorable by ContentId
		/// </summary>
		private LayoutAnchorable? FindLayoutAnchorableRecursive(ILayoutContainer? container, string contentId)
		{
			if (container == null) return null;

			// Check if this is the anchorable we're looking for
			if (container is LayoutAnchorable anchorable && anchorable.ContentId == contentId)
				return anchorable;

			// Recursively search children
			foreach (var child in container.Children)
			{
				if (child is LayoutAnchorable childAnchorable && childAnchorable.ContentId == contentId)
					return childAnchorable;

				if (child is ILayoutContainer childContainer)
				{
					var result = FindLayoutAnchorableRecursive(childContainer, contentId);
					if (result != null)
						return result;
				}
			}

			return null;
		}

		/// <summary>
		/// Gets the document pane where document tabs are displayed
		/// </summary>
		public LayoutDocumentPane? GetDocumentPane()
		{
			// Search recursively through the layout for the document pane
			return FindDocumentPane(dockingManager.Layout?.RootPanel);
		}

		/// <summary>
		/// Recursively searches for a LayoutDocumentPane in the layout hierarchy
		/// </summary>
		private LayoutDocumentPane? FindDocumentPane(ILayoutContainer? container)
		{
			if (container == null) return null;

			// Check if this is a document pane
			if (container is LayoutDocumentPane docPane)
				return docPane;

			// Recursively search children
			foreach (var child in container.Children)
			{
				if (child is LayoutDocumentPane foundPane)
					return foundPane;

				if (child is ILayoutContainer childContainer)
				{
					var result = FindDocumentPane(childContainer);
					if (result != null)
						return result;
				}
			}

			return null;
		}

		/// <summary>
		/// Adds a new document to the document pane
		/// </summary>
		/// <param name="title">The title of the document</param>
		/// <param name="contentId">The unique content ID</param>
		/// <param name="content">The content to display</param>
		/// <returns>The created LayoutDocument</returns>
		public LayoutDocument? AddDocument(string title, string contentId, object content)
		{
			var docPane = GetDocumentPane();
			if (docPane != null)
			{
				var newDoc = new LayoutDocument
				{
					Title = title,
					ContentId = contentId,
					Content = content
				};

				docPane.Children.Add(newDoc);
				newDoc.IsSelected = true;

				// Subscribe to selection changes for this document
				newDoc.IsSelectedChanged += OnDocumentIsSelectedChanged;

				// Enable selection mode for new viewports if no command is active
				if (content is ViewportControl viewport)
				{
					var viewModel = CommandInput.DataContext as CommandInputViewModel;
					if (viewModel != null && !viewModel.HasActiveCommand)
					{
						viewport.EnableSelectionMode();
						System.Diagnostics.Debug.WriteLine($"Selection mode ENABLED for new viewport {title}");
					}
					
					// Wire up viewport selection events to restore focus to command input
					CommandInput.WireUpViewportEvents(viewport);
					
					// Wire up viewport selection events to update properties panel
					WireViewportSelectionEvents(viewport);
				}

				// Update properties when a new document is added
				UpdatePropertiesPanel();
				
				// Focus the command input after a short delay to ensure the document is fully loaded
				Dispatcher.BeginInvoke(new Action(() =>
				{
					CommandInput.Focus();
				}), System.Windows.Threading.DispatcherPriority.Loaded);

				return newDoc;
			}
			return null;
		}

		/// <summary>
		/// Applies theme to all TextBox controls in the docking area
		/// </summary>
		public void ApplyThemeToDocuments()
		{
			var docPane = GetDocumentPane();
			if (docPane != null)
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

		/// <summary>
		/// Applies theme resources to a TextBox
		/// </summary>
		private void ApplyThemeToTextBox(TextBox textBox)
		{
			textBox.SetResourceReference(Control.BackgroundProperty, "PrimaryBackgroundBrush");
			textBox.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
		}

		/// <summary>
		/// Get the currently active viewport control from the document pane
		/// </summary>
		public ViewportControl? GetActiveViewport()
		{
			var docPane = GetDocumentPane();
			if (docPane == null)
			{
				System.Diagnostics.Debug.WriteLine("GetActiveViewport: No document pane found");
				return null;
			}

			System.Diagnostics.Debug.WriteLine($"GetActiveViewport: Document pane has {docPane.Children.Count} children");

			// Find the selected document in the document pane
			var selectedDoc = docPane.Children.OfType<LayoutDocument>()
				.FirstOrDefault(d => d.IsSelected);

			if (selectedDoc == null)
			{
				System.Diagnostics.Debug.WriteLine("GetActiveViewport: No selected document found");

				// Log what documents exist
				foreach (var doc in docPane.Children.OfType<LayoutDocument>())
				{
					System.Diagnostics.Debug.WriteLine($"  Document: '{doc.Title}', Content type: {doc.Content?.GetType().Name ?? "null"}");
				}

				return null;
			}

			System.Diagnostics.Debug.WriteLine($"GetActiveViewport: Selected document is '{selectedDoc.Title}', Content type: {selectedDoc.Content?.GetType().Name ?? "null"}");

			// Check if the selected document contains a ViewportControl
			if (selectedDoc.Content is ViewportControl viewport)
			{
				System.Diagnostics.Debug.WriteLine($"GetActiveViewport: Found active viewport for '{selectedDoc.Title}'");
				return viewport;
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"GetActiveViewport: Selected document '{selectedDoc.Title}' does not contain a ViewportControl (contains {selectedDoc.Content?.GetType().Name ?? "null"})");
				return null;
			}
		}

		/// <summary>
		/// Wire up command state changes to control viewport selection mode
		/// </summary>
		private void SetupCommandStateTracking()
		{
			// Get the ViewModel from the CommandInputControl
			var viewModel = CommandInput.DataContext as CommandInputViewModel;
			if (viewModel == null)
			{
				System.Diagnostics.Debug.WriteLine("SetupCommandStateTracking: ViewModel not found");
				return;
			}

			// Subscribe to the ActiveCommandChanged event
			viewModel.ActiveCommandChanged += (s, e) =>
			{
				UpdateViewportSelectionMode();
			};

			// Subscribe to PropertyChanged for HasActiveCommand
			viewModel.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(CommandInputViewModel.HasActiveCommand))
				{
					UpdateViewportSelectionMode();
				}
			};

			System.Diagnostics.Debug.WriteLine("SetupCommandStateTracking: Command state tracking initialized");
		}

		/// <summary>
		/// Wire up layer change notifications to update properties panel
		/// </summary>
		private void SetupLayerChangeTracking()
		{
			// Get the ViewModel from the LayersControl
			var viewModel = layersControl.DataContext as LayersViewModel;
			if (viewModel == null)
			{
				System.Diagnostics.Debug.WriteLine("SetupLayerChangeTracking: LayersViewModel not found");
				return;
			}

			// Subscribe to the LayersModified event
			viewModel.LayersModified += (s, e) =>
			{
				System.Diagnostics.Debug.WriteLine("LayersModified event received - updating properties panel");
				UpdatePropertiesPanel();
			};

			System.Diagnostics.Debug.WriteLine("SetupLayerChangeTracking: Layer change tracking initialized");
		}

		/// <summary>
		/// Update selection mode for all viewports based on command state
		/// </summary>
		private void UpdateViewportSelectionMode()
		{
			var viewModel = CommandInput.DataContext as CommandInputViewModel;
			if (viewModel == null) return;

			bool hasActiveCommand = viewModel.HasActiveCommand;
			
			// Check if the active command requires selection mode
			bool commandRequiresSelection = hasActiveCommand && 
											viewModel.ActiveCommand?.RequiresSelection == true;

			// Get all viewport documents
			var docPane = GetDocumentPane();
			if (docPane != null)
			{
				foreach (var doc in docPane.Children.OfType<LayoutDocument>())
				{
					if (doc.Content is ViewportControl viewport)
					{
						if (hasActiveCommand && !commandRequiresSelection)
						{
							// Disable selection mode when a command is active (unless it requires selection)
							viewport.DisableSelectionMode();
						 System.Diagnostics.Debug.WriteLine($"Selection mode DISABLED for {doc.Title} (command active)");
						}
						else
						{
							// Enable selection mode when no command is active OR when command requires selection
							viewport.EnableSelectionMode();
							System.Diagnostics.Debug.WriteLine($"Selection mode ENABLED for {doc.Title} (no command or command requires selection)");
						}
					}
				}
			}
		}

        /// <summary>
        /// Set up document selection tracking to update properties panel
        /// </summary>
        private void SetupDocumentTracking()
		{
			var docPane = GetDocumentPane();
			if (docPane != null)
			{
				// Subscribe to IsSelectedChanged for all existing documents
				foreach (var doc in docPane.Children.OfType<LayoutDocument>())
				{
					doc.IsSelectedChanged += OnDocumentIsSelectedChanged;
				}
			}
		}

		/// <summary>
		/// Handle document selection changes
		/// </summary>
		private void OnDocumentIsSelectedChanged(object? sender, EventArgs e)
		{
			if (sender is LayoutDocument doc && doc.IsSelected)
			{
				System.Diagnostics.Debug.WriteLine($"Document '{doc.Title}' was selected - updating properties");
				UpdatePropertiesPanel();
				
				// Restore focus to command input when switching documents
				// Use BeginInvoke to ensure the document switch is complete
				Dispatcher.BeginInvoke(new Action(() =>
				{
					CommandInput.Focus();
				}), System.Windows.Threading.DispatcherPriority.Input);
			}
		}

		/// <summary>
		/// Update the properties and layers panels to show the current document
		/// </summary>
		private void UpdatePropertiesPanel()
		{
			var viewport = GetActiveViewport();
			propertiesControl.UpdateFromViewport(viewport);
			layersControl.UpdateFromViewport(viewport);
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			// Try to load layout, but if it fails or doesn't exist, use default
			try
			{
				LoadLayout();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to load layout: {ex.Message}");
			}

			// Cache the layers anchorable reference after layout is loaded
			_layersAnchorable = FindLayoutAnchorable("layers");
			
			// Verify layers control is present
			System.Diagnostics.Debug.WriteLine($"LayersControl initialized: {layersControl != null}");
			if (layersControl != null)
			{
				System.Diagnostics.Debug.WriteLine($"LayersControl DataContext: {layersControl.DataContext != null}");
			}
			System.Diagnostics.Debug.WriteLine($"Layers LayoutAnchorable found: {_layersAnchorable != null}");
		}

		private void OnUnloaded(object sender, RoutedEventArgs e)
		{
			SaveLayout();
		}

		private void SaveLayout()
		{
			try
			{
				var layoutSerializer = new XmlLayoutSerializer(dockingManager);
				var layoutPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LayoutFileName);
				using var stream = new StreamWriter(layoutPath);
				layoutSerializer.Serialize(stream);
				System.Diagnostics.Debug.WriteLine($"Layout saved to: {layoutPath}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to save layout: {ex.Message}");
			}
		}

		private void LoadLayout()
		{
			var layoutPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LayoutFileName);
			
			if (!File.Exists(layoutPath))
			{
				System.Diagnostics.Debug.WriteLine($"Layout file not found: {layoutPath} - using default layout");
				return;
			}

			System.Diagnostics.Debug.WriteLine($"Loading layout from: {layoutPath}");

			try
			{
				var layoutSerializer = new XmlLayoutSerializer(dockingManager);
				
				// Handle missing content when deserializing
				layoutSerializer.LayoutSerializationCallback += (s, args) =>
				{
					System.Diagnostics.Debug.WriteLine($"Layout deserialization callback for ContentId: {args.Model.ContentId}");
					
					// Restore controls that might be missing from saved layout
					switch (args.Model.ContentId)
					{
						case "commandInput":
							args.Content = commandInputControl;
							System.Diagnostics.Debug.WriteLine("Restored commandInput");
							break;
						case "properties":
							args.Content = propertiesControl;
							System.Diagnostics.Debug.WriteLine("Restored properties");
							break;
						case "layers":
							args.Content = layersControl;
							System.Diagnostics.Debug.WriteLine("Restored layers");
							break;
						case "solutionExplorer":
						case "output":
						case "errorList":
							// Keep default content for these
							System.Diagnostics.Debug.WriteLine($"Keeping default content for {args.Model.ContentId}");
							break;
						default:
							// ✅ CHANGED: Don't cancel document deserialization
							// Instead, let it fail silently - the document won't be restored
							// but the layout panels will still work
							if (args.Model is LayoutDocument)
							{
								// Just don't set args.Content - this allows the layout to load
								// but the document won't be restored (which is what we want)
								System.Diagnostics.Debug.WriteLine($"Skipping document restoration for: {args.Model.ContentId}");
								// DON'T set args.Cancel = true
							}
							break;
					}
				};
				
				using var stream = new StreamReader(layoutPath);
				layoutSerializer.Deserialize(stream);

				// After loading layout, subscribe to all document selection changes
				var docPane = GetDocumentPane();
				if (docPane != null)
				{
					foreach (var doc in docPane.Children.OfType<LayoutDocument>())
					{
						doc.IsSelectedChanged += OnDocumentIsSelectedChanged;
					}
				}
				
				System.Diagnostics.Debug.WriteLine("Layout loaded successfully");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error loading layout: {ex.Message}");
				// If layout loading fails, delete the corrupt file so we use default next time
				try
				{
					File.Delete(layoutPath);
					System.Diagnostics.Debug.WriteLine("Deleted corrupt layout file");
				}
				catch { }
			}
		}

		// Add this method to wire up selection change events
		private void WireViewportSelectionEvents(ViewportControl viewport)
		{
            var viewModel = viewport.DataContext as ViewportViewModel;
            if (viewModel != null)
            {
                // Subscribe to SelectionChanged event
                viewModel.SelectionChanged += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("Selection changed - updating properties panel");
                    UpdatePropertiesPanel();
                };

                System.Diagnostics.Debug.WriteLine("Wired up viewport selection events");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WARNING: ViewportViewModel not found when trying to wire selection events");
            }
        }
	}
}
