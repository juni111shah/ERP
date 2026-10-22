using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Parties;
using ERP.Modules.RealEstate.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

public sealed record DealInput(
    Guid LegalEntityId, DealType DealType, string ClientName, DateOnly DealDate, decimal DealValue, decimal CommissionRatePercent,
    Guid? PropertyId = null, Guid? UnitId = null, string? UnitReference = null, Guid? ClientPartyId = null, Guid? AgentPartyId = null,
    string? ExternalAgentName = null, Guid? TeamLeadPartyId = null, Guid? TaxCodeId = null, decimal AgentSharePercent = 0,
    decimal TeamLeadSharePercent = 0, string? Notes = null, JsonObject? CustomFields = null);

public sealed record DealDto(
    Guid Id, string? Number, Guid LegalEntityId, DealType DealType, Guid? PropertyId, Guid? UnitId, string? UnitReference, Guid? ClientPartyId, string ClientName,
    Guid? AgentPartyId, string? ExternalAgentName, Guid? TeamLeadPartyId, DateOnly DealDate, DealStage Stage, decimal DealValue, string CurrencyCode,
    decimal CommissionRatePercent, Guid? TaxCodeId, decimal AgentSharePercent, decimal TeamLeadSharePercent, decimal GrossCommission, decimal AgentCommission,
    decimal TeamLeadCommission, string? LostReason, string? Notes, JsonObject CustomFields)
{
    public static DealDto From(BrokerageDeal d, JsonObject customFields) => new(
        d.Id, d.Number, d.LegalEntityId, d.DealType, d.PropertyId, d.UnitId, d.UnitReference, d.ClientPartyId, d.ClientName, d.AgentPartyId, d.ExternalAgentName,
        d.TeamLeadPartyId, d.DealDate, d.Stage, d.DealValue, d.CurrencyCode, d.CommissionRatePercent, d.TaxCodeId, d.AgentSharePercent, d.TeamLeadSharePercent,
        d.GrossCommission, Share(d.GrossCommission, d.AgentSharePercent), Share(d.GrossCommission, d.TeamLeadSharePercent), d.LostReason, d.Notes, customFields);

    private static decimal Share(decimal gross, decimal percent) => Math.Round(gross * percent / 100m, 2, MidpointRounding.AwayFromZero);
}

public sealed record GetDeal(Guid Id) : IQuery<DealDto>, IRequirePermission
{
    public string RequiredPermission => P.DealsRead;
}

public sealed record CreateDeal(DealInput Input) : ICommand<DealDto>, IRequirePermission
{
    public string RequiredPermission => P.DealsCreate;
}

public sealed record UpdateDeal(Guid Id, DealInput Input) : ICommand<DealDto>, IRequirePermission
{
    public string RequiredPermission => P.DealsEdit;
}

/// <summary>Moves a deal through the pipeline (Lead -> Viewing -> Negotiation -> Agreed) or marks it lost (needs a reason).</summary>
public sealed record MoveDealStage(Guid Id, DealStage Stage, string? LostReason = null) : ICommand<DealDto>, IRequirePermission
{
    public string RequiredPermission => Stage == DealStage.Lost ? P.DealsLose : P.DealsEdit;
}

public sealed class DealInputValidator : AbstractValidator<DealInput>
{
    public DealInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.DealType).IsInEnum();
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DealValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CommissionRatePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.AgentSharePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.TeamLeadSharePercent).InclusiveBetween(0, 100);
        RuleFor(x => x).Must(x => x.AgentSharePercent + x.TeamLeadSharePercent <= 100).WithName("agentSharePercent")
            .WithMessage("The agent and team-lead shares cannot exceed 100% together.");
    }
}

public sealed class CreateDealValidator : AbstractValidator<CreateDeal>
{
    public CreateDealValidator() => RuleFor(x => x.Input).SetValidator(new DealInputValidator());
}

public sealed class UpdateDealValidator : AbstractValidator<UpdateDeal>
{
    public UpdateDealValidator() => RuleFor(x => x.Input).SetValidator(new DealInputValidator());
}

internal sealed class DealHandlers(
    IRepository<BrokerageDeal> deals, IReadRepository<Property> properties, IReadRepository<Unit> units, IReadRepository<Party> parties,
    INumberingService numbers, ICustomFieldService fields, ILegalEntityGuard legalEntityGuard, IClock clock) :
    IRequestHandler<GetDeal, DealDto>,
    IRequestHandler<CreateDeal, DealDto>,
    IRequestHandler<UpdateDeal, DealDto>,
    IRequestHandler<MoveDealStage, DealDto>
{
    public async Task<DealDto> Handle(GetDeal q, CancellationToken ct)
    {
        var deal = await deals.GetAsync(q.Id, ct);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, ct));
    }

    public async Task<DealDto> Handle(CreateDeal cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await legalEntityGuard.RequireAsync(i.LegalEntityId, ct);
        await ValidateReferencesAsync(i, ct);
        var deal = new BrokerageDeal { LegalEntityId = entity.Id, CurrencyCode = entity.BaseCurrency };
        Map(i, deal);
        deal.Number = await numbers.NextAsync(Docs.Deal, deal.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow), ct);
        await fields.ApplyAsync(Docs.Deal, deal, i.CustomFields, isCreate: true, ct);
        deals.Add(deal);
        await deals.SaveChangesAsync(ct);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, ct));
    }

    public async Task<DealDto> Handle(UpdateDeal cmd, CancellationToken ct)
    {
        var deal = await deals.GetForUpdateAsync(cmd.Id, ct);
        if (deal.Stage is DealStage.Lost or >= DealStage.Invoiced) throw new ConflictException($"A deal in stage {deal.Stage} can no longer be edited.");
        if (cmd.Input.LegalEntityId != deal.LegalEntityId) throw new FieldValidationException("legalEntityId", "A deal cannot move to another legal entity.");
        await ValidateReferencesAsync(cmd.Input, ct);
        Map(cmd.Input, deal);
        await fields.ApplyAsync(Docs.Deal, deal, cmd.Input.CustomFields, isCreate: false, ct);
        await deals.SaveChangesAsync(ct);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, ct));
    }

    public async Task<DealDto> Handle(MoveDealStage cmd, CancellationToken ct)
    {
        var deal = await deals.GetForUpdateAsync(cmd.Id, ct);
        deal.MoveTo(cmd.Stage, cmd.LostReason);
        await deals.SaveChangesAsync(ct);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, ct));
    }

    private async Task ValidateReferencesAsync(DealInput i, CancellationToken ct)
    {
        if (i.PropertyId is { } propertyId && !await properties.AnyAsync(p => p.Id == propertyId, ct)) throw new FieldValidationException("propertyId", "Property not found.");
        if (i.UnitId is { } unitId && !await units.AnyAsync(u => u.Id == unitId && (i.PropertyId == null || u.PropertyId == i.PropertyId), ct))
            throw new FieldValidationException("unitId", "Unit not found in that property.");
        foreach (var (field, id) in new[] { ("clientPartyId", i.ClientPartyId), ("agentPartyId", i.AgentPartyId), ("teamLeadPartyId", i.TeamLeadPartyId) })
            if (id is { } partyId && !await parties.AnyAsync(p => p.Id == partyId, ct)) throw new FieldValidationException(field, "Party not found.");
    }

    private static void Map(DealInput i, BrokerageDeal d)
    {
        d.DealType = i.DealType; d.PropertyId = i.PropertyId; d.UnitId = i.UnitId; d.UnitReference = i.UnitReference; d.ClientPartyId = i.ClientPartyId;
        d.ClientName = i.ClientName.Trim(); d.AgentPartyId = i.AgentPartyId; d.ExternalAgentName = i.ExternalAgentName; d.TeamLeadPartyId = i.TeamLeadPartyId;
        d.DealDate = i.DealDate; d.DealValue = i.DealValue; d.CommissionRatePercent = i.CommissionRatePercent; d.TaxCodeId = i.TaxCodeId;
        d.AgentSharePercent = i.AgentSharePercent; d.TeamLeadSharePercent = i.TeamLeadSharePercent; d.Notes = i.Notes;
        d.ComputeCommission();
    }
}
