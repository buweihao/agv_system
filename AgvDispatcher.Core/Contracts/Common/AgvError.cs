using System;

namespace AgvDispatcher.Core.Contracts.Common
{
    /// <summary>
    /// 标准化错误对象，用于在 AgvResult 中传递详细的错误信息。
    /// </summary>
    public class AgvError
    {
        /// <summary>
        /// 错误码（字符串形式，便于跨模块、前后端统一）。
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// 简短的错误信息。
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// 详细的错误上下文或堆栈信息。
        /// </summary>
        public string? Detail { get; init; }

        public AgvError(string code, string message, string? detail = null)
        {
            Code = code;
            Message = message;
            Detail = detail;
        }
    }
}
