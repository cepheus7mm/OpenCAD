using OpenCAD;
using System.Numerics;

namespace GraphicsEngine
{
    /// <summary>
    /// Rendering context containing view/projection matrices and state flags
    /// </summary>
    public class RenderContext
    {
        public Matrix4x4 ViewMatrix { get; set; }
        public Matrix4x4 ProjectionMatrix { get; set; }
        public bool IsHighlighted { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// Interface for rendering OpenCAD objects
    /// </summary>
    public interface IRenderer
    {
        bool CanRender(OpenCADObject obj);
        void Render(OpenCADObject obj, RenderContext context);
    }
}