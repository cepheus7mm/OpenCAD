using OpenCAD;
using OpenCAD.Geometry.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("move", "Move selected objects", "m")]
    public class MoveCommand : EditCommandBase
    {

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
            _commandName = OpenCADStrings.MoveCommandName;
        }

        protected override async Task OnObjectsSelected()
        {
            await base.OnObjectsSelected();
        }

        public override bool ProcessInput(string input)
        {
            // During selection phase, let EditCommandBase handle it
            if (_currentInputMode == InputMode.ObjectSelection)
            {
                return base.ProcessInput(input);
            }

            // During point picking phase, pass to PointInputHelper
            if (_inputHelper != null)
            {
                return _inputHelper.ProcessKeyboardInput(input);
            }

            return false;
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