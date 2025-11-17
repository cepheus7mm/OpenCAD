using OpenCAD;
using System;
using System.Collections.Generic;
using System.Linq;
using Xceed.Wpf.Toolkit.PropertyGrid;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Represents a property item displayed in the properties panel.
	/// Uses ObservableObject to support two-way binding and editing.
	/// </summary>
	public class PropertyItem : ObservableObject
	{
		private string _propertyName = string.Empty;
		private string _value = string.Empty;
		private bool _isReadOnly = true;
		private Type? _valueType;
		private object? _rawValue;
		private Property? _property;

		/// <summary>
		/// Gets or sets the property name/label
		/// </summary>
		public string PropertyName
		{
			get => _propertyName;
			set => SetField(ref _propertyName, value);
		}

		public Property? Property
		{
			get => _property;
			set => SetField(ref _property, value);
        }


        /// <summary>
        /// Gets or sets the property value as a string
        /// </summary>
        public string Value
		{
			get => _value;
			set
			{
                var oldValue = _rawValue;
                if (SetField(ref _value, value))
				{
					// Raise event when value changes for external handling
					ValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(null, nameof(Value), oldValue, value));
                }
			}
		}

		/// <summary>
		/// Gets or sets the raw value object (for complex types)
		/// </summary>
		public object? RawValue
		{
			get => _rawValue;
			set
			{
				var oldValue = _rawValue;
                if (SetField(ref _rawValue, value))
				{
                    // Raise event when value changes for external handling
                    ValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(null, nameof(RawValue), oldValue, value));
                }
			}
		}

		/// <summary>
		/// Gets or sets the type of the property value
		/// </summary>
		public Type? ValueType
		{
			get => _valueType;
			set
			{
				if (SetField(ref _valueType, value))
				{
					OnPropertyChanged(nameof(IsEnum));
					OnPropertyChanged(nameof(IsBoolean));
					OnPropertyChanged(nameof(EnumValues));
				}
			}
		}

		/// <summary>
		/// Gets whether this property is read-only
		/// </summary>
		public bool IsReadOnly
		{
			get => _isReadOnly;
			set => SetField(ref _isReadOnly, value);
		}

		/// <summary>
		/// Gets whether the value type is an enum
		/// </summary>
		public bool IsEnum => ValueType?.IsEnum ?? false;

		/// <summary>
		/// Gets whether the value type is a boolean
		/// </summary>
		public bool IsBoolean => ValueType == typeof(bool);

		/// <summary>
		/// Gets whether the value type is a Color
		/// </summary>
		public bool IsColor => ValueType == typeof(System.Drawing.Color) || ValueType == typeof(System.Windows.Media.Color);

		/// <summary>
		/// Gets whether the color is set to "ByLayer" (transparent black)
		/// </summary>
		public bool IsColorByLayer
		{
			get
			{
				if (RawValue is System.Drawing.Color color)
				{
					return color.A == 0 && color.R == 0 && color.G == 0 && color.B == 0;
				}
				return false;
			}
		}

		/// <summary>
		/// Gets the possible enum values if this is an enum type
		/// </summary>
		public IEnumerable<object>? EnumValues => 
			IsEnum && ValueType != null 
				? Enum.GetValues(ValueType).Cast<object>() 
				: null;

		/// <summary>
		/// Event raised when the value changes
		/// </summary>
		public event EventHandler<PropertyValueChangedEventArgs>? ValueChanged;
	}
}