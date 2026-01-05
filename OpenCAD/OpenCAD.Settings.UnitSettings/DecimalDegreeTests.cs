using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCAD.Settings;

namespace OpenCAD.Settings.UnitSettingsTests
{
    [TestClass]
    public class DecimalDegreeTests
    {
        private const double TOL = 1e-12;
        private UnitSettings _unitSettings = null;

        [TestInitialize]
        public void Initialize()
        {
            _unitSettings = new UnitSettings();
        }

        [TestMethod]
        public void DecimalDegrees_Basic()
        {
            Assert.AreEqual(Math.PI / 2, _unitSettings.StringToAngle("90"), TOL);
        }

        [TestMethod]
        public void DecimalDegrees_WithDegreeSymbol()
        {
            Assert.AreEqual(Math.PI / 2, _unitSettings.StringToAngle("90°"), TOL);
        }

        [TestMethod]
        public void DecimalDegrees_WithD()
        {
            Assert.AreEqual(Math.PI / 2, _unitSettings.StringToAngle("90d"), TOL);
            Assert.AreEqual(Math.PI / 2, _unitSettings.StringToAngle("90D"), TOL);
        }

        [TestMethod]
        public void DecimalDegrees_WithDegWords()
        {
            Assert.AreEqual(Math.PI / 2, _unitSettings.StringToAngle("90deg"), TOL);
            Assert.AreEqual(Math.PI / 2, _unitSettings.StringToAngle("90 degrees"), TOL);
        }

        [TestMethod]
        public void DecimalDegrees_TrailingDecimalPoint()
        {
            Assert.AreEqual(Math.PI / 2, _unitSettings.StringToAngle("90."), TOL);
        }
    }
}