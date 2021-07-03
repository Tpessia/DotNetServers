using System;
using System.Net;
using System.Net.Sockets;

namespace DotNetServers.WebSocket
{
    public interface IWebSocketServer : IDisposable
    {
        event EventHandler<WebSocketOpenedEventArgs> Opened;
        event EventHandler<WebSocketDataEventArgs> Data;
        event EventHandler<WebSocketErrorEventArgs> Error;
        event EventHandler<WebSocketClosedEventArgs> Closed;

        Action<TcpClient, string> Send { get; }
        Action<string> Broadcast { get; }
        bool IsRunning { get; }

        void Start(IPEndPoint endpoint, TimeSpan? streamTimeout = null);
        void Stop();
    }

    public class WebSocketOpenedEventArgs : EventArgs
    {
        public WebSocketOpenedEventArgs(TcpClient client)
        {
            Client = client;
        }

        public TcpClient Client { get; }
    }

    public class WebSocketClosedEventArgs : EventArgs
    {
        public WebSocketClosedEventArgs(TcpClient client)
        {
            Client = client;
        }

        public TcpClient Client { get; }
    }

    public class WebSocketDataEventArgs : EventArgs
    {
        public WebSocketDataEventArgs(TcpClient client, string data)
        {
            Client = client;
            Data = data;
        }

        public TcpClient Client { get; }
        public string Data { get; }
    }

    public class WebSocketErrorEventArgs : EventArgs
    {
        public WebSocketErrorEventArgs(TcpClient client, Exception exception)
        {
            Client = client;
            Exception = exception;
        }

        public TcpClient Client { get; }
        public Exception Exception { get; }
    }
}
