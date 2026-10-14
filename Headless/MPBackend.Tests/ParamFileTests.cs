using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPBackend.Core.Params;

namespace MPBackend.Tests
{
    [TestClass]
    public class ParamFileTests
    {
        [TestMethod]
        public void ParsesMissionPlannerMavproxyAndQgcFormats()
        {
            var mp = ParamService.ParseParamFile("# comment\nRTL_ALT_M,15\nFRAME_CLASS,1\n");
            Assert.AreEqual(15, mp["RTL_ALT_M"]);
            Assert.AreEqual(1, mp["FRAME_CLASS"]);

            var mavproxy = ParamService.ParseParamFile("RTL_ALT_M 15.5\nFRAME_CLASS     2\n");
            Assert.AreEqual(15.5, mavproxy["RTL_ALT_M"], 1e-6);
            Assert.AreEqual(2, mavproxy["FRAME_CLASS"]);

            var qgc = ParamService.ParseParamFile("# Vehicle-Id Component-Id Name Value Type\n1\t1\tRTL_ALT_M\t12\t9\n");
            Assert.AreEqual(12, qgc["RTL_ALT_M"]);
        }
    }
}
