using MariGlobals.Class.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MariSocketClient.Entities
{
    public struct WebSocketConfig
    {
        public SocketEndpoint Url { get; set; }

        public ushort BufferSize { get; set; }

        public bool AutoReconnect { get; set; }

        public TimeSpan ReconnectInterval { get; set; }

        /// <summary>
        /// The max of reconnect attempts, set -1 if infinite.
        /// </summary>
        public long MaxReconnectAttempts { get; set; }

        public TimeSpan ConnectionTimeOut { get; set; }

        internal WebSocketConfig ValidateInstance()
        {
            if (Url.HasNoContent())
                throw new ArgumentNullException(nameof(Url));

            if (BufferSize.HasNoContent())
                BufferSize = 512;

            if (AutoReconnect.HasNoContent())
                AutoReconnect = false;

            if (ReconnectInterval.HasNoContent())
                ReconnectInterval = TimeSpan.FromSeconds(10);

            if (MaxReconnectAttempts.HasNoContent())
                MaxReconnectAttempts = -1;

            if (ConnectionTimeOut.HasNoContent())
                ConnectionTimeOut = TimeSpan.FromSeconds(30);

            return this;
        }
    }
}