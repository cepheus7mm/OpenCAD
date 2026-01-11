using System;
using System.Collections.Generic;
using System.Numerics;
using OpenTK.Graphics.OpenGL;

namespace GraphicsEngine
{
    /// <summary>
    /// Shader program optimized for polylines rendered from vertex stream.
    /// Expects a vertex layout that provides per-vertex previous, current and next positions
    /// so the vertex shader can compute segment directions, join angle and the miter vector
    /// entirely on the GPU and emit screen-space offsets for correct screen-space thickness.
    ///
    /// Layout (required):
    ///   location 0 : vec3 aPrevPosition   (previous vertex position in world space)
    ///   location 1 : vec3 aPosition       (current vertex position in world space)
    ///   location 2 : vec3 aNextPosition   (next vertex position in world space)
    ///   location 3 : float aDistance      (cumulative world distance along polyline)
    ///   location 4 : float aSide          (+1.0 for one edge of the strip, -1.0 for the other)
    ///   location 5 : float aWidth         (per-vertex width in world units, 0 = use uniform width)
    ///
    /// Rendering:
    ///   - Use a triangle-strip (or equivalent) builder that emits two vertices per poly vertex
    ///     with aSide = -1/+1 to form a screen-space thickened polyline with correct joins.
    /// </summary>
    public class PolylineShaderProgram
    {
        private readonly int _programId;
        private readonly Dictionary<string, int> _uniformLocations = new();

        public PolylineShaderProgram()
        {
            string vertexShaderSource = @"
                #version 330 core

                // Per-vertex (world space)
                layout(location = 0) in vec3 aPrevPosition;
                layout(location = 1) in vec3 aPosition;
                layout(location = 2) in vec3 aNextPosition;
                layout(location = 3) in float aDistance;
                layout(location = 4) in float aSide; // +1 or -1
                layout(location = 5) in float aWidth; // per-vertex width in world units (0 = use uniform)

                uniform mat4 mvp;
                uniform vec2 viewport;         // viewport size in pixels
                uniform float renderLineWidth; // width used to expand geometry (pixels) - used when aWidth == 0
                uniform int hasVariableWidth;  // 1 if using per-vertex widths, 0 otherwise

                out vec2 fragScreenPos;
                out vec2 vLineStart; // screen-space segment start (pixels)
                out vec2 vLineEnd;   // screen-space segment end (pixels)
                out float vDistance; // per-fragment cumulative world distance (for stipple)
                out float vHalfWidth; // per-fragment half-width for distance calculations

                // Convert clip-space position to screen-space (pixels)
                vec2 ClipToScreen(vec4 clip)
                {
                    vec3 ndc = clip.xyz / clip.w;
                    vec2 screen = (ndc.xy * 0.5 + 0.5) * viewport;
                    return screen;
                }

                void main()
                {
                    // Transform positions to clip-space
                    vec4 clipPrev = mvp * vec4(aPrevPosition, 1.0);
                    vec4 clipCur  = mvp * vec4(aPosition, 1.0);
                    vec4 clipNext = mvp * vec4(aNextPosition, 1.0);

                    // Screen-space (pixels)
                    vec2 sPrev = ClipToScreen(clipPrev);
                    vec2 sCur  = ClipToScreen(clipCur);
                    vec2 sNext = ClipToScreen(clipNext);

                    // Direction vectors for incoming and outgoing segments in screen-space
                    vec2 segIn  = sCur - sPrev;
                    vec2 segOut = sNext - sCur;
                    
                    float lenIn  = length(segIn);
                    float lenOut = length(segOut);
                    
                    const float EPSILON = 0.01; // screen-space pixels threshold for degenerate segments
                    
                    vec2 dirIn  = lenIn > EPSILON ? segIn / lenIn : vec2(0.0);
                    vec2 dirOut = lenOut > EPSILON ? segOut / lenOut : vec2(0.0);

                    // Determine effective half-width based on whether we're using variable or uniform width
                    float halfWidth;
                    if (hasVariableWidth == 1 && aWidth > 0.0) {
                        // Convert world-space width to screen-space pixels
                        // Project a small world-space vector perpendicular to the segment to determine scaling
                        vec2 dir = lenOut > EPSILON ? dirOut : (lenIn > EPSILON ? dirIn : vec2(1.0, 0.0));
                        vec2 perpDir = vec2(-dir.y, dir.x); // perpendicular in screen space
                        
                        // Approximate world-to-screen scale by measuring a small perpendicular offset
                        vec3 worldPerp = vec3(perpDir * 0.01, 0.0); // small offset in screen direction
                        vec4 clipOffset = mvp * vec4(aPosition + worldPerp * (aWidth * 0.5), 1.0);
                        vec2 screenOffset = ClipToScreen(clipOffset);
                        float screenDist = length(screenOffset - sCur);
                        
                        // Scale to get actual width in screen pixels
                        halfWidth = (aWidth * 0.5) * (screenDist / (0.01 * (aWidth * 0.5)));
                        vHalfWidth = halfWidth;
                    } else {
                        // Use uniform pixel width
                        halfWidth = renderLineWidth * 0.5;
                        vHalfWidth = halfWidth;
                    }

                    // Compute offset based on whether we have a miter join or an endpoint
                    vec2 offset;
                    
                    bool isStart = lenIn <= EPSILON;
                    bool isEnd = lenOut <= EPSILON;
                    
                    if (isStart && isEnd) {
                        // Degenerate: single point, no direction - offset perpendicular to X
                        vec2 normal = vec2(0.0, 1.0);
                        offset = normal * (aSide * halfWidth);
                    }
                    else if (isStart) {
                        // Start endpoint: use perpendicular to outgoing direction only
                        vec2 normal = vec2(-dirOut.y, dirOut.x);
                        offset = normal * (aSide * halfWidth);
                    }
                    else if (isEnd) {
                        // End endpoint: use perpendicular to incoming direction only
                        vec2 normal = vec2(-dirIn.y, dirIn.x);
                        offset = normal * (aSide * halfWidth);
                    }
                    else {
                        // Interior vertex: compute miter join
                        vec2 nIn  = vec2(-dirIn.y, dirIn.x);
                        vec2 nOut = vec2(-dirOut.y, dirOut.x);
                        vec2 miter = normalize(nIn + nOut);
                        
                        float dotNM = dot(miter, nIn);
                        
                        // Clamp miter length to prevent excessive spikes at sharp angles
                        const float MAX_MITER = 3.0;
                        float miterLength = clamp(1.0 / max(0.001, abs(dotNM)), 1.0, MAX_MITER);
                        
                        offset = miter * (aSide * halfWidth * miterLength);
                    }

                    // Convert offset from screen-space (pixels) into clip-space displacement
                    vec2 ndcOffset = (offset / viewport) * 2.0;
                    vec4 clipOffset = vec4(ndcOffset * clipCur.w, 0.0, 0.0);

                    // Output final gl_Position (clip-space + offset)
                    gl_Position = clipCur + clipOffset;

                    // Emit interpolants for fragment shader
                    fragScreenPos = ClipToScreen(gl_Position);
                    vLineStart = sCur;
                    vLineEnd = sNext;
                    vDistance = aDistance;
                }";

            string fragmentShaderSource = @"
                #version 330 core

                uniform vec4 color;
                uniform vec2 viewport;
                uniform int lineTypePattern;
                uniform float lineTypeScale;
                uniform float coreLineWidth; // actual visual core width in pixels (when not using variable width)
                uniform float glowRadius;
                uniform int hasVariableWidth;

                in vec2 fragScreenPos;
                in vec2 vLineStart;
                in vec2 vLineEnd;
                in float vDistance;
                in float vHalfWidth;

                out vec4 FragColor;

                // Distance from a point to a segment (screen-space in pixels)
                float distanceToSegment(vec2 p, vec2 a, vec2 b) {
                    vec2 pa = p - a;
                    vec2 ba = b - a;
                    float denom = dot(ba, ba);
                    if (denom <= 1e-9) return length(pa);
                    float h = clamp(dot(pa, ba) / denom, 0.0, 1.0);
                    return length(pa - ba * h);
                }

                void main()
                {
                    // Distance of this fragment to the segment (in pixels)
                    float dist = distanceToSegment(fragScreenPos, vLineStart, vLineEnd);

                    // Apply stipple/linetype using the interpolated world-space distance
                    float alongLine = vDistance;
                    bool hasAlong = alongLine >= 0.0;

                    if (hasAlong && lineTypePattern != 0) {
                        if (lineTypePattern == 1) { // Dashed
                            float dashLength = 0.25 * lineTypeScale;
                            float gapLength = 0.125 * lineTypeScale;
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
                            float dotLength = 0.05 * lineTypeScale;
                            float gapLength = 0.10 * lineTypeScale;
                            float cycle = dashLength + gapLength + dotLength + gapLength;
                            float pos = mod(alongLine, cycle);
                            if ((pos > dashLength && pos < dashLength + gapLength) ||
                                (pos > dashLength + gapLength + dotLength)) discard;
                        }
                        else if (lineTypePattern == 4) { // DashDotDot
                            float dashLength = 0.25 * lineTypeScale;
                            float dotLength = 0.05 * lineTypeScale;
                            float gapLength = 0.10 * lineTypeScale;
                            float cycle = dashLength + gapLength + dotLength + gapLength + dotLength + gapLength;
                            float pos = mod(alongLine, cycle);
                            if ((pos > dashLength && pos < dashLength + gapLength) ||
                                (pos > dashLength + gapLength + dotLength && pos < dashLength + 2.0 * gapLength + dotLength) ||
                                (pos > dashLength + 2.0 * gapLength + 2.0 * dotLength)) discard;
                        }
                        else if (lineTypePattern == 5) { // Center
                            float longDash = 0.5 * lineTypeScale;
                            float shortDash = 0.125 * lineTypeScale;
                            float gapLength = 0.1 * lineTypeScale;
                            float cycle = longDash + gapLength + shortDash + gapLength;
                            float pos = mod(alongLine, cycle);
                            if ((pos > longDash && pos < longDash + gapLength) ||
                                (pos > longDash + gapLength + shortDash)) discard;
                        }
                        else if (lineTypePattern == 6) { // Hidden
                            float dashLength = 0.125 * lineTypeScale;
                            float gapLength = 0.075 * lineTypeScale;
                            float cycle = dashLength + gapLength;
                            float pos = mod(alongLine, cycle);
                            if (pos > dashLength) discard;
                        }
                        else if (lineTypePattern == 7) { // Phantom
                            float longDash = 0.5 * lineTypeScale;
                            float shortDash = 0.125 * lineTypeScale;
                            float gapLength = 0.1 * lineTypeScale;
                            float cycle = longDash + gapLength + shortDash + gapLength + shortDash + gapLength;
                            float pos = mod(alongLine, cycle);
                            if ((pos > longDash && pos < longDash + gapLength) ||
                                (pos > longDash + gapLength + shortDash && pos < longDash + 2.0 * gapLength + shortDash) ||
                                (pos > longDash + 2.0 * gapLength + 2.0 * shortDash)) discard;
                        }
                        else if (lineTypePattern == 8) { // Fine dashed (for selection)
                            float dashLength = 0.15 * lineTypeScale;
                            float gapLength = 0.075 * lineTypeScale;
                            float cycle = dashLength + gapLength;
                            float pos = mod(alongLine, cycle);
                            if (pos > dashLength) discard;
                        }
                    }

                    // Determine effective half-width based on whether we're using variable or uniform width
                    float halfCore = hasVariableWidth == 1 ? vHalfWidth : (coreLineWidth * 0.5);

                    // Glow / falloff behaviour
                    if (glowRadius > 0.0) {
                        if (dist <= halfCore) {
                            FragColor = vec4(color.rgb, color.a);
                        } else if (dist <= halfCore + glowRadius) {
                            float glowFactor = 1.0 - smoothstep(halfCore, halfCore + glowRadius, dist);
                            float glowAlpha = glowFactor * 0.45;
                            FragColor = vec4(color.rgb, color.a * glowAlpha);
                        } else {
                            discard;
                        }
                    } else {
                        if (dist > halfCore) discard;
                        FragColor = color;
                    }
                }";

            int vs = CompileShader(ShaderType.VertexShader, vertexShaderSource);
            int fs = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

            _programId = GL.CreateProgram();
            GL.AttachShader(_programId, vs);
            GL.AttachShader(_programId, fs);
            GL.LinkProgram(_programId);

            GL.GetProgram(_programId, GetProgramParameterName.LinkStatus, out int status);
            if (status == 0)
            {
                string info = GL.GetProgramInfoLog(_programId);
                throw new Exception($"PolylineShaderProgram link failed: {info}");
            }

            GL.DetachShader(_programId, vs);
            GL.DetachShader(_programId, fs);
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            GLDiag.Check("PolylineShaderProgram ctor end");
        }

        private int CompileShader(ShaderType type, string src)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, src);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0)
            {
                string info = GL.GetShaderInfoLog(shader);
                throw new Exception($"{type} compilation failed: {info}");
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

            float[] rowMajor =
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            };

            GL.UniformMatrix4(location, 1, true, rowMajor);
        }

        public void SetVector2(string name, Vector2 v)
        {
            int location = GetUniformLocation(name);
            GL.Uniform2(location, v.X, v.Y);
        }

        public void SetVector4(string name, Vector4 v)
        {
            int location = GetUniformLocation(name);
            GL.Uniform4(location, v.X, v.Y, v.Z, v.W);
        }

        public void SetFloat(string name, float value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform1(location, value);
        }

        public void SetInt(string name, int value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform1(location, value);
        }

        private int GetUniformLocation(string name)
        {
            if (!_uniformLocations.ContainsKey(name))
            {
                int loc = GL.GetUniformLocation(_programId, name);
                _uniformLocations[name] = loc;
                if (loc == -1)
                {
                    //System.Diagnostics.Debug.WriteLine($"Warning: Uniform '{name}' not found in PolylineShaderProgram");
                }
            }

            return _uniformLocations[name];
        }

        public int ProgramId => _programId;
    }
}