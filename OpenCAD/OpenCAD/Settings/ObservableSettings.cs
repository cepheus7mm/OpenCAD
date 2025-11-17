using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenCAD.Settings
{
    /// <summary>
    /// Base class for settings objects that need to notify property changes.
    /// </summary>
    public class ObservableSettings : OpenCADObject, INotifyPropertyChanged
    {
        public ObservableSettings() : base() { }
        public ObservableSettings(OpenCADDocument document) : base(document) { }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event for the given property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets the property and raises PropertyChanged if the value changes.
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}