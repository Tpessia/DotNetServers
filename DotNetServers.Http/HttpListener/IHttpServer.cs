using System;
using System.Net;
using System.Threading.Tasks;

namespace DotNetServers.Http.HttpListener
{
    public interface IHttpServer : IDisposable
    {
        event EventHandler Opened;
        event EventHandler<HttpDataEventArgs> Data;
        event EventHandler<HttpErrorEventArgs> Error;
        event EventHandler Closed;

        bool IsRunning { get; }

        void Start(string endpoint, Func<string, HttpListenerRequest, HttpListenerResponse, Task<string>> respond);
        void Stop();
    }

    public class HttpDataEventArgs : EventArgs
    {
        public HttpDataEventArgs(string data)
        {
            Data = data;
        }

        public string Data { get; }
    }

    public class HttpErrorEventArgs : EventArgs
    {
        public HttpErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }

        public Exception Exception { get; }
    }
}
