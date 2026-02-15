using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Editing.Fillet
{
    public enum FilletStep
    {
        PromptRadius,
        SelectFirstObject,
        HoverSecondObject,
        SelectSecondObject,
        Commit
    }
}
