using DotNetServers.Http;
using DotNetServers.Shared;
using DotNetServers.Tcp;
using DotNetServers.WebSocket;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DotNetServers
{
    class Program
    {
        static void Main(string[] args)
        {
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddressList = ipHostInfo.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork);
            var ipAddress = ipAddressList.ElementAt(0); // Choose IP from available interfaces list

            // TCP Server

            ITcpServer tcpServer = new TcpServer();

            Func<string, Task<string>> tcpProcess = async request =>
            {
                var rndNumber = await RandomNumberGenerator();
                var response = $"{rndNumber} - {request}";
                return response;
            };

            tcpServer.Opened += (sender, e) => Console.WriteLine("TcpServer Opened!");
            tcpServer.Data += (sender, e) => Console.WriteLine("TcpServer Data: " + e.Data);
            tcpServer.Error += (sender, e) => Console.WriteLine("TcpServer Error: " + e.Exception.ToErrorString());
            tcpServer.Closed += (sender, e) => Console.WriteLine("TcpServer Closed!");

            var tcpEndpoint = new IPEndPoint(ipAddress, port: 8000);
            Console.WriteLine($"Starting TcpServer: tcp://{tcpEndpoint}");

            tcpServer.Start(tcpEndpoint, tcpProcess);

            // HTTP Server

            IHttpServer httpServer = new HttpServer();

            Func<HttpRequest, Task<HttpResponse>> httpProcess = async request =>
            {
                var rndNumber = await RandomNumberGenerator();
                var response = $"{rndNumber} - {request.Body}";
                return new HttpResponse(HttpStatusCode.OK, response);
            };

            httpServer.Opened += (sender, e) => Console.WriteLine("HttpServer Opened!");
            httpServer.Data += (sender, e) => Console.WriteLine("HttpServer Data: " + e.Data);
            httpServer.Error += (sender, e) => Console.WriteLine("HttpServer Error: " + e.Exception.ToErrorString());
            httpServer.Closed += (sender, e) => Console.WriteLine("HttpServer Closed!");

            var httpEndpoint = new IPEndPoint(ipAddress, port: 8001);
            Console.WriteLine($"Starting HttpServer: http://{httpEndpoint}");

            httpServer.Start(httpEndpoint, httpProcess);

            // WebSocket Server

            IWebSocketServer wsServer = new WebSocketServer();

            Func<TcpClient, string, Task> wsProcess = async (client, data) =>
            {
                var rndNumber = await RandomNumberGenerator();
                var response = $"{rndNumber} - {data}";
                wsServer.Send(client, response);
            };

            wsServer.Opened += (sender, e) => Console.WriteLine(e.Client == null ? "WebSocketServer Opened!" : "WebSocketServer Client Connected: " + e.Client.Client.RemoteEndPoint.ToString());
            wsServer.Data += async (sender, e) => { Console.WriteLine("WebSocketServer Data: " + e.Data); await wsProcess(e.Client, e.Data); };
            wsServer.Error += (sender, e) => Console.WriteLine(e.Client == null ? "WebSocketServer Error: " + e.Exception.ToErrorString() : "WebSocketServer Client Error: " + e.Exception.ToErrorString());
            wsServer.Closed += (sender, e) => Console.WriteLine(e.Client == null ? "WebSocketServer Closed!" : "WebSocketServer Client Disconnected: " + e.Client.Client.RemoteEndPoint.ToString());

            var wsEndpoint = new IPEndPoint(ipAddress, port: 8002);
            Console.WriteLine($"Starting WebSocketServer: ws://{wsEndpoint}");

            wsServer.Start(wsEndpoint);

            // Wait for requests

            Console.WriteLine(Environment.NewLine + "Running! Press any key to stop..." + Environment.NewLine);
            Console.ReadKey();

            tcpServer.Stop();
            httpServer.Stop();
            wsServer.Stop();
        }

        private static Task<int> RandomNumberGenerator()
        {
            var rnd = new Random();
            var number = rnd.Next(100000, 999999);
            return Task.FromResult(number);
        }
    }
}
