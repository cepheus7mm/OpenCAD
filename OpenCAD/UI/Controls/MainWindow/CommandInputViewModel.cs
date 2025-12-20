using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
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
                getDocument: () => _document,
                setCommandPrompt: SetCommandPrompt
            );
            _undoRedoManager.Context = _commandContext;

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
                    string currentInput = CommandText.Trim();
                    
                    // If there's an active command and input is empty, treat Space like Enter (accept default/process empty)
                    if (_activeCommand != null && string.IsNullOrEmpty(currentInput))
                    {
                        ExecuteCommand();
                        return true;
                    }
                    
                    // If input is not empty, allow space to be typed normally (coordinate separator, etc.)
                    if (!string.IsNullOrEmpty(currentInput))
                    {
                        return false; // Let space be typed
                    }
                    
                    // No active command and empty input: repeat last command
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
        public async Task ExecuteCommandProgrammatically(string commandName)
        {
            string resolvedCommand = ResolveCommandAlias(commandName);

            // Output to history to show the command was executed
            AppendToHistory($"> {resolvedCommand}");

            try
            {
                await Task.Run(async () => { await ProcessNewCommand(resolvedCommand); });
            }
            catch (Exception ex)
            {
                AppendToHistory($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies the active input helper about text changes (for preview).
        /// Does NOT complete any async tasks - only updates preview state.
        /// </summary>
        public void NotifyTextChanged(string currentText)
        {
            // Only notify if there's an active command
            if (_activeCommand is CommandBase commandBase)
            {
                commandBase.NotifyInputHelperTextChanged(currentText);
            }
        }

        #endregion

        #region Private Methods - Command Execution

        private async Task ExecuteCommand()
        {
            string input = CommandText.Trim();

            // Priority 1: If there's an active multi-step command, process input for it
            // This handles both empty input (for defaults) and typed input
            if (_activeCommand != null)
            {
                ProcessActiveCommandInput(input);
                return;
            }

            // Priority 2: No active command - check if we should repeat the last command
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
            CommandText = string.Empty;

            try
            {
                await Task.Run(async () => { await ProcessNewCommand(input); });
            }
            catch (Exception ex)
            {
                AppendToHistory($"Error: {ex.Message}");
            }

            FocusRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ProcessActiveCommandInput(string input)
        {
            // For active commands, we pass the input (which may be empty for default acceptance)
            // directly to the command's ProcessInput method. The input helper will handle:
            // - Empty string: Check for and accept default value if available
            // - Non-empty string: Parse as typed value/keyword/point
            
            // Note: We don't append empty input to history (would clutter the display)
            if (!string.IsNullOrEmpty(input))
            {
                AppendToHistory($"> {input}");
            }

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

        private async Task ProcessNewCommand(string input)
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

            _activeCommand = command;
            _activeCommand.PromptChanged += OnCommandPromptChanged;
            _activeCommand.CommandCompletedEvent += OnCommandCompleted;

            await command.Initialize(_commandContext);
            await command.Execute();
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
            // Marshal to UI thread because completion can be signalled from background threads
            var disp = Application.Current?.Dispatcher;
            if (disp != null && !disp.CheckAccess())
            {
                // Post to UI thread and return -- avoid blocking the caller
                disp.BeginInvoke(new Action(CompleteActiveCommandCore), System.Windows.Threading.DispatcherPriority.Normal);
                return;
            }

            // We're on UI thread already
            CompleteActiveCommandCore();
        }

        // Extracted core logic so it can be invoked directly on UI thread
        private void CompleteActiveCommandCore()
        {
            System.Diagnostics.Debug.WriteLine("CommandInputViewModel: CompleteActiveCommandCoreEntered");
            UnsubscribeFromActiveCommand();
            _activeCommand = null;
            _getActiveViewport?.Invoke()?.ClearPreviewObjects();
            _getActiveViewport?.Invoke()?.Refresh();
            UpdatePrompt();
            OnPropertyChanged(nameof(HasActiveCommand));
            ActiveCommandChanged?.Invoke(this, EventArgs.Empty);

            // Request focus back to command input after command completes
            FocusRequested?.Invoke(this, EventArgs.Empty);

            System.Diagnostics.Debug.WriteLine("CommandInputViewModel: Focus requested after command completion");
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
                _activeCommand.CommandCompletedEvent -= OnCommandCompleted;
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

        #region Private Methods - Prompt API for helpers

        /// <summary>
        /// Called by CommandContext (helpers/commands) to set the prompt line.
        /// This method sets the prompt text shown on the input line. It intentionally
        /// does not write anything to history — history handling will be reworked later.
        /// </summary>
        /// <param name="prompt">Complete prompt text (helpers should pass the full display prompt)</param>
        private void SetCommandPrompt(string prompt)
        {
            // Ensure a trailing caret is shown consistently
            AppendToHistory(PromptText);
            PromptText = string.IsNullOrEmpty(prompt) ? "Command >" : $"{prompt} >";
        }

        #endregion

        #region Event Handlers

        private void OnCommandPromptChanged(object? sender, EventArgs e)
        {
            UpdatePrompt();
        }

        private void OnCommandCompleted(object? sender, EventArgs e)
        {
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