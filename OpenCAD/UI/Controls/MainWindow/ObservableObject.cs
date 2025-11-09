using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Base class that implements INotifyPropertyChanged for property change notification.
	/// </summary>
	public abstract class ObservableObject : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		/// <summary>
		/// Raises the PropertyChanged event for the specified property.
		/// </summary>
		protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		/// <summary>
		/// Sets the field and raises PropertyChanged if the value changes.
		/// </summary>
		/// <returns>True if the value was changed, false if it was already equal.</returns>
		protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
				return false;
			
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}
	}
}