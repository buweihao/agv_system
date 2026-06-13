using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using System.Diagnostics;
using System.Text.Json;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiProtocolLogger
    {
        private readonly IOperationLogService? _logService;

        public OkapiProtocolLogger(IOperationLogService? logService = null)
        {
            _logService = logService;
        }

        public void LogSend(string vehicleId, string commandType, string url, object requestBody, string? taskId = null)
        {
            WriteLog(vehicleId, "OkapiSend", $"Sending {commandType} to {url}", requestBody, null, taskId);
        }

        public void LogReceive(string vehicleId, string commandType, string responseBody, string? taskId = null)
        {
            WriteLog(vehicleId, "OkapiReceive", $"Received response for {commandType}", null, responseBody, taskId);
        }

        public void LogCallback(string endpoint, string requestBody, string? vehicleId = null, string? taskId = null)
        {
            WriteLog(vehicleId ?? "Unknown", "OkapiCallback", $"Received callback on {endpoint}", requestBody, null, taskId);
        }

        public void LogError(string vehicleId, string action, string errorMessage, string? taskId = null)
        {
            WriteLog(vehicleId, "OkapiHttpError", errorMessage, null, null, taskId);
        }

        public void LogTimeout(string vehicleId, string commandType, string url, string? taskId = null)
        {
            WriteLog(vehicleId, "OkapiTimeout", $"Timeout waiting for {commandType} at {url}", null, null, taskId);
        }

        public void LogVendorRejected(string vehicleId, string commandType, string message, string? taskId = null)
        {
            WriteLog(vehicleId, "OkapiVendorRejected", $"Vendor rejected {commandType}: {message}", null, null, taskId);
        }

        public void LogParseError(string vehicleId, string action, string errorMessage, string rawData, string? taskId = null)
        {
            WriteLog(vehicleId, "OkapiParseError", $"Parse error in {action}: {errorMessage}", null, rawData, taskId);
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
