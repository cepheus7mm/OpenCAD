using System;
using System.Collections.Generic;
using System.Numerics;
using OpenTK.Graphics.OpenGL;

namespace GraphicsEngine
{
    /// <summary>
    /// Dedicated shader program wrapper for <see cref="LineRenderer"/>.
    /// Produces screen-space thick lines by expanding a segment into a quad in the vertex shader.
    /// </summary>
    public sealed class LineShaderProgram
    {
        private readonly int _programId;
        private readonly Dictionary<string, int> _uniformLocations = new();

        public LineShaderProgram()
        {
            const string vertexShaderSource = @"
#version 330 core

layout (location = 0) in vec3 aStart;     // world
layout (location = 1) in vec3 aEnd;       // world
layout (location = 2) in float aDistance; // 0 (start) or totalLen (end) - world units
layout (location = 3) in float aSide;     // -1 or +1

uniform mat4 mvp;
uniform vec2 viewport;     // pixels
uniform float lineWidth;   // core width in pixels
uniform float glowRadius;  // pixels

out vec2 fragScreenPos;      // pixels
out float vDistance;         // world distance used for stipple
out vec2 vLineStartScreen;   // pixels
out vec2 vLineEndScreen;     // pixels

vec2 ClipToScreen(vec4 clip)
{
    vec3 ndc = clip.xyz / clip.w;
    return (ndc.xy * 0.5 + 0.5) * viewport;
}

void main()
{
    vec4 clipStart = mvp * vec4(aStart, 1.0);
    vec4 clipEnd   = mvp * vec4(aEnd,   1.0);

    bool isStart = (aDistance <= 0.000001);
    vec4 baseClip = isStart ? clipStart : clipEnd;

    vec2 sStart = ClipToScreen(clipStart);
    vec2 sEnd   = ClipToScreen(clipEnd);

    vec2 dir = sEnd - sStart;
    float len = length(dir);
    vec2 n = (len > 0.0001) ? vec2(-dir.y, dir.x) / len : vec2(0.0, 1.0);

    float renderWidth = lineWidth + 2.0 * glowRadius;
    float halfWidth = 0.5 * renderWidth;

    vec2 offsetPixels = n * (aSide * halfWidth);

    // Expand in clip space (same technique as PolylineShaderProgram)
    vec2 ndcOffset = (offsetPixels / viewport) * 2.0;
    vec4 clipOffset = vec4(ndcOffset * baseClip.w, 0.0, 0.0);

    gl_Position = baseClip + clipOffset;

    vec2 baseScreen = isStart ? sStart : sEnd;
    fragScreenPos = baseScreen + offsetPixels;

    vDistance = aDistance;
    vLineStartScreen = sStart;
    vLineEndScreen = sEnd;
}";

            const string fragmentShaderSource = @"
#version 330 core

uniform vec4 color;
uniform int lineTypePattern;
uniform float lineWidth;     // core width (pixels)
uniform float glowRadius;    // pixels
uniform float lineTypeScale; // world-unit pattern scale

in vec2 fragScreenPos;
in float vDistance;
in vec2 vLineStartScreen;
in vec2 vLineEndScreen;

out vec4 FragColor;

float distanceToSegment(vec2 p, vec2 a, vec2 b)
{
    vec2 pa = p - a;
    vec2 ba = b - a;
    float denom = dot(ba, ba);
    if (denom <= 0.000001) return length(pa);
    float h = clamp(dot(pa, ba) / denom, 0.0, 1.0);
    return length(pa - ba * h);
}

void main()
{
    float dist = distanceToSegment(fragScreenPos, vLineStartScreen, vLineEndScreen);

    float alongLine = vDistance;
    bool hasAlong = alongLine >= 0.0;

    if (hasAlong && lineTypePattern != 0)
    {
        if (lineTypePattern == 1) { // Dashed
            float dashLength = 0.25 * lineTypeScale;
            float gapLength  = 0.125 * lineTypeScale;
            float cycle = dashLength + gapLength;
            float pos = mod(alongLine, cycle);
            if (pos > dashLength) discard;
        }
        else if (lineTypePattern == 2) { // Dotted
            float dotLength = 0.05 * lineTypeScale;
            float gapLength = 0.10 * lineTypeScale;
            float cycle = dotLength + gapLength;
            float pos = mod(alongLine, cycle);
            if (pos > dotLength) discard;
        }
        else if (lineTypePattern == 3) { // DashDot
            float dashLength = 0.25 * lineTypeScale;
            float dotLength  = 0.05 * lineTypeScale;
            float gapLength  = 0.10 * lineTypeScale;
            float cycle = dashLength + gapLength + dotLength + gapLength;
            float pos = mod(alongLine, cycle);
            if ((pos > dashLength && pos < dashLength + gapLength) ||
                (pos > dashLength + gapLength + dotLength)) discard;
        }
        else if (lineTypePattern == 4) { // DashDotDot
            float dashLength = 0.25 * lineTypeScale;
            float dotLength  = 0.05 * lineTypeScale;
            float gapLength  = 0.10 * lineTypeScale;
            float cycle = dashLength + gapLength + dotLength + gapLength + dotLength + gapLength;
            float pos = mod(alongLine, cycle);
            if ((pos > dashLength && pos < dashLength + gapLength) ||
                (pos > dashLength + gapLength + dotLength && pos < dashLength + 2.0 * gapLength + dotLength) ||
                (pos > dashLength + 2.0 * gapLength + 2.0 * dotLength)) discard;
        }
        else if (lineTypePattern == 5) { // Center
            float longDash  = 0.5 * lineTypeScale;
            float shortDash = 0.125 * lineTypeScale;
            float gapLength = 0.1 * lineTypeScale;
            float cycle = longDash + gapLength + shortDash + gapLength;
            float pos = mod(alongLine, cycle);
            if ((pos > longDash && pos < longDash + gapLength) ||
                (pos > longDash + gapLength + shortDash)) discard;
        }
        else if (lineTypePattern == 6) { // Hidden
            float dashLength = 0.125 * lineTypeScale;
            float gapLength  = 0.075 * lineTypeScale;
            float cycle = dashLength + gapLength;
            float pos = mod(alongLine, cycle);
            if (pos > dashLength) discard;
        }
        else if (lineTypePattern == 7) { // Phantom
            float longDash  = 0.5 * lineTypeScale;
            float shortDash = 0.125 * lineTypeScale;
            float gapLength = 0.1 * lineTypeScale;
            float cycle = longDash + gapLength + shortDash + gapLength + shortDash + gapLength;
            float pos = mod(alongLine, cycle);
            if ((pos > longDash && pos < longDash + gapLength) ||
                (pos > longDash + gapLength + shortDash && pos < longDash + 2.0 * gapLength + shortDash) ||
                (pos > longDash + 2.0 * gapLength + 2.0 * shortDash)) discard;
        }
        else if (lineTypePattern == 8) { // Fine dashed
            float dashLength = 0.15 * lineTypeScale;
            float gapLength  = 0.075 * lineTypeScale;
            float cycle = dashLength + gapLength;
            float pos = mod(alongLine, cycle);
            if (pos > dashLength) discard;
        }
    }

    if (glowRadius > 0.0)
    {
        float halfWidth = lineWidth * 0.5;

        if (dist <= halfWidth)
        {
            FragColor = vec4(color.rgb, color.a);
        }
        else if (dist <= halfWidth + glowRadius)
        {
            float glowFactor = 1.0 - smoothstep(halfWidth, halfWidth + glowRadius, dist);
            float glowAlpha = glowFactor * 0.45;
            FragColor = vec4(color.rgb, color.a * glowAlpha);
        }
        else
        {
            discard;
        }
    }
    else
    {
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
                throw new Exception($"LineShaderProgram linking failed: {infoLog}");
            }

            GL.DetachShader(_programId, vertexShader);
            GL.DetachShader(_programId, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            GLDiag.Check("LineShaderProgram ctor end");
        }

        private static int CompileShader(ShaderType type, string source)
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

        public void Use() => GL.UseProgram(_programId);

        public void SetMatrix4(string name, Matrix4x4 matrix)
        {
            int location = GetUniformLocation(name);

            // System.Numerics is row-major in memory; GLSL expects column-major.
            // Keep project convention: upload row-major and let GL transpose.
            float[] rowMajor =
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            };

            GL.UniformMatrix4(location, 1, true, rowMajor);
        }

        public void SetVector2(string name, Vector2 vector)
        {
            int location = GetUniformLocation(name);
            GL.Uniform2(location, vector.X, vector.Y);
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

        private int GetUniformLocation(string name)
        {
            if (!_uniformLocations.TryGetValue(name, out int location))
            {
                location = GL.GetUniformLocation(_programId, name);
                _uniformLocations[name] = location;
            }

            return location;
        }

        public int ProgramId => _programId;
    }
}