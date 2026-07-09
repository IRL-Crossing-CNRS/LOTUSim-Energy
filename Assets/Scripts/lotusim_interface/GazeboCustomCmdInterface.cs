using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace LiquidAi
{
    public class MinimalTcpListener : MonoBehaviour
    {
        TcpListener m_tcp_listener;
        Thread m_receive_tcp_thread;
        bool m_shutdown = false;
        public int port = 23457;

        void Start()
        {
            m_tcp_listener = new TcpListener(IPAddress.Any, port);
            m_tcp_listener.Start();
            m_receive_tcp_thread = new Thread(ReceiveTCPData) { IsBackground = true };
            m_receive_tcp_thread.Start();
            Debug.Log("MinimalTcpListener started.");
        }

        void OnDestroy()
        {
            m_shutdown = true;
            if (m_tcp_listener != null) m_tcp_listener.Stop();
            if (m_receive_tcp_thread != null) m_receive_tcp_thread.Join();
            Debug.Log("MinimalTcpListener destroyed.");
        }

        void ReceiveTCPData()
        {
            while (!m_shutdown)
            {
                try
                {
                    TcpClient client = m_tcp_listener.AcceptTcpClient();
                    Debug.Log("TCP Client connected.");
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            // Do nothing with the data (just read and discard)
                        }
                    }
                    client.Close();
                    Debug.Log("TCP Client disconnected.");
                }
                catch (SocketException ex)
                {
                    Debug.LogError("Socket Exception: " + ex.Message);
                }
            }
        }
    }
}