namespace UI.Commands.InputHelpers
{
    /// <summary>
    /// Contract for input helpers that can receive keyboard input.
    /// </summary>
    public interface IInputHelper
    {
        /// <summary>
        /// Process keyboard input routed to the helper.
        /// Return value is not used to indicate completion in the current flow;
        /// keep returning false to preserve existing CommandBase semantics.
        /// </summary>
        bool ProcessKeyboardInput(string input);
    }
}