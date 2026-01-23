using GraphicsEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTK.Wpf;
using System.Numerics;

namespace GraphicsEngineTests
{
    [TestClass]
    public class RenderEngineTests
    {
        private RenderEngine engine;

        [TestInitialize]
        public void Setup()
        {
            engine = new RenderEngine();
            engine.ResizeViewport(1000, 500); // default aspect = 2.0
        }

        // ------------------------------------------------------------
        // 1. Aspect Ratio Calculation
        // ------------------------------------------------------------
        [TestMethod]
        public void ResizeViewport_ComputesCorrectAspectRatio()
        {
            engine.ResizeViewport(1920, 1080);

            float expected = 1920f / 1080f;
            Assert.AreEqual(expected, engine.AspectRatio, 1e-6f);
        }

        // ------------------------------------------------------------
        // 2. OrthoCamera Projection Used in Orthographic Mode
        // ------------------------------------------------------------
        [TestMethod]
        public void ProjectionMatrix_UsesOrthoCameraInOrthographicMode()
        {
            engine.ProjectionMode = ProjectionMode.Orthographic;
            engine.ResizeViewport(1200, 600);

            float aspect = 1200f / 600f;
            Matrix4x4 expected = engine.Camera.ProjectionMatrix;
            Matrix4x4 actual = engine.ProjectionMatrix;

            Assert.AreEqual(expected, actual);
        }

        // ------------------------------------------------------------
        // 3. Perspective Camera Projection Used in Perspective Mode
        // ------------------------------------------------------------
        [TestMethod]
        public void ProjectionMatrix_UsesCameraInPerspectiveMode()
        {
            engine.ProjectionMode = ProjectionMode.Perspective;
            engine.ResizeViewport(800, 600);

            float aspect = 800f / 600f;
            Matrix4x4 expected = engine.Camera.ProjectionMatrix;
            Matrix4x4 actual = engine.ProjectionMatrix;

            Assert.AreEqual(expected, actual);
        }

        // ------------------------------------------------------------
        // 4. OrthoCamera View Matrix Used in Orthographic Mode
        // ------------------------------------------------------------
        [TestMethod]
        public void ViewMatrix_UsesOrthoCameraInOrthographicMode()
        {
            engine.ProjectionMode = ProjectionMode.Orthographic;

            Matrix4x4 expected = engine.Camera.ViewMatrix;
            Matrix4x4 actual = engine.ViewMatrix;

            Assert.AreEqual(expected, actual);
        }

        // ------------------------------------------------------------
        // 5. Perspective Camera View Matrix Used in Perspective Mode
        // ------------------------------------------------------------
        [TestMethod]
        public void ViewMatrix_UsesCameraInPerspectiveMode()
        {
            engine.ProjectionMode = ProjectionMode.Perspective;

            Matrix4x4 expected = engine.Camera.ViewMatrix;
            Matrix4x4 actual = engine.ViewMatrix;

            Assert.AreEqual(expected, actual);
        }

        // ------------------------------------------------------------
        // 6. ScreenToWorldOrthoPixels Maps Screen Center to Camera Target
        // ------------------------------------------------------------
        //[TestMethod]
        //public void ScreenToWorldOrthoPixels_MapsCenterCorrectly()
        //{
        //    engine.ProjectionMode = ProjectionMode.Orthographic;
        //    engine.ResizeViewport(1000, 1000);

        //    float cx = 500f;
        //    float cy = 500f;

        //    Vector3 world = engine.ScreenToWorldOrthoPixels(cx, cy, 0f);

        //    Assert.AreEqual(engine.OrthoCamera.Target.X, world.X, 1e-6f);
        //    Assert.AreEqual(engine.OrthoCamera.Target.Y, world.Y, 1e-6f);
        //    Assert.AreEqual(0f, world.Z, 1e-6f);
        //}

        // ------------------------------------------------------------
        // 7. ResizeViewport Updates Projection Matrix
        // ------------------------------------------------------------
        [TestMethod]
        public void ResizeViewport_UpdatesProjectionMatrix()
        {
            engine.ProjectionMode = ProjectionMode.Orthographic;

            Matrix4x4 before = engine.ProjectionMatrix;

            engine.ResizeViewport(2000, 1500);
            Matrix4x4 after = engine.ProjectionMatrix;

            Assert.AreNotEqual(before, after);
        }
    }
}