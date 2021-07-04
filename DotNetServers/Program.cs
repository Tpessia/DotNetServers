using DotNetServers.Shared;
using DotNetServers.Tcp;
using DotNetServers.WebSocket;
using System;
using System.Collections.Generic;
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

            var tcpEndpoint = new IPEndPoint(IPAddress.Any, port: 8000);
            Console.WriteLine($"Starting TcpServer: tcp://{tcpEndpoint}/");

            tcpServer.Start(tcpEndpoint, tcpProcess);

            // HTTP Server (TcpListener)

            Http.TcpListener.IHttpServer httpServer1 = new Http.TcpListener.HttpServer();

            Func<Http.TcpListener.HttpRequest, Task<Http.TcpListener.HttpResponse>> httpProcess1 = async request =>
            {
                var rndNumber = await RandomNumberGenerator();
                var response = $"{rndNumber} - {request.Body}";
                var headers = new Dictionary<string, string> { { "Server", "MyHttpServer/1.0.0" } };
                return new Http.TcpListener.HttpResponse(HttpStatusCode.OK, response, headers);
            };

            httpServer1.Opened += (sender, e) => Console.WriteLine("HttpServer1 Opened!");
            httpServer1.Data += (sender, e) => Console.WriteLine("HttpServer1 Data: " + e.Data);
            httpServer1.Error += (sender, e) => Console.WriteLine("HttpServer1 Error: " + e.Exception.ToErrorString());
            httpServer1.Closed += (sender, e) => Console.WriteLine("HttpServer1 Closed!");

            var httpEndpoint1 = new IPEndPoint(IPAddress.Any, port: 8010);
            Console.WriteLine($"Starting HttpServer1: http://{httpEndpoint1}/");

            httpServer1.Start(httpEndpoint1, httpProcess1);

            // HTTP Server (HttpListener)

            Http.HttpListener.IHttpServer httpServer2 = new Http.HttpListener.HttpServer();

            Func<string, HttpListenerRequest, HttpListenerResponse, Task<string>> httpProcess2 = async (data, req, res) =>
            {
                var rndNumber = await RandomNumberGenerator();
                var response = $"{rndNumber} - {data}";
                res.StatusCode = (int)HttpStatusCode.OK;
                res.Headers.Add("Server", "MyHttpServer/1.0.0");
                return response;
            };

            httpServer2.Opened += (sender, e) => Console.WriteLine("HttpServer2 Opened!");
            httpServer2.Data += (sender, e) => Console.WriteLine("HttpServer2 Data: " + e.Data);
            httpServer2.Error += (sender, e) => Console.WriteLine("HttpServer2 Error: " + e.Exception.ToErrorString());
            httpServer2.Closed += (sender, e) => Console.WriteLine("HttpServer2 Closed!");

            //var httpEndpoint2 = "http://*:8011/"; // Requires Admin
            var httpEndpoint2 = "http://localhost:8011/";
            Console.WriteLine($"Starting HttpServer2: {httpEndpoint2}");

            httpServer2.Start(httpEndpoint2, httpProcess2);

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

            var wsEndpoint = new IPEndPoint(IPAddress.Any, port: 8020);
            Console.WriteLine($"Starting WebSocketServer: ws://{wsEndpoint}/");

            wsServer.Start(wsEndpoint);

            // Wait for requests

            Console.WriteLine(Environment.NewLine + "Running! Press any key to stop..." + Environment.NewLine);
            Console.ReadKey();

            tcpServer.Stop();
            httpServer1.Stop();
            httpServer2.Stop();
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
