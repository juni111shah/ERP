using ERP.Domain.Platform;

namespace ERP.Application.Platform;

public sealed record PublicPlan(
    Guid Id, string Code, string Name, string? Description, decimal MonthlyPrice, decimal AnnualPrice, string Currency, int TrialDays,
    int? MaxUsers, int? MaxLegalEntities, IReadOnlyList<string> Modules, IReadOnlyList<string> Features, bool IsRecommended);

public sealed record SignupConfig(SignupMode Mode, bool EmailVerification, Guid? DefaultPlanId, string Currency, string? SupportEmail, string? TermsUrl, IReadOnlyList<PublicPlan> Plans);

public sealed record SignupInput(
    string BusinessName, string SignInKey, string Industry, string CountryCode, string BaseCurrency, string? TimeZone,
    string ContactName, string Email, string? Phone, string Password, Guid? PlanId, BillingCycle? Cycle, bool AcceptTerms, string? Website);

public sealed record SignupResult(SignupStatus Status, string Message, string? SignInKey);
public sealed record SignInKeyAvailability(bool Available, string? Reason);

/// <summary>The two public documents, ready to show.</summary>
public sealed record LegalDocuments(string CompanyName, string? SupportEmail, string? TermsUrl, string Terms, string Privacy, DateTime? UpdatedAt);
