using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using OpenCAD;
using UI.Controls.Viewport;
using System.Windows.Media;
using System.Drawing;
using System.Windows;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Interaction logic for PropertiesControl.xaml
	/// Displays properties of the active document or selected objects
	/// </summary>
	public partial class PropertiesControl : UserControl
	{
		private PropertiesViewModel ViewModel => (PropertiesViewModel)DataContext;

		public PropertiesControl()
		{
			InitializeComponent();
			DataContext = new PropertiesViewModel();
		}

		/// <summary>
		/// Update properties to display information from the given viewport
		/// </summary>
		public void UpdateFromViewport(ViewportControl? viewport)
		{
			// Call the ViewModel's UpdateFromViewport method directly
			// instead of setting ActiveViewport property to avoid the SetField check
			ViewModel.UpdateFromViewport(viewport);
		}

		/// <summary>
		/// Set the initial ComboBox selection when loaded
		/// </summary>
		private void ColorMode_Loaded(object sender, RoutedEventArgs e)
		{
			if (sender is ComboBox comboBox && comboBox.Tag is PropertyItem propertyItem)
			{
				comboBox.SelectedIndex = propertyItem.IsColorByLayer ? 0 : 1;
			}
		}

		/// <summary>
		/// Handles the color mode selection change (ByLayer vs Custom)
		/// </summary>
		private void ColorMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (sender is ComboBox comboBox && comboBox.Tag is PropertyItem propertyItem)
			{
				bool setToByLayer = comboBox.SelectedIndex == 0;

				if (setToByLayer && !propertyItem.IsColorByLayer)
				{
					// Set to ByLayer (transparent black)
					propertyItem.RawValue = System.Drawing.Color.FromArgb(0, 0, 0, 0);
					propertyItem.Value = OpenCADStrings.ByLayer;
				}
				else if (!setToByLayer && propertyItem.IsColorByLayer)
				{
					// Changing from ByLayer to custom - set default color (White)
					propertyItem.RawValue = System.Drawing.Color.White;
					propertyItem.Value = "A255 R255 G255 B255";
				}
				
				// Notify property changed for IsColorByLayer (for visibility binding)
				propertyItem.OnPropertyChanged(nameof(propertyItem.IsColorByLayer));
			}
		}
	}
}