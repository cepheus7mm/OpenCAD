using OpenCAD.Settings;
namespace OpenCAD.Settings.UnitSettingsTests
{

    [TestClass]
    public class BearingTests
    {
        private const double TOL = 1e-12;
        private UnitSettings _unitSettings = null;

        [TestInitialize]
        public void Initialize()
        {
            _unitSettings = new UnitSettings();
        }

        [TestMethod]
        public void Bearing_NE()
        {
            Assert.AreEqual(Math.PI / 4, _unitSettings.StringToAngle("N45E"), TOL);
        }

        [TestMethod]
        public void Bearing_SE()
        {
            Assert.AreEqual(Math.PI - Math.PI / 4, _unitSettings.StringToAngle("S45E"), TOL);
        }

        [TestMethod]
        public void Bearing_SW()
        {
            Assert.AreEqual(Math.PI + Math.PI / 4, _unitSettings.StringToAngle("S45W"), TOL);
        }

        [TestMethod]
        public void Bearing_NW()
        {
            Assert.AreEqual(2 * Math.PI - Math.PI / 4, _unitSettings.StringToAngle("N45W"), TOL);
        }

        [TestMethod]
        public void Bearing_WithDms()
        {
            Assert.AreEqual(Math.PI / 4, _unitSettings.StringToAngle("N 45° 0' 0\" E"), TOL);
        }
    }
}