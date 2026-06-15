using System.Text.Json.Serialization;

namespace AgvDispatcher.Infrastructure.Okapi.Models
{
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
        public string Message { get; set; } = "success";
    }

    public class OkapiApiResult
    {
        public bool Success { get; set; }
        public int? HttpStatusCode { get; set; }
        public int? VendorCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string RawResponse { get; set; } = string.Empty;
    }

    public class OkapiApiResult<T>
    {
        public bool Success { get; set; }
        public int? HttpStatusCode { get; set; }
        public int? VendorCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string RawResponse { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    public class OkapiAgvInfoDto
    {
        [JsonPropertyName("agvId")]
        public int AgvId { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("state")]
        public int State { get; set; }

        [JsonPropertyName("errorMsg")]
        public string ErrorMsg { get; set; } = string.Empty;

        [JsonPropertyName("electricity")]
        public int Electricity { get; set; }

        [JsonPropertyName("loading")]
        public bool Loading { get; set; }

        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        [JsonPropertyName("pointNo")]
        public int PointNo { get; set; }

        [JsonPropertyName("lineNo")]
        public int LineNo { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("angle")]
        public int Angle { get; set; }

        [JsonPropertyName("updateTime")]
        public string UpdateTime { get; set; } = string.Empty;
    }

    public class OkapiTaskInfoDto
    {
        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("state")]
        public int State { get; set; }

        [JsonPropertyName("agvId")]
        public int AgvId { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;

        [JsonPropertyName("createTime")]
        public string CreateTime { get; set; } = string.Empty;
    }
}
