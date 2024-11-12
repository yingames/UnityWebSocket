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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;

namespace UnityWebSocket
{
    /// <summary>
    /// Class providing static access methods to work with JSLIB WebSocket
    /// </summary>
    internal static class WebSocketManager
    {
        /* Map of websocket instances */
        private static Dictionary<int, WebSocket> sockets = new Dictionary<int, WebSocket>();

        /* Delegates */
        public delegate void OnOpenCallback(int instanceId);
        public delegate void OnMessageCallback(int instanceId, IntPtr msgPtr, int msgSize);
        public delegate void OnMessageStrCallback(int instanceId, IntPtr msgPtr, int msgSize);
        public delegate void OnErrorCallback(int instanceId, IntPtr errorPtr);
        public delegate void OnCloseCallback(int instanceId, int closeCode, IntPtr reasonPtr);

        /* WebSocket JSLIB functions */
        [DllImport("__Internal")]
        public static extern int WebSocketConnect(int instanceId);

        [DllImport("__Internal")]
        public static extern int WebSocketClose(int instanceId, int code, string reason);

        [DllImport("__Internal")]
        public static extern int WebSocketSend(int instanceId, byte[] dataPtr, int dataLength);

        [DllImport("__Internal")]
        public static extern int WebSocketSendStr(int instanceId, string data);

        [DllImport("__Internal")]
        public static extern int WebSocketGetState(int instanceId);

        /* WebSocket JSLIB callback setters and other functions */
        [DllImport("__Internal")]
        public static extern int WebSocketAllocate(string url);

        [DllImport("__Internal")]
        public static extern int WebSocketAddSubProtocol(int instanceId, string protocol);

        [DllImport("__Internal")]
        public static extern void WebSocketFree(int instanceId);

        [DllImport("__Internal")]
        public static extern void WebSocketSetOnOpen(OnOpenCallback callback);

        [DllImport("__Internal")]
        public static extern void WebSocketSetOnMessage(OnMessageCallback callback);

        [DllImport("__Internal")]
        public static extern void WebSocketSetOnMessageStr(OnMessageStrCallback callback);

        [DllImport("__Internal")]
        public static extern void WebSocketSetOnError(OnErrorCallback callback);

        [DllImport("__Internal")]
        public static extern void WebSocketSetOnClose(OnCloseCallback callback);

        /* If callbacks was initialized and set */
        private static bool isInitialized = false;

        /* Initialize WebSocket callbacks to JSLIB */
        private static void Initialize()
        {
            WebSocketSetOnOpen(DelegateOnOpenEvent);
            WebSocketSetOnMessage(DelegateOnMessageEvent);
            WebSocketSetOnMessageStr(DelegateOnMessageStrEvent);
            WebSocketSetOnError(DelegateOnErrorEvent);
            WebSocketSetOnClose(DelegateOnCloseEvent);

            isInitialized = true;
        }

        [MonoPInvokeCallback(typeof(OnOpenCallback))]
        public static void DelegateOnOpenEvent(int instanceId)
        {
            if (sockets.TryGetValue(instanceId, out var socket))
            {
                socket.HandleOnOpen();
            }
        }

        [MonoPInvokeCallback(typeof(OnMessageCallback))]
        public static void DelegateOnMessageEvent(int instanceId, IntPtr msgPtr, int msgSize)
        {
            if (sockets.TryGetValue(instanceId, out var socket))
            {
                var buffer = PooledBuffer.Create(Opcode.Binary);
                buffer.Write(msgPtr, 0, msgSize);
                socket.HandleOnMessage(buffer);
            }
        }

        [MonoPInvokeCallback(typeof(OnMessageStrCallback))]
        public static void DelegateOnMessageStrEvent(int instanceId, IntPtr msgPtr, int msgSize)
        {
            if (sockets.TryGetValue(instanceId, out var socket))
            {
                var buffer = PooledBuffer.Create(Opcode.Text);
                buffer.Write(msgPtr, 0, msgSize);
                socket.HandleOnMessage(buffer);
            }
        }

        [MonoPInvokeCallback(typeof(OnErrorCallback))]
        public static void DelegateOnErrorEvent(int instanceId, IntPtr errorPtr)
        {
            if (sockets.TryGetValue(instanceId, out var socket))
            {
                string errorMsg = Marshal.PtrToStringAuto(errorPtr);
                socket.HandleOnError(errorMsg);
            }
        }

        [MonoPInvokeCallback(typeof(OnCloseCallback))]
        public static void DelegateOnCloseEvent(int instanceId, int closeCode, IntPtr reasonPtr)
        {
            if (sockets.TryGetValue(instanceId, out var socket))
            {
                string reason = Marshal.PtrToStringAuto(reasonPtr);
                socket.HandleOnClose((ushort)closeCode, reason);
            }
        }

        internal static int AllocateInstance(string address)
        {
            if (!isInitialized) Initialize();
            return WebSocketAllocate(address);
        }

        internal static void Add(WebSocket socket)
        {
            if (!sockets.ContainsKey(socket.instanceId))
            {
                sockets.Add(socket.instanceId, socket);
            }
        }

        internal static void Remove(int instanceId)
        {
            if (sockets.ContainsKey(instanceId))
            {
                sockets.Remove(instanceId);
            }
        }
    }
}
#endif
