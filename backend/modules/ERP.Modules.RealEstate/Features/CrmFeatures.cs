using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Reminders;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

// ---- Listings --------------------------------------------------------------------------------------------------------

public sealed record ListingInput(Guid PropertyId, Guid? UnitId, ListingKind Kind, string Title, decimal Price, string? Channels = null, string? Description = null);
public sealed record ListingDto(Guid Id, Guid LegalEntityId, Guid PropertyId, Guid? UnitId, ListingKind Kind, string Title, decimal Price, ListingStatus Status, string? Channels, string? Description, DateOnly? ListedOn, DateOnly? ClosedOn)
{
    public static ListingDto From(Listing l) => new(l.Id, l.LegalEntityId, l.PropertyId, l.UnitId, l.Kind, l.Title, l.Price, l.Status, l.Channels, l.Description, l.ListedOn, l.ClosedOn);
}

public sealed record GetListing(Guid Id) : IQuery<ListingDto>, IRequirePermission { public string RequiredPermission => P.CrmRead; }
public sealed record CreateListing(ListingInput Input) : ICommand<ListingDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
public sealed record UpdateListing(Guid Id, ListingInput Input) : ICommand<ListingDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
/// <summary>Publishes a draft, closes a published listing (let, sold or withdrawn), or returns a closed one to draft.</summary>
public sealed record ChangeListingStatus(Guid Id, ListingStatus Status) : ICommand<ListingDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }

public sealed class ListingInputValidator : AbstractValidator<ListingInput>
{
    public ListingInputValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Channels).MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}
public sealed class CreateListingValidator : AbstractValidator<CreateListing> { public CreateListingValidator() => RuleFor(x => x.Input).SetValidator(new ListingInputValidator()); }
public sealed class UpdateListingValidator : AbstractValidator<UpdateListing> { public UpdateListingValidator() => RuleFor(x => x.Input).SetValidator(new ListingInputValidator()); }
public sealed class ChangeListingStatusValidator : AbstractValidator<ChangeListingStatus> { public ChangeListingStatusValidator() => RuleFor(x => x.Status).IsInEnum(); }

internal sealed class ListingHandlers(IRepository<Listing> listings, IReadRepository<Property> properties, IReadRepository<Unit> units, IClock clock) :
    IRequestHandler<GetListing, ListingDto>,
    IRequestHandler<CreateListing, ListingDto>,
    IRequestHandler<UpdateListing, ListingDto>,
    IRequestHandler<ChangeListingStatus, ListingDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<ListingDto> Handle(GetListing q, CancellationToken ct) => ListingDto.From(await listings.GetAsync(q.Id, ct));

    public async Task<ListingDto> Handle(CreateListing cmd, CancellationToken ct)
    {
        var l = new Listing();
        await MapAsync(cmd.Input, l, ct);
        listings.Add(l);
        await listings.SaveChangesAsync(ct);
        return ListingDto.From(l);
    }

    public async Task<ListingDto> Handle(UpdateListing cmd, CancellationToken ct)
    {
        var l = await listings.GetForUpdateAsync(cmd.Id, ct);
        if (l.Status == ListingStatus.Closed) throw new ConflictException("A closed listing can no longer be edited. Reopen it as a draft first.");
        if (cmd.Input.PropertyId != l.PropertyId) throw new FieldValidationException("propertyId", "A listing cannot move to another property.");
        await MapAsync(cmd.Input, l, ct);
        await listings.SaveChangesAsync(ct);
        return ListingDto.From(l);
    }

    public async Task<ListingDto> Handle(ChangeListingStatus cmd, CancellationToken ct)
    {
        var l = await listings.GetForUpdateAsync(cmd.Id, ct);
        var ok = (l.Status, cmd.Status) switch
        {
            (ListingStatus.Draft, ListingStatus.Published) => true,
            (ListingStatus.Published, ListingStatus.Closed) => true,
            (ListingStatus.Published, ListingStatus.Draft) => true,
            (ListingStatus.Closed, ListingStatus.Draft) => true,
            _ => false,
        };
        if (!ok) throw new ConflictException($"A {l.Status} listing cannot become {cmd.Status}.");
        if (cmd.Status == ListingStatus.Published) { l.ListedOn ??= Today; l.ClosedOn = null; }
        if (cmd.Status == ListingStatus.Closed) l.ClosedOn = Today;
        if (cmd.Status == ListingStatus.Draft) l.ClosedOn = null;
        l.Status = cmd.Status;
        await listings.SaveChangesAsync(ct);
        return ListingDto.From(l);
    }

    private async Task MapAsync(ListingInput i, Listing l, CancellationToken ct)
    {
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        if (i.UnitId is { } unitId && !await units.AnyAsync(u => u.Id == unitId && u.PropertyId == i.PropertyId, ct)) throw new FieldValidationException("unitId", "That unit is not in this property.");
        l.LegalEntityId = property.LegalEntityId; l.PropertyId = i.PropertyId; l.UnitId = i.UnitId; l.Kind = i.Kind; l.Title = i.Title.Trim();
        l.Price = i.Price; l.Channels = i.Channels; l.Description = i.Description;
    }
}

// ---- Leads -----------------------------------------------------------------------------------------------------------

public sealed record LeadInput(Guid LegalEntityId, string Name, string? Phone, string? Email, string? Source, string? Interest, Guid? AgentPartyId, Guid? ListingId, Guid? UnitId, string? Notes, DateOnly? FollowUpOn);
public sealed record LeadDto(Guid Id, Guid LegalEntityId, string Name, string? Phone, string? Email, string? Source, string? Interest, Guid? AgentPartyId, LeadStage Stage, string? LostReason, Guid? ListingId, Guid? UnitId, string? Notes, DateOnly? FollowUpOn, Guid? DealId)
{
    public static LeadDto From(Lead l) => new(l.Id, l.LegalEntityId, l.Name, l.Phone, l.Email, l.Source, l.Interest, l.AgentPartyId, l.Stage, l.LostReason, l.ListingId, l.UnitId, l.Notes, l.FollowUpOn, l.DealId);
}
public sealed record PipelineStageDto(LeadStage Stage, int Count);
public sealed record LeadPipelineDto(IReadOnlyList<PipelineStageDto> Stages, int Open, int Won, int Lost, int FollowUpsDue, decimal WinRatePercent);
public enum LeadConvertTo { Sale = 1, Lease = 2 }
public sealed record ConvertLeadInput(LeadConvertTo? DealType = null, decimal? DealValue = null, decimal? CommissionRatePercent = null, Guid? ClientPartyId = null);

public sealed record GetLead(Guid Id) : IQuery<LeadDto>, IRequirePermission { public string RequiredPermission => P.CrmRead; }
public sealed record CreateLead(LeadInput Input) : ICommand<LeadDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
public sealed record UpdateLead(Guid Id, LeadInput Input) : ICommand<LeadDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
/// <summary>Moves a lead along the pipeline. Losing needs a reason; winning is done by converting the lead to a deal.</summary>
public sealed record MoveLeadStage(Guid Id, LeadStage Stage, string? LostReason = null) : ICommand<LeadDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
/// <summary>Creates a brokerage deal from the lead (value and type default from its listing), then marks the lead Won.</summary>
public sealed record ConvertLeadToDeal(Guid Id, ConvertLeadInput Input) : ICommand<LeadDto>, IRequirePermission { public string RequiredPermission => P.CrmManage; }
public sealed record GetLeadPipeline(Guid? AgentPartyId = null) : IQuery<LeadPipelineDto>, IRequirePermission { public string RequiredPermission => P.CrmRead; }

public sealed class LeadInputValidator : AbstractValidator<LeadInput>
{
    public LeadInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Email).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Source).MaximumLength(60);
        RuleFor(x => x.Interest).MaximumLength(300);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Phone) || !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Enter a phone number or an email so the lead can be contacted.");
    }
}
public sealed class CreateLeadValidator : AbstractValidator<CreateLead> { public CreateLeadValidator() => RuleFor(x => x.Input).SetValidator(new LeadInputValidator()); }
public sealed class UpdateLeadValidator : AbstractValidator<UpdateLead> { public UpdateLeadValidator() => RuleFor(x => x.Input).SetValidator(new LeadInputValidator()); }
public sealed class MoveLeadStageValidator : AbstractValidator<MoveLeadStage>
{
    public MoveLeadStageValidator()
    {
        RuleFor(x => x.Stage).IsInEnum().NotEqual(LeadStage.Won).WithMessage("Convert the lead to a deal to win it.");
        RuleFor(x => x.LostReason).NotEmpty().When(x => x.Stage == LeadStage.Lost).WithMessage("Say why the lead was lost.");
        RuleFor(x => x.LostReason).MaximumLength(300);
    }
}
public sealed class ConvertLeadToDealValidator : AbstractValidator<ConvertLeadToDeal>
{
    public ConvertLeadToDealValidator()
    {
        RuleFor(x => x.Input.DealValue).GreaterThan(0).When(x => x.Input.DealValue is not null);
        RuleFor(x => x.Input.CommissionRatePercent).InclusiveBetween(0, 100).When(x => x.Input.CommissionRatePercent is not null);
    }
}

internal sealed class LeadHandlers(IRepository<Lead> leads, IReadRepository<Listing> listings, IReadRepository<Unit> units, IReadRepository<Party> parties,
    ILegalEntityGuard guard, ISender sender, IClock clock) :
    IRequestHandler<GetLead, LeadDto>,
    IRequestHandler<CreateLead, LeadDto>,
    IRequestHandler<UpdateLead, LeadDto>,
    IRequestHandler<MoveLeadStage, LeadDto>,
    IRequestHandler<ConvertLeadToDeal, LeadDto>,
    IRequestHandler<GetLeadPipeline, LeadPipelineDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<LeadDto> Handle(GetLead q, CancellationToken ct) => LeadDto.From(await leads.GetAsync(q.Id, ct));

    public async Task<LeadDto> Handle(CreateLead cmd, CancellationToken ct)
    {
        await guard.RequireAsync(cmd.Input.LegalEntityId, ct);
        var l = new Lead { LegalEntityId = cmd.Input.LegalEntityId };
        await MapAsync(cmd.Input, l, ct);
        leads.Add(l);
        await leads.SaveChangesAsync(ct);
        return LeadDto.From(l);
    }

    public async Task<LeadDto> Handle(UpdateLead cmd, CancellationToken ct)
    {
        var l = await leads.GetForUpdateAsync(cmd.Id, ct);
        if (l.Stage == LeadStage.Won) throw new ConflictException("A won lead can no longer be edited.");
        if (cmd.Input.LegalEntityId != l.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of a lead cannot change.");
        await MapAsync(cmd.Input, l, ct);
        await leads.SaveChangesAsync(ct);
        return LeadDto.From(l);
    }

    public async Task<LeadDto> Handle(MoveLeadStage cmd, CancellationToken ct)
    {
        var l = await leads.GetForUpdateAsync(cmd.Id, ct);
        if (l.Stage == LeadStage.Won) throw new ConflictException("A won lead cannot move.");
        if (l.Stage == LeadStage.Lost && cmd.Stage != LeadStage.New) throw new ConflictException("A lost lead can only be reopened as New.");
        l.Stage = cmd.Stage;
        l.LostReason = cmd.Stage == LeadStage.Lost ? cmd.LostReason!.Trim() : null;
        await leads.SaveChangesAsync(ct);
        return LeadDto.From(l);
    }

    public async Task<LeadDto> Handle(ConvertLeadToDeal cmd, CancellationToken ct)
    {
        var l = await leads.GetForUpdateAsync(cmd.Id, ct);
        if (l.Stage == LeadStage.Won) throw new ConflictException("This lead was already converted.");
        if (l.Stage == LeadStage.Lost) throw new ConflictException("Reopen the lost lead before converting it.");
        var i = cmd.Input;
        var listing = l.ListingId is { } lid ? await listings.FindAsync(lid, ct) : null;
        var kind = i.DealType ?? (listing?.Kind == ListingKind.Sale ? LeadConvertTo.Sale : LeadConvertTo.Lease);
        var value = i.DealValue ?? listing?.Price ?? throw new FieldValidationException("dealValue", "Enter the deal value (the lead has no listing to take it from).");
        var rate = i.CommissionRatePercent ?? (kind == LeadConvertTo.Sale ? 2m : 5m);
        var deal = await sender.Send(new CreateDeal(new DealInput(l.LegalEntityId, kind == LeadConvertTo.Sale ? DealType.Sale : DealType.Lease, l.Name, Today, value, rate,
            listing?.PropertyId, l.UnitId ?? listing?.UnitId, null, i.ClientPartyId, l.AgentPartyId, Notes: $"Converted from lead. {l.Interest}".Trim())), ct);
        l.Stage = LeadStage.Won; l.LostReason = null; l.DealId = deal.Id;
        await leads.SaveChangesAsync(ct);
        return LeadDto.From(l);
    }

    public async Task<LeadPipelineDto> Handle(GetLeadPipeline q, CancellationToken ct)
    {
        var rows = await leads.Query(new Specification<Lead>(l => q.AgentPartyId == null || l.AgentPartyId == q.AgentPartyId)).Select(l => new { l.Stage, l.FollowUpOn }).ToListAsync(ct);
        var stages = Enum.GetValues<LeadStage>().Select(s => new PipelineStageDto(s, rows.Count(r => r.Stage == s))).ToList();
        var won = rows.Count(r => r.Stage == LeadStage.Won); var lost = rows.Count(r => r.Stage == LeadStage.Lost);
        var due = rows.Count(r => r.Stage is not (LeadStage.Won or LeadStage.Lost) && r.FollowUpOn is { } d && d <= Today);
        return new(stages, rows.Count - won - lost, won, lost, due, won + lost == 0 ? 0 : Math.Round(won * 100m / (won + lost), 1));
    }

    private async Task MapAsync(LeadInput i, Lead l, CancellationToken ct)
    {
        if (i.AgentPartyId is { } agent && !await parties.AnyAsync(p => p.Id == agent, ct)) throw new FieldValidationException("agentPartyId", "Agent not found.");
        if (i.ListingId is { } lid && !await listings.AnyAsync(x => x.Id == lid, ct)) throw new FieldValidationException("listingId", "Listing not found.");
        if (i.UnitId is { } uid && !await units.AnyAsync(u => u.Id == uid, ct)) throw new FieldValidationException("unitId", "Unit not found.");
        l.Name = i.Name.Trim(); l.Phone = i.Phone?.Trim(); l.Email = i.Email?.Trim(); l.Source = i.Source; l.Interest = i.Interest;
        l.AgentPartyId = i.AgentPartyId; l.ListingId = i.ListingId; l.UnitId = i.UnitId; l.Notes = i.Notes; l.FollowUpOn = i.FollowUpOn;
    }
}

/// <summary>Leads whose follow-up date has arrived are reminded to the brokerage team once per date.</summary>
internal sealed class CrmReminders(IReadRepository<Lead> leads) : IReminderSource
{
    public string? ModuleKey => RealEstateModule.Key;

    public async Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
    {
        var due = await leads.Query(new Specification<Lead>(l => l.FollowUpOn != null && l.FollowUpOn <= today && l.Stage != LeadStage.Won && l.Stage != LeadStage.Lost)).ToListAsync(ct);
        return due.Select(l => new ReminderCandidate($"lead-followup:{l.Id}:{l.FollowUpOn:yyyyMMdd}", $"Follow up with lead {l.Name}", l.Interest ?? l.Phone, "/real-estate/leads", P.CrmManage, Severity: "info")).ToList();
    }
}

/// <summary>Leads in the global search.</summary>
internal sealed class CrmSearch(IReadRepository<Lead> leads, ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => RealEstateModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        if (!user.HasPermission(P.CrmRead)) return [];
        return (await leads.Query(new Specification<Lead>(l => l.Name.Contains(term) || (l.Phone != null && l.Phone.Contains(term)) || (l.Email != null && l.Email.Contains(term)))).Take(limit).Select(l => new { l.Name, l.Stage, l.Phone }).ToListAsync(ct))
            .Select(l => new SearchHit("Leads", l.Name, $"{l.Stage} · {l.Phone}", $"/real-estate/leads?search={Uri.EscapeDataString(l.Name)}")).ToList();
    }
}
