using MariGlobals.Class.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MariSocketClient.Entities
{
    public struct SocketEndpoint
    {
        public string Host { get; }

        public int? Port { get; }

        public bool Ssl { get; }

        public string Url { get; private set; }

        public SocketEndpoint(string host, bool ssl = false, int? port = null)
        {
            Host = host ??
                throw new ArgumentNullException(nameof(host));

            Port = port;
            Ssl = ssl;

            Url =
                $"{(ssl ? "wss" : "ws")}" +
                $"://{host}" +
                $"{(port.HasValue ? $":{port.Value}" : "")}";
        }

        public SocketEndpoint(int port, bool ssl = false)
        {
            Host = "localhost";
            Port = port;
            Ssl = ssl;

            Url =
                $"{(ssl ? "wss" : "ws")}" +
                $"://localhost" +
                $":{port}";
        }

        public SocketEndpoint(bool ssl = false)
        {
            Host = "localhost";
            Ssl = ssl;
            Port = null;

            Url =
                $"{(ssl ? "wss" : "ws")}" +
                $"://localhost";
        }

        public override string ToString()
            => Url;

        public SocketEndpoint WithPath(string path)
        {
            path = path.Replace("/", "");

            if (Url.EndsWith("/"))
                Url += $"{path}";
            else
                Url += $"/{path}";

            return this;
        }

        public SocketEndpoint WithParam(string key, string value)
        {
            if (Url.Contains("?"))
                Url += $"&{key}={value}";
            else
                Url += $"?{key}={value}";

            return this;
        }

        public Uri ToUri()
            => new Uri(Url);
    }
}