using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenCAD;
using OpenCAD.Geometry;
using UI.Commands;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Controls.MainWindow
{
    /// <summary>
    /// ViewModel for CommandInputControl
    /// </summary>
    public class CommandInputViewModel : INotifyPropertyChanged
    {
        #region Fields

        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        private string? _lastCommand;
        private IInputCommand? _activeCommand;
        private Point3D? _lastEnteredPoint;
        private Func<ViewportControl?>? _getActiveViewport;
        private OpenCADDocument? _document;

        private string _commandText = string.Empty;
        private string _historyText = "Welcome to OpenCAD Command Input\nType 'help' for available commands";
        private string _promptText = "Command >";

        private readonly CommandRegistry _commandRegistry;
        private readonly ICommandContext _commandContext;
        private readonly UndoRedoManager _undoRedoManager;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the command text input
        /// </summary>
        public string CommandText
        {
            get => _commandText;
            set
            {
                if (_commandText != value)
                {
                    _commandText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the history text display
        /// </summary>
        public string HistoryText
        {
            get => _historyText;
            set
            {
                if (_historyText != value)
                {
                    _historyText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the prompt text
        /// </summary>
        public string PromptText
        {
            get => _promptText;
            set
            {
                if (_promptText != value)
                {
                    _promptText = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets whether there is an active multi-step command
        /// </summary>
        public bool HasActiveCommand => _activeCommand != null;

        /// <summary>
        /// Gets the currently active command (for checking RequiresSelection, etc.)
        /// </summary>
        public IInputCommand? ActiveCommand => _activeCommand;

        #endregion

        #region Events

        /// <summary>
        /// Event raised when geometry is created
        /// </summary>
        public event EventHandler<GeometryCreatedEventArgs>? GeometryCreated;

        /// <summary>
        /// Event raised when the scroll position should be updated
        /// </summary>
        public event EventHandler? ScrollToEndRequested;

        /// <summary>
        /// Event raised when focus should be set to the command input
        /// </summary>
        public event EventHandler? FocusRequested;

        /// <summary>
        /// Event raised when the active command state changes
        /// </summary>
        public event EventHandler? ActiveCommandChanged;

        #endregion

        #region Constructor

        public CommandInputViewModel()
        {
            // Initialize undo/redo manager
            _undoRedoManager = new UndoRedoManager();

            // Initialize command context
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
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the function to get the active viewport
        /// </summary>
        public void SetActiveViewportProvider(Func<ViewportControl?> getActiveViewport)
        {
            _getActiveViewport = getActiveViewport;
            System.Diagnostics.Debug.WriteLine("CommandInputViewModel: Viewport provider set");
        }

        /// <summary>
        /// Set the current document
        /// </summary>
        public void SetDocument(OpenCADDocument document)
        {
            _document = document;
        }

        /// <summary>
        /// Get the undo/redo manager
        /// </summary>
        public UndoRedoManager GetUndoRedoManager() => _undoRedoManager;

        /// <summary>
        /// Handle key down events
        /// </summary>
        public bool HandleKeyDown(Key key)
        {
            switch (key)
            {
                case Key.Enter:
                    ExecuteCommand();
                    return true;

                case Key.Space:
                    // Allow space to be typed normally when:
                    // 1. There's text in the input field (user is typing coordinates/values)
                    // 2. There's an active command (space might be needed as separator in point input)
                    string currentInput = CommandText.Trim();
                    if (!string.IsNullOrEmpty(currentInput) || _activeCommand != null)
                    {
                        return false; // Let space be typed normally
                    }
                    // Only execute (repeat last command) if input is empty AND no active command
                    ExecuteCommand();
                    return true;

                case Key.Up:
                    NavigateHistoryBackward();
                    return true;

                case Key.Down:
                    NavigateHistoryForward();
                    return true;

                case Key.Escape:
                    if (_activeCommand != null)
                    {
                        CancelCurrentCommand();
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Clear the command history display
        /// </summary>
        public void ClearHistory()
        {
            HistoryText = string.Empty;
        }

        /// <summary>
        /// Execute a command programmatically without user typing it
        /// </summary>
        public void ExecuteCommandProgrammatically(string commandName)
        {
            System.Diagnostics.Debug.WriteLine($"=== ExecuteCommandProgrammatically: '{commandName}' ===");
            
            string resolvedCommand = ResolveCommandAlias(commandName);
            System.Diagnostics.Debug.WriteLine($"  Resolved to: '{resolvedCommand}'");
            
            // Output to history to show the command was executed
            AppendToHistory($"> {resolvedCommand}");
            
            try
            {
                ProcessNewCommand(resolvedCommand);
            }
            catch (Exception ex)
            {
                AppendToHistory($"Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  ERROR executing command: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods - Command Execution

        private void ExecuteCommand()
        {
            string input = CommandText.Trim();

            // If there's an active multi-step command, process input for it
            if (_activeCommand != null)
            {
                ProcessActiveCommandInput(input);
                return;
            }

            // No active command - check if we should repeat the last command
            if (string.IsNullOrEmpty(input))
            {
                if (!string.IsNullOrEmpty(_lastCommand))
                {
                    input = _lastCommand;
                    AppendToHistory($"> {input}");
                }
                else
                {
                    return; // No command to repeat
                }
            }
            else
            {
                input = ResolveCommandAlias(input);
                _commandHistory.Add(input);
                _historyIndex = -1;
                AppendToHistory($"> {input}");
            }

            _lastCommand = input;

            try
            {
                ProcessNewCommand(input);
            }
            catch (Exception ex)
            {
                AppendToHistory($"Error: {ex.Message}");
            }

            CommandText = string.Empty;
            FocusRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ProcessActiveCommandInput(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                bool isComplete = _activeCommand!.ProcessInput(input);
                if (isComplete)
                {
                    CompleteActiveCommand();
                }
                CommandText = string.Empty;
                FocusRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            AppendToHistory($"> {input}");

            try
            {
                bool isComplete = _activeCommand!.ProcessInput(input);
                if (isComplete)
                {
                    CompleteActiveCommand();
                }
            }
            catch (Exception ex)
            {
                AppendToHistory($"Error: {ex.Message}");
                CompleteActiveCommand();
            }

            CommandText = string.Empty;
            FocusRequested?.Invoke(this, EventArgs.Empty);
        }

        private string ResolveCommandAlias(string input)
        {
            string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                string commandName = parts[0].ToLower();
                string? canonicalName = _commandRegistry.GetCanonicalName(commandName);

                if (canonicalName != null)
                {
                    parts[0] = canonicalName;
                    return string.Join(" ", parts);
                }
            }
            return input;
        }

        private void ProcessNewCommand(string input)
        {
            string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string commandName = parts[0].ToLower();

            var commandType = _commandRegistry.GetCommandType(commandName);
            if (commandType == null)
            {
                AppendToHistory($"Unknown command: {commandName}. Type 'help' for available commands.");
                return;
            }

            IInputCommand? command = CreateCommandInstance(commandType);
            if (command == null)
            {
                AppendToHistory($"Error: Could not create command '{commandName}'");
                return;
            }

            command.Initialize(_commandContext);
            command.Execute();

            if (command.IsMultiStep)
            {
                _activeCommand = command;
                _activeCommand.PromptChanged += OnCommandPromptChanged;
                _activeCommand.CommandCompleted += OnCommandCompleted;
                UpdatePrompt();
                OnPropertyChanged(nameof(HasActiveCommand));
                ActiveCommandChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private IInputCommand? CreateCommandInstance(Type commandType)
        {
            if (commandType == typeof(ClearCommand))
            {
                return new ClearCommand(ClearHistory);
            }
            else if (commandType == typeof(HelpCommand))
            {
                var helpCommand = new HelpCommand();
                helpCommand.SetCommandRegistry(_commandRegistry.GetCommandInfo());
                return helpCommand;
            }
            else
            {
                return (IInputCommand?)Activator.CreateInstance(commandType);
            }
        }

        #endregion

        #region Private Methods - Command State Management

        private void CompleteActiveCommand()
        {
            UnsubscribeFromActiveCommand();
            _activeCommand = null;
            UpdatePrompt();
            OnPropertyChanged(nameof(HasActiveCommand));
            ActiveCommandChanged?.Invoke(this, EventArgs.Empty);
            
            // Request focus back to command input after command completes
            FocusRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CancelCurrentCommand()
        {
            _activeCommand?.Cancel();
            CompleteActiveCommand();
            CommandText = string.Empty;
        }

        private void UnsubscribeFromActiveCommand()
        {
            if (_activeCommand != null)
            {
                _activeCommand.PromptChanged -= OnCommandPromptChanged;
                _activeCommand.CommandCompleted -= OnCommandCompleted;
            }
        }

        private void UpdatePrompt()
        {
            PromptText = _activeCommand != null 
                ? $"{_activeCommand.CurrentPrompt} >" 
                : "Command >";
        }

        #endregion

        #region Private Methods - History Navigation

        private void NavigateHistoryBackward()
        {
            if (_commandHistory.Count > 0 && _historyIndex < _commandHistory.Count - 1)
            {
                _historyIndex++;
                CommandText = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
            }
        }

        private void NavigateHistoryForward()
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                CommandText = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
            }
            else if (_historyIndex == 0)
            {
                _historyIndex = -1;
                CommandText = string.Empty;
            }
        }

        #endregion

        #region Private Methods - Output

        private void AppendToHistory(string text)
        {
            if (!string.IsNullOrEmpty(HistoryText))
            {
                HistoryText += Environment.NewLine + text;
            }
            else
            {
                HistoryText = text;
            }

            ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Event Handlers

        private void OnCommandPromptChanged(object? sender, EventArgs e)
        {
            UpdatePrompt();
        }

        private void OnCommandCompleted(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("CommandInputViewModel: Command completed via event");
            CompleteActiveCommand();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}