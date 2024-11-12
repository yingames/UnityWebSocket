#if NET_LEGACY
#error .NET Runtime is Legacy.
/* https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket
System.Net.WebSockets.ClientWebSocket Applies to Product	Versions
.NET	Core 1.0, Core 1.1, Core 2.0, Core 2.1, Core 2.2, Core 3.0, Core 3.1, 5, 6, 7, 8, 9
.NET Framework	4.5, 4.5.1, 4.5.2, 4.6, 4.6.1, 4.6.2, 4.7, 4.7.1, 4.7.2, 4.8, 4.8.1
.NET Standard	2.0, 2.1
*/
#elif !(UNITY_WEBGL && !UNITY_EDITOR) && !FORCE_WEBGL_IMPL_ENABLE

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.IO;
using System.Collections.Concurrent;

namespace UnityWebSocket
{
    public class WebSocket : IWebSocket
    {
        public string Address { get; private set; }
        public string[] SubProtocols { get; private set; }

        public WebSocketState ReadyState
        {
            get
            {
                if (socket == null)
                    return WebSocketState.Closed;
                switch (socket.State)
                {
                    case System.Net.WebSockets.WebSocketState.Closed:
                    case System.Net.WebSockets.WebSocketState.None:
                        return WebSocketState.Closed;
                    case System.Net.WebSockets.WebSocketState.CloseReceived:
                    case System.Net.WebSockets.WebSocketState.CloseSent:
                        return WebSocketState.Closing;
                    case System.Net.WebSockets.WebSocketState.Connecting:
                        return WebSocketState.Connecting;
                    case System.Net.WebSockets.WebSocketState.Open:
                        return WebSocketState.Open;
                }
                return WebSocketState.Closed;
            }
        }

        public event EventHandler<OpenEventArgs> OnOpen;
        public event EventHandler<CloseEventArgs> OnClose;
        public event EventHandler<ErrorEventArgs> OnError;
        public event EventHandler<PooledBuffer> OnMessage;

        private ClientWebSocket socket;
        private bool isOpening => socket != null && socket.State == System.Net.WebSockets.WebSocketState.Open;
        private ConcurrentQueue<PooledBuffer> sendQueue = new ConcurrentQueue<PooledBuffer>();
        private ConcurrentQueue<EventArgs> eventQueue = new ConcurrentQueue<EventArgs>();
        private bool closeProcessing;
        private CancellationTokenSource cts = null;

        #region APIs 
        public WebSocket(string address)
        {
            this.Address = address;
        }

        public WebSocket(string address, string subProtocol)
        {
            this.Address = address;
            this.SubProtocols = new string[] { subProtocol };
        }

        public WebSocket(string address, string[] subProtocols)
        {
            this.Address = address;
            this.SubProtocols = subProtocols;
        }

        public void ConnectAsync()
        {
            if (socket != null)
            {
                HandleError(new Exception("Socket is busy."));
                return;
            }

            WebSocketManager.Instance.Add(this);

            socket = new ClientWebSocket();
            cts = new CancellationTokenSource();

            // support sub protocols
            if (this.SubProtocols != null)
            {
                foreach (var protocol in this.SubProtocols)
                {
                    if (string.IsNullOrEmpty(protocol)) continue;
                    Log($"Add Sub Protocol {protocol}");
                    socket.Options.AddSubProtocol(protocol);
                }
            }

            Task.Run(ConnectTask);
        }

        public void CloseAsync()
        {
            if (!isOpening) return;
            closeProcessing = true;
        }

        public void SendAsync(PooledBuffer buffer)
        {
            if (!isOpening) return;
            sendQueue.Enqueue(buffer);
        }

        public void SendAsync(byte[] data)
        {
            if (!isOpening) return;
            var buffer = PooledBuffer.Create(Opcode.Binary, data);
            sendQueue.Enqueue(buffer);
        }

        public void SendAsync(string text)
        {
            if (!isOpening) return;
            var data = Encoding.UTF8.GetBytes(text);
            var buffer = PooledBuffer.Create(Opcode.Text, data);
            sendQueue.Enqueue(buffer);
        }

        #endregion

        private void CleanSendQueue()
        {
            while (sendQueue.TryDequeue(out var buffer))
            {
                buffer.Dispose();
            }
        }

        private void CleanEventQueue()
        {
            while (eventQueue.TryDequeue(out var e))
            {
                if (e is PooledBuffer)
                {
                    (e as PooledBuffer).Dispose();
                }
            }
        }

        private async Task ConnectTask()
        {
            Log("Connect Task Begin ...");

            try
            {
                var uri = new Uri(Address);
                await socket.ConnectAsync(uri, cts.Token);
            }
            catch (Exception e)
            {
                HandleError(e);
                HandleClose((ushort)CloseStatusCode.Abnormal, e.Message);
                return;
            }

            HandleOpen();

            Log("Connect Task Success !");

            StartReceiveTask();
            StartSendTask();
        }

        private async void StartSendTask()
        {
            Log("Send Task Begin ...");

            try
            {
                PooledBuffer buffer = null;
                while (!closeProcessing && socket != null && cts != null && !cts.IsCancellationRequested)
                {
                    while (!closeProcessing && sendQueue.Count > 0 && sendQueue.TryDequeue(out buffer))
                    {
                        Log($"Send, type: {buffer.Opcode}, size: {buffer.Length}, queue left: {sendQueue.Count}");
                        await socket.SendAsync(new ArraySegment<byte>(buffer.Bytes), buffer.Opcode == Opcode.Text ? WebSocketMessageType.Text : WebSocketMessageType.Binary, true, cts.Token);
                        buffer.Dispose();
                    }
                    Thread.Sleep(3);
                }
                if (closeProcessing && socket != null && cts != null && !cts.IsCancellationRequested)
                {
                    CleanSendQueue();
                    Log($"Close Send Begin ...");
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Normal Closure", cts.Token);
                    Log($"Close Send Success !");
                }
            }
            catch (Exception e)
            {
                HandleError(e);
            }
            finally
            {
                closeProcessing = false;
            }

            Log("Send Task End !");
        }

        private async void StartReceiveTask()
        {
            Log("Receive Task Begin ...");

            string closeReason = "";
            ushort closeCode = 0;
            bool isClosed = false;
            var mem = new byte[8192];
            var segment = new ArraySegment<byte>(mem);

            try
            {
                PooledBuffer buffer = null;
                int index = 0;
                while (!isClosed && !cts.IsCancellationRequested)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(segment, cts.Token);
                    if (buffer == null) buffer = PooledBuffer.Create();
                    buffer.Write(mem, 0, result.Count, index);
                    if (!result.EndOfMessage)
                    {
                        index += result.Count;
                        continue;
                    }
                    index = 0;

                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Binary:
                            HandleMessage(Opcode.Binary, buffer);
                            break;
                        case WebSocketMessageType.Text:
                            HandleMessage(Opcode.Text, buffer);
                            break;
                        case WebSocketMessageType.Close:
                            isClosed = true;
                            closeCode = (ushort)result.CloseStatus;
                            closeReason = result.CloseStatusDescription;
                            break;
                    }
                    buffer = null;
                }
            }
            catch (Exception e)
            {
                HandleError(e);
                closeCode = (ushort)CloseStatusCode.Abnormal;
                closeReason = e.Message;
            }

            HandleClose(closeCode, closeReason);

            Log("Receive Task End !");
        }

        private void SocketDispose()
        {
            Log("Dispose");
            WebSocketManager.Instance.Remove(this);
            CleanSendQueue();
            CleanEventQueue();
            socket.Dispose();
            socket = null;
            cts.Dispose();
            cts = null;
        }

        private void HandleOpen()
        {
            Log("OnOpen");
            eventQueue.Enqueue(new OpenEventArgs());
        }

        private void HandleMessage(Opcode opcode, PooledBuffer rawData)
        {
            Log($"OnMessage, type: {opcode}, size: {rawData.Length}");
            rawData.Opcode = opcode;
            eventQueue.Enqueue(rawData);
        }

        private void HandleClose(ushort code, string reason)
        {
            Log($"OnClose, code: {code}, reason: {reason}");
            eventQueue.Enqueue(new CloseEventArgs(code, reason));
        }

        private void HandleError(Exception exception)
        {
            Log("OnError, error: " + exception.Message);
            eventQueue.Enqueue(new ErrorEventArgs(exception.Message));
        }

        internal void Update()
        {
            while (eventQueue.Count > 0 && eventQueue.TryDequeue(out var e))
            {
                if (e is CloseEventArgs)
                {
                    OnClose?.Invoke(this, e as CloseEventArgs);
                    SocketDispose();
                    break;
                }
                else if (e is OpenEventArgs)
                {
                    OnOpen?.Invoke(this, e as OpenEventArgs);
                }
                else if (e is PooledBuffer)
                {
                    OnMessage?.Invoke(this, e as PooledBuffer);
                }
                else if (e is ErrorEventArgs)
                {
                    OnError?.Invoke(this, e as ErrorEventArgs);
                }
            }
        }

        internal void Abort()
        {
            Log("Abort");
            if (cts != null)
            {
                cts.Cancel();
            }
        }

        [System.Diagnostics.Conditional("UNITY_WEB_SOCKET_LOG")]
        static void Log(string msg)
        {
            var time = DateTime.Now.ToString("HH:mm:ss.fff");
            var thread = Thread.CurrentThread.ManagedThreadId;
            UnityEngine.Debug.Log($"[{time}][UnityWebSocket][T-{thread:D3}] {msg}");
        }
    }
}
#endif
