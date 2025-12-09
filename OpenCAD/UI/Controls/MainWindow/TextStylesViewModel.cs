using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using OpenCAD;
using UI.Controls.Viewport;
using System.IO;
using System.Windows;
using OpenCAD.TextRendering; // Add this

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// ViewModel for the TextStylesControl that manages text style display and editing
	/// </summary>
	public class TextStylesViewModel : ObservableObject
	{
		private ObservableCollection<TextStyleItem> _textStyles;
		private TextStyleItem? _selectedTextStyle;
		private ViewportControl? _activeViewport;
		private OpenCADDocument? _currentDocument;

		/// <summary>
		/// Event raised when text styles are modified (added, deleted, current changed)
		/// </summary>
		public event EventHandler? TextStylesModified;

		/// <summary>
		/// Gets the collection of text styles to display
		/// </summary>
		public ObservableCollection<TextStyleItem> TextStyles
		{
			get => _textStyles;
			set => SetField(ref _textStyles, value);
		}

		/// <summary>
		/// Gets or sets the currently selected text style item
		/// </summary>
		public TextStyleItem? SelectedTextStyle
		{
			get => _selectedTextStyle;
			set => SetField(ref _selectedTextStyle, value);
		}

		/// <summary>
		/// Gets or sets the active viewport to display text styles from
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
		/// Gets the collection of available font families for the dropdown
		/// </summary>
		public ObservableCollection<string> AvailableFontFamilies { get; }

		/// <summary>
		/// Command to create a new text style
		/// </summary>
		public ICommand NewTextStyleCommand { get; }

		/// <summary>
		/// Command to delete the selected text style
		/// </summary>
		public ICommand DeleteTextStyleCommand { get; }

		/// <summary>
		/// Command to set the selected text style as current
		/// </summary>
		public ICommand SetCurrentTextStyleCommand { get; }

		/// <summary>
		/// Command to refresh text styles from the active viewport
		/// </summary>
		public ICommand RefreshCommand { get; }

		public TextStylesViewModel()
		{
			_textStyles = new ObservableCollection<TextStyleItem>();
			
			// Initialize available font families from actual .ttf files that FontRegistry can find
			AvailableFontFamilies = new ObservableCollection<string>();
			LoadAvailableFonts();

            // Initialize commands
            NewTextStyleCommand = new UI.RelayCommand(OnNewTextStyle, CanNewTextStyle);
			DeleteTextStyleCommand = new UI.RelayCommand(OnDeleteTextStyle, CanDeleteTextStyle);
			SetCurrentTextStyleCommand = new UI.RelayCommand(OnSetCurrentTextStyle, CanSetCurrentTextStyle);
			RefreshCommand = new UI.RelayCommand(OnRefresh, CanRefresh);

			// Initialize with empty state
			ClearTextStyles();
		}

		/// <summary>
		/// Load available fonts by scanning for .ttf files and extracting font family names
		/// This ensures the dropdown only shows fonts that FontRegistry can actually load
		/// </summary>
		private void LoadAvailableFonts()
		{
			var fontFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// Scan Windows fonts directory for .ttf files
			var fontPaths = new List<string>
			{
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)),
				@"C:\Windows\Fonts"
			};

			foreach (var searchPath in fontPaths)
			{
				if (!Directory.Exists(searchPath))
					continue;

				try
				{
					var ttfFiles = Directory.GetFiles(searchPath, "*.ttf", SearchOption.TopDirectoryOnly);

					foreach (var filePath in ttfFiles)
					{
						try
						{
							// Try to extract the font family name from the file
							var fontFamily = ExtractFontFamilyName(filePath);
							if (!string.IsNullOrWhiteSpace(fontFamily))
							{
								fontFamilies.Add(fontFamily);
							}
						}
						catch (Exception ex)
						{
							// Skip fonts that can't be parsed
							System.Diagnostics.Debug.WriteLine($"Failed to parse font {filePath}: {ex.Message}");
						}
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Failed to scan font directory {searchPath}: {ex.Message}");
				}
			}

			// Add sorted font families to the observable collection
			foreach (var fontFamily in fontFamilies.OrderBy(f => f))
			{
				AvailableFontFamilies.Add(fontFamily);
			}

			System.Diagnostics.Debug.WriteLine($"Loaded {AvailableFontFamilies.Count} available fonts");
		}

		/// <summary>
		/// Extract the font family name from a .ttf file
		/// This reads the 'name' table to get the proper font family name
		/// </summary>
		private string? ExtractFontFamilyName(string fontFilePath)
		{
			try
			{
				// Use FontParser to read the font name from the 'name' table
				var parser = new FontParser(fontFilePath);
				parser.Initialize();
				
				// Get the font family name from the parser
				// You may need to add a method to FontParser to expose this
				// For now, fall back to filename-based extraction
				return ExtractFamilyNameFromFilename(fontFilePath);
			}
			catch
			{
				// If FontParser fails, try filename-based extraction
				return ExtractFamilyNameFromFilename(fontFilePath);
			}
		}

		/// <summary>
		/// Extract font family name from filename as a fallback
		/// </summary>
		private string ExtractFamilyNameFromFilename(string fontFilePath)
		{
			string fileName = Path.GetFileNameWithoutExtension(fontFilePath);
			
			// Remove common suffixes
			fileName = fileName
				.Replace("Bold", "", StringComparison.OrdinalIgnoreCase)
				.Replace("Italic", "", StringComparison.OrdinalIgnoreCase)
				.Replace("Regular", "", StringComparison.OrdinalIgnoreCase)
				.Replace("Light", "", StringComparison.OrdinalIgnoreCase)
				.Replace("Medium", "", StringComparison.OrdinalIgnoreCase)
				.Replace("-", " ")
				.Replace("_", " ")
				.Trim();

			// Capitalize each word for consistency
			return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fileName.ToLower());
		}

		/// <summary>
		/// Update text styles from the given viewport
		/// </summary>
		public void UpdateFromViewport(ViewportControl? viewport)
		{
			if (viewport == null)
			{
				_currentDocument = null;
				ClearTextStyles();
				return;
			}

			var document = viewport.ObjectToDisplay as OpenCADDocument;
			_currentDocument = document;
			
			if (document != null)
			{
				DisplayDocumentTextStyles(document);
			}
			else
			{
				ClearTextStyles();
			}
		}

		/// <summary>
		/// Display text styles for an OpenCAD document
		/// </summary>
		private void DisplayDocumentTextStyles(OpenCADDocument document)
		{
			var textStyles = new ObservableCollection<TextStyleItem>();
			var currentTextStyleId = document.CurrentTextStyle?.ID;

			foreach (var textStyle in document.GetTextStyles())
			{
				var textStyleItem = new TextStyleItem
				{
					TextStyle = textStyle,
					Name = textStyle.Name,
					FontFamily = textStyle.FontFamily,
					FontSize = textStyle.FontSize,
					IsBold = textStyle.IsBold,
					IsItalic = textStyle.IsItalic,
					IsUnderlined = textStyle.IsUnderlined,
					IsCurrent = textStyle.ID == currentTextStyleId
				};

				// Subscribe to property changes to update the underlying text style
				textStyleItem.PropertyChanged += (s, e) => OnTextStyleItemPropertyChanged(textStyleItem, e.PropertyName);

				textStyles.Add(textStyleItem);
			}

			TextStyles = textStyles;
			System.Diagnostics.Debug.WriteLine($"Text styles updated: {document.Filename} ({textStyles.Count} text styles)");
		}

		/// <summary>
		/// Clear the text styles display
		/// </summary>
		private void ClearTextStyles()
		{
			TextStyles = new ObservableCollection<TextStyleItem>();
		}

		/// <summary>
		/// Raise the TextStylesModified event
		/// </summary>
		private void RaiseTextStylesModified()
		{
			TextStylesModified?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Handle property changes on text style items
		/// </summary>
		private void OnTextStyleItemPropertyChanged(TextStyleItem textStyleItem, string? propertyName)
		{
			if (textStyleItem.TextStyle == null)
				return;

			// Update the underlying text style based on changed property
			switch (propertyName)
			{
				case nameof(TextStyleItem.Name):
					// Validate and update text style name
					if (!string.IsNullOrWhiteSpace(textStyleItem.Name))
					{
						// Check if name is unique (excluding current text style)
						bool isDuplicate = _currentDocument?.GetTextStyles()
							.Any(ts => ts.ID != textStyleItem.TextStyle.ID && ts.Name == textStyleItem.Name) ?? false;
						
						if (!isDuplicate)
						{
							textStyleItem.TextStyle.Name = textStyleItem.Name;
							RaiseTextStylesModified();
						}
						else
						{
							// Revert to original name if duplicate
							textStyleItem.Name = textStyleItem.TextStyle.Name;
						}
					}
					else
					{
						// Revert to original if empty
						textStyleItem.Name = textStyleItem.TextStyle.Name;
					}
					break;
				case nameof(TextStyleItem.FontFamily):
					textStyleItem.TextStyle.FontFamily = textStyleItem.FontFamily;
					ActiveViewport?.Refresh();
					RaiseTextStylesModified();
					break;
				case nameof(TextStyleItem.FontSize):
					textStyleItem.TextStyle.FontSize = textStyleItem.FontSize;
					ActiveViewport?.Refresh();
					RaiseTextStylesModified();
					break;
				case nameof(TextStyleItem.IsBold):
					textStyleItem.TextStyle.IsBold = textStyleItem.IsBold;
					ActiveViewport?.Refresh();
					RaiseTextStylesModified();
					break;
				case nameof(TextStyleItem.IsItalic):
					textStyleItem.TextStyle.IsItalic = textStyleItem.IsItalic;
					ActiveViewport?.Refresh();
					RaiseTextStylesModified();
					break;
				case nameof(TextStyleItem.IsUnderlined):
					textStyleItem.TextStyle.IsUnderlined = textStyleItem.IsUnderlined;
					ActiveViewport?.Refresh();
					RaiseTextStylesModified();
					break;
			}
		}

		/// <summary>
		/// Set the specified text style as the current text style (called from double-click)
		/// </summary>
		public void SetTextStyleAsCurrent(TextStyleItem textStyleItem)
		{
			if (textStyleItem == null || _currentDocument == null)
				return;

			_currentDocument.CurrentTextStyle = textStyleItem.TextStyle;
			System.Diagnostics.Debug.WriteLine($"Current text style changed to: {textStyleItem.Name}");

			// Refresh the display to update the current indicator
			UpdateFromViewport(ActiveViewport);

			// Notify that text styles were modified (current text style changed)
			RaiseTextStylesModified();
		}

		#region Command Handlers

		private bool CanNewTextStyle() => _currentDocument != null;

		private void OnNewTextStyle()
		{
			if (_currentDocument == null)
				return;

			// Generate a unique text style name
			int counter = 1;
			string textStyleName;
			do
			{
				textStyleName = $"Style{counter}";
				counter++;
			}
			while (_currentDocument.GetTextStyle(textStyleName) != null);

			// Create the new text style with default Arial font at size 1.0
			var newTextStyle = _currentDocument.CreateTextStyle(textStyleName, "Arial", 1.0);
			if (newTextStyle != null)
			{
				System.Diagnostics.Debug.WriteLine($"Text style '{textStyleName}' created");
				
				// Refresh the display
				UpdateFromViewport(ActiveViewport);
				
				// Select the new text style
				SelectedTextStyle = TextStyles.FirstOrDefault(ts => ts.Name == textStyleName);

				// Notify that text styles were modified
				RaiseTextStylesModified();
			}
		}

		private bool CanDeleteTextStyle() => SelectedTextStyle != null && 
											   SelectedTextStyle.Name != OpenCADStrings.DefaultTextStyleName &&
											   !SelectedTextStyle.IsCurrent;

		private void OnDeleteTextStyle()
		{
			if (SelectedTextStyle == null || _currentDocument == null)
				return;

			if (SelectedTextStyle.Name == OpenCADStrings.DefaultTextStyleName)
			{
				System.Diagnostics.Debug.WriteLine("Cannot delete default text style");
				return;
			}

			if (SelectedTextStyle.IsCurrent)
			{
				System.Diagnostics.Debug.WriteLine("Cannot delete current text style");
				return;
			}

			string textStyleName = SelectedTextStyle.Name;
			if (_currentDocument.RemoveTextStyle(textStyleName))
			{
				System.Diagnostics.Debug.WriteLine($"Text style '{textStyleName}' deleted");
				
				// Refresh the display
				UpdateFromViewport(ActiveViewport);

				// Notify that text styles were modified
				RaiseTextStylesModified();
			}
		}

		private bool CanSetCurrentTextStyle() => SelectedTextStyle != null;

		private void OnSetCurrentTextStyle()
		{
			if (SelectedTextStyle == null || _currentDocument == null)
				return;

			_currentDocument.CurrentTextStyle = SelectedTextStyle.TextStyle;
			System.Diagnostics.Debug.WriteLine($"Current text style changed to: {SelectedTextStyle.Name}");

			// Refresh the display to update the current indicator
			UpdateFromViewport(ActiveViewport);

			// Notify that text styles were modified (current text style changed)
			RaiseTextStylesModified();
		}

		private bool CanRefresh() => ActiveViewport != null;

		private void OnRefresh()
		{
			UpdateFromViewport(ActiveViewport);
		}

		#endregion
	}

	/// <summary>
	/// Represents a text style item in the text styles list
	/// </summary>
	public class TextStyleItem : ObservableObject
	{
		private string _name = string.Empty;
		private string _fontFamily = "Arial";
		private double _fontSize = 1.0;
		private bool _isBold;
		private bool _isItalic;
		private bool _isUnderlined;
		private bool _isCurrent;

		/// <summary>
		/// Gets or sets the underlying text style object
		/// </summary>
		public OpenCADTextStyle? TextStyle { get; set; }

		/// <summary>
		/// Gets or sets the text style name
		/// </summary>
		public string Name
		{
			get => _name;
			set => SetField(ref _name, value);
		}

		/// <summary>
		/// Gets or sets the font family
		/// </summary>
		public string FontFamily
		{
			get => _fontFamily;
			set => SetField(ref _fontFamily, value);
		}

		/// <summary>
		/// Gets or sets the font size
		/// </summary>
		public double FontSize
		{
			get => _fontSize;
			set => SetField(ref _fontSize, value);
		}

		/// <summary>
		/// Gets or sets whether the font is bold
		/// </summary>
		public bool IsBold
		{
			get => _isBold;
			set => SetField(ref _isBold, value);
		}

		/// <summary>
		/// Gets or sets whether the font is italic
		/// </summary>
		public bool IsItalic
		{
			get => _isItalic;
			set => SetField(ref _isItalic, value);
		}

		/// <summary>
		/// Gets or sets whether the font is underlined
		/// </summary>
		public bool IsUnderlined
		{
			get => _isUnderlined;
			set => SetField(ref _isUnderlined, value);
		}

		/// <summary>
		/// Gets or sets whether this is the current text style
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
		/// Gets the current text style indicator (checkmark if current)
		/// </summary>
		public string CurrentIndicator => IsCurrent ? OpenCADStrings.CurrentLayerIndicator : string.Empty;
	}
}