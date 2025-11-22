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
    [InputCommand("copy", "Copy selected objects", "cp")]
    public class CopyCommand : EditCommandBase
    {
        public override void Initialize(ICommandContext context)
        {
            base.Initialize(context);
            _commandName = OpenCADStrings.CopyCommandName;
        }

        protected override async Task OnObjectsSelected()
        {
            _preserveOriginal = true;
            _isRepeatable = true;
            await base.OnObjectsSelected();
        }

        public override bool ProcessInput(string input)
        {
            // During selection phase, let EditCommandBase handle it
            if (SelectedObjects == null)
            {
                return base.ProcessInput(input);
            }

            // During point picking phase, pass to PointInputHelper
            if (_pointInputHelper != null)
            {
                return _pointInputHelper.ProcessKeyboardInput(input);
            }

            return false;
        }

        protected override Matrix4D GetTransformation()
        {
            if (!Matrix4D.TryCreateTranslation(_basePoint!, _targetPoint!, out Matrix4D translation))
            {
                return Matrix4D.Identity;
            }
            return translation;
        }
    }
}