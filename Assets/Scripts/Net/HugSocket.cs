using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Hug
{
    /// <summary>
    /// Tiny cross-platform WebSocket client.
    ///
    /// In a WebGL build the browser owns the socket: a .jslib plugin opens it and
    /// calls back into OnWsOpen/OnWsMessage/... via SendMessage. Everywhere else
    /// (editor, standalone) we use System.Net.WebSockets on a background task.
    ///
    /// All callbacks are funnelled through a queue and dispatched on the main
    /// thread from Update(), so handlers can touch Unity objects freely.
    /// </summary>
    public class HugSocket : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Connecting,
            Open,
            Closed,
            Failed,
        }

        public State Current { get; private set; } = State.Idle;
        public string LastError { get; private set; }

        public event Action Opened;
        public event Action<string> Received;
        public event Action<string> Failed;
        public event Action<int> Closed;

        readonly ConcurrentQueue<Action> _events = new ConcurrentQueue<Action>();

#if UNITY_WEBGL && !UNITY_EDITOR

        [DllImport("__Internal")] static extern void HugWS_Connect(string url, string go);
        [DllImport("__Internal")] static extern void HugWS_Send(string msg);
        [DllImport("__Internal")] static extern void HugWS_Close();

        public void Connect(string url)
        {
            if (Current == State.Connecting || Current == State.Open) return;
            Current = State.Connecting;
            LastError = null;
            HugWS_Connect(url, gameObject.name);
        }

        public void Send(string json)
        {
            if (Current == State.Open) HugWS_Send(json);
        }

        public void Close()
        {
            try { HugWS_Close(); } catch (Exception) { /* already gone */ }
            Current = State.Closed;
        }

        // --- called from Assets/Plugins/WebGL/HugWebSocket.jslib -----------------

        public void OnWsOpen(string _)
        {
            _events.Enqueue(() =>
            {
                if (Current == State.Closed) return;
                Current = State.Open;
                Opened?.Invoke();
            });
        }

        public void OnWsMessage(string json)
        {
            _events.Enqueue(() => Received?.Invoke(json));
        }

        public void OnWsError(string message)
        {
            _events.Enqueue(() =>
            {
                LastError = string.IsNullOrEmpty(message) ? "Connection failed" : message;
                Current = State.Failed;
                Failed?.Invoke(LastError);
            });
        }

        public void OnWsClose(string code)
        {
            int c;
            int.TryParse(code, out c);
            _events.Enqueue(() =>
            {
                Current = State.Closed;
                Closed?.Invoke(c);
            });
        }

#else

        ClientWebSocket _ws;
        CancellationTokenSource _cts;
        readonly ConcurrentQueue<string> _outbox = new ConcurrentQueue<string>();
        readonly SemaphoreSlim _outSignal = new SemaphoreSlim(0);

        public void Connect(string url)
        {
            if (Current == State.Connecting || Current == State.Open) return;
            Current = State.Connecting;
            LastError = null;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _ = Task.Run(() => ConnectAsync(url, token));
        }

        async Task ConnectAsync(string url, CancellationToken token)
        {
            try
            {
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(url), token);
                _events.Enqueue(() =>
                {
                    Current = State.Open;
                    Opened?.Invoke();
                });
                _ = Task.Run(ReceiveLoop);
                _ = Task.Run(SendLoop);
            }
            catch (Exception e)
            {
                string message = e.Message;
                _events.Enqueue(() =>
                {
                    LastError = message;
                    Current = State.Failed;
                    Failed?.Invoke(message);
                });
            }
        }

        public void Send(string json)
        {
            if (Current != State.Open) return;
            _outbox.Enqueue(json);
            _outSignal.Release();
        }

        public void Close()
        {
            try
            {
                _cts?.Cancel();
                _ws?.Abort();
            }
            catch (Exception)
            {
                // ignore
            }
            Current = State.Closed;
        }

        async Task SendLoop()
        {
            while (_cts != null && !_cts.IsCancellationRequested)
            {
                try
                {
                    await _outSignal.WaitAsync(_cts.Token);
                }
                catch (Exception)
                {
                    return;
                }

                while (_outbox.TryDequeue(out var json))
                {
                    try
                    {
                        var bytes = Encoding.UTF8.GetBytes(json);
                        await _ws.SendAsync(
                            new ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text,
                            true,
                            _cts.Token);
                    }
                    catch (Exception)
                    {
                        return;
                    }
                }
            }
        }

        async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            var builder = new StringBuilder();

            while (_ws != null && _ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                }
                catch (Exception)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    int code = (int)result.CloseStatus.GetValueOrDefault(WebSocketCloseStatus.NormalClosure);
                    _events.Enqueue(() =>
                    {
                        Current = State.Closed;
                        Closed?.Invoke(code);
                    });
                    return;
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (result.EndOfMessage)
                {
                    string json = builder.ToString();
                    builder.Length = 0;
                    _events.Enqueue(() => Received?.Invoke(json));
                }
            }

            _events.Enqueue(() =>
            {
                if (Current != State.Failed)
                {
                    Current = State.Closed;
                    Closed?.Invoke(0);
                }
            });
        }

#endif

        void Update()
        {
            while (_events.TryDequeue(out var evt)) evt();
        }

        void OnDestroy()
        {
            if (Current == State.Open || Current == State.Connecting) Close();
        }
    }
}
