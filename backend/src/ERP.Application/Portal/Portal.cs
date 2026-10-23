using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Portal;

public sealed record PortalSession(string AccessToken, int ExpiresIn, Guid PartyId, string PartyName);

/// <summary>Email one-time-code sign-in for customers (tenants). Implemented in Infrastructure; the token it issues carries only the "portal_party" claim, no staff permissions.</summary>
public interface IPortalAuth
{
    /// <summary>Always completes silently, so the caller cannot learn whether an email is registered.</summary>
    Task RequestCodeAsync(string workspaceCode, string email, CancellationToken ct);
    Task<PortalSession> VerifyCodeAsync(string workspaceCode, string email, string code, CancellationToken ct);
}

public sealed record RequestPortalCode(string Workspace, string Email) : ICommand;
public sealed record VerifyPortalCode(string Workspace, string Email, string Code) : ICommand<PortalSession>;

public sealed class RequestPortalCodeValidator : AbstractValidator<RequestPortalCode>
{
    public RequestPortalCodeValidator() { RuleFor(x => x.Workspace).NotEmpty(); RuleFor(x => x.Email).NotEmpty().EmailAddress(); }
}

public sealed record PortalProfileDto(Guid PartyId, string Name, string? Email, string? Phone);
public sealed record PortalInvoiceDto(Guid Id, string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, string CurrencyCode, decimal Total, decimal Paid, decimal Outstanding, DocumentStatus Status);
public sealed record PortalReceiptDto(Guid Id, string Number, DateOnly Date, SettlementMethod Method, string CurrencyCode, decimal Amount, string? Reference);
public sealed record PortalStatementLine(string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, int DaysOverdue, decimal Outstanding);
public sealed record PortalStatementDto(DateOnly AsOf, IReadOnlyList<PortalStatementLine> Items, decimal UnallocatedReceipts, decimal Balance);

public sealed record GetPortalProfile : IQuery<PortalProfileDto>;
public sealed record ListPortalInvoices : IQuery<IReadOnlyList<PortalInvoiceDto>>;
public sealed record ListPortalReceipts : IQuery<IReadOnlyList<PortalReceiptDto>>;
public sealed record GetPortalStatement(DateOnly AsOf) : IQuery<PortalStatementDto>;

internal sealed class PortalHandlers(IPortalAuth auth, ICurrentUser user, IReadRepository<Party> parties, IReadRepository<Invoice> invoices, IReadRepository<Settlement> settlements) :
    IRequestHandler<RequestPortalCode, Unit>,
    IRequestHandler<VerifyPortalCode, PortalSession>,
    IRequestHandler<GetPortalProfile, PortalProfileDto>,
    IRequestHandler<ListPortalInvoices, IReadOnlyList<PortalInvoiceDto>>,
    IRequestHandler<ListPortalReceipts, IReadOnlyList<PortalReceiptDto>>,
    IRequestHandler<GetPortalStatement, PortalStatementDto>
{
    private Guid Me => user.PortalPartyId ?? throw new ForbiddenException();

    public async Task<Unit> Handle(RequestPortalCode cmd, CancellationToken ct) { await auth.RequestCodeAsync(cmd.Workspace.Trim(), cmd.Email.Trim(), ct); return Unit.Value; }
    public Task<PortalSession> Handle(VerifyPortalCode cmd, CancellationToken ct) => auth.VerifyCodeAsync(cmd.Workspace.Trim(), cmd.Email.Trim(), cmd.Code.Trim(), ct);

    public async Task<PortalProfileDto> Handle(GetPortalProfile q, CancellationToken ct)
    {
        var p = await parties.GetAsync(Me, ct);
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
}
