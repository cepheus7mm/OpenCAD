using System.Collections.ObjectModel;
using System.Windows.Input;
using OpenCAD;
using UI.Controls.Viewport;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// ViewModel for the PropertiesControl that manages property display and editing
	/// </summary>
	public class PropertiesViewModel : ObservableObject
	{
		private ObservableCollection<PropertyItem> _properties;
		private PropertyItem? _selectedProperty;
		private bool _isEditing;
		private ViewportControl? _activeViewport;
		private OpenCADDocument? _currentDocument;

		/// <summary>
		/// Gets the collection of properties to display
		/// </summary>
		public ObservableCollection<PropertyItem> Properties
		{
			get => _properties;
			set => SetField(ref _properties, value);
		}

		/// <summary>
		/// Gets or sets the currently selected property item
		/// </summary>
		public PropertyItem? SelectedProperty
		{
			get => _selectedProperty;
			set => SetField(ref _selectedProperty, value);
		}

		/// <summary>
		/// Gets or sets whether editing mode is active
		/// </summary>
		public bool IsEditing
		{
			get => _isEditing;
			set => SetField(ref _isEditing, value);
		}

		/// <summary>
		/// Gets or sets the active viewport to display properties from
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
		/// Command to refresh properties from the active viewport
		/// </summary>
		public ICommand RefreshCommand { get; }

		/// <summary>
		/// Command to apply property changes to the underlying object
		/// </summary>
		public ICommand ApplyChangesCommand { get; }

		/// <summary>
		/// Command to cancel property editing
		/// </summary>
		public ICommand CancelEditCommand { get; }

		public PropertiesViewModel()
		{
			_properties = new ObservableCollection<PropertyItem>();
			
			// Initialize with empty state
			ClearProperties();

			// Initialize commands
			RefreshCommand = new RelayCommand(OnRefresh, CanRefresh);
			ApplyChangesCommand = new RelayCommand(OnApplyChanges, CanApplyChanges);
			CancelEditCommand = new RelayCommand(OnCancelEdit, CanCancelEdit);
		}

		/// <summary>
		/// Update properties from the given viewport
		/// </summary>
		public void UpdateFromViewport(ViewportControl? viewport)
		{
			if (viewport == null)
			{
				_currentDocument = null;
				ClearProperties();
				return;
			}

			var document = viewport.ObjectToDisplay as OpenCADDocument;
			_currentDocument = document;
			
			// Check if there are selected objects in the viewport
			var viewModel = viewport.DataContext as ViewportViewModel;
			if (viewModel != null && viewModel.SelectedObjects != null && viewModel.SelectedObjects.Count > 0)
			{
				// Show properties of selected objects
				if (viewModel.SelectedObjects.Count == 1)
				{
					// Single object selected - show its properties
					DisplayObjectProperties(viewModel.SelectedObjects[0]);
				}
				else
				{
					// Multiple objects selected - show count and common properties
					DisplayMultipleObjectsProperties(viewModel.SelectedObjects);
				}
			}
			else if (document != null)
			{
				// No selection - show document properties
				DisplayDocumentProperties(document);
			}
			else
			{
				ClearProperties();
			}
		}

		/// <summary>
		/// Display properties for a single OpenCAD object
		/// </summary>
		private void DisplayObjectProperties(OpenCADObject obj)
		{
			System.Diagnostics.Debug.WriteLine($"DisplayObjectProperties called for: {obj.GetType().Name}");
			
			var properties = new ObservableCollection<PropertyItem>();

			// Add object type and ID (read-only)
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.ObjectType, 
				Value = obj.GetType().Name,
				IsReadOnly = true 
			});
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.ObjectID, 
				Value = obj.ID.ToString(),
				IsReadOnly = true 
			});

			// Add layer information from properties collection
			var layerProp = obj.GetProperty(PropertyType.Layer);
			if (layerProp != null)
			{
				var layerId = (Guid)layerProp.GetValue(0);
				// Resolve layer name through document
				var layerName = GetNameFromID(layerId);
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.Layer, 
					Value = layerName,
					IsReadOnly = true 
				});
			}
			else
			{
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.Layer, 
					Value = OpenCADStrings.NullValue,
					IsReadOnly = true 
				});
			}

			// Add color property
			var color = obj.GetEffectiveColor();
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.Color, 
				Value = FormatPropertyValue(color),
				IsReadOnly = false 
			});

			// Add line type property
			var lineType = obj.GetEffectiveLineType();
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.LineType, 
				Value = lineType.ToString(),
				IsReadOnly = false 
			});

			// Add line weight property
			var lineWeight = obj.GetEffectiveLineWeight();
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.LineWeight, 
				Value = lineWeight.ToDisplayString(),
				IsReadOnly = false 
			});

			// Add geometry-specific properties
			if (obj is OpenCAD.Geometry.Line line)
			{
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.GeometrySection, 
					Value = string.Empty,
					IsReadOnly = true 
				});
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.StartPoint, 
					Value = FormatPropertyValue(line.Start),
					IsReadOnly = false 
				});
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.EndPoint, 
					Value = FormatPropertyValue(line.End),
					IsReadOnly = false 
				});
				
				// Calculate and display length
				var dx = line.End.X - line.Start.X;
				var dy = line.End.Y - line.Start.Y;
				var dz = line.End.Z - line.Start.Z;
				var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.Length, 
					Value = length.ToString(OpenCADStrings.DoubleFormat),
					IsReadOnly = true 
				});
			}

			// Add custom properties from the object
			var objectProperties = obj.GetProperties();
			if (objectProperties != null && objectProperties.Any())
			{
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.CustomPropertiesSection, 
					Value = string.Empty,
					IsReadOnly = true 
				});
				
				foreach (var prop in objectProperties)
				{
					if (prop != null && prop.Type != PropertyType.Layer) // Skip layer as we already showed it
					{
						// Handle properties with multiple values
						if (prop.Count > 1)
						{
							for (int i = 0; i < prop.Count; i++)
							{
								var propValue = prop.GetPropertyValue(i);
								var propertyItem = new PropertyItem 
								{ 
									Property = propValue.Name, 
									Value = FormatPropertyValue(propValue.Value),
									IsReadOnly = false
								};
								
								// Subscribe to value changes for this property
								propertyItem.ValueChanged += (s, e) => OnPropertyValueChanged(propertyItem, prop, i);
								
								properties.Add(propertyItem);
							}
						}
						else if (prop.Count == 1)
						{
							var propValue = prop.GetPropertyValue(0);
							var propertyItem = new PropertyItem 
							{ 
								Property = propValue.Name, 
								Value = FormatPropertyValue(propValue.Value),
								IsReadOnly = false
							};
							
							// Subscribe to value changes
							propertyItem.ValueChanged += (s, e) => OnPropertyValueChanged(propertyItem, prop, 0);
							
							properties.Add(propertyItem);
						}
					}
				}
			}

			Properties = properties;
			System.Diagnostics.Debug.WriteLine($"Properties updated for object: {obj.GetType().Name} ({properties.Count} properties)");
		}

		/// <summary>
		/// Display properties for multiple selected objects
		/// </summary>
		private void DisplayMultipleObjectsProperties(IReadOnlyList<OpenCADObject> objects)
		{
			System.Diagnostics.Debug.WriteLine($"DisplayMultipleObjectsProperties called for {objects.Count} objects");
			
			var properties = new ObservableCollection<PropertyItem>();

			// Show selection count
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.Selection, 
				Value = $"{objects.Count} {OpenCADStrings.ObjectsSelected}",
				IsReadOnly = true 
			});

			// Show object types
			var types = objects.Select(o => o.GetType().Name).Distinct().ToList();
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.ObjectTypes, 
				Value = string.Join(", ", types),
				IsReadOnly = true 
			});

			// Check if all objects are on the same layer
			var layers = objects.Select(o => o.GetProperty(PropertyType.Layer)?.GetValue(0)).Distinct().ToList();
			if (layers.Count == 1 && layers[0] != null)
			{
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.Layer, 
					Value = FormatPropertyValue(layers[0]),
					IsReadOnly = true 
				});
			}
			else
			{
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.Layer, 
					Value = OpenCADStrings.MultipleValues,
					IsReadOnly = true 
				});
			}

			// Show common color (if all the same)
			var colors = objects.Select(o => o.GetEffectiveColor()).Distinct().ToList();
			if (colors.Count == 1)
			{
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.Color, 
					Value = FormatPropertyValue(colors[0]),
					IsReadOnly = false 
				});
			}
			else
			{
				properties.Add(new PropertyItem 
				{ 
					Property = OpenCADStrings.Color, 
				 Value = OpenCADStrings.MultipleValues,
					IsReadOnly = false 
				});
			}

			Properties = properties;
			System.Diagnostics.Debug.WriteLine($"Properties updated for multiple selection ({properties.Count} properties)");
		}

		/// <summary>
		/// Display properties for an OpenCAD document
		/// </summary>
		private void DisplayDocumentProperties(OpenCADDocument document)
		{
			System.Diagnostics.Debug.WriteLine($"DisplayDocumentProperties called for: {document.Filename}");
			
			var properties = new ObservableCollection<PropertyItem>();

			// Add object metadata (read-only)
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.DocumentType, 
				Value = OpenCADStrings.OpenCADDocumentType,
				IsReadOnly = true 
			});
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.DocumentID, 
				Value = document.ID.ToString(),
				IsReadOnly = true 
			});

			// Add editable properties from the property collection
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
								var propertyItem = new PropertyItem 
								{ 
									Property = propValue.Name, 
									Value = FormatPropertyValue(propValue.Value),
									IsReadOnly = false // Allow editing for document properties
								};
								
								// Subscribe to value changes for this property
								propertyItem.ValueChanged += (s, e) => OnPropertyValueChanged(propertyItem, prop, i);
								
								properties.Add(propertyItem);
							}
						}
						else if (prop.Count == 1)
						{
							// Single value property
							var propValue = prop.GetPropertyValue(0);
							var propertyItem = new PropertyItem 
							{ 
								Property = propValue.Name, 
							 Value = FormatPropertyValue(propValue.Value),
								IsReadOnly = false
							};
							
							// Subscribe to value changes
							propertyItem.ValueChanged += (s, e) => OnPropertyValueChanged(propertyItem, prop, 0);
							
							properties.Add(propertyItem);
						}
					}
				}
			}

			// Add layer count (read-only)
			var layerCount = document.GetLayers().Count();
			System.Diagnostics.Debug.WriteLine($"Layer count in document: {layerCount}");
			
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.LayersSection, 
				Value = string.Empty,
				IsReadOnly = true 
			});
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.LayerCount, 
				Value = layerCount.ToString(),
				IsReadOnly = true 
			});

			// Add child object count (read-only)
			var childCount = document.GetChildren().Count();
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.ChildrenSection, 
				Value = string.Empty,
				IsReadOnly = true 
			});
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.TotalChildren, 
				Value = childCount.ToString(),
				IsReadOnly = true 
			});
			properties.Add(new PropertyItem 
			{ 
				Property = OpenCADStrings.DrawableObjects, 
				Value = (childCount - 1).ToString(),
				IsReadOnly = true 
			});

			Properties = properties;
			System.Diagnostics.Debug.WriteLine(
				string.Format(OpenCADStrings.PropertiesUpdatedFormat, document.Filename, properties.Count));
		}

		/// <summary>
		/// Clear the properties display
		/// </summary>
		public void ClearProperties()
		{
			Properties = new ObservableCollection<PropertyItem>
			{
				new PropertyItem 
				{ 
					Property = OpenCADStrings.NoDocument, 
					Value = OpenCADStrings.PlaceholderValue,
					IsReadOnly = true 
				}
			};
		}

		/// <summary>
		/// Handle property value changes from the UI
		/// </summary>
		private void OnPropertyValueChanged(PropertyItem propertyItem, Property property, int index)
		{
			if (_currentDocument == null)
				return;

			try
			{
				// Parse the new value based on property type
				var newValue = ParsePropertyValue(propertyItem.Value, property.Type);
				
				// Update the underlying property
				property.SetValue(index, newValue);
				
				System.Diagnostics.Debug.WriteLine(
					string.Format(OpenCADStrings.PropertyValueUpdatedFormat, propertyItem.Property, propertyItem.Value));
				
				// Optionally refresh the viewport if needed
				ActiveViewport?.Refresh();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(
					string.Format(OpenCADStrings.PropertyUpdateErrorFormat, propertyItem.Property, ex.Message));
				// Revert to original value on error
				propertyItem.Value = FormatPropertyValue(property.GetValue(index));
			}
		}

		/// <summary>
		/// Resolves a GUID to a meaningful name by searching the document hierarchy.
		/// Specifically handles layer IDs and other object references.
		/// </summary>
		/// <param name="guid">The GUID to resolve</param>
		/// <returns>The name of the object if found, otherwise the GUID string</returns>
		private string GetNameFromID(Guid guid)
		{
			if (_currentDocument == null)
				return guid.ToString();

			// First, check if it's a layer by searching all layers
			var layers = _currentDocument.GetLayers();
			var matchingLayer = layers.FirstOrDefault(layer => layer.ID == guid);
			if (matchingLayer != null)
			{
				return matchingLayer.Name;
			}

			// Search through all children recursively
			var foundObject = SearchObjectById(_currentDocument, guid);
			if (foundObject != null)
			{
				// Try to get a meaningful name based on object type
				if (foundObject is OpenCADLayer layer)
				{
					return layer.Name;
				}
				
				// For other objects, return type and shortened ID
				return $"{foundObject.GetType().Name} ({guid:D})";
			}

			// If not found, return just the GUID
			return guid.ToString();
		}

		/// <summary>
		/// Recursively searches for an object by ID in the document hierarchy
		/// </summary>
		/// <param name="parent">The parent object to start searching from</param>
		/// <param name="id">The ID to search for</param>
		/// <returns>The matching object if found, otherwise null</returns>
		private OpenCADObject? SearchObjectById(OpenCADObject parent, Guid id)
		{
			if (parent == null)
				return null;

			// Check if this is the object we're looking for
			if (parent.ID == id)
				return parent;

			// Search all children recursively
			foreach (var child in parent.GetChildren())
			{
				var result = SearchObjectById(child, id);
				if (result != null)
					return result;
			}

			return null;
		}

		/// <summary>
		/// Format a property value for display
		/// </summary>
		private string FormatPropertyValue(object value)
		{
			if (value == null)
				return OpenCADStrings.NullValue;

			// Handle specific types
			if (value is OpenCAD.Geometry.Point3D point)
				return string.Format(OpenCADStrings.Point3DFormat, point.X, point.Y, point.Z);
			
			if (value is System.Drawing.Color color)
				return string.Format(OpenCADStrings.ColorARGBFormat, color.A, color.R, color.G, color.B);
			
			if (value is OpenCADLayer layer)
				return layer.Name;
			
			if (value is System.Guid guid)
			{
				// Resolve GUID to name using the current document
				return GetNameFromID(guid);
			}
			
			if (value is LineType lineType)
				return lineType.ToString();
			
			if (value is LineWeight lineWeight)
				return lineWeight.ToDisplayString();
			
			if (value is double doubleVal)
				return doubleVal.ToString(OpenCADStrings.DoubleFormat);
			
			if (value is float floatVal)
				return floatVal.ToString("F4");
			
			if (value is bool boolVal)
				return boolVal ? OpenCADStrings.TrueValue : OpenCADStrings.FalseValue;
			
			if (value is int intVal)
				return intVal.ToString();

			// Default to ToString()
			return value.ToString() ?? OpenCADStrings.EmptyValue;
		}

		/// <summary>
		/// Parse a string value back to the appropriate type
		/// </summary>
		private object ParsePropertyValue(string stringValue, PropertyType propertyType)
		{
			return propertyType switch
			{
				PropertyType.Boolean => bool.Parse(stringValue),
				PropertyType.Integer => int.Parse(stringValue),
				PropertyType.Double => double.Parse(stringValue),
				PropertyType.String => stringValue,
				_ => stringValue // Default to string for complex types
			};
		}

		#region Command Handlers

		private bool CanRefresh() => ActiveViewport != null;

		private void OnRefresh()
		{
			UpdateFromViewport(ActiveViewport);
		}

		private bool CanApplyChanges() => IsEditing && SelectedProperty != null;

		private void OnApplyChanges()
		{
			// Property changes are applied immediately via ValueChanged event
			// This command can be used to commit all changes at once if needed
			IsEditing = false;
		}

		private bool CanCancelEdit() => IsEditing;

		private void OnCancelEdit()
		{
			// Refresh to revert any unsaved changes
			UpdateFromViewport(ActiveViewport);
			IsEditing = false;
		}

		#endregion
	}
}