using System.Text.Json.Serialization;

namespace WpfApp16.Models
{
    public class AppConfig
    {
        public string BaseUrl { get; set; } = "http://127.0.0.1:8080/api/agv/";
        public string ApiGetAgvInfos { get; set; } = "GetAgvInfos";
        public string ApiTaskDownload { get; set; } = "TaskDownload";
        public string ApiDeleteTask { get; set; } = "DeleteTask";
        public string ApiGetTaskInfos { get; set; } = "GetTaskInfos";
        public string ApiRequestControl { get; set; } = "RequestControl";
        public string ListenUrl { get; set; } = "http://127.0.0.1:8080/api/";
    }

    public class TaskDownloadRequest
    {
        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public int Type { get; set; } = 1;

        [JsonPropertyName("prio")]
        public int Prio { get; set; } = 1;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;

        [JsonPropertyName("createTime")]
        public string CreateTime { get; set; } = string.Empty;
    }

    public class DeleteTaskRequest
    {
        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;
    }

    public class ReturnTaskStateRequest
    {
        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public int State { get; set; }

        [JsonPropertyName("agvId")]
        public int AgvId { get; set; }

        [JsonPropertyName("faultCode")]
        public int FaultCode { get; set; }

        [JsonPropertyName("updateTime")]
        public string UpdateTime { get; set; } = string.Empty;
    }

    public class ReturnTaskStateResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; } = 0;

        [JsonPropertyName("message")]
        public string Message { get; set; } = "success";
    }

    public class RequestControlRequest
    {
        [JsonPropertyName("areaId")]
        public string AreaId { get; set; } = string.Empty;

        [JsonPropertyName("agvId")]
        public int AgvId { get; set; }

        [JsonPropertyName("requestType")]
        public int RequestType { get; set; } = 1;

        [JsonPropertyName("requestTime")]
        public string RequestTime { get; set; } = string.Empty;
    }

    public class RequestControlResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; } = 0;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
