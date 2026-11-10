namespace ERP.Api.Controllers;

/// <summary>
/// Body of the void endpoints (invoices, receipts, bills, payments...): <c>{ "reason": string }</c>, required, 3-500 characters after trimming.
/// A missing body or reason is rejected by the command validator with 400 (errors.reason), like a short or long one.
/// </summary>
public sealed record VoidRequest(string? Reason);
