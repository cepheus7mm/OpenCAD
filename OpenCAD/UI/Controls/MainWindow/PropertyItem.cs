using System;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Represents a property item displayed in the properties panel.
	/// Uses ObservableObject to support two-way binding and editing.
	/// </summary>
	public class PropertyItem : ObservableObject
	{
		private string _property = string.Empty;
		private string _value = string.Empty;
		private bool _isReadOnly = true;

		/// <summary>
		/// Gets or sets the property name/label
		/// </summary>
		public string Property
		{
			get => _property;
			set => SetField(ref _property, value);
		}

		/// <summary>
		/// Gets or sets the property value
		/// </summary>
		public string Value
		{
			get => _value;
			set
			{
				if (SetField(ref _value, value))
				{
					// Raise event when value changes for external handling
					ValueChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets or sets whether this property is read-only
		/// </summary>
		public bool IsReadOnly
		{
			get => _isReadOnly;
			set => SetField(ref _isReadOnly, value);
		}

		/// <summary>
		/// Event raised when the value changes
		/// </summary>
		public event EventHandler? ValueChanged;
	}
}