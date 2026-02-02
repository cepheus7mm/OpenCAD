using OpenCAD;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD
{
    public sealed class PreviewManager : IPreviewManager
    {
        private readonly HashSet<OpenCADObject> _hiddenOriginals = new();
        private readonly HashSet<OpenCADObject> _previewObjects = new();

        public event Action<OpenCADObject>? PreviewAdded;
        public event Action<OpenCADObject>? PreviewRemoved;
        public event Action<OpenCADObject>? OriginalHidden;
        public event Action<OpenCADObject>? OriginalRestored;

        public void Begin()
        {
            Clear();
        }

        public void HideOriginal(OpenCADObject original)
        {
            if (_hiddenOriginals.Add(original))
                OriginalHidden?.Invoke(original);
        }

        public void ShowPreview(OpenCADObject preview)
        {
            if (_previewObjects.Add(preview))
                PreviewAdded?.Invoke(preview);
        }

        public void Clear()
        {
            foreach (var original in _hiddenOriginals)
                OriginalRestored?.Invoke(original);

            foreach (var preview in _previewObjects)
                PreviewRemoved?.Invoke(preview);

            _hiddenOriginals.Clear();
            _previewObjects.Clear();
        }

        public void Commit()
        {
            foreach (var original in _hiddenOriginals)
                OriginalHidden?.Invoke(original);

            _hiddenOriginals.Clear();
            _previewObjects.Clear();
        }
    }
}
