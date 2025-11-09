using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using System.Numerics;
using System;

namespace GraphicsEngine
{
    /// <summary>
    /// Shader program wrapper for modern OpenGL with RGBA color support and line stippling
    /// </summary>
    public class ShaderProgram
    {
        private readonly int _programId;
        private readonly Dictionary<string, int> _uniformLocations = new();

        public ShaderProgram()
        {
            // Updated shaders to support quad-based line rendering with perpendicular end caps
            string vertexShaderSource = @"
                #version 330 core
                layout (location = 0) in vec3 aPosition;

                uniform mat4 mvp;
                uniform vec4 color;

                out vec4 fragColor;
                out vec4 screenPos;

                void main()
                {
                    gl_Position = mvp * vec4(aPosition, 1.0);
                    fragColor = color;
                    screenPos = gl_Position;
                }";

            string fragmentShaderSource = @"
                #version 330 core
                in vec4 fragColor;
                in vec4 screenPos;
                out vec4 FragColor;

                uniform int lineTypePattern;  // 0=Continuous, 1=Dashed, 2=Dotted, etc.
                uniform vec2 lineStart;
                uniform vec2 lineEnd;
                uniform vec2 viewport;

                void main()
                {
                    // lineTypePattern values:
                    // 0 = Continuous (no stipple)
                    // 1 = Dashed
                    // 2 = Dotted
                    // 3 = DashDot
                    // 4 = DashDotDot
                    // 5 = Center (long-short)
                    // 6 = Hidden (short dashes)
                    // 7 = Phantom (long-short-short)
                    // 8 = Selected (fine dash for selection)
                    
                    if (lineTypePattern > 0)
                    {
                        // Convert from clip space to screen space
                        vec2 screenCoord = (screenPos.xy / screenPos.w) * 0.5 + 0.5;
                        screenCoord *= viewport;
                        
                        // Calculate the line vector in screen space
                        vec2 lineVec = lineEnd - lineStart;
                        float lineLength = length(lineVec);
                        
                        if (lineLength > 0.0)
                        {
                            vec2 lineDir = lineVec / lineLength;
                            
                            // Project current fragment onto the line
                            vec2 toFrag = screenCoord - lineStart;
                            float distAlongLine = dot(toFrag, lineDir);
                            
                            // Apply pattern based on lineTypePattern
                            bool visible = true;
                            
                            if (lineTypePattern == 1) {
                                // Dashed: 12 on, 6 off
                                float pattern = mod(distAlongLine, 18.0);
                                visible = pattern <= 12.0;
                            }
                            else if (lineTypePattern == 2) {
                                // Dotted: 2 on, 6 off
                                float pattern = mod(distAlongLine, 8.0);
                                visible = pattern <= 2.0;
                            }
                            else if (lineTypePattern == 3) {
                                // DashDot: 12 on, 4 off, 2 on, 4 off
                                float pattern = mod(distAlongLine, 22.0);
                                visible = (pattern <= 12.0) || (pattern >= 16.0 && pattern <= 18.0);
                            }
                            else if (lineTypePattern == 4) {
                                // DashDotDot: 12 on, 4 off, 2 on, 4 off, 2 on, 4 off
                                float pattern = mod(distAlongLine, 30.0);
                                visible = (pattern <= 12.0) || 
                                         (pattern >= 16.0 && pattern <= 18.0) || 
                                         (pattern >= 22.0 && pattern <= 24.0);
                            }
                            else if (lineTypePattern == 5) {
                                // Center: 24 on, 6 off, 6 on, 6 off
                                float pattern = mod(distAlongLine, 42.0);
                                visible = (pattern <= 24.0) || (pattern >= 30.0 && pattern <= 36.0);
                            }
                            else if (lineTypePattern == 6) {
                                // Hidden: 6 on, 6 off (short dashes)
                                float pattern = mod(distAlongLine, 12.0);
                                visible = pattern <= 6.0;
                            }
                            else if (lineTypePattern == 7) {
                                // Phantom: 24 on, 6 off, 6 on, 6 off, 6 on, 6 off
                                float pattern = mod(distAlongLine, 54.0);
                                visible = (pattern <= 24.0) || 
                                         (pattern >= 30.0 && pattern <= 36.0) || 
                                         (pattern >= 42.0 && pattern <= 48.0);
                            }
                            else if (lineTypePattern == 8) {
                                // Selected: 4 on, 2 off (fine dash for selection)
                                float pattern = mod(distAlongLine, 6.0);
                                visible = pattern <= 4.0;
                            }
                            
                            if (!visible) {
                                discard;
                            }
                        }
                    }
                    
                    FragColor = fragColor;
                }";

            int vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
            int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

            _programId = GL.CreateProgram();
            GL.AttachShader(_programId, vertexShader);
            GL.AttachShader(_programId, fragmentShader);
            GL.LinkProgram(_programId);

            GL.GetProgram(_programId, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(_programId);
                throw new Exception($"Shader program linking failed: {infoLog}");
            }

            GL.DetachShader(_programId, vertexShader);
            GL.DetachShader(_programId, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            System.Diagnostics.Debug.WriteLine($"Shader program created successfully (id={_programId})");
            GLDiag.Check("ShaderProgram ctor end");
        }

        private int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception($"{type} compilation failed: {infoLog}");
            }

            return shader;
        }

        public void Use()
        {
            GL.UseProgram(_programId);
        }

        public void SetMatrix4(string name, Matrix4x4 matrix)
        {
            int location = GetUniformLocation(name);

            // Row-major packing (System.Numerics) and let GL transpose for GLSL column-major
            float[] rowMajor =
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            };

            GL.UniformMatrix4(location, 1, true, rowMajor);
        }

        public void SetVector3(string name, Vector3 vector)
        {
            int location = GetUniformLocation(name);
            GL.Uniform3(location, vector.X, vector.Y, vector.Z);
        }

        public void SetVector4(string name, Vector4 vector)
        {
            int location = GetUniformLocation(name);
            GL.Uniform4(location, vector.X, vector.Y, vector.Z, vector.W);
        }

        public void SetInt(string name, int value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform1(location, value);
        }

        public void SetFloat(string name, float value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform1(location, value);
        }

        public void SetVector2(string name, Vector2 vector)
        {
            int location = GetUniformLocation(name);
            GL.Uniform2(location, vector.X, vector.Y);
        }

        private int GetUniformLocation(string name)
        {
            if (!_uniformLocations.ContainsKey(name))
            {
                int location = GL.GetUniformLocation(_programId, name);
                _uniformLocations[name] = location;

                if (location == -1)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Uniform '{name}' not found in shader (program={_programId})");
                }
            }

            return _uniformLocations[name];
        }

        public int ProgramId => _programId;
    }
}