using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;

namespace MariSocketClient.Entities.MariEventArgs
{
    public class DisconnectedEventArgs : EventArgs
    {
        internal DisconnectedEventArgs(WebSocketCloseStatus code, string reason)
        {
            Code = code;
            Reason = reason;
        }

        public WebSocketCloseStatus Code { get; }

        public string Reason { get; set; }
    }
}