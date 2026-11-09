using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Common;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using ERP.Modules.IndustryPacks.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.IndustryPacks.Features;

public sealed record RecordLineInput(string Description, decimal Quantity, decimal UnitPrice, string? Code = null, Guid? ItemId = null, string? UnitOfMeasure = null, DateOnly? DueDate = null, decimal? CompletedQuantity = null, Guid? Id = null);

public sealed record RecordInput(
    Guid LegalEntityId, string Title, DateOnly RecordDate, JsonObject? Data = null, Guid? PartyId = null, Guid? ParentId = null, Guid? BusinessUnitId = null,
    DateOnly? DueDate = null, decimal? Amount = null, string? Notes = null, List<RecordLineInput>? Lines = null);

public sealed record RecordLineDto(Guid Id, int Seq, string? Code, string Description, Guid? ItemId, string? UnitOfMeasure, decimal Quantity, decimal UnitPrice, decimal Amount, decimal CompletedQuantity, DateOnly? DueDate, Guid? InvoiceId);
public sealed record RecordEventDto(Guid Id, string? FromStatus, string ToStatus, DateTime OccurredAt, string? UserName, string? Reason);

public sealed record RecordDto(
    Guid Id, string ModuleKey, string RecordType, Guid LegalEntityId, string Number, string Title, string Status, Guid? PartyId, string? PartyName, Guid? ParentId, string? ParentTitle,
    Guid? BusinessUnitId, DateOnly RecordDate, DateOnly? DueDate, decimal Amount, JsonObject Data, string? Notes, Guid? InvoiceId, Guid? BillId, DateOnly? ConsumedOn, DateTime CreatedAt,
    IReadOnlyList<RecordLineDto> Lines, IReadOnlyList<RecordEventDto> Events, IReadOnlyList<RecordTransitionDef> NextSteps);

public sealed record RecordTypeView(
    string Key, string Label, string Singular, string Icon, IReadOnlyList<RecordFieldDef> Fields, IReadOnlyList<RecordStatusDef> Statuses, string? PartyRole, string? PartyLabel,
    string? ParentType, string? ParentLabel, bool HasLines, string? LinesLabel, bool HasAmount, bool HasDueDate, bool CanInvoice, bool CanBill, bool PerLineBilling, IReadOnlyList<string>? BillableStatuses,
    bool ConsumesStock, bool TracksProgress, bool Sensitive, string? TitleLabel);
public sealed record PackView(string Key, string Name, string Description, IReadOnlyList<RecordTypeView> Types);
public sealed record RecordStatusCount(string Status, int Count, decimal Amount);
public sealed record RecordTypeSummary(string Type, string Label, int Total, decimal Amount, IReadOnlyList<RecordStatusCount> ByStatus);

public abstract record RecordRequest(string Module, string Type) : IRequirePermission
{
    public abstract string RequiredPermission { get; }
}

public sealed record GetPackView(string Module) : IQuery<PackView>;
public sealed record GetRecordSummary(string Module, Guid? LegalEntityId) : IQuery<IReadOnlyList<RecordTypeSummary>>;

public sealed record ListRecords(string Module, string Type, PagedQuery Paging) : RecordRequest(Module, Type), IQuery<PagedResult<RecordDto>>
{
    public override string RequiredPermission => PackModule.ReadPermission(Module, Type);
}
public sealed record GetRecord(string Module, string Type, Guid Id) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ReadPermission(Module, Type);
}
public sealed record CreateRecord(string Module, string Type, RecordInput Input) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
public sealed record UpdateRecord(string Module, string Type, Guid Id, RecordInput Input) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
public sealed record DeleteRecord(string Module, string Type, Guid Id) : RecordRequest(Module, Type), ICommand
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
public sealed record TransitionRecord(string Module, string Type, Guid Id, string To, string? Reason) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
/// <summary>Raises a core invoice (and issues it, posting the ledger) from the whole record or the chosen lines.</summary>
public sealed record InvoiceRecord(string Module, string Type, Guid Id, DateOnly? Date, Guid? TaxCodeId, IReadOnlyList<Guid>? LineIds) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
/// <summary>Raises a core vendor bill and posts it, unless an approval policy holds it.</summary>
public sealed record BillRecord(string Module, string Type, Guid Id, DateOnly? Date, Guid? TaxCodeId, string? VendorReference) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
/// <summary>Issues the lines' materials from stock to the definition's expense account.</summary>
public sealed record ConsumeRecord(string Module, string Type, Guid Id, DateOnly? Date, Guid? WarehouseId) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
/// <summary>Records cumulative progress on lines; a line cannot exceed its quantity or go backwards without a reason in the notes.</summary>
public sealed record UpdateRecordProgress(string Module, string Type, Guid Id, IReadOnlyList<RecordProgressLine> Lines) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
public sealed record RecordProgressLine(Guid LineId, decimal CompletedQuantity);

public sealed class RecordInputValidator : AbstractValidator<RecordInput>
{
    public RecordInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty().WithMessage("Choose a legal entity.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Enter a name.").MaximumLength(300);
        RuleFor(x => x.RecordDate).MustBePlausibleDate();
        RuleFor(x => x.DueDate).Must(d => d is null || d >= new DateOnly(2000, 1, 1)).WithMessage("Enter a valid date.");
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.Amount).Must(a => a is null || a >= 0).WithMessage("The amount cannot be negative.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().WithMessage("Enter a description.").MaximumLength(500);
            l.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("The quantity cannot be negative.");
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("The price cannot be negative.");
            l.RuleFor(x => x.CompletedQuantity).Must(q => q is null || q >= 0).WithMessage("The completed quantity cannot be negative.");
        });
    }
}

public sealed class CreateRecordValidator : AbstractValidator<CreateRecord>
{
    public CreateRecordValidator() => RuleFor(x => x.Input).SetValidator(new RecordInputValidator());
}

public sealed class UpdateRecordValidator : AbstractValidator<UpdateRecord>
{
    public UpdateRecordValidator() => RuleFor(x => x.Input).SetValidator(new RecordInputValidator());
}

public sealed class TransitionRecordValidator : AbstractValidator<TransitionRecord>
{
    public TransitionRecordValidator()
    {
        RuleFor(x => x.To).NotEmpty().WithMessage("Choose the next status.");
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

internal sealed class RecordHandlers(
    IRepository<IndustryRecord> records,
    IRepository<IndustryRecordEvent> events,
    IRepository<IndustryRecordLine> recordLines,
    IRepository<AuditEvent> audit,
    IReadRepository<Party> parties,
    IReadRepository<Item> items,
    IReadRepository<BusinessUnit> units,
    ILegalEntityGuard legalEntities,
    INumberingService numbering,
    IPostingService posting,
    IStockPostingService stock,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork unitOfWork,
    ISender sender) :
    IRequestHandler<GetPackView, PackView>,
    IRequestHandler<GetRecordSummary, IReadOnlyList<RecordTypeSummary>>,
    IRequestHandler<ListRecords, PagedResult<RecordDto>>,
    IRequestHandler<GetRecord, RecordDto>,
    IRequestHandler<CreateRecord, RecordDto>,
    IRequestHandler<UpdateRecord, RecordDto>,
    IRequestHandler<DeleteRecord, Unit>,
    IRequestHandler<TransitionRecord, RecordDto>,
    IRequestHandler<InvoiceRecord, RecordDto>,
    IRequestHandler<BillRecord, RecordDto>,
    IRequestHandler<ConsumeRecord, RecordDto>,
    IRequestHandler<UpdateRecordProgress, RecordDto>
{
    public Task<PackView> Handle(GetPackView q, CancellationToken ct)
    {
        var pack = PackCatalog.Require(q.Module);
        var types = pack.Types.Where(t => user.HasPermission(PackModule.ReadPermission(pack.Key, t.Key))).Select(t => new RecordTypeView(
            t.Key, t.Label, t.Singular, t.Icon, VisibleFields(pack.Key, t), t.Statuses, t.PartyRole, t.PartyLabel, t.ParentType, t.ParentLabel, t.HasLines, t.LinesLabel, t.HasAmount, t.HasDueDate,
            t.Billing?.Invoice == true, t.Billing?.Bill == true, t.Billing?.PerLine == true, t.Billing?.WhenStatus, t.ConsumesStock, t.TracksProgress, t.Sensitive, t.TitleLabel)).ToList();
        return Task.FromResult(new PackView(pack.Key, pack.Name, pack.Description, types));
    }

    public async Task<IReadOnlyList<RecordTypeSummary>> Handle(GetRecordSummary q, CancellationToken ct)
    {
        var pack = PackCatalog.Require(q.Module);
        var readable = pack.Types.Where(t => user.HasPermission(PackModule.ReadPermission(pack.Key, t.Key))).ToList();
        var keys = readable.Select(t => t.Key).ToArray();
        var all = await records.Query().Where(r => r.ModuleKey == pack.Key && keys.Contains(r.RecordType) && (q.LegalEntityId == null || r.LegalEntityId == q.LegalEntityId))
            .Select(r => new { r.RecordType, r.Status, r.Amount }).ToListAsync(ct);
        var rows = all.GroupBy(r => new { r.RecordType, r.Status }).Select(g => new { g.Key.RecordType, g.Key.Status, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).ToList();
        return readable.Select(t =>
        {
            var mine = rows.Where(r => r.RecordType == t.Key).ToList();
            return new RecordTypeSummary(t.Key, t.Label, mine.Sum(r => r.Count), mine.Sum(r => r.Amount), mine.Select(r => new RecordStatusCount(r.Status, r.Count, r.Amount)).ToList());
        }).ToList();
    }

    public async Task<PagedResult<RecordDto>> Handle(ListRecords q, CancellationToken ct)
    {
        var def = PackCatalog.Require(q.Module, q.Type);
        var page = await records.PageAsync(q.Paging, new Specification<IndustryRecord>(r => r.ModuleKey == q.Module && r.RecordType == q.Type), ct);
        return page.Map(r => ToDto(r, def, []));
    }

    public async Task<RecordDto> Handle(GetRecord q, CancellationToken ct)
    {
        var def = PackCatalog.Require(q.Module, q.Type);
        var record = await LoadAsync(q.Module, q.Type, q.Id, tracked: false, ct);
        if (def.Sensitive)
        {
            // Restricted records leave a trace of who opened them; writes are already audited.
            audit.Add(new AuditEvent { OccurredAt = clock.UtcNow, UserId = user.UserId, Action = "Viewed", RecordType = $"{q.Module}.{q.Type}", RecordId = record.Id.ToString(), IpAddress = user.IpAddress });
            await unitOfWork.SaveChangesAsync(ct);
        }
        return ToDto(record, def, record.Lines);
    }

    public async Task<RecordDto> Handle(CreateRecord cmd, CancellationToken ct)
    {
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        var i = cmd.Input;
        await legalEntities.RequireAsync(i.LegalEntityId, ct);
        var record = new IndustryRecord
        {
            ModuleKey = cmd.Module, RecordType = cmd.Type, LegalEntityId = i.LegalEntityId, Status = def.InitialStatus,
            Number = await numbering.NextAsync(PackModule.SequenceKey(cmd.Module, cmd.Type), i.LegalEntityId, i.RecordDate, ct),
        };
        await ApplyAsync(record, def, cmd.Module, i, ct);
        records.Add(record);
        AddLines(record, def, i.Lines);
        Recalculate(record, def, i.Amount);
        record.Events.Add(NewEvent(record, null, record.Status, null));
        await records.SaveChangesAsync(ct);
        return ToDto(record, def, record.Lines);
    }

    public async Task<RecordDto> Handle(UpdateRecord cmd, CancellationToken ct)
    {
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        var record = await LoadAsync(cmd.Module, cmd.Type, cmd.Id, tracked: true, ct);
        if (record.InvoiceId is not null || record.BillId is not null || record.ConsumedOn is not null)
            throw new ConflictException("This record has already reached finance or stock, so it cannot be edited. Void that document first.");
        await legalEntities.RequireAsync(cmd.Input.LegalEntityId, ct);
        if (cmd.Input.LegalEntityId != record.LegalEntityId) throw new FieldValidationException("legalEntityId", "A record cannot move to another legal entity.");
        await ApplyAsync(record, def, cmd.Module, cmd.Input, ct);
        if (cmd.Input.Lines is not null)
        {
            if (record.Lines.Any(l => l.InvoiceId is not null)) throw new ConflictException("Some lines are already billed, so the lines cannot be replaced.");
            foreach (var line in record.Lines.ToList()) { record.Lines.Remove(line); recordLines.Remove(line); }
            AddLines(record, def, cmd.Input.Lines);
        }
        Recalculate(record, def, cmd.Input.Amount);
        await records.SaveChangesAsync(ct);
        return ToDto(record, def, record.Lines);
    }

    public async Task<Unit> Handle(DeleteRecord cmd, CancellationToken ct)
    {
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        var record = await LoadAsync(cmd.Module, cmd.Type, cmd.Id, tracked: true, ct);
        if (record.Status != def.InitialStatus || record.InvoiceId is not null || record.BillId is not null || record.ConsumedOn is not null || record.Lines.Any(l => l.InvoiceId is not null))
            throw new ConflictException("Only a record that has not moved on can be deleted. Cancel it instead.");
        if (await records.AnyAsync(r => r.ParentId == record.Id, ct)) throw new ConflictException("Other records point to this one, so it cannot be deleted.");
        records.Remove(record);
        await records.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<RecordDto> Handle(TransitionRecord cmd, CancellationToken ct)
    {
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        var record = await LoadAsync(cmd.Module, cmd.Type, cmd.Id, tracked: true, ct);
        var step = def.Transitions.FirstOrDefault(t => t.From == record.Status && t.To == cmd.To)
            ?? throw new ConflictException($"A record in status '{record.Status}' cannot move to '{cmd.To}'.");
        if (step.ReasonRequired && string.IsNullOrWhiteSpace(cmd.Reason)) throw new FieldValidationException("reason", "Enter a reason.");
        if (step.Approval)
        {
            if (!user.HasPermission(PackModule.ApprovePermission(cmd.Module, cmd.Type))) throw new ForbiddenException();
            if (record.CreatedBy is { } maker && maker == user.UserId) throw new ConflictException("The person who created a record cannot approve it.");
        }
        var isCancellation = cmd.To is "cancelled" or "voided";
        if (isCancellation && (record.InvoiceId is not null || record.BillId is not null || record.Lines.Any(l => l.InvoiceId is not null)))
            throw new ConflictException("Void the finance document first, then cancel this record.");
        if (isCancellation && record.ConsumedOn is not null)
        {
            await stock.ReverseIssueAsync(record.Number, ct);
            record.ConsumedOn = null;
        }
        var from = record.Status;
        record.Status = cmd.To;
        record.Events.Add(NewEvent(record, from, cmd.To, cmd.Reason?.Trim()));
        await records.SaveChangesAsync(ct);
        return ToDto(record, def, record.Lines);
    }

    public async Task<RecordDto> Handle(InvoiceRecord cmd, CancellationToken ct)
    {
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        if (def.Billing?.Invoice != true) throw new ConflictException($"{def.Label} are not invoiced.");
        var record = await LoadAsync(cmd.Module, cmd.Type, cmd.Id, tracked: true, ct);
        EnsureBillable(record, def);
        if (record.PartyId is not { } partyId) throw new FieldValidationException("partyId", $"Choose the {(def.PartyLabel ?? "customer").ToLowerInvariant()} first.");

        var date = cmd.Date ?? DateOnly.FromDateTime(clock.UtcNow);
        List<IndustryRecordLine> chosen;
        if (def.Billing.PerLine)
        {
            chosen = record.Lines.Where(l => l.InvoiceId is null && (cmd.LineIds is null || cmd.LineIds.Contains(l.Id))).OrderBy(l => l.Seq).ToList();
            if (chosen.Count == 0) throw new ConflictException("There is nothing left to bill on this record.");
        }
        else
        {
            if (record.InvoiceId is not null) throw new ConflictException("This record is already invoiced.");
            chosen = record.Lines.OrderBy(l => l.Seq).ToList();
        }
        var lines = chosen.Count > 0
            ? chosen.Select(l => new InvoiceLineInput($"{record.Number} {l.Description}", l.Quantity, l.UnitPrice, cmd.TaxCodeId, ItemId: l.ItemId)).ToList()
            : [new InvoiceLineInput($"{record.Number} {record.Title}", 1, record.Amount, cmd.TaxCodeId)];
        var due = chosen.Select(l => l.DueDate).FirstOrDefault(d => d is not null) ?? record.DueDate;

        return await unitOfWork.InTransactionAsync(async _ =>
        {
            var invoice = await sender.Send(new CreateInvoiceCommand(new InvoiceInput(record.LegalEntityId, partyId, date, due is { } d && d >= date ? d : date, lines,
                Notes: $"{def.Singular} {record.Number}: {record.Title}", SourceType: RecordInvoiceVoidedHandler.SourceType, SourceId: record.Id, BusinessUnitId: record.BusinessUnitId)), ct);
            await sender.Send(new IssueInvoiceCommand(invoice.Id), ct);
            if (def.Billing.PerLine) foreach (var line in chosen) line.InvoiceId = invoice.Id;
            else record.InvoiceId = invoice.Id;
            MoveAfterBilling(record, def);
            await records.SaveChangesAsync(ct);
            return ToDto(record, def, record.Lines);
        }, ct);
    }

    public async Task<RecordDto> Handle(BillRecord cmd, CancellationToken ct)
    {
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        if (def.Billing?.Bill != true) throw new ConflictException($"{def.Label} do not raise vendor bills.");
        var record = await LoadAsync(cmd.Module, cmd.Type, cmd.Id, tracked: true, ct);
        EnsureBillable(record, def);
        if (record.BillId is not null) throw new ConflictException("This record is already billed.");
        if (record.PartyId is not { } partyId) throw new FieldValidationException("partyId", $"Choose the {(def.PartyLabel ?? "vendor").ToLowerInvariant()} first.");
        var account = await posting.AccountForRoleAsync(def.Billing.ExpenseAccountRole ?? "purchases.expense", ct);
        var date = cmd.Date ?? DateOnly.FromDateTime(clock.UtcNow);
        var lines = record.Lines.Count > 0
            ? record.Lines.OrderBy(l => l.Seq).Select(l => new BillLineInput($"{record.Number} {l.Description}", l.Quantity, l.UnitPrice, account, cmd.TaxCodeId)).ToList()
            : [new BillLineInput($"{record.Number} {record.Title}", 1, record.Amount, account, cmd.TaxCodeId)];
        return await unitOfWork.InTransactionAsync(async _ =>
        {
            var bill = await sender.Send(new CreateBill(new BillInput(record.LegalEntityId, partyId, cmd.VendorReference ?? record.Number, date, record.DueDate is { } d && d >= date ? d : date, lines,
                $"{def.Singular} {record.Number}: {record.Title}", record.BusinessUnitId)), ct);
            await sender.Send(new PostBill(bill.Id), ct);
            record.BillId = bill.Id;
            MoveAfterBilling(record, def);
            await records.SaveChangesAsync(ct);
            return ToDto(record, def, record.Lines);
        }, ct);
    }

    public async Task<RecordDto> Handle(ConsumeRecord cmd, CancellationToken ct)
    {
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        if (!def.ConsumesStock) throw new ConflictException($"{def.Label} do not use stock.");
        var record = await LoadAsync(cmd.Module, cmd.Type, cmd.Id, tracked: true, ct);
        if (record.ConsumedOn is not null) throw new ConflictException("These materials have already been issued.");
        var lines = record.Lines.Where(l => l.ItemId is not null && l.Quantity > 0).ToList();
        if (lines.Count == 0) throw new ConflictException("Add a line with a stock item first.");
        if (!await stock.IsInventoryEnabledAsync(ct)) throw new ConflictException("Turn on the Inventory module to issue materials.");
        if (cmd.WarehouseId is { } w && !await stock.IsWarehouseUsableAsync(w, record.LegalEntityId, ct)) throw new FieldValidationException("warehouseId", "Choose an active warehouse of this legal entity.");

        var needed = lines.GroupBy(l => l.ItemId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var onHand = await stock.GetOnHandAsync(needed.Keys, cmd.WarehouseId, record.LegalEntityId, ct);
        foreach (var (itemId, quantity) in needed)
            if (onHand.GetValueOrDefault(itemId) < quantity)
                throw new ConflictException($"Not enough stock of '{lines.First(l => l.ItemId == itemId).Description}': {onHand.GetValueOrDefault(itemId):0.##} on hand, {quantity:0.##} needed.");

        var date = cmd.Date ?? DateOnly.FromDateTime(clock.UtcNow);
        var account = await posting.AccountForRoleAsync(def.ConsumeAccountRole ?? "cogs", ct);
        return await unitOfWork.InTransactionAsync(async _ =>
        {
            foreach (var line in lines)
                await stock.IssueStockAsync(new StockIssueRequest(record.LegalEntityId, line.ItemId!.Value, line.Quantity, date, record.Number, $"{def.Singular} {record.Number}: {line.Description}", cmd.WarehouseId, account), ct);
            record.ConsumedOn = date;
            await records.SaveChangesAsync(ct);
            return ToDto(record, def, record.Lines);
        }, ct);
    }

    public async Task<RecordDto> Handle(UpdateRecordProgress cmd, CancellationToken ct)
    {
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        if (!def.TracksProgress) throw new ConflictException($"{def.Label} do not track progress.");
        var record = await LoadAsync(cmd.Module, cmd.Type, cmd.Id, tracked: true, ct);
        foreach (var input in cmd.Lines)
        {
            var line = record.Lines.FirstOrDefault(l => l.Id == input.LineId) ?? throw new FieldValidationException("lines", "One of the lines is not on this record.");
            if (input.CompletedQuantity < 0 || input.CompletedQuantity > line.Quantity)
                throw new FieldValidationException("lines", $"'{line.Description}' can be completed up to {line.Quantity:0.##}.");
            line.CompletedQuantity = input.CompletedQuantity;
        }
        await records.SaveChangesAsync(ct);
        return ToDto(record, def, record.Lines);
    }

    private static void EnsureBillable(IndustryRecord record, RecordTypeDef def)
    {
        if (def.Billing?.WhenStatus is { Count: > 0 } allowed && !allowed.Contains(record.Status))
            throw new ConflictException($"A record in status '{record.Status}' cannot be billed yet.");
    }

    private void MoveAfterBilling(IndustryRecord record, RecordTypeDef def)
    {
        if (def.Billing?.AfterBillingStatus is not { } after || after == record.Status) return;
        if (def.Billing.PerLine && record.Lines.Any(l => l.InvoiceId is null)) return;
        record.Events.Add(NewEvent(record, record.Status, after, "Billed"));
        record.Status = after;
    }

    private async Task<IndustryRecord> LoadAsync(string module, string type, Guid id, bool tracked, CancellationToken ct)
    {
        var spec = new Specification<IndustryRecord>(r => r.Id == id && r.ModuleKey == module && r.RecordType == type).Include(r => r.Lines).Include(r => r.Events);
        var query = tracked ? records.Tracked(spec) : records.Query(spec);
        return await query.FirstOrDefaultAsync(ct) ?? throw new NotFoundException("That record was not found.");
    }

    private async Task ApplyAsync(IndustryRecord record, RecordTypeDef def, string module, RecordInput i, CancellationToken ct)
    {
        record.Title = i.Title.Trim();
        record.RecordDate = i.RecordDate;
        record.DueDate = def.HasDueDate ? i.DueDate : null;
        record.Notes = i.Notes?.Trim();
        record.Data = ValidateData(def, module, i.Data, record.Data);

        if (i.BusinessUnitId is { } unitId)
        {
            if (!await units.AnyAsync(u => u.Id == unitId && u.LegalEntityId == i.LegalEntityId, ct)) throw new FieldValidationException("businessUnitId", "Choose a business unit of this legal entity.");
        }
        record.BusinessUnitId = i.BusinessUnitId;

        if (i.PartyId is { } partyId)
        {
            if (def.PartyRole is null) throw new FieldValidationException("partyId", $"{def.Label} do not have a party.");
            var party = await parties.FindAsync(partyId, ct) ?? throw new FieldValidationException("partyId", "That party was not found.");
            if (def.PartyRole == "Customer") ERP.Application.Parties.CustomerRules.RequireCustomer(party);
            if (def.PartyRole == "Vendor") ERP.Application.Parties.VendorRules.RequireVendor(party);
            record.PartyId = party.Id;
            record.PartyName = party.Name;
        }
        else { record.PartyId = null; record.PartyName = null; }

        if (i.ParentId is { } parentId)
        {
            if (def.ParentType is null) throw new FieldValidationException("parentId", $"{def.Label} do not belong to another record.");
            var parent = await records.Query().Where(r => r.Id == parentId && r.ModuleKey == module && r.RecordType == def.ParentType).Select(r => new { r.Id, r.Number, r.Title, r.LegalEntityId }).FirstOrDefaultAsync(ct)
                ?? throw new FieldValidationException("parentId", $"Choose a valid {(def.ParentLabel ?? "record").ToLowerInvariant()}.");
            if (parent.LegalEntityId != i.LegalEntityId) throw new FieldValidationException("parentId", "It belongs to another legal entity.");
            record.ParentId = parent.Id;
            record.ParentTitle = parent.Title;
        }
        else { record.ParentId = null; record.ParentTitle = null; }

        var itemIds = (i.Lines ?? []).Where(l => l.ItemId is not null).Select(l => l.ItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0 && await items.CountAsync(x => itemIds.Contains(x.Id), ct) != itemIds.Count) throw new FieldValidationException("lines", "One of the items was not found.");
    }

    /// <summary>Keeps only declared fields, checks each value against its type, and keeps restricted values the caller was not shown.</summary>
    private string ValidateData(RecordTypeDef def, string module, JsonObject? input, string existingJson)
    {
        var existing = JsonNode.Parse(existingJson)?.AsObject() ?? [];
        var canSeeSensitive = user.HasPermission(PackModule.SensitivePermission(module, def.Key));
        var errors = new Dictionary<string, string[]>();
        var result = new JsonObject();
        foreach (var field in def.Fields)
        {
            if (field.Sensitive && !canSeeSensitive)
            {
                if (existing[field.Key] is { } kept) result[field.Key] = kept.DeepClone();
                continue;
            }
            var value = input?[field.Key];
            var isEmpty = value is null || (value is JsonValue v && v.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s));
            if (isEmpty)
            {
                if (field.Required) errors[field.Key] = [$"{field.Label} is required."];
                continue;
            }
            var error = CheckField(field, value!);
            if (error is not null) errors[field.Key] = [error];
            else result[field.Key] = value!.DeepClone();
        }
        if (errors.Count > 0) throw new FieldValidationException(errors);
        return result.ToJsonString();
    }

    private static string? CheckField(RecordFieldDef field, JsonNode value)
    {
        if (value is not JsonValue v) return $"{field.Label} has an invalid value.";
        switch (field.Type)
        {
            case "number":
            case "money":
                return v.TryGetValue<decimal>(out var n) && (field.Type != "money" || n >= 0) ? null : $"{field.Label} must be a valid number.";
            case "date":
                return v.TryGetValue<string>(out var d) && DateOnly.TryParse(d, out _) ? null : $"{field.Label} must be a date.";
            case "bool":
                return v.TryGetValue<bool>(out _) ? null : $"{field.Label} must be yes or no.";
            case "select":
                return v.TryGetValue<string>(out var o) && field.Options!.Contains(o) ? null : $"{field.Label} must be one of: {string.Join(", ", field.Options!)}.";
            default:
                return v.TryGetValue<string>(out var t) && t.Length <= 2000 ? null : $"{field.Label} is too long.";
        }
    }

    private static void AddLines(IndustryRecord record, RecordTypeDef def, List<RecordLineInput>? lines)
    {
        if (lines is null) return;
        if (!def.HasLines && lines.Count > 0) throw new FieldValidationException("lines", $"{def.Label} do not have lines.");
        var seq = 0;
        foreach (var l in lines)
        {
            record.Lines.Add(new IndustryRecordLine
            {
                RecordId = record.Id, Seq = ++seq, Code = l.Code?.Trim(), Description = l.Description.Trim(), ItemId = l.ItemId, UnitOfMeasure = l.UnitOfMeasure?.Trim(),
                Quantity = l.Quantity, UnitPrice = l.UnitPrice, Amount = Math.Round(l.Quantity * l.UnitPrice, 2, MidpointRounding.AwayFromZero),
                CompletedQuantity = l.CompletedQuantity ?? 0, DueDate = l.DueDate,
            });
        }
    }

    private static void Recalculate(IndustryRecord record, RecordTypeDef def, decimal? amount)
    {
        if (def.HasLines && record.Lines.Count > 0) record.Amount = record.Lines.Sum(l => l.Amount);
        else if (def.HasAmount) record.Amount = Math.Round(amount ?? 0, 2, MidpointRounding.AwayFromZero);
        else record.Amount = 0;
    }

    private IndustryRecordEvent NewEvent(IndustryRecord record, string? from, string to, string? reason) => new()
    {
        RecordId = record.Id, FromStatus = from, ToStatus = to, OccurredAt = clock.UtcNow, UserId = user.UserId, UserName = user.Email, Reason = reason,
    };

    private IReadOnlyList<RecordFieldDef> VisibleFields(string module, RecordTypeDef def)
        => user.HasPermission(PackModule.SensitivePermission(module, def.Key)) ? def.Fields : def.Fields.Where(f => !f.Sensitive).ToList();

    private RecordDto ToDto(IndustryRecord r, RecordTypeDef def, IReadOnlyList<IndustryRecordLine> lines)
    {
        var data = JsonNode.Parse(string.IsNullOrWhiteSpace(r.Data) ? "{}" : r.Data)?.AsObject() ?? [];
        if (!user.HasPermission(PackModule.SensitivePermission(r.ModuleKey, def.Key)))
            foreach (var field in def.Fields.Where(f => f.Sensitive)) data.Remove(field.Key);
        var next = def.Transitions.Where(t => t.From == r.Status && (!t.Approval || user.HasPermission(PackModule.ApprovePermission(r.ModuleKey, def.Key)))).ToList();
        return new RecordDto(r.Id, r.ModuleKey, r.RecordType, r.LegalEntityId, r.Number, r.Title, r.Status, r.PartyId, r.PartyName, r.ParentId, r.ParentTitle, r.BusinessUnitId, r.RecordDate, r.DueDate, r.Amount, data,
            r.Notes, r.InvoiceId, r.BillId, r.ConsumedOn, r.CreatedAt,
            lines.OrderBy(l => l.Seq).Select(l => new RecordLineDto(l.Id, l.Seq, l.Code, l.Description, l.ItemId, l.UnitOfMeasure, l.Quantity, l.UnitPrice, l.Amount, l.CompletedQuantity, l.DueDate, l.InvoiceId)).ToList(),
            r.Events.OrderBy(e => e.OccurredAt).Select(e => new RecordEventDto(e.Id, e.FromStatus, e.ToStatus, e.OccurredAt, e.UserName, e.Reason)).ToList(), next);
    }
}

/// <summary>When a billed invoice is voided, the record or its lines can be billed again.</summary>
internal sealed class RecordInvoiceVoidedHandler(IRepository<IndustryRecord> records) : INotificationHandler<InvoiceVoided>
{
    public const string SourceType = "industry.record";

    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != SourceType || n.SourceId is not { } id) return;
        var record = await records.Tracked(new Specification<IndustryRecord>(r => r.Id == id).Include(r => r.Lines)).FirstOrDefaultAsync(ct);
        if (record is null) return;
        if (record.InvoiceId == n.InvoiceId) record.InvoiceId = null;
        foreach (var line in record.Lines.Where(l => l.InvoiceId == n.InvoiceId)) line.InvoiceId = null;
    }
}

/// <summary>When a vendor bill made from a record is voided or deleted, the record can be billed again.</summary>
internal sealed class RecordBillReleasedHandler(IRepository<IndustryRecord> records) : INotificationHandler<BillReleased>
{
    public async Task Handle(BillReleased n, CancellationToken ct)
    {
        var record = await records.Tracked(new Specification<IndustryRecord>(r => r.BillId == n.BillId)).FirstOrDefaultAsync(ct);
        if (record is not null) record.BillId = null;
    }
}
