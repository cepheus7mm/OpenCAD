using System;
using System.Collections.Generic;

namespace OpenCAD.Undo
{
    public sealed class TrimOperation
    {
        public OpenCADObject OriginalObject { get; }
        public IReadOnlyList<OpenCADObject> ResultObjects { get; }

        public TrimOperation(OpenCADObject originalObject, IEnumerable<OpenCADObject> resultObjects)
        {
            OriginalObject = originalObject ?? throw new ArgumentNullException(nameof(originalObject));
            ResultObjects = new List<OpenCADObject>(resultObjects ?? throw new ArgumentNullException(nameof(resultObjects)));
        }
    }
}