using MariGlobals.Class.Utils;
using MariSocketClient.Clients;
using MariSocketClient.Entities.MariEventArgs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MariSocketClient.Utils
{
    public static class MariSocketClientExtensions
    {
        internal static async Task Try
            (this Task task, MariWebSocketClient socket, bool cancel = false)
        {
            await task
                .TryAsync((ex) => HandleExceptionFromTryAsync(ex, socket, cancel))
                .ConfigureAwait(false);
        }

        internal static async Task<TResult> Try<TResult>
            (this Task<TResult> task, MariWebSocketClient socket, bool cancel = false)
        {
            return await task
                .TryAsync((ex) => HandleExceptionFromTryAsync(ex, socket, cancel))
                .ConfigureAwait(false);
        }

        private static async Task HandleExceptionFromTryAsync
            (Exception ex, MariWebSocketClient socket, bool cancel)
        {
            await socket._onError.InvokeAsync(new ErrorEventArgs(ex))
                .ConfigureAwait(false);

            if (cancel)
                throw ex;
        }
    }
}