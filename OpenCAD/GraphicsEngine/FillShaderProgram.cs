using OpenTK.Graphics.OpenGL;
using System;
using System.Numerics;

namespace GraphicsEngine
{
    public class FillShaderProgram
    {
        private readonly int _programId;
        private readonly int _mvpLoc;
        private readonly int _colorLoc;

        public FillShaderProgram()
        {
            string vs = @"
                #version 330 core
                layout (location = 0) in vec3 aPosition;
                uniform mat4 mvp;
                void main()
                {
                    gl_Position = mvp * vec4(aPosition, 1.0);
                }";
            string fs = @"
                #version 330 core
                uniform vec4 color;
                out vec4 FragColor;
                void main()
                {
                    FragColor = color;
                }";

            int v = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(v, vs);
            GL.CompileShader(v);
            GL.GetShader(v, ShaderParameter.CompileStatus, out int okV);
            if (okV == 0) throw new Exception(GL.GetShaderInfoLog(v));

            int f = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(f, fs);
            GL.CompileShader(f);
            GL.GetShader(f, ShaderParameter.CompileStatus, out int okF);
            if (okF == 0) throw new Exception(GL.GetShaderInfoLog(f));

            _programId = GL.CreateProgram();
            GL.AttachShader(_programId, v);
            GL.AttachShader(_programId, f);
            GL.LinkProgram(_programId);
            GL.GetProgram(_programId, GetProgramParameterName.LinkStatus, out int linked);
            if (linked == 0) throw new Exception(GL.GetProgramInfoLog(_programId));

            GL.DetachShader(_programId, v);
            GL.DetachShader(_programId, f);
            GL.DeleteShader(v);
            GL.DeleteShader(f);

            _mvpLoc = GL.GetUniformLocation(_programId, "mvp");
            _colorLoc = GL.GetUniformLocation(_programId, "color");
        }

        public void Use() => GL.UseProgram(_programId);

        public void SetMvp(Matrix4x4 mvp)
        {
            float[] m =
            {
                mvp.M11, mvp.M12, mvp.M13, mvp.M14,
                mvp.M21, mvp.M22, mvp.M23, mvp.M24,
                mvp.M31, mvp.M32, mvp.M33, mvp.M34,
                mvp.M41, mvp.M42, mvp.M43, mvp.M44
            };
            GL.UniformMatrix4(_mvpLoc, 1, true, m);
        }

        public void SetColor(Vector4 rgba) => GL.Uniform4(_colorLoc, rgba.X, rgba.Y, rgba.Z, rgba.W);

        public int ProgramId => _programId;
    }
}