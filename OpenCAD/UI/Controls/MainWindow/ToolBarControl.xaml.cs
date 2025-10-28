using System.Windows;
using System.Windows.Controls;

namespace UI.Controls.MainWindow
{
    /// <summary>
    /// Interaction logic for ToolBarControl.xaml
    /// </summary>
    public partial class ToolBarControl : UserControl
    {
        // Events to notify the main window of toolbar actions
        public event EventHandler? NewFileRequested;
        public event EventHandler? OpenRequested;
    public event EventHandler? SaveRequested;
   public event EventHandler? CutRequested;
        public event EventHandler? CopyRequested;
  public event EventHandler? PasteRequested;

      public ToolBarControl()
        {
 InitializeComponent();
      }

     private void NewFile_Click(object sender, RoutedEventArgs e)
{
         NewFileRequested?.Invoke(this, EventArgs.Empty);
     }

   private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
          SaveRequested?.Invoke(this, EventArgs.Empty);
        }

 private void Cut_Click(object sender, RoutedEventArgs e)
        {
     CutRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
       CopyRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
     {
 PasteRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
