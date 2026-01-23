using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Numerics;
using GraphicsEngine;

namespace GraphicsEngineTests
{
    [TestClass]
    public class OrthoCameraTests
    {
        private OrthoCamera cam;

        [TestInitialize]
        public void Setup()
        {
            cam = new OrthoCamera(800);
        }

        // ------------------------------------------------------------
        // 1. Default State
        // ------------------------------------------------------------
        [TestMethod]
        public void DefaultState_IsCorrect()
        {
            Assert.AreEqual(new Vector3(0, 0, 10), cam.Position);
            Assert.AreEqual(new Vector3(0, 0, 0), cam.Target);
            Assert.AreEqual(Vector3.UnitY, cam.Up);
            Assert.AreEqual(100f, cam.WorldWidth, 1e-6f);
        }

        // ------------------------------------------------------------
        // 2. View Matrix
        // ------------------------------------------------------------
        [TestMethod]
        public void ViewMatrix_LooksDownNegativeZ()
        {
            Matrix4x4 view = cam.GetViewMatrix();

            // Transform the camera position by the view matrix → should land at origin
            Vector3 transformed = Vector3.Transform(cam.Position, view);

            Assert.AreEqual(0f, transformed.X, 1e-6f);
            Assert.AreEqual(0f, transformed.Y, 1e-6f);
            Assert.AreEqual(0f, transformed.Z, 1e-6f);
        }

        // ------------------------------------------------------------
        // 3. Projection Matrix
        // ------------------------------------------------------------
        [TestMethod]
        public void ProjectionMatrix_HasCorrectExtents()
        {
            float aspect = 16f / 9f;
            Matrix4x4 proj = cam.GetProjectionMatrix();

            float halfW = cam.WorldWidth * 0.5f;
            float halfH = halfW / aspect;

            float expectedM11 = 1f / halfW; // = 0.02 for OrthoWidth=100
            float expectedM22 = 1f / halfH;

            Assert.AreEqual(expectedM11, proj.M11, 1e-6f);
            Assert.AreEqual(expectedM22, proj.M22, 1e-6f);
        }

        // ------------------------------------------------------------
        // 4. Zoom
        // ------------------------------------------------------------
        [TestMethod]
        public void Zoom_ChangesOrthoWidth()
        {
            float original = cam.WorldWidth;

            cam.Zoom(2f); // zoom in
            Assert.IsTrue(cam.WorldWidth < original);
            var newWidth = cam.WorldWidth;
            cam.Zoom(0.5f); // zoom out
            Assert.IsTrue(cam.WorldWidth > newWidth);
        }

        // ------------------------------------------------------------
        // 5. Pan
        // ------------------------------------------------------------
        [TestMethod]
        public void Pan_MovesCameraAndTarget()
        {
            Vector3 oldPos = cam.Position;
            Vector3 oldTarget = cam.Target;

            cam.Pan(new Vector2(100, 50));

            Assert.AreNotEqual(oldPos, cam.Position);
            Assert.AreNotEqual(oldTarget, cam.Target);

            // Camera and target must move by the same delta
            Vector3 deltaPos = cam.Position - oldPos;
            Vector3 deltaTarget = cam.Target - oldTarget;

            Assert.AreEqual(deltaPos.X, deltaTarget.X, 1e-6f);
            Assert.AreEqual(deltaPos.Y, deltaTarget.Y, 1e-6f);
            Assert.AreEqual(deltaPos.Z, deltaTarget.Z, 1e-6f);
        }

        // ------------------------------------------------------------
        // 6. Pan respects aspect ratio
        // ------------------------------------------------------------
        //[TestMethod]
        //public void Pan_RespectsAspectRatio()
        //{
        //    cam.Width = 100f;

        //    // Square viewport
        //    cam.Pan(new Vector2(100, 0), 1000, 1000);
        //    float dxSquare = cam.Position.X;

        //    // Reset
        //    cam = new OrthoCamera();
        //    cam.Width = 100f;

        //    // Wide viewport
        //    cam.Pan(new Vector2(100, 0), 2000, 1000);
        //    float dxWide = cam.Position.X;

        //    // Wide viewport should move less in world units
        //    Assert.IsTrue(Math.Abs(dxWide) < Math.Abs(dxSquare));
        //}
    }
}