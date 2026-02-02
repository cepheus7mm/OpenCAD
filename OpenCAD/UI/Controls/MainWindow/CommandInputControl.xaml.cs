using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenCAD;
using OpenCAD.Undo;
using UI.Controls.Viewport;
using UI.Commands.InputHelpers; // Add this

namespace UI.Controls.MainWindow
{
    /// <summary>
    /// Interaction logic for CommandInputControl.xaml
    /// Provides a command-line interface for creating geometry
    /// </summary>
    public partial class CommandInputControl : UserControl
    {
        private readonly CommandInputViewModel _viewModel;

        // Event to notify when new geometry should be added
        public event EventHandler<GeometryCreatedEventArgs>? GeometryCreated;

        public CommandInputControl()
        {
            InitializeComponent();

            _viewModel = new CommandInputViewModel();
            DataContext = _viewModel;

            // Forward events from ViewModel
            _viewModel.GeometryCreated += (s, e) => GeometryCreated?.Invoke(this, e);
            // Use the TextBox's own ScrollToEnd (we removed the outer ScrollViewer)
            _viewModel.ScrollToEndRequested += (s, e) => historyTextBox.ScrollToEnd();
            _viewModel.FocusRequested += (s, e) => FocusCommandInput();

            // Setup bindings
            commandTextBox.SetBinding(TextBox.TextProperty, 
                new System.Windows.Data.Binding(nameof(CommandInputViewModel.CommandText))
                {
                    Source = _viewModel,
                    Mode = System.Windows.Data.BindingMode.TwoWay,
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
                });

            // Bind history to the read-only TextBox so selection & copy work.
            historyTextBox.SetBinding(TextBox.TextProperty, 
                new System.Windows.Data.Binding(nameof(CommandInputViewModel.HistoryText))
                {
                    Source = _viewModel
                });

            promptTextBlock.SetBinding(TextBlock.TextProperty, 
                new System.Windows.Data.Binding(nameof(CommandInputViewModel.PromptText))
                {
                    Source = _viewModel
                });

            // Prevent any paste into the history TextBox (extra safety; IsReadOnly already blocks edits)
            historyTextBox.PreviewKeyDown += HistoryTextBox_PreviewKeyDown;
            historyTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPasteExecuted, OnPasteCanExecute));

            // Allow focus so mouse selection and keyboard copy (Ctrl+C) work.
            // To keep normal typing directed to the command input, intercept most printable keys here and return focus.
            historyTextBox.PreviewKeyDown += HistoryTextBox_RedirectTyping;

            commandTextBox.Focus();
        }

        private void OnPasteExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // Swallow paste attempts into history
            e.Handled = true;
        }

        private void OnPasteCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
            e.Handled = true;
        }

        private void HistoryTextBox_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            // Prevent Ctrl+V and Shift+Insert into the history TextBox
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
            {
                e.Handled = true;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && e.Key == Key.Insert)
            {
                e.Handled = true;
            }
        }

        private void HistoryTextBox_RedirectTyping(object? sender, KeyEventArgs e)
        {
            // Allow navigation and copy-related shortcuts.
            bool isCopy =
                (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) ||
                (e.Key == Key.Insert && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) ||
                (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control);

            bool isNavigation =
                e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down ||
                e.Key == Key.PageUp || e.Key == Key.PageDown || e.Key == Key.Home || e.Key == Key.End ||
                e.Key == Key.Tab || e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.Delete;

            if (isCopy || isNavigation)
            {
                // Allow copy and navigation keys to operate in the history box.
                return;
            }

            // For printable/input keys, return focus to the command input so typing continues there.
            FocusCommandInput();
            e.Handled = true;
        }

        /// <summary>
        /// Focus the command input text box
        /// </summary>
        public void FocusCommandInput()
        {
            commandTextBox.Focus();
            Keyboard.Focus(commandTextBox);
        }

        /// <summary>
        /// Set the function to get the active viewport (called by DockingAreaControl)
        /// </summary>
        public void SetActiveViewportProvider(Func<ViewportControl?> getActiveViewport)
        {
            _viewModel.SetActiveViewportProvider(getActiveViewport);
        }

        /// <summary>
        /// Set the document
        /// </summary>
        public void SetDocument(OpenCADDocument document)
        {
            _viewModel.SetDocument(document);
        }

        /// <summary>
        /// Get the undo/redo manager
        /// </summary>
        public UndoRedoManager GetUndoRedoManager() => _viewModel.GetUndoRedoManager();

        private void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // This handles Enter key and calls ProcessInput to complete async tasks
            e.Handled = _viewModel.HandleKeyDown(e.Key);
        }

        /// <summary>
        /// Called while user is typing (NOT when they press Enter).
        /// Notifies the active input helper about text changes for preview purposes.
        /// Does NOT complete any async tasks - that happens in HandleKeyDown when Enter is pressed.
        /// </summary>
        private void CommandTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && _viewModel != null)
            {
                // Notify the ViewModel about text changes for preview
                _viewModel.NotifyTextChanged(textBox.Text);
            }
        }

        /// <summary>
        /// Execute a command programmatically (e.g., from keyboard shortcut)
        /// </summary>
        public void ExecuteCommandProgrammatically(string commandName)
        {
            _viewModel.ExecuteCommandProgrammatically(commandName);
        }

        /// <summary>
        /// Wire up viewport selection events to restore focus
        /// </summary>
        public void WireUpViewportEvents(ViewportControl viewport)
        {
            if (viewport != null && viewport.DataContext is ViewportViewModel viewModel)
            {
                viewModel.SelectionChanged += (s, e) =>
                {
                    // Return focus to command input after selection
                    FocusCommandInput();
                };
            }
        }
    }

    /// <summary>
    /// Event arguments for when geometry is created
    /// </summary>
    public class GeometryCreatedEventArgs : EventArgs
    {
        public OpenCADObject Geometry { get; }

        public GeometryCreatedEventArgs(OpenCADObject geometry)
        {
            Geometry = geometry;
        }
    }
}