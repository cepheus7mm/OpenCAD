using OpenCAD.Settings;

namespace OpenCAD.Settings.UnitSettingsTests
{
    [TestClass]
    public class RadianTests
    {
        private const double TOL = 1e-12;
        private UnitSettings _unitSettings = null;

        [TestInitialize]
        public void Initialize()
        {
            _unitSettings = new UnitSettings();
        }

        [TestMethod]
        public void Radians_WithR()
        {
            Assert.AreEqual(2.0, _unitSettings.StringToAngle("2R"), TOL);
        }

        [TestMethod]
        public void Radians_WithRad()
        {
            Assert.AreEqual(2.1, _unitSettings.StringToAngle("2.1rad"), TOL);
        }

        [TestMethod]
        public void Radians_WithWords()
        {
            Assert.AreEqual(3.14, _unitSettings.StringToAngle("3.14 radians"), TOL);
        }

        [TestMethod]
        public void Radians_Pi()
        {
            Assert.AreEqual(Math.PI, _unitSettings.StringToAngle("pi"), TOL);
            Assert.AreEqual(Math.PI, _unitSettings.StringToAngle("π"), TOL);
        }

        [TestMethod]
        public void Radians_PiFractions()
        {
            Assert.AreEqual(Math.PI / 2, _unitSettings.StringToAngle("pi/2"), TOL);
            Assert.AreEqual(3 * Math.PI / 2, _unitSettings.StringToAngle("3pi/2"), TOL);
            Assert.AreEqual(2 * Math.PI, _unitSettings.StringToAngle("2*pi"), TOL);
        }
    }
}