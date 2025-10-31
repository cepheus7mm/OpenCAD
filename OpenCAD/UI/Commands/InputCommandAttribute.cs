using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands
{
    /// <summary>
    /// Attribute to mark classes as input commands and specify their command names
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class InputCommandAttribute : Attribute
    {
        /// <summary>
        /// Primary command name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Alternative command names (shortcuts)
        /// </summary>
        public string[] Aliases { get; }

        /// <summary>
        /// Command description for help text
        /// </summary>
        public string Description { get; }

        public InputCommandAttribute(string name, string description, params string[] aliases)
        {
            Name = name;
            Description = description;
            Aliases = aliases ?? Array.Empty<string>();
        }
    }

}
