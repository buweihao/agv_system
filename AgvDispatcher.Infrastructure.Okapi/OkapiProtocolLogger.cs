using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using System.Diagnostics;
using System.Text.Json;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiProtocolLogger
    {
        private readonly IOperationLogService? _logService;
        private readonly IOkapiProtocolTraceStore? _traceStore;

        public OkapiProtocolLogger(IOperationLogService? logService = null, IOkapiProtocolTraceStore? traceStore = null)
        {
            _logService = logService;
            _traceStore = traceStore;
        }

        public void LogSend(string vehicleId, string commandType, string url, object requestBody, string? taskId = null)
        {
            WriteTrace("Send", vehicleId, commandType, taskId, url, SerializeBody(requestBody), null, null);
            WriteLog(vehicleId, "OkapiSend", $"Sending {commandType} to {url}", requestBody, null, taskId);
        }

        public void LogReceive(string vehicleId, string commandType, string responseBody, string? taskId = null)
        {
            WriteTrace("Receive", vehicleId, commandType, taskId, null, null, responseBody, null);
            WriteLog(vehicleId, "OkapiReceive", $"Received response for {commandType}", null, responseBody, taskId);
        }

        public void LogCallback(string endpoint, string requestBody, string? vehicleId = null, string? taskId = null)
        {
            WriteTrace("Callback", vehicleId ?? "Unknown", endpoint, taskId, endpoint, requestBody, null, null);
            WriteLog(vehicleId ?? "Unknown", "OkapiCallback", $"Received callback on {endpoint}", requestBody, null, taskId);
        }

        public void LogError(string vehicleId, string action, string errorMessage, string? taskId = null)
        {
            WriteTrace("Error", vehicleId, action, taskId, null, null, null, errorMessage);
            WriteLog(vehicleId, "OkapiHttpError", errorMessage, null, null, taskId);
        }

        public void LogTimeout(string vehicleId, string commandType, string url, string? taskId = null)
        {
            WriteTrace("Error", vehicleId, commandType, taskId, url, null, null, $"Timeout waiting for {commandType} at {url}");
            WriteLog(vehicleId, "OkapiTimeout", $"Timeout waiting for {commandType} at {url}", null, null, taskId);
        }

        public void LogVendorRejected(string vehicleId, string commandType, string message, string? taskId = null)
        {
            WriteTrace("Error", vehicleId, commandType, taskId, null, null, null, message);
            WriteLog(vehicleId, "OkapiVendorRejected", $"Vendor rejected {commandType}: {message}", null, null, taskId);
        }

        public void LogParseError(string vehicleId, string action, string errorMessage, string rawData, string? taskId = null)
        {
            WriteTrace("Error", vehicleId, action, taskId, null, null, rawData, errorMessage);
            WriteLog(vehicleId, "OkapiParseError", $"Parse error in {action}: {errorMessage}", null, rawData, taskId);
        }

        private static string SerializeBody(object requestBody) =>
            requestBody is string text ? text : JsonSerializer.Serialize(requestBody);

        private void WriteTrace(
            string direction,
            string vehicleId,
            string commandType,
            string? taskId,
            string? url,
            string? requestBody,
            string? responseBody,
            string? errorMessage)
        {
            _traceStore?.Add(new OkapiProtocolTraceRecord
            {
                Time = DateTimeOffset.Now,
                Direction = direction,
                VehicleId = vehicleId,
                CommandType = commandType,
                TaskId = taskId,
                Url = url,
                RequestBody = requestBody,
                ResponseBody = responseBody,
                ErrorMessage = errorMessage
            });
        }

        private void WriteLog(string vehicleId, string action, string message, object? request, string? response, string? taskId)
        {
            var log = new OperationLog
            {
                LogId = Guid.NewGuid().ToString("N"),
                Category = "VehicleProtocol",
                Action = action,
                Message = message,
                VehicleId = vehicleId,
                TaskId = taskId,
                OccurredAt = DateTime.Now
            };

            if (request != null)
            {
                log.Metadata["Request"] = request is string s ? s : JsonSerializer.Serialize(request);
            }
            if (response != null)
            {
                log.Metadata["Response"] = response;
            }

            if (_logService != null)
            {
                _logService.WriteLog(log);
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{action}] [{vehicleId}] {message}");
                if (request != null) Debug.WriteLine($"Request: {log.Metadata["Request"]}");
                if (response != null) Debug.WriteLine($"Response: {response}");
            }
        }
    }
}
