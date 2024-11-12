#if NET_LEGACY
#error .NET Runtime is Legacy.
/* https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.clientwebsocket
System.Net.WebSockets.ClientWebSocket Applies to Product	Versions
.NET	Core 1.0, Core 1.1, Core 2.0, Core 2.1, Core 2.2, Core 3.0, Core 3.1, 5, 6, 7, 8, 9
.NET Framework	4.5, 4.5.1, 4.5.2, 4.6, 4.6.1, 4.6.2, 4.7, 4.7.1, 4.7.2, 4.8, 4.8.1
.NET Standard	2.0, 2.1
*/
#elif !(UNITY_WEBGL && !UNITY_EDITOR) && !FORCE_WEBGL_IMPL_ENABLE

using System.Collections.Generic;
using UnityEngine;

namespace UnityWebSocket
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    internal class WebSocketManager : MonoBehaviour
    {
        private const string rootName = "[UnityWebSocket]";
        private static WebSocketManager _instance;
        public static WebSocketManager Instance
        {
            get
            {
                if (!_instance) CreateInstance();
                return _instance;
            }
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public static void CreateInstance()
        {
            GameObject go = GameObject.Find("/" + rootName);
            if (!go) go = new GameObject(rootName);
            _instance = go.GetComponent<WebSocketManager>();
            if (!_instance) _instance = go.AddComponent<WebSocketManager>();
        }

        private readonly List<WebSocket> sockets = new List<WebSocket>();

        public void Add(WebSocket socket)
        {
            if (!sockets.Contains(socket))
                sockets.Add(socket);
        }

        public void Remove(WebSocket socket)
        {
            if (sockets.Contains(socket))
                sockets.Remove(socket);
        }

        private void Update()
        {
            if (sockets.Count <= 0) return;
            for (int i = sockets.Count - 1; i >= 0; i--)
            {
                sockets[i].Update();
            }
        }

#if UNITY_EDITOR
        private void OnDisable()
        {
            SocketAbort();
        }

        private void SocketAbort()
        {
            for (int i = sockets.Count - 1; i >= 0; i--)
            {
                sockets[i].Abort();
            }
        }
#endif
    }
}
#endif