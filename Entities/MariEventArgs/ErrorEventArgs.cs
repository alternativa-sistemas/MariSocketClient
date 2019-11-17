using System;
using System.Collections.Generic;
using System.Text;

namespace MariSocketClient.Entities.MariEventArgs
{
    public class ErrorEventArgs : EventArgs
    {
        internal ErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }

        public Exception Exception { get; set; }
    }
}