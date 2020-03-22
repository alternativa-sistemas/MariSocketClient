using MariGlobals.Event.Concrete;
using MariGlobals.Utils;
using MariSocketClient.Entities;
using MariSocketClient.Entities.MariEventArgs;
using MariSocketClient.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MariSocketClient.Clients
{
    // Based and Special Thanks for Socks https://github.com/Yucked/Socks
    public class MariWebSocketClient : IDisposable
    {
        private readonly CancellationTokenSource _ctsConnect;
        private readonly CancellationTokenSource _ctsMain;
        private readonly WebSocketConfig _config;
        private readonly ConcurrentDictionary<string, string> _headers;
        private ClientWebSocket _socketClient;

        public bool IsDisposed { get; private set; } = false;
        public bool IsConnected { get; private set; } = false;
        public TimeSpan ReconnectInterval { get; private set; } = TimeSpan.FromSeconds(0);
        public long ReconnectAttempts { get; private set; } = 0;

        private bool AlreadyStarted { get; set; } = false;

        public MariWebSocketClient(WebSocketConfig config)
        {
            _ctsConnect = new CancellationTokenSource();
            _ctsMain = new CancellationTokenSource();
            _headers = new ConcurrentDictionary<string, string>();

            ServicePointManager
                .ServerCertificateValidationCallback += (_, __, ___, ____)
                => true;

            _config = config.ValidateInstance();

            _onConnected = new AsyncEvent();
            _onDisconnected = new AsyncEvent<DisconnectedEventArgs>();
            _onError = new AsyncEvent<ErrorEventArgs>();
            _onMessage = new AsyncEvent<MessageEventArgs>();
            _onRetry = new AsyncEvent<RetryEventArgs>();
        }

        public event AsyncEventHandler OnConnected
        {
            add => _onConnected.Register(value);
            remove => _onConnected.Unregister(value);
        }

        private readonly AsyncEvent _onConnected;

        public event AsyncEventHandler<DisconnectedEventArgs> OnDisconnected
        {
            add => _onDisconnected.Register(value);
            remove => _onDisconnected.Unregister(value);
        }

        private readonly AsyncEvent<DisconnectedEventArgs> _onDisconnected;

        public event AsyncEventHandler<ErrorEventArgs> OnError
        {
            add => _onError.Register(value);
            remove => _onError.Unregister(value);
        }

        internal readonly AsyncEvent<ErrorEventArgs> _onError;

        public event AsyncEventHandler<MessageEventArgs> OnMessage
        {
            add => _onMessage.Register(value);
            remove => _onMessage.Unregister(value);
        }

        private readonly AsyncEvent<MessageEventArgs> _onMessage;

        public event AsyncEventHandler<RetryEventArgs> OnRetry
        {
            add => _onRetry.Register(value);
            remove => _onRetry.Unregister(value);
        }

        private readonly AsyncEvent<RetryEventArgs> _onRetry;

        public async Task ConnectAsync(bool blockUntilDispose = false, CancellationToken token = default)
        {
            if (IsConnected)
                return;

            if (token.HasNoContent() || !token.CanBeCanceled)
                token = _ctsConnect.Token;

            _socketClient = new ClientWebSocket();

            AlreadyStarted = false;
            AddHeaders();

            try
            {
                var uri = new Uri(_config.Url.Url);
                await _socketClient.ConnectAsync(uri, token)
                    .Try(this, true)
                    .ConfigureAwait(false);

                IsConnected = true;

                if (blockUntilDispose)
                {
                    await _onConnected.InvokeAsync()
                        .Try(this)
                        .ConfigureAwait(false);
                }
                else
                {
                    _ = Task.Run(async () =>
                    {
                        await _onConnected.InvokeAsync()
                        .Try(this)
                        .ConfigureAwait(false);
                    });
                }

                ReconnectAttempts = 0;
                ReconnectInterval = TimeSpan.FromSeconds(0);

                if (blockUntilDispose)
                {
                    await ReceiveAsync()
                        .Try(this, true)
                        .ConfigureAwait(false);
                }
                else
                {
                    _ = Task.Run(async () =>
                    {
                        await NewReceiveAsync()
                        .ConfigureAwait(false);
                    });
                }
            }
            catch (Exception ex)
            {
                await HandleExAsync(ex)
                    .ConfigureAwait(false);
            }
        }

        private async Task NewReceiveAsync()
        {
            try
            {
                await ReceiveAsync()
                            .Try(this, true)
                            .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await HandleExAsync(ex)
                    .ConfigureAwait(false);
            }
        }

        private async Task HandleExAsync(Exception ex)
        {
            if (ex is TaskCanceledException)
                return;

            await _onDisconnected.InvokeAsync(
                new DisconnectedEventArgs(WebSocketCloseStatus.ProtocolError, ex.Message))
                .Try(this)
                .ConfigureAwait(false);

            IsConnected = false;

            await ReconnectAsync()
                .Try(this)
                .ConfigureAwait(false);
        }

        public async Task DisconnectAsync()
        {
            if (!_socketClient.State.Equals(WebSocketState.Open))
                return;

            await _socketClient.CloseAsync(WebSocketCloseStatus.NormalClosure,
                "Close requested by client.", _ctsMain.Token)
                .Try(this)
                .ConfigureAwait(false);

            IsConnected = false;
        }

        public Task SendAsync(object obj)
            => SendAsync(JsonConvert.SerializeObject(obj));

        public async Task SendAsync(string message)
        {
            if (!_socketClient.State.Equals(WebSocketState.Open))
                return;

            await _socketClient.SendAsync(Encoding.UTF8.GetBytes(message),
                WebSocketMessageType.Text, true, _ctsMain.Token)
                .Try(this)
                .ConfigureAwait(false);
        }

        private async Task ReceiveAsync()
        {
            while (_socketClient.State.Equals(WebSocketState.Open))
            {
                var bytes = new byte[_config.BufferSize];
                var result = await _socketClient.ReceiveAsync(bytes, _ctsMain.Token)
                        .ConfigureAwait(false);

                await ReadMessageAsync(result, bytes).ConfigureAwait(false);
            }
        }

        private async Task ReadMessageAsync(WebSocketReceiveResult result, byte[] buffer)
        {
            if (!result.EndOfMessage)
                return;

            if (result.MessageType.Equals(WebSocketMessageType.Text))
            {
                await _onMessage.InvokeAsync(new MessageEventArgs(Encoding.UTF8.GetString(buffer)))
                    .ConfigureAwait(false);
            }
            else if (result.MessageType.Equals(WebSocketMessageType.Close))
            {
                await _onDisconnected.InvokeAsync(
                    new DisconnectedEventArgs(result.CloseStatus.Value, Encoding.UTF8.GetString(buffer)))
                    .ConfigureAwait(false);

                if (!_config.AutoReconnect)
                {
                    if (_socketClient.State.Equals(WebSocketState.Open))
                        await _socketClient.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "Closed by remote", _ctsMain.Token)
                            .ConfigureAwait(false);

                    return;
                }
                else
                {
                    await ReconnectAsync()
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task ReconnectAsync()
        {
            if (!_config.AutoReconnect || IsConnected)
                return;

            if (ReconnectAttempts > _config.MaxReconnectAttempts && _config.MaxReconnectAttempts >= 0)
                return;

            if (ReconnectAttempts == _config.MaxReconnectAttempts)
            {
                await _onRetry.InvokeAsync(new RetryEventArgs($"Reached the max {nameof(ReconnectAttempts)}."))
                        .Try(this)
                        .ConfigureAwait(false);

                ReconnectAttempts++;
                return;
            }

            ReconnectInterval = ReconnectInterval.Add(_config.ReconnectInterval);
            ReconnectAttempts++;

            await _onRetry.InvokeAsync(new RetryEventArgs(ReconnectAttempts, ReconnectInterval))
                .Try(this)
                .ConfigureAwait(false);

            await Task.Delay(ReconnectInterval)
                .ConfigureAwait(false);

            using var ctsSource
                = new CancellationTokenSource(Convert.ToInt32(_config.ConnectionTimeOut.TotalMilliseconds));

            await ConnectAsync(true, ctsSource.Token)
               .Try(this)
               .ConfigureAwait(false);
        }

        private void AddHeaders()
        {
            if (AlreadyStarted)
                return;

            AlreadyStarted = true;

            foreach (var header in _headers)
                _socketClient.Options.SetRequestHeader(header.Key, header.Value);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            if (!_ctsConnect?.IsCancellationRequested == true)
                _ctsConnect?.Cancel(false);
            _ctsConnect?.Dispose();

            if (!_ctsMain?.IsCancellationRequested == true)
                _ctsMain?.Cancel(false);
            _ctsMain?.Dispose();

            try
            {
                _socketClient?.Abort();
            }
            catch { }

            _socketClient?.Dispose();

            _headers?.Clear();
        }

        public bool TryAddHeader(string key, string value)
            => _headers.TryAdd(key, value);

        public void ClearHeaders()
            => _headers.Clear();

        public bool TryRemoveHeader(string key, out string value)
            => _headers.TryRemove(key, out value);
    }
}