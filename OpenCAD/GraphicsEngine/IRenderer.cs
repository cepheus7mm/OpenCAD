using OpenCAD;
using System.Numerics;

namespace GraphicsEngine
{
    /// <summary>
    /// Context for rendering an object with visual state information
    /// </summary>
    public class RenderContext
    {
        public bool IsHighlighted { get; set; }
        public bool IsSelected { get; set; }
        public Matrix4x4 ViewMatrix { get; set; }
        public Matrix4x4 ProjectionMatrix { get; set; }
    }

    public interface IRenderer
    {
        bool CanRender(OpenCADObject obj);
        void Render(OpenCADObject obj, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix);
        void Render(OpenCADObject obj, RenderContext context);
    }
}