using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

public sealed record FundingInput(FundingType Type, string Lender, decimal Principal, decimal AnnualRatePercent, int TermMonths, DateOnly FirstDueDate);
public sealed record AcquisitionInput(
    Guid LegalEntityId, Guid PropertyId, string SellerName, DateOnly PurchaseDate, string? ContractReference, decimal PurchasePrice, decimal DownPayment, decimal TransferFees, decimal OtherCosts,
    Guid? PayFromAccountId, string? Notes, List<FundingInput>? Funding);

public sealed record FundingInstalmentDto(Guid Id, int Sequence, DateOnly DueDate, decimal Principal, decimal Interest, decimal Total, decimal BalanceAfter, InstalmentPayStatus Status, DateOnly? PaidOn);
public sealed record FundingSourceDto(
    Guid Id, Guid AcquisitionId, FundingType Type, string Lender, decimal Principal, decimal AnnualRatePercent, int TermMonths, DateOnly FirstDueDate, decimal MonthlyPayment, decimal FinanceCost,
    int Paid, int InstalmentCount, decimal Outstanding, DateOnly? NextDue, IReadOnlyList<FundingInstalmentDto> Instalments);
public sealed record AcquisitionDto(
    Guid Id, string? Number, Guid LegalEntityId, Guid PropertyId, string? PropertyName, string SellerName, DateOnly PurchaseDate, string? ContractReference, decimal PurchasePrice, decimal DownPayment,
    decimal TransferFees, decimal OtherCosts, Guid? PayFromAccountId, string? Notes, string CurrencyCode, AcquisitionStatus Status, decimal TotalCost, decimal CashAtCompletion, decimal Financed, decimal Unfunded,
    Guid? JournalEntryId, IReadOnlyList<FundingSourceDto> Funding);
public sealed record AcquisitionSummaryDto(Guid Id, string? Number, Guid PropertyId, string SellerName, DateOnly PurchaseDate, decimal PurchasePrice, decimal DownPayment, AcquisitionStatus Status, string CurrencyCode);
public sealed record AcquisitionOverviewDto(decimal Consideration, int Count, decimal DownPayments, decimal Financing, decimal Outstanding, decimal Unfunded);

public sealed record GetAcquisition(Guid Id) : IQuery<AcquisitionDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsRead; }
public sealed record GetAcquisitionOverview : IQuery<AcquisitionOverviewDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsRead; }
public sealed record ListFundingSources : IQuery<IReadOnlyList<FundingSourceDto>>, IRequirePermission { public string RequiredPermission => P.AcquisitionsRead; }
public sealed record CreateAcquisition(AcquisitionInput Input) : ICommand<AcquisitionDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsManage; }
public sealed record UpdateAcquisition(Guid Id, AcquisitionInput Input) : ICommand<AcquisitionDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsManage; }
public sealed record CancelAcquisition(Guid Id) : ICommand<AcquisitionDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsManage; }
/// <summary>Completes the purchase: Dr investment property (price + fees + costs), Cr bank (cash paid) and property financing payable (per funding source); the instalment schedules are generated.</summary>
public sealed record CompleteAcquisition(Guid Id) : ICommand<AcquisitionDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsComplete; }
/// <summary>Pays one financing instalment: Dr financing payable (principal) and finance costs (interest), Cr bank. Instalments are paid in order.</summary>
public sealed record PayFundingInstalment(Guid InstalmentId, Guid BankAccountId, DateOnly Date) : ICommand<FundingSourceDto>, IRequirePermission { public string RequiredPermission => P.FundingPay; }

public sealed class AcquisitionInputValidator : AbstractValidator<AcquisitionInput>
{
    public AcquisitionInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.SellerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContractReference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.PurchasePrice).GreaterThan(0);
        RuleFor(x => x.DownPayment).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TransferFees).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OtherCosts).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => x.DownPayment <= x.PurchasePrice).WithName("downPayment").WithMessage("The down payment cannot exceed the purchase price.");
        RuleFor(x => x).Must(x => x.DownPayment + (x.Funding?.Sum(f => f.Principal) ?? 0) <= x.PurchasePrice).WithName("funding").WithMessage("The down payment and the financing together cannot exceed the purchase price.");
        RuleForEach(x => x.Funding).ChildRules(f =>
        {
            f.RuleFor(y => y.Type).IsInEnum();
            f.RuleFor(y => y.Lender).NotEmpty().MaximumLength(200);
            f.RuleFor(y => y.Principal).GreaterThan(0);
            f.RuleFor(y => y.AnnualRatePercent).InclusiveBetween(0, 100);
            f.RuleFor(y => y.TermMonths).InclusiveBetween(1, 600);
        });
    }
}
public sealed class CreateAcquisitionValidator : AbstractValidator<CreateAcquisition> { public CreateAcquisitionValidator() => RuleFor(x => x.Input).SetValidator(new AcquisitionInputValidator()); }
public sealed class UpdateAcquisitionValidator : AbstractValidator<UpdateAcquisition> { public UpdateAcquisitionValidator() => RuleFor(x => x.Input).SetValidator(new AcquisitionInputValidator()); }

internal sealed class AcquisitionHandlers(
    IRepository<Acquisition> acquisitions, IRepository<FundingSource> sources, IRepository<FundingInstalment> instalments, IReadRepository<Property> properties, IReadRepository<Account> accounts,
    ILegalEntityGuard guard, IPostingService posting, INumberingService numbers, IUnitOfWork uow, IClock clock) :
    IRequestHandler<GetAcquisition, AcquisitionDto>,
    IRequestHandler<GetAcquisitionOverview, AcquisitionOverviewDto>,
    IRequestHandler<ListFundingSources, IReadOnlyList<FundingSourceDto>>,
    IRequestHandler<CreateAcquisition, AcquisitionDto>,
    IRequestHandler<UpdateAcquisition, AcquisitionDto>,
    IRequestHandler<CancelAcquisition, AcquisitionDto>,
    IRequestHandler<CompleteAcquisition, AcquisitionDto>,
    IRequestHandler<PayFundingInstalment, FundingSourceDto>
{
    public async Task<AcquisitionDto> Handle(GetAcquisition q, CancellationToken ct) => await BuildAsync(await acquisitions.GetAsync(q.Id, ct), ct);

    public async Task<AcquisitionOverviewDto> Handle(GetAcquisitionOverview q, CancellationToken ct)
    {
        var done = await acquisitions.Query(new Specification<Acquisition>(a => a.Status == AcquisitionStatus.Completed)).Select(a => new { a.Id, a.PurchasePrice, a.DownPayment }).ToListAsync(ct);
        var ids = done.Select(a => a.Id).ToList();
        var funding = await sources.Query(new Specification<FundingSource>(f => ids.Contains(f.AcquisitionId))).Select(f => new { f.Id, f.Principal }).ToListAsync(ct);
        var fundingIds = funding.Select(f => f.Id).ToList();
        var repaid = (await instalments.Query(new Specification<FundingInstalment>(i => fundingIds.Contains(i.FundingSourceId) && i.Status == InstalmentPayStatus.Paid)).Select(i => i.Principal).ToListAsync(ct)).Sum();
        var financing = funding.Sum(f => f.Principal);
        return new AcquisitionOverviewDto(done.Sum(a => a.PurchasePrice), done.Count, done.Sum(a => a.DownPayment), financing, financing - repaid, done.Sum(a => a.PurchasePrice - a.DownPayment) - financing);
    }

    public async Task<IReadOnlyList<FundingSourceDto>> Handle(ListFundingSources q, CancellationToken ct)
    {
        var list = (await sources.Query().ToListAsync(ct)).OrderBy(f => f.FirstDueDate).ThenByDescending(f => f.Principal).ToList();
        var result = new List<FundingSourceDto>();
        foreach (var s in list) result.Add(await BuildSourceAsync(s, includeInstalments: false, ct));
        return result;
    }

    public Task<AcquisitionDto> Handle(CreateAcquisition cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, token);
        await RequirePropertyAsync(i, token);
        var acquisition = new Acquisition { LegalEntityId = entity.Id, CurrencyCode = entity.BaseCurrency };
        Map(i, acquisition);
        acquisition.Number = await numbers.NextAsync(Docs.Acquisition, acquisition.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow), token);
        acquisitions.Add(acquisition);
        sources.AddRange(i.Funding?.Select(f => NewSource(f, acquisition)) ?? []);
        await acquisitions.SaveChangesAsync(token);
        return await BuildAsync(acquisition, token);
    }, ct);

    public Task<AcquisitionDto> Handle(UpdateAcquisition cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var a = await acquisitions.GetForUpdateAsync(cmd.Id, token);
        if (a.Status != AcquisitionStatus.Draft) throw new ConflictException("Financing is fixed once the purchase completes; only a draft can be edited.");
        if (cmd.Input.LegalEntityId != a.LegalEntityId) throw new FieldValidationException("legalEntityId", "An acquisition cannot move to another legal entity.");
        await RequirePropertyAsync(cmd.Input, token);
        Map(cmd.Input, a);
        foreach (var old in await sources.Tracked(new Specification<FundingSource>(f => f.AcquisitionId == a.Id)).ToListAsync(token)) sources.Remove(old);
        sources.AddRange(cmd.Input.Funding?.Select(f => NewSource(f, a)) ?? []);
        await acquisitions.SaveChangesAsync(token);
        return await BuildAsync(a, token);
    }, ct);

    public async Task<AcquisitionDto> Handle(CancelAcquisition cmd, CancellationToken ct)
    {
        var a = await acquisitions.GetForUpdateAsync(cmd.Id, ct);
        a.Cancel();
        await acquisitions.SaveChangesAsync(ct);
        return await BuildAsync(a, ct);
    }

    public Task<AcquisitionDto> Handle(CompleteAcquisition cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var a = await acquisitions.GetForUpdateAsync(cmd.Id, token);
        if (a.Status != AcquisitionStatus.Draft) throw new ConflictException($"Only a draft acquisition can be completed (this one is {a.Status}).");
        var funding = (await sources.Tracked(new Specification<FundingSource>(f => f.AcquisitionId == a.Id)).ToListAsync(token)).OrderBy(f => f.FirstDueDate).ThenByDescending(f => f.Principal).ToList(); // ordered in memory: SQLite cannot sort decimals
        var financed = funding.Sum(f => f.Principal);
        var unfunded = a.PurchasePrice - a.DownPayment - financed;
        if (unfunded != 0) throw new ConflictException($"The purchase is not fully funded: {unfunded:0.00} of the price is covered by neither the down payment nor a funding source.");
        if (a.CashAtCompletion > 0 && a.PayFromAccountId is null) throw new FieldValidationException("payFromAccountId", "Choose the bank account the down payment and fees are paid from.");

        var property = await posting.AccountForRoleAsync("realestate.investment", token);
        var memo = $"Acquisition {a.Number}";
        var lines = new List<PostingLine> { new(property, a.TotalCost, 0, memo) };
        if (a.CashAtCompletion > 0)
        {
            var bank = await accounts.FindAsync(a.PayFromAccountId!.Value, token);
            if (bank is null || bank.Type != AccountType.Asset || !bank.IsPostable || !bank.IsActive) throw new FieldValidationException("payFromAccountId", "Choose an active bank account.");
            lines.Add(new PostingLine(bank.Id, 0, a.CashAtCompletion, memo));
        }
        if (financed > 0)
        {
            var payable = await posting.AccountForRoleAsync("realestate.financing", token);
            foreach (var f in funding) lines.Add(new PostingLine(payable, 0, f.Principal, $"{f.Lender} {memo}"));
        }
        var journal = await posting.PostAsync(new PostingRequest(a.LegalEntityId, a.PurchaseDate, "acquisition", a.Id, memo, a.ContractReference ?? a.Number, lines, a.CurrencyCode), token);
        a.Complete(journal.Id, clock.UtcNow);

        foreach (var f in funding)
            instalments.AddRange(FundingSchedule.Generate(f.Principal, f.AnnualRatePercent, f.TermMonths, f.FirstDueDate).Select(p => new FundingInstalment
            {
                LegalEntityId = f.LegalEntityId, FundingSourceId = f.Id, Sequence = p.Sequence, DueDate = p.DueDate, Principal = p.Principal, Interest = p.Interest, Total = p.Total, BalanceAfter = p.BalanceAfter,
            }));
        await acquisitions.SaveChangesAsync(token);
        return await BuildAsync(a, token);
    }, ct);

    public Task<FundingSourceDto> Handle(PayFundingInstalment cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var inst = await instalments.GetForUpdateAsync(cmd.InstalmentId, token);
        if (inst.Status == InstalmentPayStatus.Paid) throw new ConflictException("This instalment is already paid.");
        var earlier = await instalments.AnyAsync(i => i.FundingSourceId == inst.FundingSourceId && i.Sequence < inst.Sequence && i.Status == InstalmentPayStatus.Scheduled, token);
        if (earlier) throw new ConflictException("Pay the earlier instalments first.");
        var source = await sources.GetAsync(inst.FundingSourceId, token);
        var bank = await accounts.FindAsync(cmd.BankAccountId, token);
        if (bank is null || bank.Type != AccountType.Asset || !bank.IsPostable || !bank.IsActive) throw new FieldValidationException("bankAccountId", "Choose an active bank account.");
        var count = await instalments.CountAsync(i => i.FundingSourceId == source.Id, token);
        var memo = $"{source.Lender} instalment {inst.Sequence}/{count}";
        var lines = new List<PostingLine> { new(await posting.AccountForRoleAsync("realestate.financing", token), inst.Principal, 0, memo) };
        if (inst.Interest > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync("realestate.expense.finance", token), inst.Interest, 0, memo));
        lines.Add(new PostingLine(bank.Id, 0, inst.Total, memo));
        var journal = await posting.PostAsync(new PostingRequest(inst.LegalEntityId, cmd.Date, "financing", inst.Id, memo, source.Lender, lines), token);
        inst.Status = InstalmentPayStatus.Paid; inst.PaidOn = cmd.Date; inst.JournalEntryId = journal.Id;
        await instalments.SaveChangesAsync(token);
        return await BuildSourceAsync(source, includeInstalments: true, token);
    }, ct);

    private async Task RequirePropertyAsync(AcquisitionInput i, CancellationToken ct)
    {
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        if (property.LegalEntityId != i.LegalEntityId) throw new FieldValidationException("propertyId", "The property belongs to another legal entity.");
    }

    private static void Map(AcquisitionInput i, Acquisition a)
    {
        a.PropertyId = i.PropertyId; a.SellerName = i.SellerName.Trim(); a.PurchaseDate = i.PurchaseDate; a.ContractReference = i.ContractReference?.Trim(); a.PurchasePrice = i.PurchasePrice;
        a.DownPayment = i.DownPayment; a.TransferFees = i.TransferFees; a.OtherCosts = i.OtherCosts; a.PayFromAccountId = i.PayFromAccountId; a.Notes = i.Notes;
    }

    private static FundingSource NewSource(FundingInput f, Acquisition a) => new()
    {
        LegalEntityId = a.LegalEntityId, AcquisitionId = a.Id, Type = f.Type, Lender = f.Lender.Trim(), Principal = f.Principal, AnnualRatePercent = f.AnnualRatePercent, TermMonths = f.TermMonths,
        FirstDueDate = f.FirstDueDate, MonthlyPayment = FundingSchedule.MonthlyPayment(f.Principal, f.AnnualRatePercent, f.TermMonths),
    };

    private async Task<AcquisitionDto> BuildAsync(Acquisition a, CancellationToken ct)
    {
        var funding = (await sources.Query(new Specification<FundingSource>(f => f.AcquisitionId == a.Id)).ToListAsync(ct)).OrderBy(f => f.FirstDueDate).ThenByDescending(f => f.Principal).ToList();
        var dtos = new List<FundingSourceDto>();
        foreach (var f in funding) dtos.Add(await BuildSourceAsync(f, includeInstalments: true, ct));
        var propertyName = await properties.Query(new Specification<Property>(p => p.Id == a.PropertyId)).Select(p => p.Name).FirstOrDefaultAsync(ct);
        var financed = funding.Sum(f => f.Principal);
        return new AcquisitionDto(a.Id, a.Number, a.LegalEntityId, a.PropertyId, propertyName, a.SellerName, a.PurchaseDate, a.ContractReference, a.PurchasePrice, a.DownPayment, a.TransferFees, a.OtherCosts,
            a.PayFromAccountId, a.Notes, a.CurrencyCode, a.Status, a.TotalCost, a.CashAtCompletion, financed, a.PurchasePrice - a.DownPayment - financed, a.JournalEntryId, dtos);
    }

    private async Task<FundingSourceDto> BuildSourceAsync(FundingSource f, bool includeInstalments, CancellationToken ct)
    {
        var rows = await instalments.Query(new Specification<FundingInstalment>(i => i.FundingSourceId == f.Id)).OrderBy(i => i.Sequence).ToListAsync(ct);
        var paid = rows.Where(r => r.Status == InstalmentPayStatus.Paid).ToList();
        var next = rows.FirstOrDefault(r => r.Status == InstalmentPayStatus.Scheduled);
        var outstanding = rows.Count == 0 ? f.Principal : f.Principal - paid.Sum(r => r.Principal);
        var cost = rows.Count == 0 ? FundingSchedule.Generate(f.Principal, f.AnnualRatePercent, f.TermMonths, f.FirstDueDate).Sum(p => p.Interest) : rows.Sum(r => r.Interest);
        return new FundingSourceDto(f.Id, f.AcquisitionId, f.Type, f.Lender, f.Principal, f.AnnualRatePercent, f.TermMonths, f.FirstDueDate, f.MonthlyPayment, cost, paid.Count, f.TermMonths, outstanding, next?.DueDate,
            includeInstalments ? rows.Select(r => new FundingInstalmentDto(r.Id, r.Sequence, r.DueDate, r.Principal, r.Interest, r.Total, r.BalanceAfter, r.Status, r.PaidOn)).ToList() : []);
    }
}

/// <summary>Unpaid financing instalments are expected cash outflows on their due dates.</summary>
public sealed class FinancingForecastSource(IReadRepository<FundingInstalment> instalments) : ICashForecastSource
{
    public string Key => "financing";
    public string Label => "Financing";
    public string? ModuleKey => RealEstateModule.Key;

    public async Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? entityId, DateOnly asOf, DateOnly horizon, CancellationToken ct)
    {
        var rows = await instalments.Query(new Specification<FundingInstalment>(i => i.Status == InstalmentPayStatus.Scheduled && i.DueDate <= horizon && (entityId == null || i.LegalEntityId == entityId)))
            .Select(i => new { i.DueDate, i.Total }).ToListAsync(ct);
        return rows.Select(r => new ForecastItem(r.DueDate, -r.Total)).ToList();
    }
}
