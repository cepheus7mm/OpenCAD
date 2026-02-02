using OpenCAD;
using System.Collections.ObjectModel;
using System.Windows.Input;
using UI.Controls.Viewport;
using Xceed.Wpf.Toolkit.PropertyGrid;

namespace UI.Controls.MainWindow
{
    public abstract class PropertyGridViewModel : ObservableObject
    {
        protected ObservableCollection<PropertyItem> _properties = new();
        protected PropertyItem? _selectedProperty;
        protected bool _isEditing;
        protected ViewportControl? _activeViewport;
        protected OpenCADDocument? _currentDocument;

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
        /// Update properties from the given viewport
        /// </summary>
        public void UpdateFromViewport(ViewportControl? viewport)
        {
            ClearProperties();
            if (viewport == null)
            {
                _currentDocument = null;
                return;
            }
            else if (_activeViewport != viewport)
            {
                _activeViewport = viewport;
            }
            UpdateFromViewport();
        }

        protected virtual void UpdateFromViewport()
        {
            var document = _activeViewport?.Document;
            _currentDocument = document;
        }

        public ObservableCollection<PropertyItem> Properties
        {
            get => _properties;
            set => SetField(ref _properties, value);
        }

        public PropertyItem? SelectedProperty
        {
            get => _selectedProperty;
            set => SetField(ref _selectedProperty, value);
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => SetField(ref _isEditing, value);
        }

        public ICommand RefreshCommand { get; protected set; }
        public ICommand ApplyChangesCommand { get; protected set; }
        public ICommand CancelEditCommand { get; protected set; }

        protected PropertyGridViewModel()
        {
            _properties = new ObservableCollection<PropertyItem>();

            // Initialize with empty state
            ClearProperties();

            RefreshCommand = new RelayCommand(OnRefresh, CanRefresh);
            ApplyChangesCommand = new RelayCommand(OnApplyChanges, CanApplyChanges);
            CancelEditCommand = new RelayCommand(OnCancelEdit, CanCancelEdit);
        }

        public abstract void Refresh();

        protected virtual bool CanRefresh() => true;
        protected virtual void OnRefresh() => Refresh();

        protected virtual bool CanApplyChanges() => IsEditing && SelectedProperty != null;
        protected virtual void OnApplyChanges() => IsEditing = false;

        protected virtual bool CanCancelEdit() => IsEditing;
        protected virtual void OnCancelEdit()
        {
            Refresh();
            IsEditing = false;
        }

        public virtual void ClearProperties()
        {
            Properties = new ObservableCollection<PropertyItem>
            {
                new PropertyItem
                {
                    PropertyName = "No Data",
                    Value = "-",
                    IsReadOnly = true
                }
            };
        }

        protected void OnPropertyValueChanged(object? sender, PropertyValueChangedEventArgs e)
        {
            IsEditing = true;
            if (sender is PropertyItem item && item.Property != null)
            {
                if (e.Source == nameof(PropertyItem.Value))
                {
                    item.Property.FromStringRepresentation(item.Value, _currentDocument);
                }
                else
                {
                    item.Property.Value = item.RawValue;
                }
                item.Value = item.Property.ToStringRepresentation(_currentDocument);
            }
            _activeViewport?.InvalidateVisual();
        }

        protected PropertyItem AddProperty(Property prop, bool isReadOnly)
        {
            var item = new PropertyItem
            {
                PropertyName = prop.Name,
                Value = prop.ToStringRepresentation(null),
                RawValue = prop.Value,
                ValueType = prop.Value?.GetType(),
                Property = prop,
                IsReadOnly = false,
            };

            item.ValueChanged += (s, e) => OnPropertyValueChanged(s, e);
            return item;
        }
    }
}