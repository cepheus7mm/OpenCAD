using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using OpenTK.Graphics.OpenGL;

namespace GraphicsEngine
{
    public sealed class Shader
    {
        public int Handle { get; }

        public Shader(string vertexSource, string fragmentSource)
        {
            int vertex = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertex, vertexSource);
            GL.CompileShader(vertex);
            CheckShaderCompile(vertex, "VERTEX");

            int fragment = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragment, fragmentSource);
            GL.CompileShader(fragment);

            GL.GetShader(fragment, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(fragment);
                System.Diagnostics.Debug.WriteLine($"❌ Fragment Shader Compilation Error:\n{infoLog}");
                throw new Exception($"Fragment shader compilation failed: {infoLog}");
            }

            Handle = GL.CreateProgram();
            GL.AttachShader(Handle, vertex);
            GL.AttachShader(Handle, fragment);
            GL.LinkProgram(Handle);
            CheckProgramLink(Handle);

            int blockIndex = GL.GetUniformBlockIndex(Handle, "LinetypeUBO");
            if (blockIndex >= 0) // block exists in this shader
            {
                GL.UniformBlockBinding(Handle, blockIndex, 3);
            }

            //
            // Cleanup
            //
            GL.DetachShader(Handle, vertex);
            GL.DetachShader(Handle, fragment);
            GL.DeleteShader(vertex);
            GL.DeleteShader(fragment);
        }

        public void Use()
        {
            GL.UseProgram(Handle);
        }

        static void CheckShaderCompile(int shader, string label)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
            if (status == 0)
            {
                string log = GL.GetShaderInfoLog(shader);
                throw new Exception($"{label} shader compile error:\n{log}");
            }
        }

        static void CheckProgramLink(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
            if (status == 0)
            {
                string log = GL.GetProgramInfoLog(program);
                throw new Exception($"Program link error:\n{log}");
            }
        }

        // Uniform helpers
        public void SetFloat(string name, float value)
        {
            int loc = GL.GetUniformLocation(Handle, name);
            GL.Uniform1(loc, value);
        }

        public void SetInt(string name, int value)
        {
            int loc = GL.GetUniformLocation(Handle, name);
            GL.Uniform1(loc, value);
        }

        public void SetVector2(string name, Vector2 value)
        {
            int loc = GL.GetUniformLocation(Handle, name);
            GL.Uniform2(loc, value.X, value.Y);
        }
        public void SetVector4(string name, Vector4 value)
        {
            int loc = GL.GetUniformLocation(Handle, name);
            GL.Uniform4(loc, value.X, value.Y, value.Z, value.W);
        }

        public void SetFloatArray(string name, float[] values)
        {
            int loc = GL.GetUniformLocation(Handle, name);
            GL.Uniform1(loc, values.Length, values);
        }
        public void SetMatrix4(string name, Matrix4x4 matrix)
        {
            int loc = GL.GetUniformLocation(Handle, name);
            if (loc == -1)
                return;

            // Convert System.Numerics.Matrix4x4 (row-major) to float[16]
            // OpenGL expects column-major by default, so we set transpose = true.
            float[] rowMajor =
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            };

            GL.UniformMatrix4(loc, 1, true, rowMajor);
        }

    }
}
