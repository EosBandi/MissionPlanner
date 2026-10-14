using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPBackend.Core;

namespace MPBackend.Tests
{
    [TestClass]
    public static class TestEnvironment
    {
        public static readonly string DataDirectory = Path.Combine(Path.GetTempPath(), "mpbackend-tests");

        /// <summary>
        /// Point MP's static Settings at a temp folder before any test runs, so tests never read or write a real
        /// Mission Planner config.xml.
        /// </summary>
        [AssemblyInitialize]
        public static void Init(TestContext _) =>
            HeadlessHost.Initialise(new BackendOptions { DataDirectory = DataDirectory });
    }
}
