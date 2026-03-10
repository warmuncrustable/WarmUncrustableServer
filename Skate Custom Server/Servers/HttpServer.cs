using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;

namespace Servers
{
    public class HttpServer
    {
        private readonly ushort _port;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _serverTask;

        public HttpServer(ushort port)
        {
            _port = port;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Prefixes.Add($"http://192.168.4.132:{_port}/");
            _listener.Start();
            Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
            Console.WriteLine($"HTTP server started on port {_port}");

            _serverTask = RunAsync(_cts.Token);
        }

        public async Task StopAsync()
        {
            if (_cts == null || _listener == null || _serverTask == null)
                return;

            _cts.Cancel();

            try
            {
                await _serverTask;
            }
            catch { }

            _listener.Stop();
            _listener.Close();
            _cts.Dispose();
        }

        private async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;

                try
                {
                    ctx = await _listener!.GetContextAsync().WaitAsync(ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                _ = Task.Run(() => HandleRequest(ctx, ct));
            }
        }

        private async Task HandleRequest(HttpListenerContext ctx, CancellationToken ct)
        {
            var req = ctx.Request;
            var res = ctx.Response;

            try
            {
                // headers the game expects
                res.Headers["Server"] = "Microsoft-IIS/6.0";
                res.Headers["Accept-Ranges"] = "bytes";
                res.Headers["X-Powered-By"] = "ASP.NET";
                res.Headers["Cache-Control"] = "no-cache";

                string path = req.Url!.AbsolutePath.TrimEnd('/');

                if (path == "")
                    path = "/";

                Console.WriteLine($"HTTP {req.HttpMethod} {path}");

                // =========================
                // SERVER STATS ENDPOINT
                // =========================
                if (path == "/serverstats")
                {
                    string json = new JObject(
                        new JProperty("signed-in", ServerGlobals.Users.Count),
                        new JProperty("dirtycast-instances", ServerGlobals.LobbyRelayServers.Count)
                    ).ToString();

                    byte[] data = Encoding.UTF8.GetBytes(json);

                    res.ContentType = "application/json";
                    res.ContentLength64 = data.Length;

                    await res.OutputStream.WriteAsync(data, 0, data.Length, ct);
                }
                else if (path == "/players")
                {
                    var arr = new JArray();

                    foreach (var entry in ServerGlobals.Users)
                    {
                        var user = entry.Value;

        		string name = user.UserIdentification.Name;
        		string status = "idle";
        		uint gameId = 0;

        		if (!user.IsAuthenticated)
            		    status = "authenticating";
        		else if (user.isMatchmaking)
            		    status = "matchmaking";
        		else if (user.CurrentGame != null)
        		{
            		    status = "in-game";
            		    gameId = user.CurrentGame.GameData.GameId;
        		}

                        arr.Add(new JObject(
                            new JProperty("id", entry.Key),
                            new JProperty("name", name),
                            new JProperty("status", status),
                            new JProperty("gameId", gameId)
                        ));
                    }

                    byte[] data = Encoding.UTF8.GetBytes(arr.ToString());

                    res.ContentType = "application/json";
                    res.ContentLength64 = data.Length;

                    await res.OutputStream.WriteAsync(data, 0, data.Length, ct);
                }
                // =========================
                // ADMIN ANNOUNCE ENDPOINT
                // =========================
                else if (path == "/announce")
                {
                    string message = req.QueryString["msg"] ?? "Hello skaters!";

                    Console.WriteLine($"[ADMIN MESSAGE] {message}");

                    foreach (var user in ServerGlobals.Users)
                    {
                        Console.WriteLine($"Sending to user: {user}");
                        // Later you can trigger a WebKit popup here
                    }

                    byte[] data = Encoding.UTF8.GetBytes("ok");
                    await res.OutputStream.WriteAsync(data, 0, data.Length, ct);
                }

                // =========================
                // STATIC FILES
                // =========================
                else
                {
                    if (path.ToLower().Contains("skate3/webkit"))
                        path = "/skate3/webkit/temp.html";

                    string basePath = AppDomain.CurrentDomain.BaseDirectory;

                    string filePath = Path.Combine(
                        basePath,
                        "wwwroot",
                        path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                    );

                    if (File.Exists(filePath))
                    {
                        byte[] data = await File.ReadAllBytesAsync(filePath, ct);

                        res.ContentType = GetContentType(filePath);
                        res.ContentLength64 = data.Length;

                        await res.OutputStream.WriteAsync(data, 0, data.Length, ct);
                    }
                    else
                    {
                        res.StatusCode = 404;

                        byte[] data = Encoding.UTF8.GetBytes("404 Not Found");
                        await res.OutputStream.WriteAsync(data, 0, data.Length, ct);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"HTTP error: {e.Message}");
            }
            finally
            {
                res.Close();
            }
        }

        private string GetContentType(string file)
        {
            string ext = Path.GetExtension(file).ToLower();

            return ext switch
            {
                ".xml" => "text/xml",
                ".html" => "text/html",
                ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".psg" => "application/octet-stream",
                _ => "application/octet-stream"
            };
        }
    }
}
