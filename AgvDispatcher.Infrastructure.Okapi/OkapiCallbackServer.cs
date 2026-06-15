using AgvDispatcher.Infrastructure.Okapi.Models;
using System.Net;
using System.Text.Json;
using System.Text;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiCallbackServer
    {
        private readonly OkapiOptions _options;
        private readonly OkapiProtocolLogger _logger;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _listenTask;
        private readonly object _syncRoot = new();

        public event EventHandler<ReturnTaskStateRequest>? TaskStateReceived;
        public event EventHandler<RequestControlRequest>? AreaControlReceived;

        public OkapiCallbackServer(OkapiOptions options, OkapiProtocolLogger logger)
        {
            _options = options;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken token)
        {
            if (!_options.EnableCallbackServer)
            {
                return Task.CompletedTask;
            }

            lock (_syncRoot)
            {
                if (_listenTask != null)
                {
                    return Task.CompletedTask; // Already running
                }

                _cts = new CancellationTokenSource();
                _listener = new HttpListener();
                
                var url = _options.ListenUrl;
                if (!url.EndsWith("/"))
                {
                    url += "/";
                }
                
                try
                {
                    _listener.Prefixes.Add(url);
                    _listener.Start();
                }
                catch (HttpListenerException ex)
                {
                    _logger.LogError("System", "CallbackServerStart", $"Failed to start listener on {url}. Ensure running as admin or URL ACL configured: {ex.Message}");
                    throw; // Important to fail fast if we can't listen
                }

                _listenTask = ListenLoopAsync(_cts.Token);
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken token)
        {
            CancellationTokenSource? ctsToCancel;
            Task? taskToWait;
            HttpListener? listenerToStop;

            lock (_syncRoot)
            {
                ctsToCancel = _cts;
                taskToWait = _listenTask;
                listenerToStop = _listener;

                _cts = null;
                _listenTask = null;
                _listener = null;
            }

            if (ctsToCancel != null)
            {
                ctsToCancel.Cancel();
                listenerToStop?.Stop();

                try
                {
                    if (taskToWait != null)
                    {
                        await taskToWait.WaitAsync(token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError("System", "CallbackServerStop", $"Error while stopping: {ex.Message}");
                }
                finally
                {
                    ctsToCancel.Dispose();
                    listenerToStop?.Close();
                }
            }
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener!.GetContextAsync().WaitAsync(token);
                    _ = Task.Run(() => ProcessRequestAsync(context), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("System", "CallbackServerListen", $"Listener exception: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            string rawBody = string.Empty;

            try
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                rawBody = await reader.ReadToEndAsync();

                _logger.LogCallback(request.Url?.PathAndQuery ?? "unknown", rawBody);

                if (request.HttpMethod != "POST")
                {
                    response.StatusCode = 405; // Method Not Allowed
                    return;
                }

                var path = request.Url?.AbsolutePath ?? string.Empty;
                object? responseObject = null;

                if (path.EndsWith("/ReturnTaskState", StringComparison.OrdinalIgnoreCase))
                {
                    var req = JsonSerializer.Deserialize<ReturnTaskStateRequest>(rawBody);
                    if (req == null)
                    {
                        response.StatusCode = 400; // Bad Request
                        return;
                    }
                    TaskStateReceived?.Invoke(this, req);
                    responseObject = new ReturnTaskStateResponse { Code = 0, Message = "success" };
                }
                else if (path.EndsWith("/RequestControl", StringComparison.OrdinalIgnoreCase))
                {
                    var req = JsonSerializer.Deserialize<RequestControlRequest>(rawBody);
                    if (req == null)
                    {
                        response.StatusCode = 400; // Bad Request
                        return;
                    }
                    AreaControlReceived?.Invoke(this, req);
                    responseObject = new RequestControlResponse { Code = 0, Message = "success" };
                }
                else
                {
                    response.StatusCode = 404; // Not Found
                    return;
                }

                var responseJson = JsonSerializer.Serialize(responseObject);
                var buffer = Encoding.UTF8.GetBytes(responseJson);
                
                response.StatusCode = 200;
                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (JsonException ex)
            {
                _logger.LogParseError("Unknown", "CallbackParse", ex.Message, rawBody, null);
                response.StatusCode = 400; // Bad Request
            }
            catch (Exception ex)
            {
                _logger.LogError("Unknown", "CallbackProcess", ex.Message, null);
                response.StatusCode = 500; // Internal Server Error
            }
            finally
            {
                response.Close();
            }
        }
    }
}
