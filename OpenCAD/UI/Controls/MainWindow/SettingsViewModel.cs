using OpenCAD;
using OpenCAD.Settings;
using System.Collections.ObjectModel;

namespace UI.Controls.MainWindow
{
    public class SettingsViewModel : PropertyGridViewModel
    {
        private ViewportSettings? _viewportSettings;

        public ViewportSettings? ViewportSettings
        {
            get => _viewportSettings;
            set
            {
                if (SetField(ref _viewportSettings, value))
                {
                    Refresh();
                }
            }
        }

        protected override void UpdateFromViewport()
        {
            base.UpdateFromViewport();
            ViewportSettings = _currentDocument?.GetViewportSettings();
            Refresh();
        }

        public override void Refresh()
        {
            if (ViewportSettings == null)
            {
                ClearProperties();
                return;
            }

            // Populate Properties from ViewportSettings and its children
            var items = new ObservableCollection<PropertyItem>();
            // Example: Add properties for ViewportSettings
            foreach (var prop in ViewportSettings.GetAllProperties())
            {
                items.Add(AddProperty(prop, false));
            }

            foreach (var child in ViewportSettings.GetChildren().OrderBy(x => x.GetType().Name))
            {
                items.Add(new PropertyItem
                {
                    PropertyName = $"-- {child.GetType().Name} --",
                    Value = string.Empty,
                    IsReadOnly = true
                });

                foreach (var prop in child.GetAllProperties())
                {
                    items.Add(AddProperty(prop, false));
                }
            }

            Properties = items;
        }
    }
}