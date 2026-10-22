namespace ERP.Application.Common;

public class NotFoundException(string message = "The record was not found.") : Exception(message);
public class ForbiddenException(string message = "You do not have permission to do this.") : Exception(message);
public class ConflictException(string message) : Exception(message);

/// <summary>Validation failure keyed by field (maps to ProblemDetails "errors").</summary>
public class FieldValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
    public FieldValidationException(string field, string error) : this(new Dictionary<string, string[]> { [field] = [error] }) { }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
