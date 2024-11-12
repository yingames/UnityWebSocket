#if NET_LEGACY
#error .NET Runtime is Legacy.
/* https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket
System.Net.WebSockets.ClientWebSocket Applies to Product	Versions
.NET	Core 1.0, Core 1.1, Core 2.0, Core 2.1, Core 2.2, Core 3.0, Core 3.1, 5, 6, 7, 8, 9
.NET Framework	4.5, 4.5.1, 4.5.2, 4.6, 4.6.1, 4.6.2, 4.7, 4.7.1, 4.7.2, 4.8, 4.8.1
.NET Standard	2.0, 2.1
*/
#elif (UNITY_WEBGL && !UNITY_EDITOR ) || FORCE_WEBGL_IMPL_ENABLE
using System;

namespace UnityWebSocket
{
    public class WebSocket : IWebSocket
    {
        public string Address { get; private set; }
        public string[] SubProtocols { get; private set; }
        public WebSocketState ReadyState { get { return (WebSocketState)WebSocketManager.WebSocketGetState(instanceId); } }

        public event EventHandler<OpenEventArgs> OnOpen;
        public event EventHandler<CloseEventArgs> OnClose;
        public event EventHandler<ErrorEventArgs> OnError;
        public event EventHandler<PooledBuffer> OnMessage;

        internal int instanceId = 0;

        public WebSocket(string address)
        {
            this.Address = address;
            AllocateInstance();
        }

        public WebSocket(string address, string subProtocol)
        {
            this.Address = address;
            this.SubProtocols = new string[] { subProtocol };
            AllocateInstance();
        }

        public WebSocket(string address, string[] subProtocols)
        {
            this.Address = address;
            this.SubProtocols = subProtocols;
            AllocateInstance();
        }

        internal void AllocateInstance()
        {
            instanceId = WebSocketManager.AllocateInstance(this.Address);
            Log($"Allocate socket with instanceId: {instanceId}");
            if (this.SubProtocols == null) return;
            foreach (var protocol in this.SubProtocols)
            {
                if (string.IsNullOrEmpty(protocol)) continue;
                Log($"Add Sub Protocol {protocol}, with instanceId: {instanceId}");
                int code = WebSocketManager.WebSocketAddSubProtocol(instanceId, protocol);
                if (code < 0)
                {
                    HandleOnError(GetErrorMessageFromCode(code));
                    break;
                }
            }
        }

        ~WebSocket()
        {
            Log($"Free socket with instanceId: {instanceId}");
            WebSocketManager.WebSocketFree(instanceId);
        }

        public void ConnectAsync()
        {
            Log($"Connect with instanceId: {instanceId}");
            WebSocketManager.Add(this);
            int code = WebSocketManager.WebSocketConnect(instanceId);
            if (code < 0) HandleOnError(GetErrorMessageFromCode(code));
        }

        public void CloseAsync()
        {
            Log($"Close with instanceId: {instanceId}");
            int code = WebSocketManager.WebSocketClose(instanceId, (int)CloseStatusCode.Normal, "Normal Closure");
            if (code < 0) HandleOnError(GetErrorMessageFromCode(code));
        }

        public void SendAsync(string text)
        {
            Log($"Send, type: {Opcode.Text}, size: {text.Length}");
            int code = WebSocketManager.WebSocketSendStr(instanceId, text);
            if (code < 0) HandleOnError(GetErrorMessageFromCode(code));
        }

        public void SendAsync(byte[] data)
        {
            Log($"Send, type: {Opcode.Binary}, size: {data.Length}");
            int code = WebSocketManager.WebSocketSend(instanceId, data, data.Length);
            if (code < 0) HandleOnError(GetErrorMessageFromCode(code));
        }

        public void SendAsync(PooledBuffer buffer)
        {
            Log($"Send, type: {buffer.Opcode}, size: {buffer.Length}");
            int code;
            if (buffer.Opcode == Opcode.Text)
            {
                code = WebSocketManager.WebSocketSendStr(instanceId, buffer.Data);
            }
            else
            {
                code = WebSocketManager.WebSocketSend(instanceId, buffer.Bytes, buffer.Length);
            }
            if (code < 0) HandleOnError(GetErrorMessageFromCode(code));
            buffer.Dispose();
        }

        internal void HandleOnOpen()
        {
            Log("OnOpen");
            OnOpen?.Invoke(this, new OpenEventArgs());
        }

        internal void HandleOnMessage(PooledBuffer buffer)
        {
            Log($"OnMessage, type: {buffer.Opcode}, size: {buffer.Length}");
            OnMessage?.Invoke(this, buffer);
        }

        internal void HandleOnClose(ushort code, string reason)
        {
            Log($"OnClose, code: {code}, reason: {reason}");
            OnClose?.Invoke(this, new CloseEventArgs(code, reason));
            WebSocketManager.Remove(instanceId);
        }

        internal void HandleOnError(string msg)
        {
            Log("OnError, error: " + msg);
            OnError?.Invoke(this, new ErrorEventArgs(msg));
        }

        internal static string GetErrorMessageFromCode(int errorCode)
        {
            switch (errorCode)
            {
                case -1: return "WebSocket instance not found.";
                case -2: return "WebSocket is already connected or in connecting state.";
                case -3: return "WebSocket is not connected.";
                case -4: return "WebSocket is already closing.";
                case -5: return "WebSocket is already closed.";
                case -6: return "WebSocket is not in open state.";
                case -7: return "Cannot close WebSocket, An invalid code was specified or reason is too long.";
                case -8: return "Not support buffer slice. ";
                default: return $"Unknown error code {errorCode}.";
            }
        }

        [System.Diagnostics.Conditional("UNITY_WEB_SOCKET_LOG")]
        static void Log(string msg)
        {
            var time = DateTime.Now.ToString("HH:mm:ss.fff");
            UnityEngine.Debug.Log($"[{time}][UnityWebSocket] {msg}");
        }
    }
}
#endif
