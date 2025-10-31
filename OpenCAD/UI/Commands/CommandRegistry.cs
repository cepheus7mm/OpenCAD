using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UI.Commands
{
    /// <summary>
    /// Registry for discovering and managing input commands
    /// </summary>
    public class CommandRegistry
    {
        private readonly Dictionary<string, Type> _commands = new();
        private readonly Dictionary<string, (string description, string[] aliases)> _commandInfo = new();

        /// <summary>
        /// Discover and register all commands with InputCommandAttribute
        /// </summary>
        public void DiscoverCommands()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var commandTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IInputCommand).IsAssignableFrom(t))
                .Where(t => t.GetCustomAttribute<InputCommandAttribute>() != null);

            foreach (var type in commandTypes)
            {
                var attribute = type.GetCustomAttribute<InputCommandAttribute>()!;
                
                // Register primary name
                _commands[attribute.Name.ToLower()] = type;
                _commandInfo[attribute.Name.ToLower()] = (attribute.Description, attribute.Aliases);

                // Register aliases
                foreach (var alias in attribute.Aliases)
                {
                    _commands[alias.ToLower()] = type;
                }
            }
        }

        /// <summary>
        /// Get command type by name
        /// </summary>
        public Type? GetCommandType(string commandName)
        {
            _commands.TryGetValue(commandName.ToLower(), out var type);
            return type;
        }

        /// <summary>
        /// Get all registered command names
        /// </summary>
        public IEnumerable<string> GetCommandNames()
        {
            return _commands.Keys;
        }

        /// <summary>
        /// Get command info for help display
        /// </summary>
        public Dictionary<string, (string description, string[] aliases)> GetCommandInfo()
        {
            return new Dictionary<string, (string, string[])>(_commandInfo);
        }

        /// <summary>
        /// Check if a command exists
        /// </summary>
        public bool HasCommand(string commandName)
        {
            return _commands.ContainsKey(commandName.ToLower());
        }
    }
}