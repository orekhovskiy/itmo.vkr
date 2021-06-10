using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace TestNetCoreConsole
{
    class MyWebSvc : IDisposable
    {
        readonly HttpListener _listener;
        readonly Uri _prefix;

        public event Action<WebSocketContext> OnWebSocketConnection;

        public MyWebSvc(string prefix)
        {
            _prefix = new Uri(prefix);
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            _listener.BeginGetContext(this.AcceptContextProc, null);
        }

        private void AcceptContextProc(IAsyncResult ar)
        {
            bool restarted= false;
            try
            {
                var ctx = _listener.EndGetContext(ar);
                _listener.BeginGetContext(this.AcceptContextProc, null);
                restarted = true;
                this.HandleRequest(ctx);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if (!restarted)
                    _listener.BeginGetContext(this.AcceptContextProc, null);
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            Console.WriteLine($"Incoming {ctx.Request.HttpMethod} request: {ctx.Request.RawUrl}");
            switch (ctx.Request.RawUrl)
            {
                case "/mysvc/rtcsignaller":
                    {
                        var task = ctx.AcceptWebSocketAsync(null);
                        task.GetAwaiter().OnCompleted(() => this.OnWebSocketAccepted(task.Result));
                    }
                    break;
                default:
                    {
                        var msg = $"OK at {DateTime.Now}";
                        var data = Encoding.UTF8.GetBytes(msg);
                        ctx.Response.ContentLength64 = data.Length;
                        ctx.Response.ContentType = "text/plain";
                        ctx.Response.ContentEncoding = Encoding.UTF8;
                        ctx.Response.StatusCode = 200;
                        ctx.Response.OutputStream.Write(data);
                        ctx.Response.OutputStream.Close();
                    }
                    break;
            }

        }

        private void OnWebSocketAccepted(HttpListenerWebSocketContext wsCtx)
        {
            this.OnWebSocketConnection?.Invoke(wsCtx);
        }

        public void Dispose()
        {
            _listener.Stop();
        }
    }
}
