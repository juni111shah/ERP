using ERP.Domain.Platform;

namespace ERP.Application.Platform;

public sealed record SignupRow(
    Guid Id, SignupStatus Status, string BusinessName, string SignInKey, string Industry, string CountryCode, string BaseCurrency, string ContactName, string Email, string? Phone,
    Guid PlanId, string PlanName, BillingCycle Cycle, DateTime? EmailVerifiedAt, DateTime CreatedAt, DateTime? ReviewedAt, string? RejectReason, Guid? ProvisionedOrgId)
{
    internal static SignupRow From(SignupRequest r, string planName) => new(
        r.Id, r.Status, r.BusinessName, r.SignInKey, r.Industry, r.CountryCode, r.BaseCurrency, r.ContactName, r.Email, r.Phone, r.PlanId, planName, r.Cycle, r.EmailVerifiedAt, r.CreatedAt, r.ReviewedAt, r.RejectReason, r.ProvisionedOrgId);
}
