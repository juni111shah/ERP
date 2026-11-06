namespace ERP.Application.Abstractions;

/// <summary>Links the audit rows written during this request to what caused them, such as the assistant chat a confirmed change came from.</summary>
public interface IAuditCorrelation
{
    string? Id { get; set; }
}

public sealed class AuditCorrelation : IAuditCorrelation
{
    public string? Id { get; set; }
}
