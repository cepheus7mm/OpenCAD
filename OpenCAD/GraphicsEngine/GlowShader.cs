using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsEngine
{
    public static class GlowShader
    {
        public const string VertexShader = @"
#version 330 core

layout(location = 0) in vec2 aPosition;

uniform vec4 uGlowColor;

void main()
{
    gl_Position = vec4(aPosition, 0.0, 1.0);
}

";
        public const string FragmentShader = @"
#version 330 core

uniform vec4 uGlowColor;

out vec4 FragColor;

void main()
{
    FragColor = uGlowColor;
}
";
    }
}
