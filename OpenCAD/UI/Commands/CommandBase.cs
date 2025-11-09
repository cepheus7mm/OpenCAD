using OpenCAD;
using OpenCAD.Geometry;

namespace UI.Commands
{
    /// <summary>
    /// Base class for all commands
    /// </summary>
    public abstract class CommandBase : IInputCommand
    {
        protected ICommandContext? Context { get; private set; }
        private string _currentPrompt = string.Empty;

        public virtual bool IsMultiStep => false;
        
        /// <summary>
        /// Gets whether this command requires selection mode to be enabled.
        /// Override in derived classes for editing commands like Erase, Move, Copy, etc.
        /// </summary>
        public virtual bool RequiresSelection => false;

        public virtual string CurrentPrompt 
        { 
            get => _currentPrompt;
            protected set 
            { 
                if (_currentPrompt != value)
                {
                    _currentPrompt = value;
                    PromptChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? PromptChanged;
        public event EventHandler? CommandCompleted;

        public virtual void Initialize(ICommandContext context)
        {
            Context = context;
        }

        public abstract void Execute();

        public virtual bool ProcessInput(string input)
        {
            return true; // Single-step commands complete immediately
        }

        public virtual void Cancel()
        {
            Context?.OutputMessage("Command cancelled.");
            CurrentPrompt = string.Empty;
        }

        /// <summary>
        /// Raise the CommandCompleted event
        /// </summary>
        protected void RaiseCommandCompleted()
        {
            CommandCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Helper method to parse a point from input
        /// </summary>
        protected Point3D? ParsePoint(string input)
        {
            string[] parts = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 3)
                return null;

            try
            {
                double x = double.Parse(parts[0]);
                double y = double.Parse(parts[1]);
                double z = double.Parse(parts[2]);
                return new Point3D(x, y, z);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}