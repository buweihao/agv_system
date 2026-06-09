namespace AgvDispatcher.Core.Models
{
    public class DispatchResult
    {
        public bool Succeeded { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? TaskId { get; set; }

        public string? VehicleId { get; set; }

        public string? CommandId { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.Now;

        public static DispatchResult Success(string message, string? taskId = null, string? vehicleId = null, string? commandId = null)
        {
            return new DispatchResult
            {
                Succeeded = true,
                Code = "Success",
                Message = message,
                TaskId = taskId,
                VehicleId = vehicleId,
                CommandId = commandId
            };
        }

        public static DispatchResult Failure(string code, string message, string? taskId = null, string? vehicleId = null)
        {
            return new DispatchResult
            {
                Succeeded = false,
                Code = code,
                Message = message,
                TaskId = taskId,
                VehicleId = vehicleId
            };
        }
    }
}
