using System.Collections.ObjectModel;
using System.Windows.Input;
using OpenCAD;
using OpenCAD.Interfaces;
using UI.Controls.Viewport;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// ViewModel for the PropertiesControl that manages property display and editing
	/// </summary>
	public class PropertiesViewModel : PropertyGridViewModel
	{
		public PropertiesViewModel()
		{
			//_properties = new ObservableCollection<PropertyItem>();
			
			//// Initialize with empty state
			//ClearProperties();

			//// Initialize commands
			//RefreshCommand = new RelayCommand(OnRefresh, CanRefresh);
			//ApplyChangesCommand = new RelayCommand(OnApplyChanges, CanApplyChanges);
			//CancelEditCommand = new RelayCommand(OnCancelEdit, CanCancelEdit);
		}

        protected override void UpdateFromViewport()
        {
			base.UpdateFromViewport();
			
			// Check if there are selected objects in the viewport
			var viewModel = _activeViewport.DataContext as ViewportViewModel;
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
			else if (_currentDocument != null)
			{
				// No selection - show document properties
				DisplayDocumentProperties(_currentDocument);
			}
			else
			{
				ClearProperties();
			}
		}

        public override void Refresh()
        {
			UpdateFromViewport(_activeViewport);
        }

		/// <summary>
		/// Display properties for a single OpenCAD object
		/// </summary>
		private void DisplayObjectProperties(OpenCADObject obj)
		{
			//System.Diagnostics.Debug.WriteLine($"DisplayObjectProperties called for: {obj.GetType().Name}");
			
			var properties = new ObservableCollection<PropertyItem>();

			// Add object type and ID (read-only)
			properties.Add(new PropertyItem 
			{ 
				PropertyName = OpenCADStrings.ObjectType, 
				Value = obj.GetType().Name,
				IsReadOnly = true 
			});

			var objectProperties = ((OpenCADObject)obj).GetAllProperties();
			foreach (var prop in objectProperties)
			{
				var propertyItem = new PropertyItem
				{
					PropertyName = prop.Name,
					Value = prop.ToStringRepresentation(_currentDocument) ?? OpenCADStrings.NullValue,
					RawValue = prop.Value, // ⭐ ADD THIS - Store the actual value object
					ValueType = prop.Value?.GetType(), // ⭐ ADD THIS - Store the type
					IsReadOnly = false
				};
				
				// Subscribe to value changes for editing
				propertyItem.ValueChanged += (s, e) => OnPropertyValueChanged(propertyItem, prop, 0);
				
				properties.Add(propertyItem);
			}

			// Add geometric properties if applicable
			var geometricProperties = GetGeometricProperties(obj);
			foreach (var geoProp in geometricProperties)
			{
				properties.Add(geoProp);
			}

			Properties = properties;
			//System.Diagnostics.Debug.WriteLine($"Properties updated for object: {obj.GetType().Name} ({properties.Count} properties)");
		}

        private ObservableCollection<PropertyItem> GetGeometricProperties(OpenCADObject obj)
        {
			var properties = new ObservableCollection<PropertyItem>();
			// Add geometry-specific properties
			if (obj is OpenCAD.Geometry.GeometryBase geometry)
			{
				properties.Add(new PropertyItem
				{
					PropertyName = OpenCADStrings.GeometrySection,
					Value = string.Empty,
					IsReadOnly = true
				});

				if (geometry.ToStringLength(out string length))
				{
					properties.Add(new PropertyItem
					{
						PropertyName = OpenCADStrings.Length,
						Value = length,
						IsReadOnly = true
					});
				}

                if (geometry.ToStringAngle(out string angle))
                {
                    properties.Add(new PropertyItem
                    {
                        PropertyName = OpenCADStrings.Angle,
                        Value = angle,
                        IsReadOnly = true
                    });
                }
            }
            return properties;
        }

		/// <summary>
		/// Display properties for multiple selected objects
		/// </summary>
		private void DisplayMultipleObjectsProperties(IReadOnlyList<OpenCADObject> objects)
		{
			//System.Diagnostics.Debug.WriteLine($"DisplayMultipleObjectsProperties called for {objects.Count} objects");
			
			var properties = new ObservableCollection<PropertyItem>();

			// Show selection count
			properties.Add(new PropertyItem 
			{ 
				PropertyName = OpenCADStrings.Selection, 
				Value = $"{objects.Count} {OpenCADStrings.ObjectsSelected}",
				IsReadOnly = true 
			});

			// Show object types
			var types = objects.Select(o => o.GetType().Name).Distinct().ToList();
			properties.Add(new PropertyItem 
			{ 
				PropertyName = OpenCADStrings.ObjectTypes, 
				Value = string.Join(", ", types),
				IsReadOnly = true 
			});

			// Check if all objects are on the same layer
			var layers = objects.Select(o => o.Layer?.Name).Distinct().ToList();
			if (layers.Count == 1 && layers[0] != null)
			{
				properties.Add(new PropertyItem 
				{ 
					PropertyName = OpenCADStrings.Layer, 
					Value = FormatPropertyValue(layers[0]),
					IsReadOnly = true 
				});
			}
			else
			{
				properties.Add(new PropertyItem 
				{ 
					PropertyName = OpenCADStrings.Layer, 
					Value = OpenCADStrings.MultipleValues,
					IsReadOnly = true 
				});
			}

			// Show common color (if all the same)
			var colors = objects.Select(o => ((IDrawable)o).Color).Distinct().ToList();
			if (colors.Count == 1)
			{
				properties.Add(new PropertyItem 
				{ 
					PropertyName = OpenCADStrings.Color, 
					Value = FormatPropertyValue(colors[0]),
					IsReadOnly = false 
				});
			}
			else
			{
				properties.Add(new PropertyItem 
				{ 
					PropertyName = OpenCADStrings.Color, 
				 Value = OpenCADStrings.MultipleValues,
					IsReadOnly = false 
				});
			}

			Properties = properties;
			//System.Diagnostics.Debug.WriteLine($"Properties updated for multiple selection ({properties.Count} properties)");
		}

		/// <summary>
		/// Display properties for an OpenCAD document
		/// </summary>
		private void DisplayDocumentProperties(OpenCADDocument document)
		{
			//System.Diagnostics.Debug.WriteLine($"DisplayDocumentProperties called for: {document.Filename}");
			
			var properties = new ObservableCollection<PropertyItem>();

			// Add object metadata (read-only)
			properties.Add(new PropertyItem 
			{ 
				PropertyName = OpenCADStrings.DocumentType, 
				Value = OpenCADStrings.OpenCADDocumentType,
				IsReadOnly = true 
			});
			properties.Add(new PropertyItem 
			{ 
				PropertyName = OpenCADStrings.DocumentID, 
				Value = document.ID.ToString(),
				IsReadOnly = true 
			});

			// Add editable properties from the property collection
			var objectProperties = document.GetProperties();
			if (objectProperties != null && objectProperties.Any())
			{
				foreach (var prop in objectProperties)
				{
					if (prop != null && prop.Type != PropertyType.ID)
					{
						{
							var propertyItem = new PropertyItem 
							{ 
								PropertyName = prop.Name, 
								Value = prop.ToStringRepresentation(_currentDocument),
								RawValue = prop.Value,
								ValueType = prop.Value?.GetType(),
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
			//System.Diagnostics.Debug.WriteLine($"Layer count in document: {layerCount}");
			
			properties.Add(new PropertyItem 
			{ 
				PropertyName = OpenCADStrings.LayersSection, 
				Value = string.Empty,
				IsReadOnly = true 
			});
			properties.Add(new PropertyItem 
			{ 
				PropertyName = OpenCADStrings.LayerCount, 
				Value = layerCount.ToString(),
				IsReadOnly = true 
			});

			// Add child object count (read-only)
			var childCount = document.GetChildren().Count();
			properties.Add(new PropertyItem 
			{ 
				PropertyName = OpenCADStrings.ChildrenSection, 
				Value = string.Empty,
				IsReadOnly = true 
			});
			properties.Add(new PropertyItem 
			{ 
				PropertyName = OpenCADStrings.TotalChildren, 
				Value = childCount.ToString(),
				IsReadOnly = true 
			});
			properties.Add(new PropertyItem 
			{ 
				PropertyName = OpenCADStrings.DrawableObjects, 
				Value = (childCount - 1).ToString(),
				IsReadOnly = true 
			});

			Properties = properties;
			System.Diagnostics.Debug.WriteLine(
				string.Format(OpenCADStrings.PropertiesUpdatedFormat, document.Filename, properties.Count));
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
				property.Value = newValue;
				
				System.Diagnostics.Debug.WriteLine(
					string.Format(OpenCADStrings.PropertyValueUpdatedFormat, propertyItem.PropertyName, propertyItem.Value));
				
				// Optionally refresh the viewport if needed
				ActiveViewport?.Refresh();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(
					string.Format(OpenCADStrings.PropertyUpdateErrorFormat, propertyItem.PropertyName, ex.Message));
				// Revert to original value on error
				propertyItem.Value = FormatPropertyValue(property.Value);
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
			{
				// Special case: transparent black (#00000000) means "ByLayer"
				if (color.A == 0 && color.R == 0 && color.G == 0 && color.B == 0)
					return OpenCADStrings.ByLayer;
				
				return string.Format(OpenCADStrings.ColorARGBFormat, color.A, color.R, color.G, color.B);
			}
			
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
				PropertyType.DoubleLength => double.Parse(stringValue),
				PropertyType.String => stringValue,
				_ => stringValue // Default to string for complex types
			};
		}
	}
}