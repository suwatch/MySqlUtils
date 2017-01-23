using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MySqlUtils
{
    public class DefaultHandler : HttpTaskAsyncHandler
    {
        static Dictionary<string, Func<Tracer, HttpTaskAsyncHandler>> _handlers;

        static DefaultHandler()
        {
            var handlers = new Dictionary<string, Func<Tracer, HttpTaskAsyncHandler>>(StringComparer.OrdinalIgnoreCase);
            handlers[Utils.GetPath("post {0}/dump")] = tracer => new MySqlDump(tracer);
            handlers[Utils.GetPath("post {0}/dump/status")] = tracer => new MySqlDump(tracer);
            handlers[Utils.GetPath("post {0}/execute")] = tracer => new MySqlExecute(tracer);
            _handlers = handlers;
        }

        public override bool IsReusable
        {
            get { return true; }
        }

        public override async Task ProcessRequestAsync(HttpContext context)
        {
            using (var tracer = new Tracer(context))
            {
                try
                {
                    if (context.IsWebSocketRequest)
                    {
                        context.AcceptWebSocketRequest(HandleWebSocket);
                    }
                    else
                    {
                        var handler = GetHandler(context, tracer);
                        await handler.ProcessRequestAsync(context);
                    }
                }
                catch (Exception ex)
                {
                    tracer.Trace(ex);
                    throw;
                }
            }
        }

        private HttpTaskAsyncHandler GetHandler(HttpContext context, Tracer tracer)
        {
            var path = context.Request.RawUrl.Split(new[] { '?' })[0];
            var key = string.Join(" ", context.Request.HttpMethod, path);
            Func<Tracer, HttpTaskAsyncHandler> handlerFactory;
            if (_handlers.TryGetValue(key, out handlerFactory))
            {
                return handlerFactory(tracer);
            }

            return new MySqlHandler(tracer);
        }

        private async Task HandleWebSocket(WebSocketContext context)
        {
            var buffer = new byte[4096];
            var socket = context.WebSocket;
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Cannot accept binary frame", CancellationToken.None);
                }
                else
                {
                    int count = result.Count;

                    //while (!result.EndOfMessage)
                    //{
                    //    if (count >= buffer.Length)
                    //    {
                    //        string closeMessage = string.Format("Maximum message size: {0} bytes.", buffer.Length);
                    //        await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeMessage, CancellationToken.None);
                    //        return;
                    //    }

                    //    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, count, maxMessageSize - count), CancellationToken.None);
                    //    count += result.Count;
                    //}

                    var receivedString = Encoding.UTF8.GetString(buffer, 0, count);
                    var echoString = "You said " + receivedString;
                    var output = new ArraySegment<byte>(Encoding.UTF8.GetBytes(echoString));
                    await socket.SendAsync(output, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }
}
