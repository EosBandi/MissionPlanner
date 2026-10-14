using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MissionPlanner;
using MPBackend.Core.Telemetry;

namespace MPBackend.Tests
{
    [TestClass]
    public class TelemetryFieldsTests
    {
        [TestMethod]
        public void ExposesCoreFieldsWithMetadata()
        {
            Assert.IsTrue(TelemetryFields.All.Count > 300, $"only {TelemetryFields.All.Count} fields");
            var roll = TelemetryFields.All.Single(f => f.Name == "roll");
            Assert.AreEqual("number", roll.Type);
            Assert.AreEqual("Attitude", roll.Group);
            Assert.IsTrue(TelemetryFields.Exists("armed"));
        }

        [TestMethod]
        public void SnapshotSelectsFieldsAndIgnoresUnknown()
        {
            var cs = new CurrentState { mode = "Guided", roll = 12.5f };
            var snap = TelemetryFields.Snapshot(cs, new[] { "mode", "roll", "nope" });
            Assert.AreEqual(2, snap.Count);
            Assert.AreEqual("Guided", snap["mode"]);
            Assert.AreEqual(12.5f, snap["roll"]);
        }
    }
}
