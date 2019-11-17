using System;
using System.Collections.Generic;
using System.Text;

namespace MariSocketClient.Entities.MariEventArgs
{
    public class RetryEventArgs : EventArgs
    {
        internal RetryEventArgs(string message)
        {
            Message = message;
        }

        internal RetryEventArgs(long attempt, TimeSpan interval)
        {
            Message = $"Reconnect attempt #{attempt}. " +
                $"Waiting {interval.TotalSeconds}s for the next retry.";
        }

        public string Message { get; }
    }
}