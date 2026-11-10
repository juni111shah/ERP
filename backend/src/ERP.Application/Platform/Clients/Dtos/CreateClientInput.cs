using ERP.Domain.Platform;

namespace ERP.Application.Platform;

public sealed record CreateClientInput(
    string WorkspaceName, string SignInKey, string Industry, string CountryCode, string BaseCurrency, string? TimeZone, string AdminName, string AdminEmail, string? Phone,
    Guid PlanId, BillingCycle Cycle, ClientStartMode StartMode, int? TrialDays, decimal? CustomPrice, string? Notes);
public sealed record CreatedClient(Guid OrgId, string SignInKey, string AdminEmail, string? TemporaryPassword);

public sealed record SetClientPlanInput(Guid PlanId, BillingCycle Cycle, decimal? CustomPrice, bool IsComplimentary, ClientStartMode? StartMode, int? TrialDays);
public sealed record ReasonInput(string? Reason);
public sealed record ExtendTrialInput(int Days);
public sealed record ActivateInput(bool IssueInvoice);
public sealed record ClientNotesInput(string? BillingEmail, string? ContactPhone, string? Notes);
public sealed record IssueInvoiceInput(string? Description, decimal? Amount, DateOnly? DueOn);
public sealed record RecordPaymentInput(DateOnly? PaidOn, string? Method, string? Reference);
public sealed record TemporaryPassword(string Password);
public sealed record SetClientDemoInput(bool IsDemo);
