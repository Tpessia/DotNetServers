using DotNetServers.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetServers.WebSocket
{
    public class WebSocketServer : IWebSocketServer, IDisposable
    {
        // Infos:
        // https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
        // Protocol: https://datatracker.ietf.org/doc/html/rfc6455
        // Frame Packets: https://tools.ietf.org/html/rfc6455#section-5.2
        // Opcodes: https://tools.ietf.org/html/rfc6455#section-11.8
        // Closing Codes: https://tools.ietf.org/html/rfc6455#section-7.4.1 / https://github.com/Luka967/websocket-close-codes
        // https://stackoverflow.com/questions/8125507/how-can-i-send-and-receive-websocket-messages-on-the-server-side
        // https://lucumr.pocoo.org/2012/9/24/websockets-101/

        // TODOs:
        // Handle client disconnect (ping/pong)
        // Handle wrong connection type (i.e. that's not WebSocket)

        public event EventHandler<WebSocketOpenedEventArgs> Opened;
        public event EventHandler<WebSocketDataEventArgs> Data;
        public event EventHandler<WebSocketErrorEventArgs> Error;
        public event EventHandler<WebSocketClosedEventArgs> Closed;

        private IPEndPoint _endpoint;
        private TcpListener _server;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly List<WebSocketClient> _clients = new List<WebSocketClient>();
        private TimeSpan _streamTimeout;

        public bool IsRunning { get; private set; } = false;

        public Action<TcpClient, string> Send => (TcpClient client, string message) => WriteMessage(client, message);
        public Action<string> Broadcast => (string message) => _clients.ForEach(c => WriteMessage(c.Client, message));

        public void Start(IPEndPoint endpoint, TimeSpan? streamTimeout = null)
        {
            _endpoint = endpoint;
            _server = new TcpListener(_endpoint);
            _streamTimeout = streamTimeout ?? TimeSpan.FromSeconds(10);

            try
            {
                Task.Run(Listen, _cts.Token);
                IsRunning = true;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new WebSocketErrorEventArgs(null, ex));
                throw;
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            try
            {
                CloseAllClients();

                _clients?.Clear();
                _server?.Stop();
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                IsRunning = false;

                Closed?.Invoke(this, new WebSocketClosedEventArgs(null));
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new WebSocketErrorEventArgs(null, ex));
                throw;
            }
        }

        private void CloseClient(TcpClient client, bool sendFrame = true)
        {
            var wsClient = _clients?.SingleOrDefault(c => c.Client == client);

            try
            {
                if (sendFrame && client != null && client.Connected)
                {
                    WriteMessage(client, null, Opcode.ConnectionCloseFrame);
                    client.Close();
                }

                if (wsClient == null)
                {
                    wsClient.Cancellation.Cancel();
                    _clients.Remove(wsClient);
                }

                Closed?.Invoke(this, new WebSocketClosedEventArgs(client));
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new WebSocketErrorEventArgs(client, ex));
                wsClient?.Cancellation?.Cancel();
                throw;
            }
        }

        public void CloseAllClients()
        {
            foreach (var client in _clients)
            {
                try
                {
                    CloseClient(client.Client);
                }
                catch (Exception ex)
                {
                    Error?.Invoke(this, new WebSocketErrorEventArgs(null, ex));
                }
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

                Opened?.Invoke(this, new WebSocketOpenedEventArgs(null));

                // Enter the listening loop
                while (true)
                {
                    if (_cts.IsCancellationRequested) break;

                    // Perform a blocking call to accept requests
                    // Can use server.AcceptSocket() or _server.AcceptTcpClient()
                    var client = _server.AcceptTcpClient();

                    // Process request
                    var ctsClient = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                    var clientTask = Task.Run(() => Process(client), ctsClient.Token);

                    _clients.Add(new WebSocketClient(client, ctsClient));
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new WebSocketErrorEventArgs(null, ex));
                throw;
            }
        }

        private void Process(TcpClient client)
        {
            client.ReceiveTimeout = (int)_streamTimeout.TotalMilliseconds;
            client.SendTimeout = (int)_streamTimeout.TotalMilliseconds;

            try
            {
                var netStream = client.GetStream();

                while (true)
                {
                    if (_cts.IsCancellationRequested) break;

                    // Perform a blocking call to receive data
                    var bytes = ReadClient(client, netStream);
                    var str = Encoding.UTF8.GetString(bytes);

                    var isHandshake = Regex.IsMatch(str, "^GET", RegexOptions.IgnoreCase);

                    if (isHandshake)
                    {
                        Handshake(netStream, str);
                        Opened?.Invoke(this, new WebSocketOpenedEventArgs(client));
                    }
                    else
                    {
                        var (opcode, message) = ReadMessage(bytes);

                        if (opcode == Opcode.TextFrame)
                            Data?.Invoke(this, new WebSocketDataEventArgs(client, message));
                        else if (opcode == Opcode.ConnectionCloseFrame)
                            CloseClient(client, sendFrame: false);
                        //else if (opcode == Opcode.PongFrame)
                    }
                }

                // Shutdown and end connection (on task cancellation)
                if (client != null && client.Connected) client.Close();
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new WebSocketErrorEventArgs(client, ex));
                try { CloseClient(client); }
                catch { Error?.Invoke(this, new WebSocketErrorEventArgs(client, ex)); }
                throw;
            }
        }

        private byte[] ReadClient(TcpClient client, NetworkStream netStream)
        {
            while (!netStream.CanRead || !netStream.DataAvailable) ;
            while (client.Available < 3) ; // match against "get"

            var bytes = new byte[client.Available];
            netStream.Read(bytes, 0, client.Available);

            return bytes;
        }

        private void Handshake(NetworkStream stream, string str)
        {
            //Console.WriteLine("=====Handshaking from client=====\n{0}", str);

            // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
            // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
            // 3. Compute SHA-1 and Base64 hash of the new value
            // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
            string swk = Regex.Match(str, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
            string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
            string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

            // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
            byte[] response = Encoding.UTF8.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: websocket\r\n" +
                "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

            stream.Write(response, 0, response.Length);
        }

        private (Opcode, string) ReadMessage(byte[] bytes)
        {
            bool fin = (bytes[0] & 0b10000000) != 0,
                mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

            if (!mask) throw new Exception("Mask bit not set");

            int opcodeInt = bytes[0] & 0b00001111;

            if (!Enum.IsDefined(typeof(Opcode), opcodeInt))
                throw new Exception("Invalid opcode");

            var opcode = (Opcode)opcodeInt;

            return (opcode, DecodeMessage(bytes));
        }

        private void WriteMessage(TcpClient client, string message, Opcode opcode = Opcode.TextFrame)
        {
            var stream = client.GetStream();
            var bytes = EncodeMessageToSend(message, opcode);
            stream.Write(bytes);
        }

        private string DecodeMessage(byte[] bytes)
        {
            byte secondByte = bytes[1];
            int dataLength = secondByte & 127;
            int indexFirstMask = 2;

            if (dataLength == 126)
                indexFirstMask = 4;
            else if (dataLength == 127)
                indexFirstMask = 10;

            var keys = bytes.Skip(indexFirstMask).Take(4);
            var indexFirstDataByte = indexFirstMask + 4;

            var decoded = new byte[bytes.Length - indexFirstDataByte];
            for (int i = indexFirstDataByte, j = 0; i < bytes.Length; i++, j++)
                decoded[j] = (byte)(bytes[i] ^ keys.ElementAt(j % 4));

            return Encoding.UTF8.GetString(decoded, 0, decoded.Length);
        }

        private byte[] EncodeMessageToSend(string message, Opcode opcode)
        {
            byte[] response;
            byte[] bytesRaw = message != null ? Encoding.UTF8.GetBytes(message) : new byte[0];
            byte[] frame = new byte[10];

            int indexStartRawData;
            int length = bytesRaw.Length;

            frame[0] = (byte)(0b10000000 | (int)opcode);
            if (length <= 125)
            {
                frame[1] = (byte)length;
                indexStartRawData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame[1] = (byte)126;
                frame[2] = (byte)((length >> 8) & 255);
                frame[3] = (byte)(length & 255);
                indexStartRawData = 4;
            }
            else
            {
                frame[1] = (byte)127;
                frame[2] = (byte)((length >> 56) & 255);
                frame[3] = (byte)((length >> 48) & 255);
                frame[4] = (byte)((length >> 40) & 255);
                frame[5] = (byte)((length >> 32) & 255);
                frame[6] = (byte)((length >> 24) & 255);
                frame[7] = (byte)((length >> 16) & 255);
                frame[8] = (byte)((length >> 8) & 255);
                frame[9] = (byte)(length & 255);

                indexStartRawData = 10;
            }

            response = new byte[indexStartRawData + length];

            int i, reponseIdx = 0;

            // Add the frame bytes to the reponse
            for (i = 0; i < indexStartRawData; i++)
            {
                response[reponseIdx] = frame[i];
                reponseIdx++;
            }

            // Add the data bytes to the response
            for (i = 0; i < length; i++)
            {
                response[reponseIdx] = bytesRaw[i];
                reponseIdx++;
            }

            return response;
        }

        private class WebSocketClient
        {
            public WebSocketClient(TcpClient client, CancellationTokenSource cancellation)
            {
                Client = client;
                Cancellation = cancellation;
            }

            public TcpClient Client { get; set; }
            public CancellationTokenSource Cancellation { get; set; }
        }

        private enum Opcode
        {
            ContinuationFrame = 0,
            TextFrame = 1,
            BinaryFrame = 2,
            ConnectionCloseFrame = 8,
            PingFrame = 9,
            PongFrame = 10
        }
    }
}

// Read example

//static string ReadMessage(byte[] bytes)
//{
//    string message = null;

//    bool fin = (bytes[0] & 0b10000000) != 0,
//        mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

//    int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
//        msglen = bytes[1] - 128, // & 0111 1111
//        offset = 2;

//    if (msglen == 126)
//    {
//        // was ToUInt16(bytes, offset) but the result is incorrect
//        msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
//        offset = 4;
//    }
//    else if (msglen == 127)
//    {
//        Console.WriteLine("TODO: msglen == 127, needs qword to store msglen");
//        // i don't really know the byte order, please edit this
//        // msglen = BitConverter.ToUInt64(new byte[] { bytes[5], bytes[4], bytes[3], bytes[2], bytes[9], bytes[8], bytes[7], bytes[6] }, 0);
//        // offset = 10;
//    }

//    if ((Opcode)opcode == Opcode.TextFrame)
//    {
//        if (msglen == 0)
//        {
//            throw new Exception("Received empty message");
//        }
//        else if (mask)
//        {
//            byte[] decoded = new byte[msglen];
//            byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
//            offset += 4;

//            for (int i = 0; i < msglen; ++i)
//                decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

//            message = Encoding.UTF8.GetString(decoded);
//        }
//        else
//        {
//            throw new Exception("Mask bit not set");
//        }
//    }
//    else if ((Opcode)opcode == Opcode.ConnectionCloseFrame)
//    {
//        //CloseConnection();
//    }
//    else
//    {
//        throw new Exception("Invalid opcode");
//    }

//    return message;
//}