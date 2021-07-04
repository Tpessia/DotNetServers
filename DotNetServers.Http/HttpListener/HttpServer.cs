using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetServers.Http.HttpListener
{
    public class HttpServer : IHttpServer, IDisposable
    {
        private string _endPoint;
        private System.Net.HttpListener _server;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Func<string, HttpListenerRequest, HttpListenerResponse, Task<string>> _respond;

        public bool IsRunning { get; private set; } = false;

        public event EventHandler Opened;
        public event EventHandler<HttpDataEventArgs> Data;
        public event EventHandler<HttpErrorEventArgs> Error;
        public event EventHandler Closed;

        public void Start(string endPoint, Func<string, HttpListenerRequest, HttpListenerResponse, Task<string>> respond)
        {
            if (IsRunning) return;

            _endPoint = endPoint;
            _server = new System.Net.HttpListener();
            _server.Prefixes.Add(endPoint);
            _respond = respond;

            try
            {
                Task.Run(Listen, _cts.Token);
                IsRunning = true;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new HttpErrorEventArgs(ex));
                throw;
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            try
            {
                _server?.Stop();
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                IsRunning = false;

                Closed?.Invoke(this, null);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new HttpErrorEventArgs(ex));
                throw;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void Listen()
        {
            try
            {
                _server.Start();

                Opened?.Invoke(this, null);

                // Enter the listening loop
                while (true)
                {
                    if (_cts.IsCancellationRequested) break;

                    // Perform a blocking call to accept requests
                    // Has sync and async implementations
                    var context = _server.GetContext();

                    // Process request
                    Task.Run(() => Process(context), _cts.Token);
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new HttpErrorEventArgs(ex));
                throw;
            }
        }

        private async Task Process(HttpListenerContext httpContext)
        {
            try
            {
                HttpListenerRequest httpRequest = httpContext.Request;
                HttpListenerResponse httpResponse = httpContext.Response;

                string data;

                // Read and translate data bytes to a UTF8 string
                using (var inStream = httpRequest.InputStream)
                using (var readStream = new StreamReader(inStream, Encoding.UTF8))
                {
                    data = readStream.ReadToEnd();
                }

                // Process the data sent by the client
                Data?.Invoke(this, new HttpDataEventArgs(data));
                var response = await _respond(data, httpRequest, httpResponse);
                var msg = Encoding.UTF8.GetBytes(response);

                httpResponse.ContentType ??= "text/plain";
                httpResponse.ContentEncoding = Encoding.UTF8;
                httpResponse.ContentLength64 = msg.LongLength;

                // Send back a response
                // Has sync and async implementations
                httpResponse.OutputStream.Write(msg, 0, msg.Length);

                // Shutdown and end connection
                httpResponse.Close();
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new HttpErrorEventArgs(ex));
                httpContext?.Response?.Close();
                throw;
            }
        }
    }
}