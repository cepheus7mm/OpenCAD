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

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
            _commandName = OpenCADStrings.RotateCommandName;
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
            if (!Matrix4D.TryCreateRotationMatrix(BasePoint!, TargetPoint!, out var transformation))
            {
                return Matrix4D.Identity;
            }

            return transformation;
        }
    }
}