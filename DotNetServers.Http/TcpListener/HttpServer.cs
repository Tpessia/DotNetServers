using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetServers.Http.TcpListener
{
    public class HttpServer : IHttpServer, IDisposable
    {
        private IPEndPoint _endPoint;
        private System.Net.Sockets.TcpListener _server;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Func<HttpRequest, Task<HttpResponse>> _respond;
        private TimeSpan timeout;

        public bool IsRunning { get; private set; } = false;

        public event EventHandler Opened;
        public event EventHandler<HttpDataEventArgs> Data;
        public event EventHandler<HttpErrorEventArgs> Error;
        public event EventHandler Closed;

        public void Start(IPEndPoint endpoint, Func<HttpRequest, Task<HttpResponse>> respond, TimeSpan? timeout = null)
        {
            if (IsRunning) return;

            _endPoint = endpoint;
            _server = new System.Net.Sockets.TcpListener(_endPoint);
            _respond = respond;
            this.timeout = timeout ?? TimeSpan.FromSeconds(10);

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
                    // Can use server.AcceptSocket() or _server.AcceptTcpClient()
                    // Has sync and async implementations
                    var client = _server.AcceptTcpClient();

                    // Process request
                    Task.Run(() => Process(client), _cts.Token);
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new HttpErrorEventArgs(ex));
                throw;
            }
        }

        private async Task Process(TcpClient client)
        {
            client.ReceiveTimeout = (int)timeout.TotalMilliseconds;
            client.SendTimeout = (int)timeout.TotalMilliseconds;

            try
            {
                // Buffer for reading data
                var buffer = new byte[8096];
                var data = "";
                var length = 0;

                // Get a stream object for reading and writing
                var netStream = client.GetStream();

                netStream.ReadTimeout = (int)timeout.TotalMilliseconds;
                netStream.WriteTimeout = (int)timeout.TotalMilliseconds;

                // Loop to receive all the data sent by the client
                do
                {
                    if (_cts.IsCancellationRequested) break;
                    
                    // Perform a blocking call to read available bytes
                    // Has sync and async implementations
                    length = netStream.Read(buffer, 0, buffer.Length); // buffer vs client.Available

                    // Translate data bytes to a UTF8 string
                    data += Encoding.UTF8.GetString(buffer, 0, length);
                } while (netStream.DataAvailable);

                // Process the data sent by the client
                Data?.Invoke(this, new HttpDataEventArgs(data));
                var httpRequest = HttpParser.ParseRequest(data);
                var response = await _respond(httpRequest);
                var httpResponse = HttpParser.BuildResponse(response);
                var msg = Encoding.UTF8.GetBytes(httpResponse);

                // Send back a response
                // Has sync and async implementations
                netStream.Write(msg);

                // Shutdown and end connection
                if (client != null && client.Connected) client.Close();
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new HttpErrorEventArgs(ex));
                client?.Close();
                throw;
            }
        }
    }
}