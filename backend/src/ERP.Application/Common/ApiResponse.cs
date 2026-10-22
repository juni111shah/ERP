using System.Text.Json.Serialization;

namespace ERP.Application.Common;

/// <summary>
/// Standard envelope for every API response:
/// <c>{ success, statusCode, message, data, errors, traceId }</c>. Paged lists add <c>meta</c>.
/// Error envelopes also carry the RFC 7807 members (<c>type, title, status, detail</c>, extensions),
/// so ProblemDetails-aware clients keep working.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; init; }
    public string? TraceId { get; init; }

    // RFC 7807 compatibility (errors only)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Type { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Title { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Status { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Detail { get; init; }
    [JsonExtensionData] public IDictionary<string, object?>? Extensions { get; init; }
}

public sealed record PageMeta(int Page, int PageSize, int Total, int TotalPages, bool HasNext, bool HasPrevious);

/// <summary>Paged variant: <c>data</c> is the page of items, <c>meta</c> the paging information.</summary>
public sealed class ApiPagedResponse<T> : ApiResponse<IEnumerable<T>>
{
    public PageMeta Meta { get; init; } = default!;
}

public static class ApiResponses
{
    public static string DefaultMessage(int status) => status switch
    {
        200 => "OK",
        201 => "Created",
        202 => "Accepted",
        400 => "Validation failed",
        401 => "Authentication required",
        403 => "Forbidden",
        404 => "Not found",
        409 => "Conflict",
        423 => "Locked",
        429 => "Too many requests",
        >= 500 => "An unexpected error occurred",
        _ => status < 400 ? "OK" : "Request failed",
    };

    public static ApiResponse<object?> Ok(object? data, int status, string? traceId, string? message = null) => new()
    {
        Success = true, StatusCode = status, Message = message ?? DefaultMessage(status), Data = data, TraceId = traceId,
    };

    public static ApiPagedResponse<object?> Paged(IPagedResult page, int status, string? traceId, string? message = null) => new()
    {
        Success = true, StatusCode = status, Message = message ?? DefaultMessage(status), TraceId = traceId,
        Data = page.Items.Cast<object?>(),
        Meta = new PageMeta(page.Page, page.PageSize, page.Total,
            page.PageSize <= 0 ? 0 : (int)Math.Ceiling(page.Total / (double)page.PageSize),
            page.Page * page.PageSize < page.Total, page.Page > 1),
    };

    public static ApiResponse<object?> Error(int status, string? title, string? detail, string? traceId,
        IDictionary<string, string[]>? errors = null, string? type = null, IDictionary<string, object?>? extensions = null) => new()
    {
        Success = false, StatusCode = status, Message = detail ?? title ?? DefaultMessage(status), Errors = errors, TraceId = traceId,
        Type = type ?? $"https://httpstatuses.io/{status}", Title = title ?? DefaultMessage(status), Status = status, Detail = detail,
        Extensions = extensions is { Count: > 0 } ? extensions : null,
    };
}
