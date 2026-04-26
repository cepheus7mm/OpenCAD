using System.ComponentModel;
using OpenCAD;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenCAD.Undo;
using UI.Controls.Viewport;
using OpenCAD.Geometry.Helpers;
using UI.Commands.Interfaces;

namespace UI.Commands.Editing
{
    [InputCommand("rotate", "Rotate selected objects", "ro")]
    public class RotateCommand : EditCommandBase
    {

        public override async Task Initialize(ICommandContext context, CommandArgs? args = null)
        {
            await base.Initialize(context, args);
            _commandName = OpenCADStrings.RotateCommandName;
        }

        protected override Matrix4D GetTransformation()
        {
            if (!Matrix4D.TryCreateRotationMatrix(BasePoint!, TargetPoint!, out var transformation))
            {
                return Matrix4D.Identity;
            }

            return transformation;
        }
    }
}