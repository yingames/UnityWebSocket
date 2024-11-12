// #define UNITY_WEBSOCKET_DEMO_LOG

using System.Collections;
using UnityEngine;
using System.Linq;

namespace UnityWebSocket.Demo
{
    public class UnityWebSocketMemoryDemo : MonoBehaviour
    {
        public string address = "wss://echo.websocket.events";

        private IWebSocket socket;
        private int receiveCount = 0;

        private void Start()
        {
            StartCoroutine(RoutineTest());
        }

        static readonly byte[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray().Select(c => (byte)c).ToArray();
        private PooledBuffer RandomBuffer(int length)
        {
            PooledBuffer buffer = PooledBuffer.Create(Opcode.Binary);
            for (int i = 0; i < length - 1; i++)
            {
                buffer.Write(chars[Random.Range(0, chars.Length)], i);
            }
            buffer.Write((byte)0, length - 1);

            return buffer;
        }

        private IEnumerator RoutineTest()
        {
            while (true)
            {
                if (socket == null)
                {
                    socket = new WebSocket(address);
                    socket.OnOpen += Socket_OnOpen;
                    socket.OnMessage += Socket_OnMessage;
                    socket.OnClose += Socket_OnClose;
                    socket.OnError += Socket_OnError;
                    socket.ConnectAsync();
                }

                socket.ConnectAsync();

                while (socket.ReadyState != WebSocketState.Open)
                {
                    yield return null;
                }

                var delay = new WaitForSeconds(1f);
                var sendCount = 0;
                while (socket.ReadyState == WebSocketState.Open)
                {
                    // buffer with random length to detect reuse
                    var message = RandomBuffer(Random.Range(1, 512));

                    // buffer with 32 bytes to detect reuse
                    // var message = RandomBuffer(32);
#if UNITY_WEBSOCKET_DEMO_LOG
                    Debug.Log(string.Format("Send: {0}", message.Data));
#endif

                    socket.SendAsync(message);
                    sendCount += 1;
                    yield return delay;
                }
            }
        }

        private void Socket_OnOpen(object sender, OpenEventArgs e)
        {
            Debug.Log(string.Format("Connected: {0}", address));
        }

        private void Socket_OnMessage(object sender, PooledBuffer e)
        {
#if UNITY_WEBSOCKET_DEMO_LOG
            Debug.Log(string.Format("Recv: {0}", e.Data));
#endif

            e.Dispose();

            receiveCount += 1;
        }

        private void Socket_OnClose(object sender, CloseEventArgs e)
        {
            Debug.Log(string.Format("Closed: StatusCode: {0}, Reason: {1}", e.StatusCode, e.Reason));
        }

        private void Socket_OnError(object sender, ErrorEventArgs e)
        {
            Debug.Log(string.Format("Error: {0}", e.Message));
        }

        private void OnApplicationQuit()
        {
            if (socket != null && socket.ReadyState != WebSocketState.Closed)
            {
                socket.CloseAsync();
            }
        }
    }
}
