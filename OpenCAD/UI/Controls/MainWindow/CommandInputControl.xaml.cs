using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenCAD;
using OpenCAD.Geometry;
using UI.Commands;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Controls.MainWindow
{
    /// <summary>
    /// Interaction logic for CommandInputControl.xaml
    /// Provides a command-line interface for creating geometry
    /// </summary>
    public partial class CommandInputControl : UserControl
    {
        // Event to notify when new geometry should be added
        public event EventHandler<GeometryCreatedEventArgs>? GeometryCreated;

        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        
        // Command system
        private readonly CommandRegistry _commandRegistry;
        private readonly ICommandContext _commandContext;
        private IInputCommand? _activeCommand;
        private Point3D? _lastEnteredPoint;
        private Func<ViewportControl?>? _getActiveViewport;

        private readonly UndoRedoManager _undoRedoManager;
        private OpenCADDocument? _document;

        public CommandInputControl()
        {
            InitializeComponent();

            // Initialize undo/redo manager
            _undoRedoManager = new UndoRedoManager();

            // Initialize command context with viewport provider
            _commandContext = new CommandContext(
                outputMessage: AppendToHistory,
                getLastPoint: () => _lastEnteredPoint,
                setLastPoint: point => _lastEnteredPoint = point,
                raiseGeometryCreated: geometry => GeometryCreated?.Invoke(this, new GeometryCreatedEventArgs(geometry)),
                getActiveViewport: () => _getActiveViewport?.Invoke(),
                getUndoRedoManager: () => _undoRedoManager,
                getDocument: () => _document
            );

            // Initialize and discover commands
            _commandRegistry = new CommandRegistry();
            _commandRegistry.DiscoverCommands();

            commandTextBox.Focus();
        }

        /// <summary>
        /// Set the function to get the active viewport (called by DockingAreaControl)
        /// </summary>
        public void SetActiveViewportProvider(Func<ViewportControl?> getActiveViewport)
        {
            _getActiveViewport = getActiveViewport;
            System.Diagnostics.Debug.WriteLine("CommandInputControl: Viewport provider set");
        }

        private void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if ( e.Key == Key.Enter )
            {
                ExecuteCommand();
                e.Handled = true;
            }
            else if ( e.Key == Key.Up )
            {
                // Navigate command history backwards
                if ( _commandHistory.Count > 0 && _historyIndex < _commandHistory.Count - 1 )
                {
                    _historyIndex++;
                    commandTextBox.Text = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
                    commandTextBox.CaretIndex = commandTextBox.Text.Length;
                }
                e.Handled = true;
            }
            else if ( e.Key == Key.Down )
            {
                // Navigate command history forwards
                if ( _historyIndex > 0 )
                {
                    _historyIndex--;
                    commandTextBox.Text = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
                    commandTextBox.CaretIndex = commandTextBox.Text.Length;
                }
                else if ( _historyIndex == 0 )
                {
                    _historyIndex = -1;
                    commandTextBox.Text = string.Empty;
                }
                e.Handled = true;
            }
            else if ( e.Key == Key.Escape )
            {
                // Cancel current command
                if ( _activeCommand != null )
                {
                    CancelCurrentCommand();
                    e.Handled = true;
                }
            }
        }

        private void ExecuteCommand()
        {
            string input = commandTextBox.Text.Trim();

            // If there's an active multi-step command, process input for it
            if ( _activeCommand != null )
            {
                // Handle empty input for multi-step commands
                if ( string.IsNullOrEmpty(input) )
                {
                    // Let the command handle empty input (e.g., using last point)
                    bool isComplete = _activeCommand.ProcessInput(input);
                    if ( isComplete )
                    {
                        UnsubscribeFromActiveCommand();
                        _activeCommand = null;
                        UpdatePrompt();
                    }
                    commandTextBox.Clear();
                    commandTextBox.Focus();
                    return;
                }

                // Log input
                AppendToHistory($"> {input}");

                try
                {
                    bool isComplete = _activeCommand.ProcessInput(input);
                    if ( isComplete )
                    {
                        UnsubscribeFromActiveCommand();
                        _activeCommand = null;
                        UpdatePrompt();
                    }
                }
                catch ( Exception ex )
                {
                    AppendToHistory($"Error: {ex.Message}");
                    UnsubscribeFromActiveCommand();
                    _activeCommand = null;
                    UpdatePrompt();
                }

                commandTextBox.Clear();
                commandTextBox.Focus();
                return;
            }

            // No active command, parse new command
            if ( string.IsNullOrEmpty(input) )
                return;

            // Add to history
            _commandHistory.Add(input);
            _historyIndex = -1;

            // Log command
            AppendToHistory($"> {input}");

            // Parse and execute command
            try
            {
                ProcessNewCommand(input);
            }
            catch ( Exception ex )
            {
                AppendToHistory($"Error: {ex.Message}");
            }

            // Clear input
            commandTextBox.Clear();
            commandTextBox.Focus();
        }

        private void ProcessNewCommand(string input)
        {
            string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if ( parts.Length == 0 ) return;

            string commandName = parts[0].ToLower();

            // Get command type from registry
            var commandType = _commandRegistry.GetCommandType(commandName);
            if ( commandType == null )
            {
                AppendToHistory($"Unknown command: {commandName}. Type 'help' for available commands.");
                return;
            }

            // Create command instance
            IInputCommand? command = null;

            // Special handling for commands that need constructor parameters
            if ( commandType == typeof(ClearCommand) )
            {
                command = new ClearCommand(() => 
                {
                    historyTextBlock.Text = string.Empty;
                });
            }
            else if ( commandType == typeof(HelpCommand) )
            {
                var helpCommand = new HelpCommand();
                helpCommand.SetCommandRegistry(_commandRegistry.GetCommandInfo());
                command = helpCommand;
            }
            else
            {
                // Default: parameterless constructor
                command = (IInputCommand?)Activator.CreateInstance(commandType);
            }

            if ( command == null )
            {
                AppendToHistory($"Error: Could not create command '{commandName}'");
                return;
            }

            // Initialize and execute command
            command.Initialize(_commandContext);
            command.Execute();

            // If it's a multi-step command, set it as active
            if ( command.IsMultiStep )
            {
                _activeCommand = command;
                // Subscribe to command events
                _activeCommand.PromptChanged += OnCommandPromptChanged;
                _activeCommand.CommandCompleted += OnCommandCompleted;
                UpdatePrompt();
            }
        }

        private void OnCommandPromptChanged(object? sender, EventArgs e)
        {
            // Update the prompt when the command's prompt changes
            UpdatePrompt();
        }

        private void OnCommandCompleted(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("CommandInputControl: Command completed via event");
            // Command completed (likely via mouse input)
            UnsubscribeFromActiveCommand();
            _activeCommand = null;
            UpdatePrompt();
        }

        private void UnsubscribeFromActiveCommand()
        {
            if ( _activeCommand != null )
            {
                _activeCommand.PromptChanged -= OnCommandPromptChanged;
                _activeCommand.CommandCompleted -= OnCommandCompleted;
            }
        }

        private void CancelCurrentCommand()
        {
            _activeCommand?.Cancel();
            UnsubscribeFromActiveCommand();
            _activeCommand = null;
            commandTextBox.Clear();
            UpdatePrompt();
        }

        private void UpdatePrompt()
        {
            if ( _activeCommand != null )
            {
                promptTextBlock.Text = _activeCommand.CurrentPrompt + " >";
            }
            else
            {
                promptTextBlock.Text = "Command >";
            }
        }

        private void AppendToHistory(string text)
        {
            // Add newline before text if history is not empty (not for the first line)
            if ( !string.IsNullOrEmpty(historyTextBlock.Text) )
            {
                historyTextBlock.Text += Environment.NewLine + text;
            }
            else
            {
                historyTextBlock.Text = text;
            }
            historyScrollViewer.ScrollToEnd();
        }

        // Add methods to set the document
        public void SetDocument(OpenCADDocument document)
        {
            _document = document;
        }

        public UndoRedoManager GetUndoRedoManager() => _undoRedoManager;
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