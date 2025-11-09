using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using OpenCAD;
using UI.Controls.Viewport;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// ViewModel for the LayersControl that manages layer display and editing
	/// </summary>
	public class LayersViewModel : ObservableObject
	{
		private ObservableCollection<LayerItem> _layers;
		private LayerItem? _selectedLayer;
		private ViewportControl? _activeViewport;
		private OpenCADDocument? _currentDocument;

		/// <summary>
		/// Event raised when layers are modified (added, deleted, current changed)
		/// </summary>
		public event EventHandler? LayersModified;

		/// <summary>
		/// Gets the collection of layers to display
		/// </summary>
		public ObservableCollection<LayerItem> Layers
		{
			get => _layers;
			set => SetField(ref _layers, value);
		}

		/// <summary>
		/// Gets or sets the currently selected layer item
		/// </summary>
		public LayerItem? SelectedLayer
		{
			get => _selectedLayer;
			set => SetField(ref _selectedLayer, value);
		}

		/// <summary>
		/// Gets or sets the active viewport to display layers from
		/// </summary>
		public ViewportControl? ActiveViewport
		{
			get => _activeViewport;
			set
			{
				if (SetField(ref _activeViewport, value))
				{
					UpdateFromViewport(value);
				}
			}
		}

		/// <summary>
		/// Gets the collection of available line types for the dropdown
		/// </summary>
		public ObservableCollection<LineType> AvailableLineTypes { get; }

		/// <summary>
		/// Gets the collection of available line weights for the dropdown
		/// </summary>
		public ObservableCollection<LineWeight> AvailableLineWeights { get; }

		/// <summary>
		/// Command to create a new layer
		/// </summary>
		public ICommand NewLayerCommand { get; }

		/// <summary>
		/// Command to delete the selected layer
		/// </summary>
		public ICommand DeleteLayerCommand { get; }

		/// <summary>
		/// Command to set the selected layer as current
		/// </summary>
		public ICommand SetCurrentLayerCommand { get; }

		/// <summary>
		/// Command to toggle layer visibility
		/// </summary>
		public ICommand ToggleVisibilityCommand { get; }

		/// <summary>
		/// Command to toggle layer locked state
		/// </summary>
		public ICommand ToggleLockCommand { get; }

		/// <summary>
		/// Command to refresh layers from the active viewport
		/// </summary>
		public ICommand RefreshCommand { get; }

		public LayersViewModel()
		{
			_layers = new ObservableCollection<LayerItem>();
			
			// Initialize available line types (exclude ByLayer for layer properties)
			AvailableLineTypes = new ObservableCollection<LineType>
			{
				LineType.Continuous,
				LineType.Dashed,
				LineType.Dotted,
				LineType.DashDot,
				LineType.DashDotDot,
				LineType.Center,
				LineType.Hidden,
				LineType.Phantom
			};

			// Initialize available line weights (exclude ByLayer for layer properties)
			AvailableLineWeights = new ObservableCollection<LineWeight>
			{
				LineWeight.Default,
				LineWeight.Hairline,
				LineWeight.LineWeight005,
				LineWeight.LineWeight009,
				LineWeight.LineWeight013,
				LineWeight.LineWeight015,
				LineWeight.LineWeight018,
				LineWeight.LineWeight020,
				LineWeight.LineWeight025,
				LineWeight.LineWeight030,
				LineWeight.LineWeight035,
				LineWeight.LineWeight040,
				LineWeight.LineWeight050,
				LineWeight.LineWeight053,
				LineWeight.LineWeight060,
				LineWeight.LineWeight070,
				LineWeight.LineWeight080,
				LineWeight.LineWeight090,
				LineWeight.LineWeight100,
				LineWeight.LineWeight106,
				LineWeight.LineWeight120,
				LineWeight.LineWeight140,
				LineWeight.LineWeight158,
				LineWeight.LineWeight200,
				LineWeight.LineWeight211
			};
			
			// Initialize commands
			NewLayerCommand = new UI.RelayCommand(OnNewLayer, CanNewLayer);
			DeleteLayerCommand = new UI.RelayCommand(OnDeleteLayer, CanDeleteLayer);
			SetCurrentLayerCommand = new UI.RelayCommand(OnSetCurrentLayer, CanSetCurrentLayer);
			ToggleVisibilityCommand = new UI.RelayCommand<LayerItem>(OnToggleVisibility);
			ToggleLockCommand = new UI.RelayCommand<LayerItem>(OnToggleLock);
			RefreshCommand = new UI.RelayCommand(OnRefresh, CanRefresh);

			// Initialize with empty state
			ClearLayers();
		}

		/// <summary>
		/// Update layers from the given viewport
		/// </summary>
		public void UpdateFromViewport(ViewportControl? viewport)
		{
			if (viewport == null)
			{
				_currentDocument = null;
				ClearLayers();
				return;
			}

			var document = viewport.ObjectToDisplay as OpenCADDocument;
			_currentDocument = document;
			
			if (document != null)
			{
				DisplayDocumentLayers(document);
			}
			else
			{
				ClearLayers();
			}
		}

		/// <summary>
		/// Display layers for an OpenCAD document
		/// </summary>
		private void DisplayDocumentLayers(OpenCADDocument document)
		{
			var layers = new ObservableCollection<LayerItem>();
			var currentLayerId = document.CurrentLayer?.ID;

			foreach (var layer in document.GetLayers())
			{
				var layerItem = new LayerItem
				{
					Layer = layer,
					Name = layer.Name,
					Color = layer.Color,
					LineType = layer.LineType,
					LineWeight = layer.LineWeight,
					IsVisible = layer.IsVisible,
					IsLocked = layer.IsLocked,
					IsCurrent = layer.ID == currentLayerId
				};

				// Subscribe to property changes to update the underlying layer
				layerItem.PropertyChanged += (s, e) => OnLayerItemPropertyChanged(layerItem, e.PropertyName);

				layers.Add(layerItem);
			}

			Layers = layers;
			System.Diagnostics.Debug.WriteLine(
				string.Format(OpenCADStrings.LayersUpdatedFormat, document.Filename, layers.Count));
		}

		/// <summary>
		/// Clear the layers display
		/// </summary>
		private void ClearLayers()
		{
			Layers = new ObservableCollection<LayerItem>();
		}

		/// <summary>
		/// Raise the LayersModified event
		/// </summary>
		private void RaiseLayersModified()
		{
			LayersModified?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Handle property changes on layer items
		/// </summary>
		private void OnLayerItemPropertyChanged(LayerItem layerItem, string? propertyName)
		{
			if (layerItem.Layer == null)
				return;

			// Update the underlying layer based on changed property
			switch (propertyName)
			{
				case nameof(LayerItem.Name):
					// Validate and update layer name
					if (!string.IsNullOrWhiteSpace(layerItem.Name))
					{
						// Check if name is unique (excluding current layer)
						bool isDuplicate = _currentDocument?.GetLayers()
							.Any(l => l.ID != layerItem.Layer.ID && l.Name == layerItem.Name) ?? false;
						
						if (!isDuplicate)
						{
							layerItem.Layer.Name = layerItem.Name;
							System.Diagnostics.Debug.WriteLine($"Layer name changed to: {layerItem.Name}");
							RaiseLayersModified();
						}
						else
						{
							// Revert to original name if duplicate
							layerItem.Name = layerItem.Layer.Name;
							System.Diagnostics.Debug.WriteLine($"Layer name '{layerItem.Name}' already exists - reverting");
						}
					}
					else
					{
						// Revert to original if empty
						layerItem.Name = layerItem.Layer.Name;
						System.Diagnostics.Debug.WriteLine("Layer name cannot be empty - reverting");
					}
					break;
				case nameof(LayerItem.IsVisible):
					layerItem.Layer.IsVisible = layerItem.IsVisible;
					ActiveViewport?.Refresh();
					break;
				case nameof(LayerItem.IsLocked):
					layerItem.Layer.IsLocked = layerItem.IsLocked;
					break;
				case nameof(LayerItem.Color):
					layerItem.Layer.Color = layerItem.Color;
					ActiveViewport?.Refresh();
					RaiseLayersModified();
					break;
				case nameof(LayerItem.LineType):
					layerItem.Layer.LineType = layerItem.LineType;
					ActiveViewport?.Refresh();
					RaiseLayersModified();
					break;
				case nameof(LayerItem.LineWeight):
					layerItem.Layer.LineWeight = layerItem.LineWeight;
					ActiveViewport?.Refresh();
					RaiseLayersModified();
					break;
			}
		}

		/// <summary>
		/// Set the specified layer as the current layer (called from double-click)
		/// </summary>
		public void SetLayerAsCurrent(LayerItem layerItem)
		{
			if (layerItem == null || _currentDocument == null)
				return;

			_currentDocument.CurrentLayer = layerItem.Layer;
			System.Diagnostics.Debug.WriteLine(
				string.Format(OpenCADStrings.CurrentLayerChangedFormat, layerItem.Name));

			// Refresh the display to update the current indicator
			UpdateFromViewport(ActiveViewport);

			// Notify that layers were modified (current layer changed)
			RaiseLayersModified();
		}

		/// <summary>
		/// Toggle the visibility of the specified layer (called from double-click)
		/// </summary>
		public void ToggleLayerVisibility(LayerItem layerItem)
		{
			if (layerItem == null)
				return;

			layerItem.IsVisible = !layerItem.IsVisible;
			System.Diagnostics.Debug.WriteLine($"Layer '{layerItem.Name}' visibility toggled to {layerItem.IsVisible}");
		}

		#region Command Handlers

		private bool CanNewLayer() => _currentDocument != null;

		private void OnNewLayer()
		{
			if (_currentDocument == null)
				return;

			// Generate a unique layer name
			int counter = 1;
			string layerName;
			do
			{
				layerName = $"{OpenCADStrings.NewLayerPrefix}{counter}";
				counter++;
			}
			while (_currentDocument.GetLayer(layerName) != null);

			// Create the new layer
			var newLayer = _currentDocument.CreateLayer(layerName);
			if (newLayer != null)
			{
				System.Diagnostics.Debug.WriteLine(
					string.Format(OpenCADStrings.LayerCreatedFormat, layerName));
				
				// Refresh the display
				UpdateFromViewport(ActiveViewport);
				
				// Select the new layer
				SelectedLayer = Layers.FirstOrDefault(l => l.Name == layerName);

				// Notify that layers were modified
				RaiseLayersModified();
			}
		}

		private bool CanDeleteLayer() => SelectedLayer != null && 
										  SelectedLayer.Name != OpenCADStrings.DefaultLayerName &&
										  !SelectedLayer.IsCurrent;

		private void OnDeleteLayer()
		{
			if (SelectedLayer == null || _currentDocument == null)
				return;

			if (SelectedLayer.Name == OpenCADStrings.DefaultLayerName)
			{
				System.Diagnostics.Debug.WriteLine(OpenCADStrings.CannotDeleteLayer0);
				return;
			}

			if (SelectedLayer.IsCurrent)
			{
				System.Diagnostics.Debug.WriteLine(OpenCADStrings.CannotDeleteCurrentLayer);
				return;
			}

			string layerName = SelectedLayer.Name;
			if (_currentDocument.RemoveLayer(layerName))
			{
				System.Diagnostics.Debug.WriteLine(
					string.Format(OpenCADStrings.LayerDeletedFormat, layerName));
				
				// Refresh the display
				UpdateFromViewport(ActiveViewport);

				// Notify that layers were modified
				RaiseLayersModified();
			}
		}

		private bool CanSetCurrentLayer() => SelectedLayer != null;

		private void OnSetCurrentLayer()
		{
			if (SelectedLayer == null || _currentDocument == null)
				return;

			_currentDocument.CurrentLayer = SelectedLayer.Layer;
			System.Diagnostics.Debug.WriteLine(
				string.Format(OpenCADStrings.CurrentLayerChangedFormat, SelectedLayer.Name));

			// Refresh the display to update the current indicator
			UpdateFromViewport(ActiveViewport);

			// Notify that layers were modified (current layer changed)
			RaiseLayersModified();
		}

		private void OnToggleVisibility(LayerItem? layerItem)
		{
			if (layerItem != null)
			{
				layerItem.IsVisible = !layerItem.IsVisible;
			}
		}

		private void OnToggleLock(LayerItem? layerItem)
		{
			if (layerItem != null)
			{
				layerItem.IsLocked = !layerItem.IsLocked;
			}
		}

		private bool CanRefresh() => ActiveViewport != null;

		private void OnRefresh()
		{
			UpdateFromViewport(ActiveViewport);
		}

		#endregion
	}

	/// <summary>
	/// Represents a layer item in the layers list
	/// </summary>
	public class LayerItem : ObservableObject
	{
		private string _name = string.Empty;
		private System.Drawing.Color _color;
		private LineType _lineType;
		private LineWeight _lineWeight;
		private bool _isVisible;
		private bool _isLocked;
		private bool _isCurrent;

		/// <summary>
		/// Gets or sets the underlying layer object
		/// </summary>
		public OpenCADLayer? Layer { get; set; }

		/// <summary>
		/// Gets or sets the layer name
		/// </summary>
		public string Name
		{
			get => _name;
			set => SetField(ref _name, value);
		}

		/// <summary>
		/// Gets or sets the layer color
		/// </summary>
		public System.Drawing.Color Color
		{
			get => _color;
			set => SetField(ref _color, value);
		}

		/// <summary>
		/// Gets or sets the layer line type
		/// </summary>
		public LineType LineType
		{
			get => _lineType;
			set => SetField(ref _lineType, value);
		}

		/// <summary>
		/// Gets or sets the layer line weight
		/// </summary>
		public LineWeight LineWeight
		{
			get => _lineWeight;
			set => SetField(ref _lineWeight, value);
		}

		/// <summary>
		/// Gets or sets whether the layer is visible
		/// </summary>
		public bool IsVisible
		{
			get => _isVisible;
			set
			{
				if (SetField(ref _isVisible, value))
				{
					OnPropertyChanged(nameof(VisibilityIndicator));
				}
			}
		}

		/// <summary>
		/// Gets or sets whether the layer is locked
		/// </summary>
		public bool IsLocked
		{
			get => _isLocked;
			set => SetField(ref _isLocked, value);
		}

		/// <summary>
		/// Gets or sets whether this is the current layer
		/// </summary>
		public bool IsCurrent
		{
			get => _isCurrent;
			set
			{
				if (SetField(ref _isCurrent, value))
				{
					OnPropertyChanged(nameof(CurrentIndicator));
				}
			}
		}

		/// <summary>
		/// Gets the color display string
		/// </summary>
		public string ColorDisplay => string.Format(OpenCADStrings.ColorARGBFormat, 
			Color.A, Color.R, Color.G, Color.B);

		/// <summary>
		/// Gets the current layer indicator (checkmark if current)
		/// </summary>
		public string CurrentIndicator => IsCurrent ? OpenCADStrings.CurrentLayerIndicator : string.Empty;

		/// <summary>
		/// Gets the visibility indicator (light bulb if visible, empty circle if hidden)
		/// </summary>
		public string VisibilityIndicator => IsVisible ? OpenCADStrings.LayerVisibleIndicator : OpenCADStrings.LayerHiddenIndicator;

		/// <summary>
		/// Gets the status indicators (current, visible, locked)
		/// </summary>
		public string StatusIndicators
		{
			get
			{
				string status = string.Empty;
				if (IsCurrent) status += OpenCADStrings.CurrentLayerIndicator;
				if (!IsVisible) status += OpenCADStrings.HiddenLayerIndicator;
				if (IsLocked) status += OpenCADStrings.LockedLayerIndicator;
				return status;
			}
		}
	}
}