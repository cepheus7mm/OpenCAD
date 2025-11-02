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
        private readonly Dictionary<string, string> _aliasToCanonical = new(); // Maps aliases to canonical names

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
                
                string canonicalName = attribute.Name.ToLower();
                
                // Register primary name
                _commands[canonicalName] = type;
                _commandInfo[canonicalName] = (attribute.Description, attribute.Aliases);
                _aliasToCanonical[canonicalName] = canonicalName; // Map canonical to itself

                // Register aliases
                foreach (var alias in attribute.Aliases)
                {
                    string lowerAlias = alias.ToLower();
                    _commands[lowerAlias] = type;
                    _aliasToCanonical[lowerAlias] = canonicalName; // Map alias to canonical
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
        /// Get the canonical (primary) command name from an alias or the name itself
        /// </summary>
        public string? GetCanonicalName(string commandName)
        {
            _aliasToCanonical.TryGetValue(commandName.ToLower(), out var canonicalName);
            return canonicalName;
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