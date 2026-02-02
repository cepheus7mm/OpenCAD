using OpenCAD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface IPreviewManager
    {
        event Action<OpenCADObject>? PreviewAdded;
        event Action<OpenCADObject>? PreviewRemoved;
        event Action<OpenCADObject>? OriginalHidden;
        event Action<OpenCADObject>? OriginalRestored;

        void Begin();
        void HideOriginal(OpenCADObject original);
        void ShowPreview(OpenCADObject preview);
        void Clear();
        void Commit();
    }
}
