using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpaceApps.Bus
{
    /// <summary>
    /// Cliente WebSocket del bus AEGIS (PC 1 · motor FastAPI). Usa System.Net.WebSockets (sin paquetes).
    /// Se conecta con el subprotocolo indicado (aegis.v1), reconecta solo si se cae la red
    /// y deja cada mensaje de texto en una cola que Unity lee desde el hilo principal.
    /// Funciona en el Editor y en builds de escritorio (no en WebGL).
    /// </summary>
    public class AegisBusClient : IDisposable
    {
        public enum State { Disconnected, Connecting, Connected, Error }

        public State Status { get; private set; } = State.Disconnected;
        public string LastError { get; private set; } = "";
        public string NegotiatedProtocol { get; private set; } = "";
        public int Received { get; private set; }

        readonly ConcurrentQueue<string> _inbox = new ConcurrentQueue<string>();
        CancellationTokenSource _cts;
        ClientWebSocket _ws;

        public void Connect(string url, string subprotocol, string helloMessage = null)
        {
            Disconnect();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(() => Loop(url, subprotocol, helloMessage, token));
        }

        async Task Loop(string url, string subprotocol, string hello, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Status = State.Connecting;
                    _ws = new ClientWebSocket();
                    if (!string.IsNullOrEmpty(subprotocol)) _ws.Options.AddSubProtocol(subprotocol);
                    _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(6));
                        await _ws.ConnectAsync(new Uri(url), timeout.Token);
                    }
                    NegotiatedProtocol = _ws.SubProtocol ?? "";
                    Status = State.Connected;
                    LastError = "";
                    if (!string.IsNullOrEmpty(hello))
                        await _ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(hello)), WebSocketMessageType.Text, true, token);
                    await ReceiveLoop(token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception e)
                {
                    LastError = e.InnerException?.Message ?? e.Message;
                    Status = State.Error;
                }
                finally
                {
                    try { _ws?.Dispose(); } catch { /* ignorar */ }
                }
                if (token.IsCancellationRequested) break;
                try { await Task.Delay(2000, token); } catch { break; }   // reintento
            }
            Status = State.Disconnected;
        }

        async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[64 * 1024];
            var sb = new StringBuilder();
            while (_ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var r = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (r.MessageType == WebSocketMessageType.Close)
                {
                    LastError = $"el servidor cerró la conexión ({r.CloseStatus} {r.CloseStatusDescription})";
                    Status = State.Error;
                    return;
                }
                sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                if (!r.EndOfMessage) continue;
                if (r.MessageType == WebSocketMessageType.Text)
                {
                    _inbox.Enqueue(sb.ToString());
                    Received++;
                    while (_inbox.Count > 200) _inbox.TryDequeue(out _);   // no acumular si Unity se pausa
                }
                sb.Clear();
            }
        }

        public bool TryDequeue(out string message) => _inbox.TryDequeue(out message);

        public void Send(string text)
        {
            var ws = _ws;
            if (ws == null || ws.State != WebSocketState.Open) return;
            ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public void Disconnect()
        {
            try { _cts?.Cancel(); } catch { /* ignorar */ }
            try { _ws?.Abort(); } catch { /* ignorar */ }
            _cts = null;
            Status = State.Disconnected;
        }

        public void Dispose() => Disconnect();
    }
}
