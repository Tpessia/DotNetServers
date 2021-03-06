using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetServers.Tcp
{
    public class TcpServer : ITcpServer, IDisposable
    {
        private IPEndPoint _endPoint;
        private TcpListener _server;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Func<string, Task<string>> _respond;
        private TimeSpan _timeout;

        public bool IsRunning { get; private set; } = false;

        public event EventHandler Opened;
        public event EventHandler<TcpDataEventArgs> Data;
        public event EventHandler<TcpErrorEventArgs> Error;
        public event EventHandler Closed;

        public void Start(IPEndPoint endpoint, Func<string, Task<string>> respond, TimeSpan? timeout = null)
        {
            if (IsRunning) return;

            _endPoint = endpoint;
            _server = new TcpListener(_endPoint);
            _respond = respond;
            _timeout = timeout ?? TimeSpan.FromSeconds(10);

            try
            {
                Task.Run(Listen, _cts.Token);
                IsRunning = true;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new TcpErrorEventArgs(ex));
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
                Error?.Invoke(this, new TcpErrorEventArgs(ex));
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
                Error?.Invoke(this, new TcpErrorEventArgs(ex));
                throw;
            }
        }

        private async Task Process(TcpClient client)
        {
            client.ReceiveTimeout = (int)_timeout.TotalMilliseconds;
            client.SendTimeout = (int)_timeout.TotalMilliseconds;

            try
            {
                // Buffer for reading data
                var buffer = new byte[8096];
                var data = "";
                var length = 0;

                // Get a stream object for reading and writing
                var netStream = client.GetStream();

                netStream.ReadTimeout = (int)_timeout.TotalMilliseconds;
                netStream.WriteTimeout = (int)_timeout.TotalMilliseconds;

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
                Data?.Invoke(this, new TcpDataEventArgs(data));
                var response = await _respond(data);
                var msg = Encoding.UTF8.GetBytes(response);

                // Send back a response
                // Has sync and async implementations
                netStream.Write(msg);

                // Shutdown and end connection
                if (client != null && client.Connected) client.Close();
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new TcpErrorEventArgs(ex));
                client?.Close();
                throw;
            }
        }
    }
}