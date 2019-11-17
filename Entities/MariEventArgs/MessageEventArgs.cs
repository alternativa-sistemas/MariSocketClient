using System;
using System.Collections.Generic;
using System.Text;

namespace MariSocketClient.Entities.MariEventArgs
{
    public class MessageEventArgs : EventArgs
    {
        internal MessageEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; set; }
    }
}