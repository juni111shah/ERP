namespace ERP.Application.Platform;

public sealed record PlanCount(Guid PlanId, string PlanName, int Clients, decimal MonthlyValue);
public sealed record MonthAmount(string Month, decimal Amount);
public sealed record ExpiringTrial(Guid OrgId, string Name, DateOnly TrialEndsOn, int DaysLeft);
public sealed record PlatformDashboard(
    string Currency, decimal MonthlyRecurring, decimal AnnualRecurring, int TotalClients, int Trialing, int Active, int PastDue, int Expired, int Suspended, int Cancelled, int WithoutPlan,
    decimal Outstanding, decimal Overdue, decimal CollectedThisMonth, decimal CollectedTotal, int PendingSignups, int NewClients30Days, int PlanRequests,
    IReadOnlyList<PlanCount> ByPlan, IReadOnlyList<MonthAmount> Collected, IReadOnlyList<ExpiringTrial> TrialsEnding, IReadOnlyList<ActivityRow> RecentActivity);
