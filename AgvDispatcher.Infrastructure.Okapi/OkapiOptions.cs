namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiOptions
    {
        public string BaseUrl { get; set; } = "http://127.0.0.1:8080/api/agv/";
        public string ListenUrl { get; set; } = "http://127.0.0.1:8080/api/";
        
        public string ApiGetAgvInfos { get; set; } = "GetAgvInfos";
        public string ApiTaskDownload { get; set; } = "TaskDownload";
        public string ApiDeleteTask { get; set; } = "DeleteTask";
        public string ApiGetTaskInfos { get; set; } = "GetTaskInfos";
        public string ApiRequestControl { get; set; } = "RequestControl";
        
        public int TimeoutSeconds { get; set; } = 10;
        
        public bool EnableCallbackServer { get; set; } = true;
        public bool EnableStatusPolling { get; set; } = false;
        public int StatusPollingIntervalSeconds { get; set; } = 2;
    }
}
