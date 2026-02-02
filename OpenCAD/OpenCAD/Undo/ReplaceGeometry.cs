using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Undo
{
    using System;
    using OpenCAD;

    namespace OpenCAD.Undo
    {
        /// <summary>
        /// Undoable action for replacing one geometry object with another.
        /// Works with immutable geometry: original is removed, replacement is added.
        /// </summary>
        public class ReplaceGeometryAction : IUndoableAction
        {
            private readonly OpenCADObject _original;
            private readonly OpenCADObject _replacement;

            public string Description { get; }

            public ReplaceGeometryAction(OpenCADObject original, OpenCADObject replacement, string description)
            {
                _original = original ?? throw new ArgumentNullException(nameof(original));
                _replacement = replacement ?? throw new ArgumentNullException(nameof(replacement));
                Description = description;
            }

            public void Execute(OpenCADDocument document)
            {
                // Remove original, add replacement
                document.Remove(_original);
                document.Add(_replacement);
                document.MarkAsModified();
            }

            public void Undo(OpenCADDocument document)
            {
                // Remove replacement, restore original
                document.Remove(_replacement);
                document.Add(_original);
                document.MarkAsModified();
            }
        }
    }
}
