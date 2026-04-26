using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.InputHelpers
{
    public sealed class InputTaskController
    {
        private readonly TaskCompletionSource<InputResult> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<InputResult> Task => _tcs.Task;

        public TaskCompletionSource<InputResult> TaskCompletionSource => _tcs;

        public void CompleteWithPoint(Point3D p)
            => _tcs.TrySetResult(InputResult.FromPoint(p));

        public void CompleteWithKeyword(string kw)
            => _tcs.TrySetResult(InputResult.FromKeyword(kw));

        public void CompleteWithDouble(double d)
            => _tcs.TrySetResult(InputResult.FromDouble(d));

        public void CompleteWithCancel()
            => _tcs.TrySetResult(InputResult.Cancel);

        public void AttachCancellation(CancellationToken token)
        {
            token.Register(() => CompleteWithCancel());
        }
    }
}
