using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UI.Controls.Viewport;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Interaction logic for DimensionStylesControl.xaml
	/// Displays and manages dimension styles in the active document
	/// </summary>
	public partial class DimensionStylesControl : UserControl
	{
		private DimensionStylesViewModel ViewModel => (DimensionStylesViewModel)DataContext;

		public DimensionStylesControl()
		{
			InitializeComponent();
			DataContext = new DimensionStylesViewModel();
		}

		/// <summary>
		/// Update dimension styles to display information from the given viewport
		/// </summary>
		public void UpdateFromViewport(ViewportControl? viewport)
		{
			ViewModel.ActiveViewport = viewport;
		}

		/// <summary>
		/// Handle double-click on the DataGrid to set current dimension style
		/// </summary>
		private void DimensionStylesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var element = e.OriginalSource as FrameworkElement;
			if (element == null)
				return;

			var row = FindVisualParent<DataGridRow>(element);
			if (row == null)
				return;

			var dimStyleItem = row.Item as DimensionStyleItem;
			if (dimStyleItem == null)
				return;

			var cell = FindVisualParent<DataGridCell>(element);
			if (cell == null)
				return;

			var columnIndex = cell.Column.DisplayIndex;

			if (columnIndex == 0)
			{
				// "Current" column - Set this dimension style as current
				ViewModel.SetDimensionStyleAsCurrent(dimStyleItem);
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