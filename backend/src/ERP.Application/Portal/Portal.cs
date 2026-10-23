using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Portal;

public sealed record PortalSession(string AccessToken, int ExpiresIn, Guid PartyId, string PartyName, string Kind = "customer");

/// <summary>Email one-time-code sign-in for customers (tenants). Implemented in Infrastructure; the token it issues carries only the "portal_party" claim, no staff permissions.</summary>
public interface IPortalAuth
{
    /// <summary>Always completes silently, so the caller cannot learn whether an email is registered.</summary>
    Task RequestCodeAsync(string workspaceCode, string email, string? kind, CancellationToken ct);
    Task<PortalSession> VerifyCodeAsync(string workspaceCode, string email, string code, string? kind, CancellationToken ct);
}

/// <summary>Kind: "customer" (tenant) or "vendor"; omitted = customer when the email belongs to a customer, otherwise vendor.</summary>
public sealed record RequestPortalCode(string Workspace, string Email, string? Kind = null) : ICommand;
public sealed record VerifyPortalCode(string Workspace, string Email, string Code, string? Kind = null) : ICommand<PortalSession>;

public sealed class RequestPortalCodeValidator : AbstractValidator<RequestPortalCode>
{
    public RequestPortalCodeValidator() { RuleFor(x => x.Workspace).NotEmpty(); RuleFor(x => x.Email).NotEmpty().EmailAddress(); RuleFor(x => x.Kind).Must(k => k is null or "customer" or "vendor").WithMessage("Kind must be customer or vendor."); }
}

public sealed record PortalProfileDto(Guid PartyId, string Name, string? Email, string? Phone);
public sealed record PortalInvoiceDto(Guid Id, string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, string CurrencyCode, decimal Total, decimal Paid, decimal Outstanding, DocumentStatus Status);
public sealed record PortalReceiptDto(Guid Id, string Number, DateOnly Date, SettlementMethod Method, string CurrencyCode, decimal Amount, string? Reference);
public sealed record PortalStatementLine(string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, int DaysOverdue, decimal Outstanding);
public sealed record PortalStatementDto(DateOnly AsOf, IReadOnlyList<PortalStatementLine> Items, decimal UnallocatedReceipts, decimal Balance);

public sealed record PortalBillDto(Guid Id, string Number, string? VendorReference, DateOnly BillDate, DateOnly DueDate, string CurrencyCode, decimal Total, decimal Paid, decimal Outstanding, DocumentStatus Status);
public sealed record PortalPaymentDto(Guid Id, string Number, DateOnly Date, SettlementMethod Method, string CurrencyCode, decimal Amount, string? Reference);
public sealed record PortalPayablesLine(string Number, DateOnly BillDate, DateOnly DueDate, int DaysOverdue, decimal Outstanding);
public sealed record PortalPayablesDto(DateOnly AsOf, IReadOnlyList<PortalPayablesLine> Items, decimal Balance);

public sealed record GetPortalProfile : IQuery<PortalProfileDto>;
public sealed record ListVendorBills : IQuery<IReadOnlyList<PortalBillDto>>;
public sealed record ListVendorPayments : IQuery<IReadOnlyList<PortalPaymentDto>>;
public sealed record GetVendorStatement(DateOnly AsOf) : IQuery<PortalPayablesDto>;
public sealed record ListPortalInvoices : IQuery<IReadOnlyList<PortalInvoiceDto>>;
public sealed record ListPortalReceipts : IQuery<IReadOnlyList<PortalReceiptDto>>;
public sealed record GetPortalStatement(DateOnly AsOf) : IQuery<PortalStatementDto>;

internal sealed class PortalHandlers(IPortalAuth auth, ICurrentUser user, IReadRepository<Party> parties, IReadRepository<Invoice> invoices, IReadRepository<Settlement> settlements, IReadRepository<Bill> bills) :
    IRequestHandler<RequestPortalCode, Unit>,
    IRequestHandler<VerifyPortalCode, PortalSession>,
    IRequestHandler<GetPortalProfile, PortalProfileDto>,
    IRequestHandler<ListPortalInvoices, IReadOnlyList<PortalInvoiceDto>>,
    IRequestHandler<ListPortalReceipts, IReadOnlyList<PortalReceiptDto>>,
    IRequestHandler<GetPortalStatement, PortalStatementDto>,
    IRequestHandler<ListVendorBills, IReadOnlyList<PortalBillDto>>,
    IRequestHandler<ListVendorPayments, IReadOnlyList<PortalPaymentDto>>,
    IRequestHandler<GetVendorStatement, PortalPayablesDto>
{
    /// <summary>The signed-in party, whatever the kind (used by the profile).</summary>
    private Guid AnyMe => user.PortalPartyId ?? throw new ForbiddenException();
    private Guid Me => user.PortalKind == "customer" && user.PortalPartyId is { } id ? id : throw new ForbiddenException();
    private Guid VendorMe => user.PortalKind == "vendor" && user.PortalPartyId is { } id ? id : throw new ForbiddenException();

    public async Task<Unit> Handle(RequestPortalCode cmd, CancellationToken ct) { await auth.RequestCodeAsync(cmd.Workspace.Trim(), cmd.Email.Trim(), cmd.Kind, ct); return Unit.Value; }
    public Task<PortalSession> Handle(VerifyPortalCode cmd, CancellationToken ct) => auth.VerifyCodeAsync(cmd.Workspace.Trim(), cmd.Email.Trim(), cmd.Code.Trim(), cmd.Kind, ct);

    public async Task<PortalProfileDto> Handle(GetPortalProfile q, CancellationToken ct)
    {
        var p = await parties.GetAsync(AnyMe, ct);
        return new(p.Id, p.Name, p.Email, p.Phone);
    }

    public async Task<IReadOnlyList<PortalInvoiceDto>> Handle(ListPortalInvoices q, CancellationToken ct)
    {
        var me = Me;
        return (await invoices.Query(new Specification<Invoice>(i => i.PartyId == me && i.Status != DocumentStatus.Draft && i.Status != DocumentStatus.PendingApproval && i.Status != DocumentStatus.Voided))
            .OrderByDescending(i => i.InvoiceDate).Take(200)
            .Select(i => new PortalInvoiceDto(i.Id, i.Number, i.IsCreditNote, i.InvoiceDate, i.DueDate, i.CurrencyCode, i.Total, i.Paid, i.Total - i.Paid, i.Status)).ToListAsync(ct));
    }

    public async Task<IReadOnlyList<PortalReceiptDto>> Handle(ListPortalReceipts q, CancellationToken ct)
    {
        var me = Me;
        return (await settlements.Query(new Specification<Settlement>(s => s.PartyId == me && s.Direction == InstrumentDirection.Received && s.Status == DocumentStatus.Issued))
            .OrderByDescending(s => s.Date).Take(200)
            .Select(s => new PortalReceiptDto(s.Id, s.Number, s.Date, s.Method, s.CurrencyCode, s.Amount, s.Reference)).ToListAsync(ct));
    }

    public async Task<PortalStatementDto> Handle(GetPortalStatement q, CancellationToken ct)
    {
        var me = Me;
        var open = await invoices.Query(new Specification<Invoice>(i => i.PartyId == me && (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid) && i.InvoiceDate <= q.AsOf))
            .Select(i => new { i.Number, i.IsCreditNote, i.InvoiceDate, i.DueDate, i.Total, i.Paid }).ToListAsync(ct);
        var unallocated = (await settlements.Query(new Specification<Settlement>(s => s.PartyId == me && s.Direction == InstrumentDirection.Received && s.Status == DocumentStatus.Issued && s.Unallocated > 0))
            .Select(s => s.Unallocated).ToListAsync(ct)).Sum();
        var lines = open.OrderBy(o => o.DueDate).Select(o => new PortalStatementLine(o.Number, o.IsCreditNote, o.InvoiceDate, o.DueDate, Math.Max(0, q.AsOf.DayNumber - o.DueDate.DayNumber), o.Total - o.Paid)).ToList();
        return new(q.AsOf, lines, unallocated, open.Sum(o => (o.IsCreditNote ? -1 : 1) * (o.Total - o.Paid)) - unallocated);
    }

    public async Task<IReadOnlyList<PortalBillDto>> Handle(ListVendorBills q, CancellationToken ct)
    {
        var me = VendorMe;
        return await bills.Query(new Specification<Bill>(b => b.PartyId == me && b.Status != DocumentStatus.Draft && b.Status != DocumentStatus.PendingApproval && b.Status != DocumentStatus.Voided))
            .OrderByDescending(b => b.BillDate).Take(200)
            .Select(b => new PortalBillDto(b.Id, b.Number, b.VendorReference, b.BillDate, b.DueDate, b.CurrencyCode, b.Total, b.Paid, b.Total - b.Paid, b.Status)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PortalPaymentDto>> Handle(ListVendorPayments q, CancellationToken ct)
    {
        var me = VendorMe;
        return await settlements.Query(new Specification<Settlement>(s => s.PartyId == me && s.Direction == InstrumentDirection.Issued && s.Status == DocumentStatus.Issued))
            .OrderByDescending(s => s.Date).Take(200)
            .Select(s => new PortalPaymentDto(s.Id, s.Number, s.Date, s.Method, s.CurrencyCode, s.Amount, s.Reference)).ToListAsync(ct);
    }

    public async Task<PortalPayablesDto> Handle(GetVendorStatement q, CancellationToken ct)
    {
        var me = VendorMe;
        var open = await bills.Query(new Specification<Bill>(b => b.PartyId == me && (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid) && b.BillDate <= q.AsOf))
            .Select(b => new { b.Number, b.BillDate, b.DueDate, b.Total, b.Paid }).ToListAsync(ct);
        var lines = open.OrderBy(o => o.DueDate).Select(o => new PortalPayablesLine(o.Number, o.BillDate, o.DueDate, Math.Max(0, q.AsOf.DayNumber - o.DueDate.DayNumber), o.Total - o.Paid)).ToList();
        return new(q.AsOf, lines, lines.Sum(l => l.Outstanding));
    }
}
