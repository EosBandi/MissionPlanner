using System;
using System.Globalization;

namespace MPBackend.Core.Links
{
    public enum LinkType
    {
        /// <summary>Serial / USB COM port.</summary>
        Serial,
        /// <summary>TCP client to host:port (e.g. SITL, ser2net).</summary>
        Tcp,
        /// <summary>UDP server listening on a local port (vehicle sends to us).</summary>
        Udp,
        /// <summary>UDP client sending to host:port.</summary>
        UdpClient,
        /// <summary>WebSocket client to a URL.</summary>
        WebSocket
    }

    /// <summary>
    /// Describes how to open a MAVLink link. Mirrors the choices on Mission Planner's connect bar.
    /// </summary>
    public sealed class ConnectionRequest
    {
        public LinkType Type { get; set; }

        /// <summary>Serial: COM port name. TCP/UDP client: remote port. UDP: local listen port.</summary>
        public string Port { get; set; }

        /// <summary>TCP / UDP client remote host.</summary>
        public string Host { get; set; }

        /// <summary>Serial baud rate.</summary>
        public int Baud { get; set; } = 115200;

        /// <summary>WebSocket URL.</summary>
        public string Url { get; set; }

        /// <summary>Download the parameter list after connecting.</summary>
        public bool FetchParams { get; set; } = true;

        /// <summary>
        /// Parse a compact connection string:
        /// <c>tcp:host:port</c>, <c>udp:port</c>, <c>udpcl:host:port</c>, <c>serial:COM3:57600</c>,
        /// <c>COM3:57600</c>, <c>ws:ws://host/path</c>.
        /// </summary>
        public static ConnectionRequest Parse(string connection)
        {
            if (string.IsNullOrWhiteSpace(connection))
                throw new FormatException("empty connection string");

            var parts = connection.Trim().Split(':');
            var scheme = parts[0].ToLowerInvariant();

            switch (scheme)
            {
                case "tcp":
                    Require(parts, 3, "tcp:host:port");
                    return new ConnectionRequest { Type = LinkType.Tcp, Host = parts[1], Port = CheckPort(parts[2]) };
                case "udp":
                    Require(parts, 2, "udp:port");
                    return new ConnectionRequest { Type = LinkType.Udp, Port = CheckPort(parts[parts.Length - 1]) };
                case "udpcl":
                case "udpclient":
                    Require(parts, 3, "udpcl:host:port");
                    return new ConnectionRequest { Type = LinkType.UdpClient, Host = parts[1], Port = CheckPort(parts[2]) };
                case "ws":
                case "wss":
                    var url = connection.Substring(connection.IndexOf(':') + 1);
                    if (!url.Contains("://"))
                        url = connection; // "ws://host/path" given directly
                    return new ConnectionRequest { Type = LinkType.WebSocket, Url = url };
                case "serial":
                    Require(parts, 2, "serial:COMx[:baud]");
                    return Serial(parts[1], parts.Length > 2 ? parts[2] : null);
                default:
                    if (scheme.StartsWith("com") || connection.StartsWith("/dev/"))
                        return Serial(parts[0], parts.Length > 1 ? parts[1] : null);
                    throw new FormatException($"unknown connection type '{parts[0]}'");
            }
        }

        public override string ToString()
        {
            switch (Type)
            {
                case LinkType.Serial: return $"serial:{Port}:{Baud}";
                case LinkType.Tcp: return $"tcp:{Host}:{Port}";
                case LinkType.Udp: return $"udp:{Port}";
                case LinkType.UdpClient: return $"udpcl:{Host}:{Port}";
                case LinkType.WebSocket: return $"ws:{Url}";
                default: return Type.ToString();
            }
        }

        private static ConnectionRequest Serial(string port, string baud)
        {
            var req = new ConnectionRequest { Type = LinkType.Serial, Port = port };
            if (!string.IsNullOrEmpty(baud))
                req.Baud = int.Parse(baud, CultureInfo.InvariantCulture);
            return req;
        }

        private static void Require(string[] parts, int count, string format)
        {
            if (parts.Length < count)
                throw new FormatException($"expected {format}");
        }

        private static string CheckPort(string port)
        {
            if (!int.TryParse(port, out var p) || p <= 0 || p > 65535)
                throw new FormatException($"invalid port '{port}'");
            return port;
        }
    }
}
