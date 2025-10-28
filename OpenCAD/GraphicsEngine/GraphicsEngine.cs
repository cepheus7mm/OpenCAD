using OpenCAD;
using OpenCAD.Geometry;
using System.Numerics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace GraphicsEngine
{
    /// <summary>
    /// Main graphics engine for rendering OpenCADObjects using OpenGL
    /// </summary>
    public class RenderEngine
    {
        private readonly List<IRenderer> _renderers = new();
        private Camera _camera;
        private Matrix4x4 _projectionMatrix;
        private Matrix4x4 _viewMatrix;
        private ShaderProgram? _shaderProgram;

        public RenderEngine()
        {
            _camera = new Camera();
        }

        /// <summary>
        /// Initialize the rendering engine
        /// </summary>
        public void Initialize(int width, int height)
        {
            // Make sure a valid and current GL context exists before this point.

            // Create shader program
            _shaderProgram = new ShaderProgram();

            // Set baseline GL state
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Disable(EnableCap.CullFace); // lines don't need culling
            GL.ClearColor(0.08f, 0.08f, 0.08f, 1.0f);

            // Enable debug output and log context info
            GLDiag.TryEnableDebugOutput();
            GLDiag.LogContextInfo();

            UpdateProjection(width, height);
            RegisterDefaultRenderers();

            GLDiag.Check("Initialize end");
            System.Diagnostics.Debug.WriteLine("RenderEngine initialized with modern OpenGL");
        }

        /// <summary>
        /// Register default renderers for geometry types
        /// </summary>
        private void RegisterDefaultRenderers()
        {
            if (_shaderProgram == null)
                throw new InvalidOperationException("ShaderProgram must be initialized before registering renderers");

            _renderers.Add(new LineRenderer(_shaderProgram));
            System.Diagnostics.Debug.WriteLine($"Registered {_renderers.Count} renderer(s)");
        }

        /// <summary>
        /// Render a collection of OpenCADObjects
        /// </summary>
        public void Render(IEnumerable<OpenCADObject> objects)
        {
            // Clear per-frame
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _viewMatrix = _camera.GetViewMatrix();

            if (_viewMatrix.IsIdentity)
            {
                System.Diagnostics.Debug.WriteLine("WARNING: View matrix is IDENTITY! Camera position may be invalid.");
                System.Diagnostics.Debug.WriteLine($"Camera - Position: {_camera.Position}, Target: {_camera.Target}, Up: {_camera.Up}");
            }

            // Quick object/reporting checks
            int count = 0;
            foreach (var obj in objects)
            {
                count++;
                RenderObject(obj);
            }
            if (count == 0)
                System.Diagnostics.Debug.WriteLine("Render called with 0 objects.");

            GLDiag.Check("End of Render");
        }

        /// <summary>
        /// Render a single OpenCADObject
        /// </summary>
        private void RenderObject(OpenCADObject obj)
        {
            var renderer = _renderers.FirstOrDefault(r => r.CanRender(obj));
            if (renderer != null)
            {
                renderer.Render(obj, _viewMatrix, _projectionMatrix);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"No renderer found for type {obj.GetType().FullName}");
            }
        }

        /// <summary>
        /// Update projection matrix when viewport size changes
        /// </summary>
        public void UpdateProjection(int width, int height)
        {
            if (width <= 0 || height <= 0) return;

            GL.Viewport(0, 0, width, height);

            float aspectRatio = (float)width / height;

            _projectionMatrix = CreatePerspectiveFieldOfViewGL(
                MathF.PI / 4f, // 45 degrees FOV
                aspectRatio,
                0.1f,          // Near plane
                1000.0f        // Far plane
            );

            // Log viewport sanity
            int[] vp = new int[4];
            GL.GetInteger(GetPName.Viewport, vp);
            System.Diagnostics.Debug.WriteLine($"Projection updated: {width}x{height}, GL viewport: {vp[2]}x{vp[3]} at ({vp[0]},{vp[1]})");

            GLDiag.Check("UpdateProjection");
        }

        // OpenGL (RH) perspective matrix with clip depth -1..1
        private static Matrix4x4 CreatePerspectiveFieldOfViewGL(float fovy, float aspect, float zNear, float zFar)
        {
            if (fovy <= 0 || fovy >= MathF.PI) throw new ArgumentOutOfRangeException(nameof(fovy));
            if (aspect <= 0) throw new ArgumentOutOfRangeException(nameof(aspect));
            if (zNear <= 0 || zFar <= 0 || zNear >= zFar) throw new ArgumentOutOfRangeException(nameof(zNear));

            float f = 1f / MathF.Tan(fovy / 2f);

            Matrix4x4 m = new Matrix4x4();
            m.M11 = f / aspect;
            m.M22 = f;
            m.M33 = (zFar + zNear) / (zNear - zFar);
            m.M34 = -1f;
            m.M43 = (2f * zFar * zNear) / (zNear - zFar);
            m.M44 = 0f;
            return m;
        }

        public Camera Camera => _camera;
    }

    /// <summary>
    /// Shader program wrapper for modern OpenGL
    /// </summary>
    public class ShaderProgram
    {
        private readonly int _programId;
        private readonly Dictionary<string, int> _uniformLocations = new();

        public ShaderProgram()
        {
            string vertexShaderSource = @"
                #version 330 core
                layout (location = 0) in vec3 aPosition;

                uniform mat4 mvp;
                uniform vec3 color;

                out vec3 fragColor;

                void main()
                {
                    gl_Position = mvp * vec4(aPosition, 1.0);
                    fragColor = color;
                }";

            string fragmentShaderSource = @"
                #version 330 core
                in vec3 fragColor;
                out vec4 FragColor;

                void main()
                {
                    FragColor = vec4(fragColor, 1.0);
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

            // Row-major packing (matches System.Numerics), ask GL to transpose -> column-major in shader
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

    /// <summary>
    /// Camera for 3D navigation
    /// </summary>
    public class Camera
    {
        public Vector3 Position { get; set; } = new Vector3(0, 0, 10);
        public Vector3 Target { get; set; } = Vector3.Zero;
        public Vector3 Up { get; set; } = Vector3.UnitY;

        public Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(Position, Target, Up);
        }

        public void Orbit(float deltaX, float deltaY)
        {
            float radius = (Position - Target).Length();
            float theta = MathF.Atan2(Position.Z - Target.Z, Position.X - Target.X);
            float phi = MathF.Acos((Position.Y - Target.Y) / radius);

            theta += deltaX;
            phi += deltaY;
            phi = Math.Clamp(phi, 0.1f, MathF.PI - 0.1f);

            Position = new Vector3(
                Target.X + radius * MathF.Sin(phi) * MathF.Cos(theta),
                Target.Y + radius * MathF.Cos(phi),
                Target.Z + radius * MathF.Sin(phi) * MathF.Sin(theta)
            );
        }

        public void Zoom(float delta)
        {
            Vector3 direction = Vector3.Normalize(Target - Position);
            Position += direction * delta;
        }

        public void Pan(float deltaX, float deltaY)
        {
            Vector3 right = Vector3.Normalize(Vector3.Cross(Target - Position, Up));
            Vector3 up = Vector3.Normalize(Vector3.Cross(right, Target - Position));

            Position += right * deltaX + up * deltaY;
            Target += right * deltaX + up * deltaY;
        }
    }

    public interface IRenderer
    {
        bool CanRender(OpenCADObject obj);
        void Render(OpenCADObject obj, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix);
    }

    /// <summary>
    /// Modern OpenGL renderer for Line geometry using VBOs and shaders
    /// </summary>
    public class LineRenderer : IRenderer
    {
        private readonly ShaderProgram _shaderProgram;
        private int _vao; // Vertex Array Object
        private int _vbo; // Vertex Buffer Object
        private readonly bool _smokeTest = Environment.GetEnvironmentVariable("OPENCAD_SMOKE_TEST") == "1";

        public LineRenderer(ShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            if (_vao == 0 || _vbo == 0)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Failed to create VAO/VBO (vao={_vao}, vbo={_vbo}). Is a GL context current?");
            }

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            GLDiag.Check("LineRenderer ctor end");
            System.Diagnostics.Debug.WriteLine($"LineRenderer initialized with VAO={_vao} and VBO={_vbo}");
        }

        public bool CanRender(OpenCADObject obj)
        {
            return obj is Line;
        }

        public void Render(OpenCADObject obj, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            // Optional smoke test: bypass matrices and geometry to isolate pipeline
            if (_smokeTest)
            {
                float[] test = new float[]
                {
                    -0.5f, 0.0f, 0.0f,
                     0.5f, 0.0f, 0.0f
                };

                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, test.Length * sizeof(float), test, BufferUsageHint.DynamicDraw);

                _shaderProgram.Use();
                _shaderProgram.SetMatrix4("model", Matrix4x4.Identity);
                _shaderProgram.SetMatrix4("view", Matrix4x4.Identity);
                _shaderProgram.SetMatrix4("projection", Matrix4x4.Identity);
                _shaderProgram.SetVector3("color", new Vector3(0.0f, 1.0f, 0.0f));

                GL.LineWidth(1.0f);
                GL.BindVertexArray(_vao);
                GL.DrawArrays(PrimitiveType.Lines, 0, 2);
                GL.BindVertexArray(0);

                GLDiag.Check("LineRenderer smoke test draw");
                return;
            }

            if (obj is not Line line) return;

            var start = line.Start;
            var end = line.End;

            float[] vertices =
            {
                (float)start.X, (float)start.Y, (float)start.Z,
                (float)end.X,   (float)end.Y,   (float)end.Z
            };

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);

            _shaderProgram.Use();

            // Build MVP in row-major order (System.Numerics multiplies as v * M), i.e., model • view • projection
            var model = Matrix4x4.Identity;
            var mvpRow = model;
            mvpRow = Matrix4x4.Multiply(mvpRow, viewMatrix);
            mvpRow = Matrix4x4.Multiply(mvpRow, projectionMatrix);

            // Upload as row-major with transpose=true (in SetMatrix4)
            _shaderProgram.SetMatrix4("mvp", mvpRow);
            _shaderProgram.SetVector3("color", new Vector3(1.0f, 0.0f, 0.0f));

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GLDiag.DumpPipelineState(_vao, _vbo, _shaderProgram.ProgramId);

            GL.LineWidth(1.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, 2);

            GL.BindVertexArray(0);

            GLDiag.Check("LineRenderer draw end");

            // Optional: quick NDC sanity using the same row-major MVP we sent (GL receives its transpose)
            Vector4 ToClip(double x, double y, double z)
                => Vector4.Transform(new Vector4((float)x, (float)y, (float)z, 1f), mvpRow);
            var ca = ToClip(start.X, start.Y, start.Z);
            var cb = ToClip(end.X, end.Y, end.Z);
            if (ca.W != 0 && cb.W != 0)
            {
                var na = ca / ca.W; var nb = cb / cb.W;
                Debug.WriteLine($"NDC A=({na.X:F3},{na.Y:F3},{na.Z:F3}), B=({nb.X:F3},{nb.Y:F3},{nb.Z:F3})");
            }
        }
    }

    internal static class GLDiag
    {
        public static void Check(string where)
        {
            var err = GL.GetError();
            if (err != ErrorCode.NoError)
            {
                System.Diagnostics.Debug.WriteLine($"GL ERROR at {where}: {err}");
            }
        }

        public static void LogContextInfo()
        {
            try
            {
                string vendor = GL.GetString(StringName.Vendor);
                string renderer = GL.GetString(StringName.Renderer);
                string version = GL.GetString(StringName.Version);
                string glsl = GL.GetString(StringName.ShadingLanguageVersion);
                System.Diagnostics.Debug.WriteLine($"OpenGL Context -> Vendor: {vendor}, Renderer: {renderer}, Version: {version}, GLSL: {glsl}");

                float[] range = new float[2];
                GL.GetFloat(GetPName.LineWidthRange, range);
                System.Diagnostics.Debug.WriteLine($"Line width range: {range[0]} .. {range[1]}");
            }
            catch { /* ignore if context not ready */ }
        }

        public static void TryEnableDebugOutput()
        {
            try
            {
                // Enable KHR_debug if available
                GL.Enable(EnableCap.DebugOutput);
                GL.Enable(EnableCap.DebugOutputSynchronous);
                GL.DebugMessageCallback(DebugCallback, IntPtr.Zero);
                System.Diagnostics.Debug.WriteLine("KHR_debug enabled.");
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("KHR_debug not available on this context.");
            }
        }

        private static void DebugCallback(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
                var msg = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message, length);
                System.Diagnostics.Debug.WriteLine($"[GL DEBUG] {severity} {type} ({id}): {msg}");
        }

        public static void DumpPipelineState(int vao, int vbo, int programId)
        {
            try
            {
                GL.GetInteger(GetPName.CurrentProgram, out int curProg);
                GL.GetInteger(GetPName.VertexArrayBinding, out int curVao);
                GL.GetInteger(GetPName.ArrayBufferBinding, out int curArrayBuf);

                GL.GetVertexAttrib(0, VertexAttribParameter.ArrayEnabled, out int attr0Enabled);
                GL.GetVertexAttrib(0, VertexAttribParameter.ArraySize, out int attr0Size);
                GL.GetVertexAttrib(0, VertexAttribParameter.ArrayType, out int attr0Type);
                GL.GetVertexAttrib(0, VertexAttribParameter.ArrayStride, out int attr0Stride);

                Debug.WriteLine($"GL State -> Program: {curProg} (expected {programId}), VAO: {curVao} (expected {vao}), ARRAY_BUFFER: {curArrayBuf} (last upload {vbo})");
                Debug.WriteLine($"Attrib[0]: enabled={attr0Enabled!=0}, size={attr0Size}, type={(VertexAttribPointerType)attr0Type}, stride={attr0Stride}");

                // Uncomment to dump all active attributes (may be verbose)
                /*
                int maxAttribs;
                GL.Get(int krb.attribs   , &maxAttribs);
                for (int i = 0; i < maxAttribs; i++)
                {
                    GL.GetVertexAttrib(i, VertexAttribParameter.VertexAttribArrayEnabled, out int enabled);
                    if (enabled != 0)
                    {
                        GL.GetVertexAttrib(i, VertexAttribParameter.VertexAttribArraySize, out int size);
                        GL.GetVertexAttrib(i, VertexAttribParameter.VertexAttribArrayType, out int type);
                        GL.GetVertexAttrib(i, VertexAttribParameter.VertexAttribArrayStride, out int stride);
                        Debug.WriteLine($"Attrib[{i}]: enabled={enabled!=0}, size={size}, type={(VertexAttribPointerType)type}, stride={stride}");
                    }
                }
                */
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DumpPipelineState failed: {ex.Message}");
            }
        }
    }
}
