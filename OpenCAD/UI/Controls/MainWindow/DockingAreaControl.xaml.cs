using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Interaction logic for DockingAreaControl.xaml
	/// </summary>
	public partial class DockingAreaControl : UserControl
	{
		public DockingAreaControl()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Gets the DockingManager instance for programmatic access
		/// </summary>
		public DockingManager DockingManager => dockingManager;

		/// <summary>
		/// Gets the document pane where document tabs are displayed
		/// </summary>
		public LayoutDocumentPane? GetDocumentPane()
		{
			// Search recursively through the layout for the document pane
			return FindDocumentPane(dockingManager.Layout?.RootPanel);
		}

		/// <summary>
		/// Recursively searches for a LayoutDocumentPane in the layout hierarchy
		/// </summary>
		private LayoutDocumentPane? FindDocumentPane(ILayoutContainer? container)
		{
			if (container == null) return null;

			// Check if this is a document pane
			if (container is LayoutDocumentPane docPane)
				return docPane;

			// Recursively search children
			foreach (var child in container.Children)
			{
				if (child is LayoutDocumentPane foundPane)
					return foundPane;

				if (child is ILayoutContainer childContainer)
				{
					var result = FindDocumentPane(childContainer);
					if (result != null)
						return result;
				}
			}

			return null;
		}

		/// <summary>
		/// Adds a new document to the document pane
		/// </summary>
		/// <param name="title">The title of the document</param>
		/// <param name="contentId">The unique content ID</param>
		/// <param name="content">The content to display</param>
		/// <returns>The created LayoutDocument</returns>
		public LayoutDocument? AddDocument(string title, string contentId, object content)
		{
			var docPane = GetDocumentPane();
			if (docPane != null)
			{
				var newDoc = new LayoutDocument
				{
					Title = title,
					ContentId = contentId,
					Content = content
				};

				docPane.Children.Add(newDoc);
				newDoc.IsSelected = true;
				return newDoc;
			}
			return null;
		}

		/// <summary>
		/// Applies theme to all TextBox controls in the docking area
		/// </summary>
		public void ApplyThemeToDocuments()
		{
			var docPane = GetDocumentPane();
			if (docPane != null)
			{
				foreach (var document in docPane.Children.OfType<LayoutDocument>())
				{
					if (document.Content is TextBox textBox)
					{
						ApplyThemeToTextBox(textBox);
					}
				}
			}
		}

		/// <summary>
		/// Applies theme resources to a TextBox
		/// </summary>
		private void ApplyThemeToTextBox(TextBox textBox)
		{
			textBox.SetResourceReference(Control.BackgroundProperty, "PrimaryBackgroundBrush");
			textBox.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
		}
	}
}
