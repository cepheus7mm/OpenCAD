using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsEngine
{
    public static class QuadShader
    {
        public const string VertexShader = @"
#version 330 core

layout(location = 0) in vec2 inPosition; // pixel coords
layout(location = 1) in vec4 inColor;

out vec4 vColor;

uniform vec2 uViewportSize;

void main()
{
    // Convert from pixel coords to NDC
    vec2 ndc = vec2(
        (inPosition.x / uViewportSize.x) * 2.0 - 1.0,
        1.0 - (inPosition.y / uViewportSize.y) * 2.0
    );

    vColor = inColor;
    gl_Position = vec4(ndc, 0.0, 1.0);
}
";

        public const string FragmentShader = @"
#version 330 core

in vec4 vColor;
out vec4 FragColor;

void main()
{
    FragColor = vColor;
}
";
    }
}
