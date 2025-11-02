using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace GraphicsEngine
{
    internal static class GLDiag
    {
        public static void Check(string where)
        {
            var err = GL.GetError();
            if (err != ErrorCode.NoError)
            {
                System.Diagnostics.Debug.WriteLine($"GL ERROR at {where}: {err}");
            }
        }

        public static void LogContextInfo()
        {
            try
            {
                string vendor = GL.GetString(StringName.Vendor);
                string renderer = GL.GetString(StringName.Renderer);
                string version = GL.GetString(StringName.Version);
                string glsl = GL.GetString(StringName.ShadingLanguageVersion);
                System.Diagnostics.Debug.WriteLine($"OpenGL Context -> Vendor: {vendor}, Renderer: {renderer}, Version: {version}, GLSL: {glsl}");

                float[] range = new float[2];
                GL.GetFloat(GetPName.LineWidthRange, range);
                System.Diagnostics.Debug.WriteLine($"Line width range: {range[0]} .. {range[1]}");
            }
            catch { /* ignore if context not ready */ }
        }

        public static void TryEnableDebugOutput()
        {
            try
            {
                // Enable KHR_debug if available
                GL.Enable(EnableCap.DebugOutput);
                GL.Enable(EnableCap.DebugOutputSynchronous);
                GL.DebugMessageCallback(DebugCallback, IntPtr.Zero);
                System.Diagnostics.Debug.WriteLine("KHR_debug enabled.");
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("KHR_debug not available on this context.");
            }
        }

        private static void DebugCallback(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            var msg = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message, length);
            System.Diagnostics.Debug.WriteLine($"[GL DEBUG] {severity} {type} ({id}): {msg}");
        }

        public static void DumpPipelineState(int vao, int vbo, int programId)
        {
            try
            {
                GL.GetInteger(GetPName.CurrentProgram, out int curProg);
                GL.GetInteger(GetPName.VertexArrayBinding, out int curVao);
                GL.GetInteger(GetPName.ArrayBufferBinding, out int curArrayBuf);

                GL.GetVertexAttrib(0, VertexAttribParameter.ArrayEnabled, out int attr0Enabled);
                GL.GetVertexAttrib(0, VertexAttribParameter.ArraySize, out int attr0Size);
                GL.GetVertexAttrib(0, VertexAttribParameter.ArrayType, out int attr0Type);
                GL.GetVertexAttrib(0, VertexAttribParameter.ArrayStride, out int attr0Stride);

                Debug.WriteLine($"GL State -> Program: {curProg} (expected {programId}), VAO: {curVao} (expected {vao}), ARRAY_BUFFER: {curArrayBuf} (last upload {vbo})");
                Debug.WriteLine($"Attrib[0]: enabled={attr0Enabled!=0}, size={attr0Size}, type={(VertexAttribPointerType)attr0Type}, stride={attr0Stride}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DumpPipelineState failed: {ex.Message}");
            }
        }
    }
}