using AgvDispatcher.Infrastructure.Okapi.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiClient
    {
        private readonly HttpClient _httpClient;
        private readonly OkapiOptions _options;
        private readonly OkapiProtocolLogger _logger;

        public OkapiClient(HttpClient httpClient, OkapiOptions options, OkapiProtocolLogger logger)
        {
            _httpClient = httpClient;
            _options = options;
            _httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            _logger = logger;
        }

        public Task<OkapiApiResult> GetAgvInfosAsync(CancellationToken token)
        {
            return SendAsync<object>(HttpMethod.Get, null, _options.ApiGetAgvInfos, "GetAgvInfos", "System", null, token);
        }
        
        public Task<OkapiApiResult> GetTaskInfosAsync(CancellationToken token)
        {
            return SendAsync<object>(HttpMethod.Get, null, _options.ApiGetTaskInfos, "GetTaskInfos", "System", null, token);
        }

        public Task<OkapiApiResult> TaskDownloadAsync(TaskDownloadRequest request, string vehicleId, CancellationToken token)
        {
            return SendAsync(HttpMethod.Post, request, _options.ApiTaskDownload, "TaskDownload", vehicleId, request.TaskId, token);
        }

        public Task<OkapiApiResult> DeleteTaskAsync(DeleteTaskRequest request, string vehicleId, CancellationToken token)
        {
            return SendAsync(HttpMethod.Post, request, _options.ApiDeleteTask, "DeleteTask", vehicleId, request.TaskId, token);
        }

        public Task<OkapiApiResult> RequestControlAsync(RequestControlRequest request, string vehicleId, CancellationToken token)
        {
            return SendAsync(HttpMethod.Post, request, _options.ApiRequestControl, "RequestControl", vehicleId, null, token);
        }

        private async Task<OkapiApiResult> SendAsync<TRequest>(HttpMethod method, TRequest? request, string endpoint, string commandType, string vehicleId, string? taskId = null, CancellationToken token = default)
        {
            var url = $"{_options.BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
            var result = new OkapiApiResult();

            try
            {
                _logger.LogSend(vehicleId, commandType, url, request ?? new object(), taskId);
                
                var requestMessage = new HttpRequestMessage(method, url);
                if (method != HttpMethod.Get && request != null)
                {
                    requestMessage.Content = JsonContent.Create(request);
                }

                var response = await _httpClient.SendAsync(requestMessage, token).ConfigureAwait(false);
                
                result.HttpStatusCode = (int)response.StatusCode;
                var rawResponse = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                result.RawResponse = rawResponse;

                if (!response.IsSuccessStatusCode)
                {
                    result.Success = false;
                    result.Message = $"HTTP Error {result.HttpStatusCode}: {rawResponse}";
                    _logger.LogError(vehicleId, commandType, result.Message, taskId);
                    return result;
                }

                _logger.LogReceive(vehicleId, commandType, rawResponse, taskId);

                using var doc = JsonDocument.Parse(rawResponse);
                if (doc.RootElement.TryGetProperty("code", out var codeProp) && codeProp.TryGetInt32(out var code))
                {
                    result.VendorCode = code;
                    if (code != 0)
                    {
                        var msg = doc.RootElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown vendor error";
                        result.Success = false;
                        result.Message = msg ?? "Unknown vendor error";
                        _logger.LogVendorRejected(vehicleId, commandType, result.Message, taskId);
                        return result;
                    }
                }

                result.Success = true;
                return result;
            }
            catch (TaskCanceledException)
            {
                result.Success = false;
                result.Message = "Timeout";
                _logger.LogTimeout(vehicleId, commandType, url, taskId);
                return result;
            }
            catch (JsonException ex)
            {
                result.Success = false;
                result.Message = "ParseError";
                _logger.LogParseError(vehicleId, commandType, ex.Message, result.RawResponse, taskId);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                _logger.LogError(vehicleId, commandType, ex.Message, taskId);
                return result;
            }
        }
    }
}
