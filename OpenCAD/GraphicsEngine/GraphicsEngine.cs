using OpenCAD;
using OpenCAD.Geometry;
using System.Numerics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace GraphicsEngine
{
    /// <summary>
    /// Projection mode for the viewport
    /// </summary>
    public enum ProjectionMode
    {
        Orthographic,
        Perspective
    }

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
        private ProjectionMode _projectionMode = ProjectionMode.Orthographic; // Default to orthographic for CAD
        private int _viewportWidth;
        private int _viewportHeight;
        private float _orthographicScale = 10.0f; // How many world units visible in viewport height
        private float _orthoCenterX = 0f;
        private float _orthoCenterY = 0f;

        public RenderEngine()
        {
            _camera = new Camera();
        }

        /// <summary>
        /// Gets or sets the current projection mode
        /// </summary>
        public ProjectionMode ProjectionMode
        {
            get => _projectionMode;
            set
            {
                if (_projectionMode != value)
                {
                    _projectionMode = value;

                    // Lock axes when entering orthographic
                    if (_projectionMode == ProjectionMode.Orthographic)
                        AlignCameraForOrthographic();

                    UpdateProjection(_viewportWidth, _viewportHeight);
                    System.Diagnostics.Debug.WriteLine($"Projection mode changed to: {_projectionMode}");
                }
            }
        }

        /// <summary>
        /// Gets or sets the orthographic scale (world units visible in viewport height)
        /// </summary>
        public float OrthographicScale
        {
            get => _orthographicScale;
            set
            {
                if (Math.Abs(_orthographicScale - value) > 0.0001f)
                {
                    _orthographicScale = Math.Max(0.1f, value); // Prevent zero or negative scale
                    if (_projectionMode == ProjectionMode.Orthographic)
                    {
                        UpdateProjection(_viewportWidth, _viewportHeight);
                    }
                }
            }
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
            System.Diagnostics.Debug.WriteLine($"RenderEngine initialized with {_projectionMode} projection");
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
            // Clear per frame to avoid accumulation/smearing
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Force a pure orthographic pipeline: no camera view in ortho
            _viewMatrix = (_projectionMode == ProjectionMode.Orthographic)
                ? Matrix4x4.Identity
                : _camera.GetViewMatrix();

            if (_projectionMode != ProjectionMode.Orthographic && _viewMatrix.IsIdentity)
            {
                System.Diagnostics.Debug.WriteLine("WARNING: View matrix is IDENTITY in perspective!");
                System.Diagnostics.Debug.WriteLine($"Camera - Position: {_camera.Position}, Target: {_camera.Target}, Up: {_camera.Up}");
            }

            int count = 0;
            var drawableObjects = objects.Where(o => o.IsDrawable).ToList();
            foreach (var obj in drawableObjects)
            {
                count++;
                RenderObject(obj);
            }

            if (count == 0)
                System.Diagnostics.Debug.WriteLine("Render called with 0 objects.");

            GLDiag.Check("End of Render");
        }

        public void RenderOverlay(IEnumerable<OpenCADObject> overlayObjects)
        {
            // Match the main pass: in orthographic, force identity view to avoid mixed conventions
            _viewMatrix = (_projectionMode == ProjectionMode.Orthographic)
                ? Matrix4x4.Identity
                : _camera.GetViewMatrix();

            // Save states
            bool depthWasEnabled = GL.IsEnabled(EnableCap.DepthTest);
            bool blendWasEnabled = GL.IsEnabled(EnableCap.Blend);

            // Overlays should render on top without depth fighting, allow alpha
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            int count = 0;
            var drawable = overlayObjects.Where(o => o.IsDrawable).ToList();
            foreach (var obj in drawable)
            {
                count++;
                RenderObject(obj);
            }
            if (count == 0)
                System.Diagnostics.Debug.WriteLine("RenderOverlay called with 0 objects.");

            // Restore states
            if (!blendWasEnabled) GL.Disable(EnableCap.Blend);
            if (depthWasEnabled) GL.Enable(EnableCap.DepthTest);

            GLDiag.Check("End of RenderOverlay");
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

            _viewportWidth = width;
            _viewportHeight = height;

            GL.Viewport(0, 0, width, height);

            float aspectRatio = (float)width / height;

            if (_projectionMode == ProjectionMode.Orthographic)
            {
                // Use centered ortho window to support pan without touching the camera
                _projectionMatrix = CreateOrthographicGL(
                    _orthographicScale, aspectRatio, 0.1f, 1000.0f,
                    _orthoCenterX, _orthoCenterY);

                // DEBUG: dump the matrix shape we expect for a pure ortho (no shear/tilt)
                var m = _projectionMatrix;
                Debug.WriteLine($"[RE] Ortho: center=({_orthoCenterX:F4},{_orthoCenterY:F4}) scale={_orthographicScale:F4} aspect={aspectRatio:F4} size=({(_orthographicScale*aspectRatio):F4}x{_orthographicScale:F4})");
                Debug.WriteLine($"[RE] Ortho M2x2=[[{m.M11:F6},{m.M12:F6}],[{m.M21:F6},{m.M22:F6}]]  T=({m.M41:F6},{m.M42:F6})  M34={m.M34:F6} M44={m.M44:F6}");
                if (MathF.Abs(m.M12) > 1e-6f || MathF.Abs(m.M21) > 1e-6f)
                    Debug.WriteLine("[RE][WARN] Ortho off-diagonal != 0 (shear/tilt) in projection.");
            }
            else
            {
                _projectionMatrix = CreatePerspectiveFieldOfViewGL(
                    MathF.PI / 4f,
                    aspectRatio,
                    0.1f,
                    1000.0f
                );
            }

            // Log viewport sanity
            int[] vp = new int[4];
            GL.GetInteger(GetPName.Viewport, vp);
            System.Diagnostics.Debug.WriteLine($"Projection updated ({_projectionMode}): {width}x{height}, GL viewport: {vp[2]}x{vp[3]} at ({vp[0]},{vp[1]})");

            GLDiag.Check("UpdateProjection");
        }

        /// <summary>
        /// Zoom the orthographic view (adjust scale)
        /// </summary>
        public void ZoomOrthographic(float delta)
        {
            if (_projectionMode == ProjectionMode.Orthographic)
            {
                // Decrease scale to zoom in, increase to zoom out
                OrthographicScale += delta;
                System.Diagnostics.Debug.WriteLine($"Orthographic scale: {_orthographicScale:F2}");
            }
        }

        /// <summary>
        /// Gets the current projection matrix
        /// </summary>
        public Matrix4x4 GetProjectionMatrix()
        {
            return _projectionMatrix;
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

        // OpenGL (RH) orthographic matrix with clip depth -1..1
        private static Matrix4x4 CreateOrthographicGL(float scale, float aspect, float zNear, float zFar)
            => CreateOrthographicGL(scale, aspect, zNear, zFar, 0f, 0f);

        // Centered ortho: panning is just changing (centerX, centerY)
        private static Matrix4x4 CreateOrthographicGL(float scale, float aspect, float zNear, float zFar, float centerX, float centerY)
        {
            if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            if (aspect <= 0) throw new ArgumentOutOfRangeException(nameof(aspect));
            if (zNear >= zFar) throw new ArgumentOutOfRangeException(nameof(zNear));

            float height = scale;
            float width = height * aspect;

            float left = centerX - width / 2f;
            float right = centerX + width / 2f;
            float bottom = centerY - height / 2f;
            float top = centerY + height / 2f;

            Matrix4x4 m = new Matrix4x4();
            m.M11 = 2f / (right - left);
            m.M22 = 2f / (top - bottom);
            m.M33 = -2f / (zFar - zNear);
            m.M41 = -(right + left) / (right - left);
            m.M42 = -(top + bottom) / (top - bottom);
            m.M43 = -(zFar + zNear) / (zFar - zNear);
            m.M44 = 1f;
            return m;
        }

        public Camera Camera => _camera;

        private void AlignCameraForOrthographic()
        {
            // Keep camera looking straight down -Z with +Y up, preserve radius
            float radius = (_camera.Position - _camera.Target).Length();
            if (radius < 0.0001f) radius = 10f;

            _camera.Up = Vector3.UnitY;
            _camera.Position = new Vector3(_camera.Target.X, _camera.Target.Y, _camera.Target.Z + radius);

            // Keep ortho window centered on the target when switching modes
            _orthoCenterX = _camera.Target.X;
            _orthoCenterY = _camera.Target.Y;
        }

        /// <summary>
        /// Pan the orthographic view by adjusting the camera position
        /// </summary>
        public void PanOrthoPixels(float deltaXpx, float deltaYpx)
        {
            if (_projectionMode != ProjectionMode.Orthographic)
            {
                _camera.Pan(deltaXpx, deltaYpx);
                return;
            }

            // Ignore tiny/no movement to avoid needless projection rebuilds
            if (MathF.Abs(deltaXpx) < 0.001f && MathF.Abs(deltaYpx) < 0.001f)
                return;

            if (_viewportWidth == 0 || _viewportHeight == 0) return;

            float worldHeight = _orthographicScale;
            float worldWidth = worldHeight * ((float)_viewportWidth / _viewportHeight);

            float worldPerPixelX = worldWidth / _viewportWidth;
            float worldPerPixelY = worldHeight / _viewportHeight;

            float dxWorld = -deltaXpx * worldPerPixelX;
            float dyWorld = -deltaYpx * worldPerPixelY;

            _orthoCenterX += dxWorld;
            _orthoCenterY -= dyWorld;

            Debug.WriteLine($"[RE] PanOrthoPixels px=({deltaXpx:F3},{deltaYpx:F3}) world=({dxWorld:F3},{dyWorld:F3}) center=({_orthoCenterX:F3},{_orthoCenterY:F3})");

            UpdateProjection(_viewportWidth, _viewportHeight);
        }

        /// <summary>
        /// Convert framebuffer pixel coordinates to world coordinates for orthographic top view.
        /// Returns Z=worldZ (default 0). Requires ProjectionMode == Orthographic.
        /// </summary>
        public Vector3 ScreenToWorldOrthoPixels(float xPx, float yPx, float worldZ = 0f)
        {
            if (_projectionMode != ProjectionMode.Orthographic)
                throw new InvalidOperationException("ScreenToWorldOrthoPixels is valid only in Orthographic mode.");

            if (_viewportWidth <= 0 || _viewportHeight <= 0)
                return new Vector3(_orthoCenterX, _orthoCenterY, worldZ);

            float aspect = (float)_viewportWidth / _viewportHeight;

            // Current ortho window dimensions in world units
            float worldHeight = _orthographicScale;
            float worldWidth = worldHeight * aspect;

            // Convert pixels -> NDC
            float ndcX = (xPx / _viewportWidth) * 2f - 1f;
            float ndcY = 1f - (yPx / _viewportHeight) * 2f; // top=+1

            // Map NDC -> world using the centered ortho window
            float halfW = worldWidth * 0.5f;
            float halfH = worldHeight * 0.5f;

            float worldX = _orthoCenterX + ndcX * halfW;
            float worldY = _orthoCenterY + ndcY * halfH;

            return new Vector3(worldX, worldY, worldZ);
        }
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
            // Calculate the view direction (normalized)
            Vector3 viewDir = Vector3.Normalize(Position - Target);
            
            // Calculate right vector (perpendicular to view direction and up)
            Vector3 right = Vector3.Normalize(Vector3.Cross(Up, viewDir));
            
            // Calculate the actual up vector (perpendicular to both)
            Vector3 actualUp = Vector3.Normalize(Vector3.Cross(viewDir, right));

            // Move both position and target by the same amount
            // This creates a "slide" effect perfect for general (perspective) views
            Vector3 offset = right * deltaX + actualUp * deltaY;
            
            Position += offset;
            Target += offset;
        }

        public void PanOrthoWorld(float deltaX, float deltaY)
        {
            // Translate strictly in world XY; keep Up axis locked to +Y
            Vector3 offset = new Vector3(deltaX, deltaY, 0f);
            Position += offset;
            Target += offset;
            Up = Vector3.UnitY;
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
        private static readonly bool _debugTilt = Environment.GetEnvironmentVariable("OPENCAD_DEBUG_TILT") == "1";

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
            if (_smokeTest)
            {
                float[] test = new float[] { -0.5f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f };
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, test.Length * sizeof(float), test, BufferUsageHint.DynamicDraw);

                _shaderProgram.Use();
                _shaderProgram.SetMatrix4("mvp", Matrix4x4.Identity);
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

            // Orthographic detection: no perspective divide
            bool isOrtho = MathF.Abs(projectionMatrix.M34) < 1e-6f && MathF.Abs(projectionMatrix.M44 - 1f) < 1e-6f;

            if (isOrtho)
            {
                // CPU path: derive ortho window from projection (row-major)
                // sx = 2/(r-l) => halfW = (r-l)/2 = 1/sx; tx_row = -(r+l)/(r-l) => centerX = -(tx_row)/sx
                float sx = projectionMatrix.M11;
                float sy = projectionMatrix.M22;
                float txRow = projectionMatrix.M41;
                float tyRow = projectionMatrix.M42;

                if (MathF.Abs(sx) > 1e-12f && MathF.Abs(sy) > 1e-12f)
                {
                    float halfW = 1.0f / sx;
                    float halfH = 1.0f / sy;
                    float centerX = -txRow / sx;
                    float centerY = -tyRow / sy;

                    float ax = (float)start.X;
                    float ay = (float)start.Y;
                    float bx = (float)end.X;
                    float by = (float)end.Y;

                    // World XY -> NDC XY
                    float ndcAx = (ax - centerX) / halfW;
                    float ndcAy = (ay - centerY) / halfH;
                    float ndcBx = (bx - centerX) / halfW;
                    float ndcBy = (by - centerY) / halfH;

                    float[] ndcVerts =
                    {
                        ndcAx, ndcAy, 0f,
                        ndcBx, ndcBy, 0f
                    };

                    GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, ndcVerts.Length * sizeof(float), ndcVerts, BufferUsageHint.DynamicDraw);

                    _shaderProgram.Use();
                    _shaderProgram.SetMatrix4("mvp", Matrix4x4.Identity);
                    _shaderProgram.SetVector3("color", new Vector3(1.0f, 0.0f, 0.0f));

                    GL.BindVertexArray(_vao);
                    GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
                    GL.EnableVertexAttribArray(0);

                    GL.LineWidth(1.0f);
                    GL.DrawArrays(PrimitiveType.Lines, 0, 2);
                    GL.BindVertexArray(0);

                    GLDiag.Check("LineRenderer draw end (CPU ortho)");

                    if (_debugTilt)
                    {
                        Debug.WriteLine($"[LR][CPU-ORTHO] center=({centerX:F4},{centerY:F4}) half=({halfW:F4},{halfH:F4}) A_ndc=({ndcAx:F4},{ndcAy:F4}) B_ndc=({ndcBx:F4},{ndcBy:F4})");
                    }
                    return;
                }
            }

            // GPU path (perspective or fallback)
            float[] vertices =
            {
                (float)start.X, (float)start.Y, (float)start.Z,
                (float)end.X,   (float)end.Y,   (float)end.Z
            };

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);

            _shaderProgram.Use();

            // Row-vector semantics: MVP = M • V • P (GL will transpose on upload)
            var model = Matrix4x4.Identity;
            var mvpRow = model;
            mvpRow = Matrix4x4.Multiply(mvpRow, viewMatrix);
            mvpRow = Matrix4x4.Multiply(mvpRow, projectionMatrix);

            if (_debugTilt)
            {
                float m11 = mvpRow.M11, m12 = mvpRow.M12, m21 = mvpRow.M21, m22 = mvpRow.M22;
                float tx = mvpRow.M41, ty = mvpRow.M42;
                Debug.WriteLine($"[LR] mode={(isOrtho ? "Ortho" : "Persp")} viewIdentity={viewMatrix.IsIdentity}");
                Debug.WriteLine($"[LR] MVP 2x2: [[{m11:F6}, {m12:F6}], [{m21:F6}, {m22:F6}]]  trans=({tx:F6},{ty:F6})");
            }

            _shaderProgram.SetMatrix4("mvp", mvpRow);
            _shaderProgram.SetVector3("color", new Vector3(1.0f, 0.0f, 0.0f));

            GL.BindVertexArray(_vao);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GLDiag.DumpPipelineState(_vao, _vbo, _shaderProgram.ProgramId);

            GL.LineWidth(1.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, 2);
            GL.BindVertexArray(0);

            GLDiag.Check("LineRenderer draw end");
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DumpPipelineState failed: {ex.Message}");
            }
        }
    }
}
