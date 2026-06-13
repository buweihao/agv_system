using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfApp16.Models;

namespace WpfApp16.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private AppConfig _config = new();
        private string _taskId = string.Empty;
        private string _source = "A01";
        private string _target = "B01";
        private string _logContent = string.Empty;
        
        // RequestControl Fields
        private string _areaId = "Area01";
        private int _agvId = 1;
        private int _requestType = 1;

        // Server Listener Fields
        private HttpListener? _httpListener;
        private CancellationTokenSource? _listenerCts;
        private bool _isListening = false;
        private string _listenStatusText = "已停止";
        private string _listenStatusColor = "#A6ADC8"; // Gray/Inactive

        public MainViewModel()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            
            // Initialize Client Commands
            GetAgvInfosCommand = new AsyncRelayCommand(_ => GetAgvInfosAsync());
            GetTaskInfosCommand = new AsyncRelayCommand(_ => GetTaskInfosAsync());
            TaskDownloadCommand = new AsyncRelayCommand(_ => TaskDownloadAsync());
            DeleteTaskCommand = new AsyncRelayCommand(_ => DeleteTaskAsync());
            RequestControlCommand = new AsyncRelayCommand(_ => RequestControlAsync());
            GenerateTaskIdCommand = new RelayCommand(_ => TaskId = Guid.NewGuid().ToString("N").ToUpper());
            ClearLogCommand = new RelayCommand(_ => LogContent = string.Empty);

            // Initialize Server Commands
            StartListenCommand = new AsyncRelayCommand(_ => StartListeningAsync(), _ => !IsListening);
            StopListenCommand = new RelayCommand(_ => StopListening(), _ => IsListening);

            // Set initial TaskId
            GenerateTaskIdCommand.Execute(null);
        }

        #region Properties

        public AppConfig Config
        {
            get => _config;
            set { _config = value; OnPropertyChanged(); }
        }

        public string TaskId
        {
            get => _taskId;
            set { _taskId = value; OnPropertyChanged(); }
        }

        public string Source
        {
            get => _source;
            set { _source = value; OnPropertyChanged(); }
        }

        public string Target
        {
            get => _target;
            set { _target = value; OnPropertyChanged(); }
        }

        public string LogContent
        {
            get => _logContent;
            set { _logContent = value; OnPropertyChanged(); }
        }

        public string AreaId
        {
            get => _areaId;
            set { _areaId = value; OnPropertyChanged(); }
        }

        public int AgvId
        {
            get => _agvId;
            set { _agvId = value; OnPropertyChanged(); }
        }

        public int RequestType
        {
            get => _requestType;
            set { _requestType = value; OnPropertyChanged(); }
        }

        public bool IsListening
        {
            get => _isListening;
            private set 
            { 
                _isListening = value; 
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ListenStatusText
        {
            get => _listenStatusText;
            private set { _listenStatusText = value; OnPropertyChanged(); }
        }

        public string ListenStatusColor
        {
            get => _listenStatusColor;
            private set { _listenStatusColor = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        public ICommand GetAgvInfosCommand { get; }
        public ICommand GetTaskInfosCommand { get; }
        public ICommand TaskDownloadCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand RequestControlCommand { get; }
        public ICommand GenerateTaskIdCommand { get; }
        public ICommand ClearLogCommand { get; }
        
        public ICommand StartListenCommand { get; }
        public ICommand StopListenCommand { get; }

        #endregion

        #region Server Listener Logic

        private async Task StartListeningAsync()
        {
            if (IsListening) return;

            _httpListener = new HttpListener();
            string prefix = Config.ListenUrl;
            if (!prefix.EndsWith("/")) prefix += "/";
            _httpListener.Prefixes.Add(prefix);

            try
            {
                _httpListener.Start();
                IsListening = true;
                ListenStatusText = $"🟢 正在监听: {prefix}";
                ListenStatusColor = "#A6E3A1"; // Green
                
                _listenerCts = new CancellationTokenSource();
                
                // Run loop in background thread
                _ = Task.Run(() => ListenLoopAsync(_httpListener, _listenerCts.Token), _listenerCts.Token);
                
                Log($"[SERVER START] 已启动 HTTP 监听: {prefix}");
            }
            catch (HttpListenerException ex)
            {
                ListenStatusText = "监听失败 (权限不足)";
                ListenStatusColor = "#F38BA8"; // Red
                Log($"[SERVER ERROR] 监听失败：权限不足。请以管理员身份运行程序，或将地址改为 localhost/127.0.0.1。详细异常: {ex.Message}", isError: true);
                _httpListener.Close();
            }
            catch (Exception ex)
            {
                ListenStatusText = "监听异常";
                ListenStatusColor = "#F38BA8"; // Red
                Log($"[SERVER ERROR] 启动监听异常: {ex.Message}", isError: true);
                _httpListener.Close();
            }
        }

        public void StopListening()
        {
            if (!IsListening) return;

            try
            {
                _listenerCts?.Cancel();
                _httpListener?.Stop();
                _httpListener?.Close();
            }
            catch (Exception ex)
            {
                Log($"[SERVER ERROR] 停止监听时发生异常: {ex.Message}", isError: true);
            }
            finally
            {
                IsListening = false;
                ListenStatusText = "已停止";
                ListenStatusColor = "#A6ADC8";
                Log("[SERVER STOP] 已停止 HTTP 监听。");
            }
        }

        private async Task ListenLoopAsync(HttpListener listener, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && listener.IsListening)
                {
                    // Wait for incoming request
                    var context = await listener.GetContextAsync();
                    
                    // Fire and forget handling to accept new requests immediately
                    _ = HandleIncomingRequestAsync(context);
                }
            }
            catch (HttpListenerException)
            {
                // Typically occurs when the listener is stopped/aborted
            }
            catch (ObjectDisposedException)
            {
                // Listener disposed
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    Application.Current.Dispatcher.Invoke(() => Log($"[SERVER ERROR] 监听循环异常: {ex.Message}", isError: true));
                }
            }
        }

        private async Task HandleIncomingRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            string requestBody = "";

            try
            {
                if (request.HasEntityBody)
                {
                    using var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
                    requestBody = await reader.ReadToEndAsync();
                    
                    // Try pretty print JSON
                    try 
                    {
                        var parsedJson = JsonSerializer.Deserialize<JsonElement>(requestBody);
                        requestBody = JsonSerializer.Serialize(parsedJson, new JsonSerializerOptions { WriteIndented = true });
                    }
                    catch { /* keep raw body */ }
                }

                // Log the request on UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Log($"[CALLBACK RECV] {request.HttpMethod} {request.Url}\nBody: {requestBody}");
                });

                // Prepare Response
                string jsonResponse = "";
                if (request.Url != null && request.Url.AbsolutePath.Contains("RequestControl", StringComparison.OrdinalIgnoreCase))
                {
                    var responseObj = new RequestControlResponse { Code = 0, Message = "success" };
                    jsonResponse = JsonSerializer.Serialize(responseObj);
                }
                else
                {
                    var responseObj = new ReturnTaskStateResponse { Code = 0, Message = "success" };
                    jsonResponse = JsonSerializer.Serialize(responseObj);
                }
                
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

                response.ContentType = "application/json";
                response.StatusCode = 200;
                response.ContentLength64 = buffer.Length;
                
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Log($"[SERVER ERROR] 处理请求异常: {ex.Message}", isError: true);
                });
                response.StatusCode = 500;
            }
            finally
            {
                // Ensure output stream is closed
                response.OutputStream.Close();
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Log(new string('-', 50));
                });
            }
        }

        #endregion

        #region API Client Methods

        private async Task GetAgvInfosAsync()
        {
            await SendRequestAsync(HttpMethod.Get, Config.ApiGetAgvInfos);
        }

        private async Task GetTaskInfosAsync()
        {
            await SendRequestAsync(HttpMethod.Get, Config.ApiGetTaskInfos);
        }

        private async Task TaskDownloadAsync()
        {
            var body = new TaskDownloadRequest
            {
                TaskId = this.TaskId,
                Source = this.Source,
                Target = this.Target,
                CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            await SendRequestAsync(HttpMethod.Post, Config.ApiTaskDownload, body);
        }

        private async Task DeleteTaskAsync()
        {
            var body = new DeleteTaskRequest { TaskId = this.TaskId };
            await SendRequestAsync(HttpMethod.Post, Config.ApiDeleteTask, body);
        }

        private async Task RequestControlAsync()
        {
            var body = new RequestControlRequest
            {
                AreaId = this.AreaId,
                AgvId = this.AgvId,
                RequestType = this.RequestType,
                RequestTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            await SendRequestAsync(HttpMethod.Post, Config.ApiRequestControl, body);
        }

        #endregion

        #region Helper Methods

        private async Task SendRequestAsync(HttpMethod method, string endpoint, object? body = null)
        {
            string url = Config.BaseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/');
            string jsonBody = body != null ? JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true }) : "None";

            Log($"[SEND] {method} {url}\nBody: {jsonBody}");

            try
            {
                using var request = new HttpRequestMessage(method, url);
                if (body != null)
                {
                    request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                }

                var response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                
                // Try to format JSON if possible
                try 
                {
                    var parsedJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    responseContent = JsonSerializer.Serialize(parsedJson, new JsonSerializerOptions { WriteIndented = true });
                }
                catch { /* Not a JSON or invalid */ }

                Log($"[RECV] Status: {(int)response.StatusCode} {response.StatusCode}\nResponse: {responseContent}");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] {ex.Message}", isError: true);
            }
            Log(new string('-', 50));
        }

        private void Log(string message, bool isError = false)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string formattedMessage = $"[{timestamp}] {(isError ? "!!! " : "")}{message}\n";
            LogContent += formattedMessage;
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
