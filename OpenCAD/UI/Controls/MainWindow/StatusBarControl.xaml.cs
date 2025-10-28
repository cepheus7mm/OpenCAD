using System.Windows;
using System.Windows.Controls;

namespace UI.Controls.MainWindow
{
    /// <summary>
    /// Interaction logic for StatusBarControl.xaml
    /// </summary>
public partial class StatusBarControl : UserControl
    {
        public StatusBarControl()
        {
   InitializeComponent();
        }

    /// <summary>
        /// Updates the status message displayed in the status bar
        /// </summary>
  /// <param name="message">The status message to display</param>
        public void UpdateStatus(string message)
  {
        statusTextBlock.Text = message;
 }

        /// <summary>
        /// Updates the cursor position (line and column) displayed in the status bar
        /// </summary>
        /// <param name="line">The current line number</param>
      /// <param name="column">The current column number</param>
        public void UpdatePosition(int line, int column)
        {
 positionTextBlock.Text = $"Line {line}, Col {column}";
        }

        /// <summary>
        /// Updates the cursor position with a custom text format
      /// </summary>
        /// <param name="positionText">The position text to display</param>
        public void UpdatePositionText(string positionText)
        {
            positionTextBlock.Text = positionText;
        }
    }
}
