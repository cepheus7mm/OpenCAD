using System.ComponentModel;
using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("rotate", "Rotate selected objects", "ro")]
    public class RotateCommand : EditCommandBase
    {

        public override void Initialize(ICommandContext context)
        {
            base.Initialize(context);
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

            if (_pointInputHelper != null)
                return _pointInputHelper.ProcessKeyboardInput(input);

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