using OpenCAD;
using OpenCAD.Geometry.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("scale", "Scale selected objects", "sc")]
    public class ScaleCommand : EditCommandBase
    {
        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
            _commandName = OpenCADStrings.ScaleCommandName;
        }

        protected override async Task OnObjectsSelected()
        {
            await base.OnObjectsSelected();
        }

        public override bool ProcessInput(string input)
        {
            if (SelectedObjects == null)
                return base.ProcessInput(input);

            if (_inputHelper != null)
                return _inputHelper.ProcessKeyboardInput(input);

            return false;
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