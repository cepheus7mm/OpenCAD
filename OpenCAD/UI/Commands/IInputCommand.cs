using OpenCAD;
using OpenCAD.Geometry;

namespace UI.Commands
{
    /// <summary>
    /// Interface for all input commands
    /// </summary>
    public interface IInputCommand
    {
        /// <summary>
        /// Initialize the command with context
        /// </summary>
        void Initialize(ICommandContext context);

        /// <summary>
        /// Start executing the command
        /// </summary>
        void Execute();

        /// <summary>
        /// Process input for multi-step commands
        /// </summary>
        /// <param name="input">User input</param>
        /// <returns>True if command is complete, false if waiting for more input</returns>
        bool ProcessInput(string input);

        /// <summary>
        /// Cancel the command
        /// </summary>
        void Cancel();

        /// <summary>
        /// Check if this command requires multiple steps
        /// </summary>
        bool IsMultiStep { get; }

        /// <summary>
        /// Gets the current prompt text for the user
        /// </summary>
        string CurrentPrompt { get; }

        /// <summary>
        /// Event raised when the prompt changes
        /// </summary>
        event EventHandler? PromptChanged;

        /// <summary>
        /// Event raised when the command completes (especially via non-keyboard input like mouse)
        /// </summary>
        event EventHandler? CommandCompleted;
    }
}