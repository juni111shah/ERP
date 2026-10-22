using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record AllocationInput(Guid InvoiceId, decimal Amount);
public sealed record ReceiptInput(Guid LegalEntityId, Guid PartyId, DateOnly Date, SettlementMethod Method, Guid BankAccountId, decimal Amount, string? Reference, List<AllocationInput>? Allocations, bool AutoAllocate = false);

public sealed record AllocationDto(Guid Id, Guid InvoiceId, string InvoiceNumber, decimal Amount);
public sealed record ReceiptDto(
    Guid Id, string Number, Guid LegalEntityId, Guid PartyId, string? PartyName, DateOnly Date, SettlementMethod Method, Guid? BankAccountId, string CurrencyCode, decimal Amount,
    decimal Unallocated, string? Reference, DocumentStatus Status, Guid? JournalEntryId, IReadOnlyList<AllocationDto> Allocations);
public sealed record ReceiptSummaryDto(Guid Id, string Number, Guid PartyId, string? PartyName, DateOnly Date, SettlementMethod Method, decimal Amount, decimal Unallocated, DocumentStatus Status, string CurrencyCode);

public sealed record GetReceipt(Guid Id) : IQuery<ReceiptDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReceiptsRead; }
/// <summary>Money received from a customer: Dr bank / cash, Cr receivables; optionally settles invoices at the same time. What is not allocated stays as customer credit.</summary>
public sealed record CreateReceipt(ReceiptInput Input) : ICommand<ReceiptDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReceiptsWrite; }
public sealed record AllocateReceipt(Guid Id, List<AllocationInput> Allocations) : ICommand<ReceiptDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReceiptsWrite; }
public sealed record VoidReceipt(Guid Id) : ICommand<ReceiptDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReceiptsWrite; }

public sealed class ReceiptInputValidator : AbstractValidator<ReceiptInput>
{
    public ReceiptInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PartyId).NotEmpty();
        RuleFor(x => x.BankAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).IsInEnum().Must(m => m != SettlementMethod.Instrument).WithMessage("Cheques are recorded in the cheque register.");
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}
public sealed class CreateReceiptValidator : AbstractValidator<CreateReceipt> { public CreateReceiptValidator() => RuleFor(x => x.Input).SetValidator(new ReceiptInputValidator()); }

internal sealed class ReceiptHandlers(
    IRepository<Settlement> settlements, IRepository<SettlementAllocation> allocationRows, IRepository<Invoice> invoices, ISettlementService settlementService,
    IReadRepository<Account> accounts, IReadRepository<Party> parties, ILegalEntityGuard guard, IPostingService posting, INumberingService numbering) :
    IRequestHandler<GetReceipt, ReceiptDto>,
    IRequestHandler<CreateReceipt, ReceiptDto>,
    IRequestHandler<AllocateReceipt, ReceiptDto>,
    IRequestHandler<VoidReceipt, ReceiptDto>
{
    public async Task<ReceiptDto> Handle(GetReceipt q, CancellationToken ct)
    {
        var s = await settlements.Query(new Specification<Settlement>(x => x.Id == q.Id).Include(x => x.Allocations)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        return await ToDtoAsync(s, ct);
    }

    public async Task<ReceiptDto> Handle(CreateReceipt cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var party = await parties.FindAsync(i.PartyId, ct) ?? throw new FieldValidationException("partyId", "Customer not found.");
        var bank = await accounts.FindAsync(i.BankAccountId, ct);
        if (bank is null || bank.Type != AccountType.Asset || !bank.IsPostable || !bank.IsActive) throw new FieldValidationException("bankAccountId", "Choose an active bank or cash account.");

        var settlement = new Settlement
        {
            LegalEntityId = entity.Id, Direction = InstrumentDirection.Received, PartyId = party.Id, PartyName = party.Name, Date = i.Date, Method = i.Method, BankAccountId = bank.Id,
            CurrencyCode = entity.BaseCurrency, Amount = i.Amount, Unallocated = i.Amount, Reference = i.Reference, Status = DocumentStatus.Issued, Number = "pending",
        };
        var applied = await settlementService.AllocateAsync(settlement, i.Allocations ?? [], ct);
        if (i.AutoAllocate) applied.AddRange(await settlementService.AutoAllocateAsync(settlement, ct)); // oldest invoice first, the rest stays on account
        settlement.Number = await numbering.NextAsync("receipt", entity.Id, i.Date, ct);
        var ar = await posting.AccountForRoleAsync(SystemRoles.ArControl, ct);
        var memo = $"Receipt from {party.Name}";
        var journal = await posting.PostAsync(new PostingRequest(entity.Id, i.Date, "receipt", settlement.Id, memo, settlement.Number,
            [new PostingLine(bank.Id, i.Amount, 0, memo), new PostingLine(ar, 0, i.Amount, memo, party.Id)], entity.BaseCurrency), ct);
        settlement.JournalEntryId = journal.Id;
        settlements.Add(settlement);
        allocationRows.AddRange(applied);
        await settlements.SaveChangesAsync(ct);
        return await ToDtoAsync(settlement, ct, applied);
    }

    public async Task<ReceiptDto> Handle(AllocateReceipt cmd, CancellationToken ct)
    {
        var s = await LoadAsync(cmd.Id, ct);
        if (s.Status != DocumentStatus.Issued) throw new ConflictException("Only a posted receipt can be allocated.");
        var applied = await settlementService.AllocateAsync(s, cmd.Allocations, ct);
        allocationRows.AddRange(applied);
        await settlements.SaveChangesAsync(ct);
        return await ToDtoAsync(s, ct, s.Allocations.Concat(applied).ToList());
    }

    public async Task<ReceiptDto> Handle(VoidReceipt cmd, CancellationToken ct)
    {
        var s = await LoadAsync(cmd.Id, ct);
        if (s.Status != DocumentStatus.Issued) throw new ConflictException("This receipt is already voided.");
        await settlementService.ReverseAsync(s, $"Void {s.Number}", ct);
        s.Status = DocumentStatus.Voided; s.Unallocated = 0;
        await settlements.SaveChangesAsync(ct);
        return await ToDtoAsync(s, ct);
    }

    private async Task<Settlement> LoadAsync(Guid id, CancellationToken ct)
        => await settlements.Tracked(new Specification<Settlement>(x => x.Id == id).Include(x => x.Allocations)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    private async Task<ReceiptDto> ToDtoAsync(Settlement s, CancellationToken ct, IReadOnlyCollection<SettlementAllocation>? allocations = null)
    {
        var rows = allocations ?? s.Allocations;
        var ids = rows.Select(a => a.InvoiceId!.Value).ToList();
        var numbers = await invoices.Query(new Specification<Invoice>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.Number }).ToDictionaryAsync(i => i.Id, i => i.Number, ct);
        return new ReceiptDto(s.Id, s.Number, s.LegalEntityId, s.PartyId, s.PartyName, s.Date, s.Method, s.BankAccountId, s.CurrencyCode, s.Amount, s.Unallocated, s.Reference, s.Status, s.JournalEntryId,
            rows.Select(a => new AllocationDto(a.Id, a.InvoiceId!.Value, numbers.GetValueOrDefault(a.InvoiceId.Value, "?"), a.Amount)).ToList());
    }
}
