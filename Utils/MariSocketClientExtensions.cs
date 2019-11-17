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
            try
            {
                await task
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await HandleExceptionFromTryAsync(ex, socket, cancel)
                    .ConfigureAwait(false);
            }
        }

        internal static async Task<TResult> Try<TResult>
            (this Task<TResult> task, MariWebSocketClient socket, bool cancel = false)
        {
            try
            {
                var memResult =
                    new ReadOnlyMemory<TResult>(new TResult[] { await task.ConfigureAwait(false) });

                return memResult.Span[0];
            }
            catch (Exception ex)
            {
                await HandleExceptionFromTryAsync(ex, socket, cancel)
                    .ConfigureAwait(false);
            }

            return default;
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