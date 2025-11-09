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
        /// Gets whether this is a multi-step command that requires multiple inputs
        /// </summary>
        bool IsMultiStep { get; }
        
        /// <summary>
        /// Gets whether this command requires selection mode to be enabled
        /// </summary>
        bool RequiresSelection { get; }
        
        /// <summary>
        /// Gets the current prompt for multi-step commands
        /// </summary>
        string CurrentPrompt { get; }
        
        /// <summary>
        /// Event raised when the prompt changes
        /// </summary>
        event EventHandler? PromptChanged;
        
        /// <summary>
        /// Event raised when the command completes
        /// </summary>
        event EventHandler? CommandCompleted;
        
        /// <summary>
        /// Initialize the command with context
        /// </summary>
        void Initialize(ICommandContext context);
        
        /// <summary>
        /// Execute the command
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
    }
}