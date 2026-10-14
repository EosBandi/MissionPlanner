using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPBackend.Core.Links;

namespace MPBackend.Tests
{
    [TestClass]
    public class ConnectionRequestTests
    {
        [TestMethod]
        public void ParsesTcp()
        {
            var r = ConnectionRequest.Parse("tcp:127.0.0.1:5760");
            Assert.AreEqual(LinkType.Tcp, r.Type);
            Assert.AreEqual("127.0.0.1", r.Host);
            Assert.AreEqual("5760", r.Port);
            Assert.AreEqual("tcp:127.0.0.1:5760", r.ToString());
        }

        [TestMethod]
        public void ParsesUdpListenAndClient()
        {
            var udp = ConnectionRequest.Parse("udp:14550");
            Assert.AreEqual(LinkType.Udp, udp.Type);
            Assert.AreEqual("14550", udp.Port);

            var cl = ConnectionRequest.Parse("udpcl:192.168.4.1:14555");
            Assert.AreEqual(LinkType.UdpClient, cl.Type);
            Assert.AreEqual("192.168.4.1", cl.Host);
            Assert.AreEqual("14555", cl.Port);
        }

        [TestMethod]
        public void ParsesSerialWithAndWithoutPrefix()
        {
            var a = ConnectionRequest.Parse("COM7:57600");
            Assert.AreEqual(LinkType.Serial, a.Type);
            Assert.AreEqual("COM7", a.Port);
            Assert.AreEqual(57600, a.Baud);

            var b = ConnectionRequest.Parse("serial:COM3");
            Assert.AreEqual("COM3", b.Port);
            Assert.AreEqual(115200, b.Baud);
        }

        [TestMethod]
        public void ParsesWebSocketUrl()
        {
            Assert.AreEqual("ws://10.0.0.2:5761/mav", ConnectionRequest.Parse("ws:ws://10.0.0.2:5761/mav").Url);
            Assert.AreEqual("ws://10.0.0.2/mav", ConnectionRequest.Parse("ws://10.0.0.2/mav").Url);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("tcp:host")]
        [DataRow("tcp:host:99999")]
        [DataRow("bogus:1")]
        public void RejectsInvalid(string connection)
        {
            Assert.ThrowsException<FormatException>(() => ConnectionRequest.Parse(connection));
        }
    }
}
