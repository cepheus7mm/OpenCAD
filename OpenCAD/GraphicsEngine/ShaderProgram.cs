using System.Collections.Generic;
using System.Numerics;
using System;
using OpenTK.Graphics.OpenGL;

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
            // Updated shaders to support quad-based line rendering with glow effect
            string vertexShaderSource = @"
                #version 330 core
                layout (location = 0) in vec3 aPosition;

                uniform mat4 mvp;
                uniform vec2 viewport;

                out vec2 fragScreenPos; // Screen-space position of fragment

                void main()
                {
                    vec4 clipPos = mvp * vec4(aPosition, 1.0);
                    gl_Position = clipPos;
                    
                    // Convert to screen space for distance calculations
                    vec3 ndc = clipPos.xyz / clipPos.w;
                    fragScreenPos = (ndc.xy * 0.5 + 0.5) * viewport;
                }";

            string fragmentShaderSource = @"
                #version 330 core

                uniform vec4 color;
                uniform vec2 lineStart;      // Screen space
                uniform vec2 lineEnd;        // Screen space
                uniform vec2 viewport;
                uniform int lineTypePattern;
                uniform float lineWidth;     // Actual line width in pixels
                uniform float glowRadius;    // Glow halo size in pixels (0 = no glow)

                in vec2 fragScreenPos;
                out vec4 FragColor;

                // Distance from point to line segment
                float distanceToSegment(vec2 p, vec2 a, vec2 b) {
                    vec2 pa = p - a;
                    vec2 ba = b - a;
                    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
                    return length(pa - ba * h);
                }

                void main() {
                    // Calculate distance from fragment to line
                    float dist = distanceToSegment(fragScreenPos, lineStart, lineEnd);
                    
                    // Line stippling logic
                    vec2 lineVec = lineEnd - lineStart;
                    float lineLength = length(lineVec);
                    
                    if (lineLength > 0.1) {
                        vec2 lineDir = lineVec / lineLength;
                        vec2 fragVec = fragScreenPos - lineStart;
                        float alongLine = dot(fragVec, lineDir);
                        
                        // Apply line type patterns
                        if (lineTypePattern == 1) { // Dashed
                            float dashLength = 10.0;
                            float gapLength = 5.0;
                            float cycle = dashLength + gapLength;
                            float pos = mod(alongLine, cycle);
                            if (pos > dashLength) discard;
                        }
                        else if (lineTypePattern == 2) { // Dotted
                            float dotLength = 2.0;
                            float gapLength = 4.0;
                            float cycle = dotLength + gapLength;
                            float pos = mod(alongLine, cycle);
                            if (pos > dotLength) discard;
                        }
                        else if (lineTypePattern == 3) { // DashDot
                            float dashLength = 10.0;
                            float dotLength = 2.0;
                            float gapLength = 4.0;
                            float cycle = dashLength + gapLength + dotLength + gapLength;
                            float pos = mod(alongLine, cycle);
                            if ((pos > dashLength && pos < dashLength + gapLength) || 
                                (pos > dashLength + gapLength + dotLength)) discard;
                        }
                        else if (lineTypePattern == 4) { // DashDotDot
                            float dashLength = 10.0;
                            float dotLength = 2.0;
                            float gapLength = 4.0;
                            float cycle = dashLength + gapLength + dotLength + gapLength + dotLength + gapLength;
                            float pos = mod(alongLine, cycle);
                            if ((pos > dashLength && pos < dashLength + gapLength) || 
                                (pos > dashLength + gapLength + dotLength && pos < dashLength + 2.0 * gapLength + dotLength) ||
                                (pos > dashLength + 2.0 * gapLength + 2.0 * dotLength)) discard;
                        }
                        else if (lineTypePattern == 5) { // Center
                            float longDash = 20.0;
                            float shortDash = 5.0;
                            float gapLength = 4.0;
                            float cycle = longDash + gapLength + shortDash + gapLength;
                            float pos = mod(alongLine, cycle);
                            if ((pos > longDash && pos < longDash + gapLength) ||
                                (pos > longDash + gapLength + shortDash)) discard;
                        }
                        else if (lineTypePattern == 6) { // Hidden
                            float dashLength = 5.0;
                            float gapLength = 3.0;
                            float cycle = dashLength + gapLength;
                            float pos = mod(alongLine, cycle);
                            if (pos > dashLength) discard;
                        }
                        else if (lineTypePattern == 7) { // Phantom
                            float longDash = 20.0;
                            float shortDash = 5.0;
                            float gapLength = 4.0;
                            float cycle = longDash + gapLength + shortDash + gapLength + shortDash + gapLength;
                            float pos = mod(alongLine, cycle);
                            if ((pos > longDash && pos < longDash + gapLength) ||
                                (pos > longDash + gapLength + shortDash && pos < longDash + 2.0 * gapLength + shortDash) ||
                                (pos > longDash + 2.0 * gapLength + 2.0 * shortDash)) discard;
                        }
                        else if (lineTypePattern == 8) { // Fine dashed (for selection)
                            float dashLength = 6.0;
                            float gapLength = 3.0;
                            float cycle = dashLength + gapLength;
                            float pos = mod(alongLine, cycle);
                            if (pos > dashLength) discard;
                        }
                    }
                    
                    // Distance-based rendering with optional glow
                    if (glowRadius > 0.0) {
                        float halfWidth = lineWidth * 0.5;
                        
                        if (dist <= halfWidth) {
                            // Inside core line: full opacity
                            FragColor = vec4(color.rgb, color.a);
                        } else if (dist <= halfWidth + glowRadius) {
                            // Inside glow halo: smooth falloff
                            float glowFactor = 1.0 - smoothstep(halfWidth, halfWidth + glowRadius, dist);
                            float glowAlpha = glowFactor * 0.45; // 0.45 = glow intensity
                            FragColor = vec4(color.rgb, color.a * glowAlpha);
                        } else {
                            // Outside glow: discard
                            discard;
                        }
                    } else {
                        // Standard rendering: discard fragments outside line width
                        if (dist > lineWidth * 0.5) discard;
                        FragColor = color;
                    }
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

            //System.Diagnostics.Debug.WriteLine($"Shader program created successfully (id={_programId})");
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
                    //System.Diagnostics.Debug.WriteLine($"Warning: Uniform '{name}' not found in shader (program={_programId})");
                }
            }

            return _uniformLocations[name];
        }

        public int ProgramId => _programId;
    }
}