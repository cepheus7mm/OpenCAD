using System;
using System.Numerics;
using OpenTK.Graphics.OpenGL;

namespace GraphicsEngine
{
    public class FillShaderProgram
    {
        private readonly int _programId;
        private readonly int _mvpLoc;
        private readonly int _colorLoc;
        private readonly int _isSelectedLoc;

        public FillShaderProgram()
        {
            string vs = @"
                #version 330 core
                layout (location = 0) in vec3 aPosition;
                uniform mat4 mvp;
                
                out vec2 fragWorldPos; // Pass world position to fragment shader
                
                void main()
                {
                    fragWorldPos = aPosition.xy; // Store world coordinates before transformation
                    gl_Position = mvp * vec4(aPosition, 1.0);
                }";
                
            string fs = @"
                #version 330 core
                uniform vec4 color;
                uniform int isSelected; // 0 = normal, 1 = selected (stippled)
                
                in vec2 fragWorldPos;
                out vec4 FragColor;
                
                void main()
                {
                    if (isSelected == 1) {
                        // Apply checkerboard stipple pattern for selected text
                        // Use screen-space coordinates for consistent pixel-level stippling
                        vec2 screenPos = gl_FragCoord.xy;
                        
                        // Create a 4x4 checkerboard pattern
                        // This removes every 4th pixel in a checkerboard arrangement
                        float gridX = mod(floor(screenPos.x / 2.0), 2.0);
                        float gridY = mod(floor(screenPos.y / 2.0), 2.0);
                        
                        // Discard pixels in a checkerboard pattern (every 4th pixel overall)
                        // When gridX and gridY are both 0 or both 1, discard the pixel
                        if (gridX == gridY) {
                            discard;
                        }
                    }
                    
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
            _isSelectedLoc = GL.GetUniformLocation(_programId, "isSelected");
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
        
        public void SetIsSelected(bool isSelected) => GL.Uniform1(_isSelectedLoc, isSelected ? 1 : 0);

        public int ProgramId => _programId;
    }
}