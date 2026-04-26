using OpenCAD;
using OpenCAD.Geometry.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("move", "Move selected objects", "m")]
    public class MoveCommand : EditCommandBase
    {

        public override async Task Initialize(ICommandContext context, CommandArgs? args = null)
        {
            await base.Initialize(context, args);
            _commandName = OpenCADStrings.MoveCommandName;
        }

        protected override Matrix4D GetTransformation()
        {
            if (!Matrix4D.TryCreateTranslation(BasePoint!, TargetPoint!, out Matrix4D translation))
            {
                return Matrix4D.Identity;
            }
            return translation;
        }
    }
}