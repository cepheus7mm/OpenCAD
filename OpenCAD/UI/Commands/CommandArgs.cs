using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands
{
    public class CommandArgs
    {
        public string? EntryPoint { get; set; }
        public Dictionary<string, object> Parameters { get; } = new();
    }
}
