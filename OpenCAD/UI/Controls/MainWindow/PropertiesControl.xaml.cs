using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using OpenCAD;
using UI.Controls.Viewport;

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
	}
}