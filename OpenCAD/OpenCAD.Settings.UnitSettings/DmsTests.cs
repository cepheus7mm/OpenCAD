using OpenCAD.Settings;

namespace OpenCAD.Settings.UnitSettingsTests
{
    [TestClass]
    public class DmsTests
    {
        private const double TOL = 1e-12;
        private UnitSettings _unitSettings = null;

        [TestInitialize]
        public void Initialize()
        {
            _unitSettings = new UnitSettings();
        }

        [TestMethod]
        public void Dms_StandardSymbols()
        {
            Assert.AreEqual(
                (90 + 30.0 / 60 + 43.5 / 3600) * Math.PI / 180,
                _unitSettings.StringToAngle("90°30'43.5\""),
                TOL);
        }

        [TestMethod]
        public void Dms_CompactLowercase()
        {
            Assert.AreEqual(
                (90 + 30.0 / 60 + 43.5 / 3600) * Math.PI / 180,
                _unitSettings.StringToAngle("90d30m43.5s"),
                TOL);
        }

        [TestMethod]
        public void Dms_CompactUppercase()
        {
            Assert.AreEqual(
                (90 + 30.0 / 60 + 43.5 / 3600) * Math.PI / 180,
                _unitSettings.StringToAngle("90D30M43.5S"),
                TOL);
        }

        [TestMethod]
        public void Dms_DegreesOnly()
        {
            Assert.AreEqual(90 * Math.PI / 180, _unitSettings.StringToAngle("90°"), TOL);
        }
    }
}