using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UI.Controls.Viewport;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Interaction logic for TextStylesControl.xaml
	/// Displays and manages text styles in the active document
	/// </summary>
	public partial class TextStylesControl : UserControl
	{
		private TextStylesViewModel ViewModel => (TextStylesViewModel)DataContext;

		public TextStylesControl()
		{
			InitializeComponent();
			DataContext = new TextStylesViewModel();
		}

		/// <summary>
		/// Update text styles to display information from the given viewport
		/// </summary>
		public void UpdateFromViewport(ViewportControl? viewport)
		{
			ViewModel.ActiveViewport = viewport;
		}

		/// <summary>
		/// Handle double-click on the DataGrid to set current text style
		/// </summary>
		private void TextStylesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			// Get the clicked element
			var element = e.OriginalSource as FrameworkElement;
			if (element == null)
				return;

			// Find the DataGridRow that was clicked
			var row = FindVisualParent<DataGridRow>(element);
			if (row == null)
				return;

			// Get the text style item from the row
			var textStyleItem = row.Item as TextStyleItem;
			if (textStyleItem == null)
				return;

			// Get the column that was clicked
			var cell = FindVisualParent<DataGridCell>(element);
			if (cell == null)
				return;

			// Check which column was double-clicked
			var columnIndex = cell.Column.DisplayIndex;
			
			if (columnIndex == 0)
			{
				// "Current" column (first column, index 0) - Set this text style as current
				ViewModel.SetTextStyleAsCurrent(textStyleItem);
				e.Handled = true;
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
	}
}