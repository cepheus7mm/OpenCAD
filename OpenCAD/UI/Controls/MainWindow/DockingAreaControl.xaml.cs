using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UI.Controls.Viewport;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using OpenCAD;
using System.Collections.ObjectModel;
using System.Linq;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Interaction logic for DockingAreaControl.xaml
	/// </summary>
	public partial class DockingAreaControl : UserControl
	{
		public DockingAreaControl()
		{
			InitializeComponent();
			
			// Set up command state tracking for viewport selection mode
			this.Loaded += (s, e) => SetupCommandStateTracking();
			
			// Subscribe to active document change to update properties
			this.Loaded += (s, e) => SetupDocumentTracking();
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
				
				// Enable selection mode for new viewports if no command is active
				if (content is ViewportControl viewport)
				{
					var viewModel = CommandInput.DataContext as CommandInputViewModel;
					if (viewModel != null && !viewModel.HasActiveCommand)
					{
						viewport.EnableSelectionMode();
						System.Diagnostics.Debug.WriteLine($"Selection mode ENABLED for new viewport {title}");
					}
				}
				
				// Update properties when a new document is added
				UpdatePropertiesPanel();
				
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
		/// Update selection mode for all viewports based on command state
		/// </summary>
		private void UpdateViewportSelectionMode()
		{
			var viewModel = CommandInput.DataContext as CommandInputViewModel;
			if (viewModel == null) return;

			bool hasActiveCommand = viewModel.HasActiveCommand;
			
			// Get all viewport documents
			var docPane = GetDocumentPane();
			if (docPane != null)
			{
				foreach (var doc in docPane.Children.OfType<LayoutDocument>())
				{
					if (doc.Content is ViewportControl viewport)
					{
						if (hasActiveCommand)
						{
							// Disable selection mode when a command is active
							viewport.DisableSelectionMode();
							System.Diagnostics.Debug.WriteLine($"Selection mode DISABLED for {doc.Title} (command active)");
						}
						else
						{
							// Enable selection mode when no command is active
							viewport.EnableSelectionMode();
							System.Diagnostics.Debug.WriteLine($"Selection mode ENABLED for {doc.Title} (no command active)");
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
				// Subscribe to document selection changes
				dockingManager.ActiveContentChanged += (s, e) =>
				{
					UpdatePropertiesPanel();
				};
			}
		}

		/// <summary>
		/// Update the properties panel to show the current document's properties
		/// </summary>
		private void UpdatePropertiesPanel()
		{
			var viewport = GetActiveViewport();
			if (viewport != null)
			{
				var document = viewport.ObjectToDisplay as OpenCADDocument;
				if (document != null)
				{
					var properties = new ObservableCollection<PropertyItem>();

					// Add object metadata
					properties.Add(new PropertyItem { Property = "Type", Value = "OpenCADDocument" });
					properties.Add(new PropertyItem { Property = "ID", Value = document.ID.ToString() });

					// Add all properties from the property collection with their names
					var objectProperties = document.GetProperties();
					if (objectProperties != null && objectProperties.Any())
					{
						foreach (var prop in objectProperties)
						{
							if (prop != null)
							{
								// Handle properties with multiple values
								if (prop.Count > 1)
								{
									// Show each named value separately
									for (int i = 0; i < prop.Count; i++)
									{
										var propValue = prop.GetPropertyValue(i);
										properties.Add(new PropertyItem 
										{ 
											Property = propValue.Name, 
											Value = FormatPropertyValue(propValue.Value) 
										});
									}
								}
								else if (prop.Count == 1)
								{
									// Single value property - use its name
									var propValue = prop.GetPropertyValue(0);
									properties.Add(new PropertyItem 
									{ 
										Property = propValue.Name, 
										Value = FormatPropertyValue(propValue.Value) 
									});
								}
							}
						}
					}

					// Add layer count
					var layerCount = document.GetLayers().Count();
					properties.Add(new PropertyItem { Property = "--- Layers ---", Value = "" });
					properties.Add(new PropertyItem { Property = "Layer Count", Value = layerCount.ToString() });

					// Add child object count (excluding the layers container)
					var childCount = document.GetChildren().Count();
					properties.Add(new PropertyItem { Property = "--- Children ---", Value = "" });
					properties.Add(new PropertyItem { Property = "Total Children", Value = childCount.ToString() });
					properties.Add(new PropertyItem { Property = "Drawable Objects", Value = (childCount - 1).ToString() }); // -1 for layers container

					propertiesGrid.DataContext = properties;
					System.Diagnostics.Debug.WriteLine($"Properties updated for document: {document.Filename} ({properties.Count} properties)");
					return;
				}
			}

			// No viewport or document found - clear properties
			propertiesGrid.DataContext = new ObservableCollection<PropertyItem>
			{
				new PropertyItem { Property = "No document", Value = "—" }
			};
		}

		/// <summary>
		/// Format a property value for display
		/// </summary>
		private string FormatPropertyValue(object value)
		{
			if (value == null)
				return "(null)";

			// Handle specific types
			if (value is OpenCAD.Geometry.Point3D point)
				return $"({point.X:F2}, {point.Y:F2}, {point.Z:F2})";
			
			if (value is System.Drawing.Color color)
				return $"ARGB({color.A}, {color.R}, {color.G}, {color.B})";
			
			if (value is Layer layer)
				return layer.Name;
			
			if (value is Guid guid)
				return guid.ToString();
			
			if (value is LineType lineType)
				return lineType.ToString();
			
			if (value is LineWeight lineWeight)
				return lineWeight.ToDisplayString();
			
			if (value is double doubleVal)
				return doubleVal.ToString("F4");
			
			if (value is float floatVal)
				return floatVal.ToString("F4");
			
			if (value is bool boolVal)
				return boolVal ? "True" : "False";
			
			if (value is int intVal)
				return intVal.ToString();

			// Default to ToString()
			return value.ToString() ?? "(empty)";
		}
	}
	
	/// <summary>
	/// Simple class to represent property items in the properties panel
	/// </summary>
	public class PropertyItem
	{
		public string Property { get; set; } = string.Empty;
		public string Value { get; set; } = string.Empty;
	}
}
