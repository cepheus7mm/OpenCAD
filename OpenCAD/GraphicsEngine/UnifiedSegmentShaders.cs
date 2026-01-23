using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsEngine
{
    public static class UnifiedSegmentShaders
    {
        public const string VertexShader = @"
#version 330 core

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in float inDistance;

out vec4 vColor;
out float vDistance;

void main()
{
    vColor = inColor;
    vDistance = inDistance;
    gl_Position = vec4(inPosition, 0.0, 1.0);
}
";

        public const string FragmentShader = @"
#version 330 core

in vec4 vColor;
in float vDistance;

out vec4 FragColor;

layout(std140) uniform LinetypeUBO
{
    int   uPatternCount;
    float uPatternLength;
    vec4  uPattern[64];
};

bool sampleLinetype(float dist)
{
    float pos = mod(dist, uPatternLength);
    float acc = 0.0;

    for (int i = 0; i < uPatternCount; i++)
    {
        int vecIndex = i / 4;
        int component = i % 4;
        float value = uPattern[vecIndex][component];
        float seg = abs(value);

        if (pos < acc + seg)
            return value > 0.0;

        acc += seg;
    }

    return true;
}

void main()
{
    if (!sampleLinetype(vDistance))
        discard;

    FragColor = vColor;
}
";
    }
}
