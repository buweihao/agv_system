using System;

namespace AgvDispatcher.Core.Contracts.Common
{
    public class AgvResult
    {
        public bool Success { get; init; }

        public FailureCode Code { get; init; } = FailureCode.None;

        public string Message { get; init; } = string.Empty;

        public bool Retryable { get; init; }

        public DateTimeOffset FinishedAt { get; init; } = DateTimeOffset.Now;

        public static AgvResult Ok(string message = "")
        {
            return new AgvResult
            {
                Success = true,
                Code = FailureCode.None,
                Message = message
            };
        }

        public static AgvResult Fail(
            FailureCode code,
            string message,
            bool retryable = false)
        {
            return new AgvResult
            {
                Success = false,
                Code = code,
                Message = message,
                Retryable = retryable
            };
        }
    }

    public class AgvResult<T> : AgvResult
    {
        public T? Data { get; init; }

        public static AgvResult<T> Ok(T data, string message = "")
        {
            return new AgvResult<T>
            {
                Success = true,
                Code = FailureCode.None,
                Message = message,
                Data = data
            };
        }

        public new static AgvResult<T> Fail(
            FailureCode code,
            string message,
            bool retryable = false)
        {
            return new AgvResult<T>
            {
                Success = false,
                Code = code,
                Message = message,
                Retryable = retryable
            };
        }
    }
}
