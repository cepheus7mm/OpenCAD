using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using OpenCAD;
using OpenCAD.Dimensions;
using OpenCAD.Styles;
using UI.Controls.Viewport;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// ViewModel for the DimensionStylesControl that manages dimension style display and editing
	/// </summary>
	public class DimensionStylesViewModel : ObservableObject
	{
		private ObservableCollection<DimensionStyleItem> _dimensionStyles;
		private DimensionStyleItem? _selectedDimensionStyle;
		private ViewportControl? _activeViewport;
		private OpenCADDocument? _currentDocument;

		/// <summary>
		/// Event raised when dimension styles are modified (added, deleted, current changed)
		/// </summary>
		public event EventHandler? DimensionStylesModified;

		/// <summary>
		/// Gets the collection of dimension styles to display
		/// </summary>
		public ObservableCollection<DimensionStyleItem> DimensionStyles
		{
			get => _dimensionStyles;
			set => SetField(ref _dimensionStyles, value);
		}

		/// <summary>
		/// Gets or sets the currently selected dimension style item
		/// </summary>
		public DimensionStyleItem? SelectedDimensionStyle
		{
			get => _selectedDimensionStyle;
			set => SetField(ref _selectedDimensionStyle, value);
		}

		/// <summary>
		/// Gets or sets the active viewport to display dimension styles from
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
		/// Gets the collection of available arrow types for the dropdown
		/// </summary>
		public ObservableCollection<ArrowType> AvailableArrowTypes { get; }

		/// <summary>
		/// Gets the collection of available text styles for the dropdown
		/// </summary>
		public ObservableCollection<OpenCADTextStyle> AvailableTextStyles { get; }

		/// <summary>
		/// Command to create a new dimension style
		/// </summary>
		public ICommand NewDimensionStyleCommand { get; }

		/// <summary>
		/// Command to delete the selected dimension style
		/// </summary>
		public ICommand DeleteDimensionStyleCommand { get; }

		/// <summary>
		/// Command to set the selected dimension style as current
		/// </summary>
		public ICommand SetCurrentDimensionStyleCommand { get; }

		/// <summary>
		/// Command to refresh dimension styles from the active viewport
		/// </summary>
		public ICommand RefreshCommand { get; }

		public DimensionStylesViewModel()
		{
			_dimensionStyles = new ObservableCollection<DimensionStyleItem>();

			// Initialize available arrow types from the enum
			AvailableArrowTypes = new ObservableCollection<ArrowType>(
				Enum.GetValues(typeof(ArrowType)).Cast<ArrowType>());

			// Initialize available text styles (populated when viewport is set)
			AvailableTextStyles = new ObservableCollection<OpenCADTextStyle>();

			// Initialize commands
			NewDimensionStyleCommand = new UI.RelayCommand(OnNewDimensionStyle, CanNewDimensionStyle);
			DeleteDimensionStyleCommand = new UI.RelayCommand(OnDeleteDimensionStyle, CanDeleteDimensionStyle);
			SetCurrentDimensionStyleCommand = new UI.RelayCommand(OnSetCurrentDimensionStyle, CanSetCurrentDimensionStyle);
			RefreshCommand = new UI.RelayCommand(OnRefresh, CanRefresh);

			// Initialize with empty state
			ClearDimensionStyles();
		}

		/// <summary>
		/// Update dimension styles from the given viewport
		/// </summary>
		public void UpdateFromViewport(ViewportControl? viewport)
		{
			if (viewport == null)
			{
				_currentDocument = null;
				ClearDimensionStyles();
				return;
			}

			var document = viewport.Document;
			_currentDocument = document;

			if (document != null)
			{
				UpdateAvailableTextStyles();
				DisplayDocumentDimensionStyles(document);
			}
			else
			{
				ClearDimensionStyles();
			}
		}

		private void UpdateAvailableTextStyles()
		{
			AvailableTextStyles.Clear();
			if (_currentDocument == null) return;

			foreach (var ts in _currentDocument.GetTextStyles().OrderBy(t => t.Name))
			{
				AvailableTextStyles.Add(ts);
			}
		}

		/// <summary>
		/// Display dimension styles for an OpenCAD document
		/// </summary>
		private void DisplayDocumentDimensionStyles(OpenCADDocument document)
		{
			var dimensionStyles = new ObservableCollection<DimensionStyleItem>();
			var currentDimStyleId = document.CurrentDimensionStyle?.ID;

			foreach (var dimStyle in document.GetDimensionStyles())
			{
				var item = new DimensionStyleItem
				{
					DimensionStyle = dimStyle,
					Name = dimStyle.Name,
					ArrowType = dimStyle.ArrowType,
					ArrowSize = dimStyle.ArrowSize,
					TextStyle = dimStyle.TextStyle,
					TextHeight = dimStyle.TextHeight,
					Offset = dimStyle.Offset,
					ExtensionLength = dimStyle.ExtensionLength,
					ExtensionOffset = dimStyle.ExtensionOffset,
					ExtensionBeyond = dimStyle.ExtensionBeyond,
					Scale = dimStyle.Scale,
					IsCurrent = dimStyle.ID == currentDimStyleId
				};

				// Subscribe to property changes to update the underlying dimension style
				item.PropertyChanged += (s, e) => OnDimensionStyleItemPropertyChanged(item, e.PropertyName);

				dimensionStyles.Add(item);
			}

			DimensionStyles = dimensionStyles;
			System.Diagnostics.Debug.WriteLine($"Dimension styles updated: {document.Filename} ({dimensionStyles.Count} dimension styles)");
		}

		/// <summary>
		/// Clear the dimension styles display
		/// </summary>
		private void ClearDimensionStyles()
		{
			DimensionStyles = new ObservableCollection<DimensionStyleItem>();
		}

		/// <summary>
		/// Raise the DimensionStylesModified event
		/// </summary>
		private void RaiseDimensionStylesModified()
		{
			DimensionStylesModified?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Property names that follow the standard "sync → refresh → notify" pattern
		/// </summary>
		private static readonly HashSet<string> _visualProperties = new()
		{
			nameof(DimensionStyleItem.ArrowType),
			nameof(DimensionStyleItem.ArrowSize),
			nameof(DimensionStyleItem.TextStyle),
			nameof(DimensionStyleItem.TextHeight),
			nameof(DimensionStyleItem.Offset),
			nameof(DimensionStyleItem.ExtensionLength),
			nameof(DimensionStyleItem.ExtensionOffset),
			nameof(DimensionStyleItem.ExtensionBeyond),
			nameof(DimensionStyleItem.Scale),
		};

		/// <summary>
		/// Handle property changes on dimension style items
		/// </summary>
		private void OnDimensionStyleItemPropertyChanged(DimensionStyleItem item, string? propertyName)
		{
			if (item.DimensionStyle == null || propertyName == null)
				return;

			if (propertyName == nameof(DimensionStyleItem.Name))
			{
				HandleNameChange(item);
				return;
			}

			if (_visualProperties.Contains(propertyName))
			{
				SyncPropertyToDimensionStyle(item, propertyName);
				ActiveViewport?.Refresh();
				RaiseDimensionStylesModified();
			}
		}

		/// <summary>
		/// Validates and applies a name change, reverting on duplicate or empty
		/// </summary>
		private void HandleNameChange(DimensionStyleItem item)
		{
			if (string.IsNullOrWhiteSpace(item.Name))
			{
				item.Name = item.DimensionStyle!.Name;
				return;
			}

			bool isDuplicate = _currentDocument?.GetDimensionStyles()
				.Any(ds => ds.ID != item.DimensionStyle!.ID && ds.Name == item.Name) ?? false;

			if (!isDuplicate)
			{
				item.DimensionStyle!.Name = item.Name;
				RaiseDimensionStylesModified();
			}
			else
			{
				item.Name = item.DimensionStyle!.Name;
			}
		}

		/// <summary>
		/// Copies the changed property value from the item to the underlying dimension style
		/// </summary>
		private static void SyncPropertyToDimensionStyle(DimensionStyleItem item, string propertyName)
		{
			var dimStyle = item.DimensionStyle!;
			switch (propertyName)
			{
				case nameof(DimensionStyleItem.ArrowType):      dimStyle.ArrowType = item.ArrowType; break;
				case nameof(DimensionStyleItem.ArrowSize):      dimStyle.ArrowSize = item.ArrowSize; break;
				case nameof(DimensionStyleItem.TextStyle):      dimStyle.TextStyle = item.TextStyle; break;
				case nameof(DimensionStyleItem.TextHeight):     dimStyle.TextHeight = item.TextHeight; break;
				case nameof(DimensionStyleItem.Offset):         dimStyle.Offset = item.Offset; break;
				case nameof(DimensionStyleItem.ExtensionLength): dimStyle.ExtensionLength = item.ExtensionLength; break;
				case nameof(DimensionStyleItem.ExtensionOffset): dimStyle.ExtensionOffset = item.ExtensionOffset; break;
				case nameof(DimensionStyleItem.ExtensionBeyond): dimStyle.ExtensionBeyond = item.ExtensionBeyond; break;
				case nameof(DimensionStyleItem.Scale):          dimStyle.Scale = item.Scale; break;
			}
		}

		/// <summary>
		/// Set the specified dimension style as the current dimension style
		/// </summary>
		public void SetDimensionStyleAsCurrent(DimensionStyleItem item)
		{
			if (item == null || _currentDocument == null)
				return;

			_currentDocument.CurrentDimensionStyle = item.DimensionStyle!;
			System.Diagnostics.Debug.WriteLine($"Current dimension style changed to: {item.Name}");

			UpdateFromViewport(ActiveViewport);
			RaiseDimensionStylesModified();
		}

		#region Command Handlers

		private bool CanNewDimensionStyle() => _currentDocument != null;

		private void OnNewDimensionStyle()
		{
			if (_currentDocument == null)
				return;

			int counter = 1;
			string dimStyleName;
			do
			{
				dimStyleName = $"DimStyle{counter}";
				counter++;
			}
			while (_currentDocument.GetDimensionStyle(dimStyleName) != null);

			var newDimStyle = _currentDocument.CreateDimensionStyle(dimStyleName);
			if (newDimStyle != null)
			{
				System.Diagnostics.Debug.WriteLine($"Dimension style '{dimStyleName}' created");

				UpdateFromViewport(ActiveViewport);
				SelectedDimensionStyle = DimensionStyles.FirstOrDefault(ds => ds.Name == dimStyleName);
				RaiseDimensionStylesModified();
			}
		}

		private bool CanDeleteDimensionStyle() => SelectedDimensionStyle != null &&
												   SelectedDimensionStyle.Name != OpenCADStrings.DefaultDimensionStyleName &&
												   !SelectedDimensionStyle.IsCurrent;

		private void OnDeleteDimensionStyle()
		{
			if (SelectedDimensionStyle == null || _currentDocument == null)
				return;

			if (SelectedDimensionStyle.Name == OpenCADStrings.DefaultDimensionStyleName)
			{
				System.Diagnostics.Debug.WriteLine("Cannot delete default dimension style");
				return;
			}

			if (SelectedDimensionStyle.IsCurrent)
			{
				System.Diagnostics.Debug.WriteLine("Cannot delete current dimension style");
				return;
			}

			string dimStyleName = SelectedDimensionStyle.Name;
			if (_currentDocument.RemoveDimensionStyle(dimStyleName))
			{
				System.Diagnostics.Debug.WriteLine($"Dimension style '{dimStyleName}' deleted");

				UpdateFromViewport(ActiveViewport);
				RaiseDimensionStylesModified();
			}
		}

		private bool CanSetCurrentDimensionStyle() => SelectedDimensionStyle != null;

		private void OnSetCurrentDimensionStyle()
		{
			if (SelectedDimensionStyle == null || _currentDocument == null)
				return;

			_currentDocument.CurrentDimensionStyle = SelectedDimensionStyle.DimensionStyle!;
			System.Diagnostics.Debug.WriteLine($"Current dimension style changed to: {SelectedDimensionStyle.Name}");

			UpdateFromViewport(ActiveViewport);
			RaiseDimensionStylesModified();
		}

		private bool CanRefresh() => ActiveViewport != null;

		private void OnRefresh()
		{
			UpdateFromViewport(ActiveViewport);
		}

		#endregion
	}

	/// <summary>
	/// Represents a dimension style item in the dimension styles list
	/// </summary>
	public class DimensionStyleItem : ObservableObject
	{
		private string _name = string.Empty;
		private ArrowType _arrowType = ArrowType.ClosedFilled;
		private float _arrowSize = 0.1f;
		private OpenCADTextStyle? _textStyle;
		private float _textHeight = 0.1f;
		private float _offset = 0.1f;
		private float _extensionLength = 0.1f;
		private float _extensionOffset = 0.1f;
		private float _extensionBeyond = 0.1f;
		private float _scale = 10.0f;
		private bool _isCurrent;

		// String backing fields for numeric editors — lets the user type freely
		// (e.g. "0." mid-keystroke) without the binding swallowing characters.
		private string _arrowSizeText = "0.1";
		private string _textHeightText = "0.1";
		private string _offsetText = "0.1";
		private string _extensionLengthText = "0.1";
		private string _extensionOffsetText = "0.1";
		private string _extensionBeyondText = "0.1";
		private string _scaleText = "10";

		/// <summary>
		/// Gets or sets the underlying dimension style object
		/// </summary>
		public OpenCADDimensionStyle? DimensionStyle { get; set; }

		/// <summary>
		/// Gets or sets the dimension style name
		/// </summary>
		public string Name
		{
			get => _name;
			set => SetField(ref _name, value);
		}

		/// <summary>
		/// Gets or sets the arrow type
		/// </summary>
		public ArrowType ArrowType
		{
			get => _arrowType;
			set => SetField(ref _arrowType, value);
		}

		// ───── Float properties (used by ViewModel for syncing to DimensionStyle) ─────

		public float ArrowSize
		{
			get => _arrowSize;
			set => SetFloatWithText(ref _arrowSize, value, ref _arrowSizeText,
				nameof(ArrowSize), nameof(ArrowSizeText));
		}

		public float TextHeight
		{
			get => _textHeight;
			set => SetFloatWithText(ref _textHeight, value, ref _textHeightText,
				nameof(TextHeight), nameof(TextHeightText));
		}

		public float Offset
		{
			get => _offset;
			set => SetFloatWithText(ref _offset, value, ref _offsetText,
				nameof(Offset), nameof(OffsetText));
		}

		public float ExtensionLength
		{
			get => _extensionLength;
			set => SetFloatWithText(ref _extensionLength, value, ref _extensionLengthText,
				nameof(ExtensionLength), nameof(ExtensionLengthText));
		}

		public float ExtensionOffset
		{
			get => _extensionOffset;
			set => SetFloatWithText(ref _extensionOffset, value, ref _extensionOffsetText,
				nameof(ExtensionOffset), nameof(ExtensionOffsetText));
		}

		public float ExtensionBeyond
		{
			get => _extensionBeyond;
			set => SetFloatWithText(ref _extensionBeyond, value, ref _extensionBeyondText,
				nameof(ExtensionBeyond), nameof(ExtensionBeyondText));
		}

		public float Scale
		{
			get => _scale;
			set => SetFloatWithText(ref _scale, value, ref _scaleText,
				nameof(Scale), nameof(ScaleText));
		}

		// ───── String properties (bound by XAML — accept any text, parse when valid) ─────

		public string ArrowSizeText
		{
			get => _arrowSizeText;
			set => SetTextWithFloat(ref _arrowSizeText, value, ref _arrowSize,
				nameof(ArrowSizeText), nameof(ArrowSize));
		}

		public string TextHeightText
		{
			get => _textHeightText;
			set => SetTextWithFloat(ref _textHeightText, value, ref _textHeight,
				nameof(TextHeightText), nameof(TextHeight));
		}

		public string OffsetText
		{
			get => _offsetText;
			set => SetTextWithFloat(ref _offsetText, value, ref _offset,
				nameof(OffsetText), nameof(Offset));
		}

		public string ExtensionLengthText
		{
			get => _extensionLengthText;
			set => SetTextWithFloat(ref _extensionLengthText, value, ref _extensionLength,
				nameof(ExtensionLengthText), nameof(ExtensionLength));
		}

		public string ExtensionOffsetText
		{
			get => _extensionOffsetText;
			set => SetTextWithFloat(ref _extensionOffsetText, value, ref _extensionOffset,
				nameof(ExtensionOffsetText), nameof(ExtensionOffset));
		}

		public string ExtensionBeyondText
		{
			get => _extensionBeyondText;
			set => SetTextWithFloat(ref _extensionBeyondText, value, ref _extensionBeyond,
				nameof(ExtensionBeyondText), nameof(ExtensionBeyond));
		}

		public string ScaleText
		{
			get => _scaleText;
			set => SetTextWithFloat(ref _scaleText, value, ref _scale,
				nameof(ScaleText), nameof(Scale));
		}

		/// <summary>
		/// Gets or sets the text style
		/// </summary>
		public OpenCADTextStyle? TextStyle
		{
			get => _textStyle;
			set => SetField(ref _textStyle, value);
		}

		/// <summary>
		/// Gets or sets whether this is the current dimension style
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
		/// Gets the current dimension style indicator (checkmark if current)
		/// </summary>
		public string CurrentIndicator => IsCurrent ? OpenCADStrings.CurrentLayerIndicator : string.Empty;

		// ───── Helpers ─────

		/// <summary>
		/// Sets a float field and keeps the corresponding text field in sync.
		/// Used when loading values from the underlying dimension style.
		/// </summary>
		private void SetFloatWithText(ref float floatField, float newValue,
			ref string textField, string floatPropName, string textPropName)
		{
			if (floatField == newValue) return;
			floatField = newValue;
			OnPropertyChanged(floatPropName);
			textField = newValue.ToString(CultureInfo.InvariantCulture);
			OnPropertyChanged(textPropName);
		}

		/// <summary>
		/// Sets a text field and, only when the text is a valid number,
		/// updates the corresponding float field. This lets the user type
		/// intermediate values like "0." without losing characters.
		/// </summary>
		private void SetTextWithFloat(ref string textField, string newText,
			ref float floatField, string textPropName, string floatPropName)
		{
			if (string.Equals(textField, newText, StringComparison.Ordinal)) return;
			textField = newText;
			OnPropertyChanged(textPropName);

			if (float.TryParse(newText, NumberStyles.Float | NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture, out float parsed))
			{
				floatField = parsed;
				OnPropertyChanged(floatPropName); // triggers ViewModel sync
			}
		}
	}
}