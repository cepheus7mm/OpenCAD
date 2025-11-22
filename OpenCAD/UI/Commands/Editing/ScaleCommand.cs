using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("scale", "Scale selected objects", "sc")]
    public class ScaleCommand : EditCommandBase
    {
        public override void Initialize(ICommandContext context)
        {
            base.Initialize(context);
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

            if (_pointInputHelper != null)
                return _pointInputHelper.ProcessKeyboardInput(input);

            return false;
        }

        protected override Matrix4D GetTransformation()
        {
            if (!Matrix4D.TryCreateUniformScaleMatrix(_basePoint!, _targetPoint!, out var transformation))
            {
                return Matrix4D.Identity;
            }

            return transformation;
        }
    }
}