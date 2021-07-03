using System;
using System.Net;
using System.Threading.Tasks;

namespace DotNetServers.Tcp
{
    public interface ITcpServer : IDisposable
    {
        event EventHandler Opened;
        event EventHandler<TcpDataEventArgs> Data;
        event EventHandler<TcpErrorEventArgs> Error;
        event EventHandler Closed;

        bool IsRunning { get; }

        void Start(IPEndPoint endpoint, Func<string, Task<string>> respond, TimeSpan? streamTimeout = null);
        void Stop();
    }

    public class TcpDataEventArgs : EventArgs
    {
        public TcpDataEventArgs(string data)
        {
            Data = data;
        }

        public string Data { get; }
    }

    public class TcpErrorEventArgs : EventArgs
    {
        public TcpErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }

        public Exception Exception { get; }
    }
}
