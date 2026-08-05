using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; init; }

        public T? Data { get; init; }

        public string? Message { get; init; }
        public IDictionary<string, string[]>? Errors { get; init; }

        public string TraceId { get; init; } = null!;

        public static ApiResponse<T> Ok(T? data, string traceId, string? message = null)
            => new() { Success = true, Data = data, Message = message, TraceId = traceId };

        public static ApiResponse<T> Fail(
            string message, string traceId, IDictionary<string, string[]>? errors = null)
            => new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
    }
}
