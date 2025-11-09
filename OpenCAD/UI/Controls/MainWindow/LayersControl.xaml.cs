using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UI.Controls.Viewport;
using Xceed.Wpf.Toolkit;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Interaction logic for LayersControl.xaml
	/// Displays and manages layers in the active document
	/// </summary>
	public partial class LayersControl : UserControl
	{
		private LayersViewModel ViewModel => (LayersViewModel)DataContext;

		public LayersControl()
		{
			InitializeComponent();
			DataContext = new LayersViewModel();
		}

		/// <summary>
		/// Update layers to display information from the given viewport
		/// </summary>
		public void UpdateFromViewport(ViewportControl? viewport)
		{
			ViewModel.ActiveViewport = viewport;
		}

		/// <summary>
		/// Handle double-click on the DataGrid to set current layer or toggle visibility
		/// </summary>
		private void LayersDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			// Get the clicked element
			var element = e.OriginalSource as FrameworkElement;
			if (element == null)
				return;

			// Find the DataGridRow that was clicked
			var row = FindVisualParent<DataGridRow>(element);
			if (row == null)
				return;

			// Get the layer item from the row
			var layerItem = row.Item as LayerItem;
			if (layerItem == null)
				return;

			// Get the column that was clicked
			var cell = FindVisualParent<DataGridCell>(element);
			if (cell == null)
				return;

			// Check which column was double-clicked
			var columnIndex = cell.Column.DisplayIndex;
			
			if (columnIndex == 0)
			{
				// "Current" column (first column, index 0) - Set this layer as current
				ViewModel.SetLayerAsCurrent(layerItem);
				e.Handled = true;
				System.Diagnostics.Debug.WriteLine($"Layer '{layerItem.Name}' set as current via double-click");
			}
			else if (columnIndex == 1)
			{
				// "On / Off" column (second column, index 1) - Toggle visibility
				ViewModel.ToggleLayerVisibility(layerItem);
				e.Handled = true;
				System.Diagnostics.Debug.WriteLine($"Layer '{layerItem.Name}' visibility toggled to {layerItem.IsVisible} via double-click");
			}
		}

		/// <summary>
		/// Helper method to find a parent of a specific type in the visual tree
		/// </summary>
		private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
		{
			var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
			
			if (parent == null)
				return null;
			
			if (parent is T typedParent)
				return typedParent;
			
			return FindVisualParent<T>(parent);
		}

		/// <summary>
		/// Handle color button click to show color picker
		/// </summary>
		private void ColorButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is LayerItem layerItem)
			{
				// Convert System.Drawing.Color to System.Windows.Media.Color
				var mediaColor = System.Windows.Media.Color.FromArgb(
					layerItem.Color.A,
					layerItem.Color.R,
					layerItem.Color.G,
					layerItem.Color.B);

				// Create a color picker dialog
				var colorPickerWindow = new Window
				{
					Title = $"Select Color for Layer: {layerItem.Name}",
					Width = 400,
					Height = 500,
					WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
					Owner = Window.GetWindow(this),
					ResizeMode = ResizeMode.NoResize
				};

				var colorPicker = new ColorPicker
				{
					SelectedColor = mediaColor,
					DisplayColorAndName = true,
					ShowAvailableColors = true,
					ShowStandardColors = true,
					ShowRecentColors = true,
					Margin = new Thickness(10)
				};

				var stackPanel = new StackPanel();
				stackPanel.Children.Add(colorPicker);

				// Add OK and Cancel buttons
				var buttonPanel = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					HorizontalAlignment = HorizontalAlignment.Right,
					Margin = new Thickness(10)
				};

				var okButton = new Button
				{
					Content = "OK",
					Width = 75,
					Height = 25,
					Margin = new Thickness(5, 0, 5, 0),
					IsDefault = true
				};
				okButton.Click += (s, args) =>
				{
					colorPickerWindow.DialogResult = true;
					colorPickerWindow.Close();
				};

				var cancelButton = new Button
				{
					Content = "Cancel",
					Width = 75,
					Height = 25,
					Margin = new Thickness(5, 0, 5, 0),
					IsCancel = true
				};
				cancelButton.Click += (s, args) =>
				{
					colorPickerWindow.DialogResult = false;
					colorPickerWindow.Close();
				};

				buttonPanel.Children.Add(okButton);
				buttonPanel.Children.Add(cancelButton);
				stackPanel.Children.Add(buttonPanel);

				colorPickerWindow.Content = stackPanel;

				// Show the dialog
				if (colorPickerWindow.ShowDialog() == true && colorPicker.SelectedColor.HasValue)
				{
					var selectedColor = colorPicker.SelectedColor.Value;
					
					// Convert back to System.Drawing.Color
					layerItem.Color = System.Drawing.Color.FromArgb(
						selectedColor.A,
						selectedColor.R,
						selectedColor.G,
						selectedColor.B);
					
					System.Diagnostics.Debug.WriteLine($"Layer '{layerItem.Name}' color changed to {layerItem.Color}");
				}
			}
		}
	}
}