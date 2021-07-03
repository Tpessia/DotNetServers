using DotNetServers.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DotNetServers.Http
{
    public static class HttpParser
    {
        public static Dictionary<HttpStatusCode, string> StatusDescriptions = new Dictionary<HttpStatusCode, string>
        {
            { HttpStatusCode.OK, "200 OK" },
            { HttpStatusCode.Created, "201 Created" },
            { HttpStatusCode.Accepted, "202 Accepted" },
            { HttpStatusCode.NoContent, "204 No Content" },
            { HttpStatusCode.MovedPermanently, "301 Moved Permanently" },
            { HttpStatusCode.Redirect, "302 Found" },
            { HttpStatusCode.NotModified, "304 Not Modified" },
            { HttpStatusCode.BadRequest, "400 Bad Request" },
            { HttpStatusCode.Unauthorized, "401 Unauthorized" },
            { HttpStatusCode.Forbidden, "403 Forbidden" },
            { HttpStatusCode.NotFound, "404 Not Found" },
            { HttpStatusCode.InternalServerError, "500 Internal Server Error" },
            { HttpStatusCode.NotImplemented, "501 Not Implemented" },
            { HttpStatusCode.BadGateway, "502 Bad Gateway" },
            { HttpStatusCode.ServiceUnavailable, "503 Service Unavailable" }
        };

        public static HttpRequest ParseRequest(string rawRequest)
        {
            var request = new HttpRequest();

            if (string.IsNullOrWhiteSpace(rawRequest)) return request;

            try
            {
                string headers;

                (request.HttpMethod, rawRequest, _) = rawRequest.Split(" ", 2);
                (request.Url, rawRequest, _) = rawRequest.Split(" ", 2);
                (request.HttpVersion, rawRequest, _) = rawRequest.Split(new[] { "\r\n", "\r", "\n" }, 2, StringSplitOptions.None);
                (headers, rawRequest, _) = rawRequest.Split(new[] { "\r\n\r\n", "\r\r", "\n\n" }, 2, StringSplitOptions.None);
                request.Body = rawRequest;

                request.Headers = headers.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Select(x => x.Split(": ")).ToDictionary(x => x[0], x => x[1]);
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to parse the HTTP request", ex);
            }

            return request;
        }

        public static string BuildResponse(HttpResponse response)
        {
            response.Headers.Remove("Content-Length");

            var customContentType = response.Headers.Remove("Content-Type", out var contentType);
            if (!customContentType) contentType = "text/plain";

            var headers = response.Headers.Join(Environment.NewLine, x => $"{x.Key}: {x.Value}");
            if (headers.Any()) headers += "\r\n";

            return $"{response.HttpVersion} {StatusDescriptions[response.Status]}" + "\r\n"
                + "Content-Length: " + response.Body.Length + "\r\n"
                + "Content-Type: " + contentType + "\r\n"
                + headers + "\r\n"
                + response.Body;
        }
    }

    public class HttpRequest
    {
        public string HttpMethod { get; set; }
        public string Url { get; set; }
        public string HttpVersion { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }

    public class HttpResponse
    {
        public HttpResponse(HttpStatusCode status, string body, string httpVersion = null, Dictionary<string, string> headers = null)
        {
            Status = status;
            if (body != null) Body = body;
            if (!string.IsNullOrWhiteSpace(httpVersion)) HttpVersion = httpVersion;
            if (headers != null) Headers = headers;
        }

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Body { get; set; } = "";
        public string HttpVersion { get; set; } = "HTTP/1.1";
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    }
}


// Request Example:
// POST /abc?a=1 HTTP/1.1
// Host: 192.168.0.13:8000
// Connection: keep-alive
// Content-Length: 12
// Pragma: no-cache
// Cache-Control: no-cache
// User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36
// Content-Type: text/plain;charset=UTF-8
// Accept: */*
// Origin: http://192.168.0.13:8000
// Referer: http://192.168.0.13:8000/
// Accept-Encoding: gzip, deflate
// Accept-Language: pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7,la;q=0.6
// 
// {"test":123}

// Response Example:
// HTTP/1.0 200 OK
// Content-Length: 17
// Content-Type: "text/plain"
// {"hello":"world"}