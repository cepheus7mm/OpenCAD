using OpenCAD.Settings;

namespace OpenCAD.Settings.UnitSettingsTests
{
    [TestClass]
    public class GradianTests
    {
        private const double TOL = 1e-12;
        private UnitSettings _unitSettings = null;

        [TestInitialize]
        public void Initialize()
        {
            _unitSettings = new UnitSettings();
        }

        [TestMethod]
        public void Gradians_Gon()
        {
            Assert.AreEqual(150 * Math.PI / 200, _unitSettings.StringToAngle("150gon"), TOL);
        }

        [TestMethod]
        public void Gradians_G()
        {
            Assert.AreEqual(150 * Math.PI / 200, _unitSettings.StringToAngle("150g"), TOL);
        }

        [TestMethod]
        public void Gradians_Words()
        {
            Assert.AreEqual(150 * Math.PI / 200, _unitSettings.StringToAngle("150gradians"), TOL);
        }
    }
}