using OpenCAD;
using OpenCAD.Geometry.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("scale", "Scale selected objects", "sc")]
    public class ScaleCommand : EditCommandBase
    {
        public override async Task Initialize(ICommandContext context, CommandArgs? args = null)
        {
            await base.Initialize(context, args);
            _commandName = OpenCADStrings.ScaleCommandName;
        }

        protected override Matrix4D GetTransformation()
        {
            if (!Matrix4D.TryCreateUniformScaleMatrix(BasePoint!, TargetPoint!, out var transformation))
            {
                return Matrix4D.Identity;
            }

            return transformation;
        }
    }
}