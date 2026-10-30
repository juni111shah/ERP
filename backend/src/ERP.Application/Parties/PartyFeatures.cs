using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Parties;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Parties;

public sealed record PartyDto(
    Guid Id, string Code, string Name, string Kind, string[] Roles, string? Email, string? Phone, string? TaxNumber,
    string? IdNumber, string? Iban, string? CurrencyCode, int? PaymentTermsDays, string? Address, bool IsActive, JsonObject CustomFields, Guid? PartyGroupId = null);

public sealed record PartyInput(
    string Name, PartyKind Kind, PartyRoles Roles, string? Email, string? Phone, string? TaxNumber, string? IdNumber,
    string? Iban, string? CurrencyCode, int? PaymentTermsDays, string? Address, bool IsActive = true, JsonObject? CustomFields = null, Guid? PartyGroupId = null);

public static class Masking
{
    /// <summary>Keeps the last 4 characters: "AE070331234567890123456" → "*******************3456".</summary>
    public static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var keep = Math.Min(4, value.Length / 3);
        return new string('*', value.Length - keep) + value[^keep..];
    }
}

/// <summary>Generic paged query (search / filter / sort) plus a role shortcut (<c>?role=customer</c>).</summary>
public sealed record ListParties : PagedQuery, IRequest<PagedResult<PartyDto>>, IRequirePermission
{
    /// <summary>Role shortcut (case-insensitive): customer, vendor, employee, other.</summary>
    public string? Role { get; init; }
    public string RequiredPermission => CorePermissions.PartiesRead;
}

public sealed record GetParty(Guid Id) : IRequest<PartyDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PartiesRead;
}

public sealed record CreateParty(PartyInput Input) : IRequest<PartyDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PartiesWrite;
}

public sealed record UpdateParty(Guid Id, PartyInput Input) : IRequest<PartyDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PartiesWrite;
}

public sealed class CreatePartyValidator : AbstractValidator<CreateParty>
{
    public CreatePartyValidator()
    {
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Input.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Input.Email));
        RuleFor(x => x.Input.Roles).NotEqual(PartyRoles.None).WithMessage("Choose at least one role (customer, vendor...).");
        RuleFor(x => x.Input.CurrencyCode).Length(3).When(x => x.Input.CurrencyCode is not null);
        RuleFor(x => x.Input.Phone).MaximumLength(50);
        RuleFor(x => x.Input.TaxNumber).MaximumLength(50);
        RuleFor(x => x.Input.IdNumber).MaximumLength(50);
        RuleFor(x => x.Input.Iban).MaximumLength(50);
        RuleFor(x => x.Input.Address).MaximumLength(1000);
    }
}

public sealed class UpdatePartyValidator : AbstractValidator<UpdateParty>
{
    public UpdatePartyValidator()
    {
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Input.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Input.Email));
        RuleFor(x => x.Input.Roles).NotEqual(PartyRoles.None);
        RuleFor(x => x.Input.CurrencyCode).Length(3).When(x => !string.IsNullOrWhiteSpace(x.Input.CurrencyCode));
        RuleFor(x => x.Input.Phone).MaximumLength(50);
        RuleFor(x => x.Input.TaxNumber).MaximumLength(50);
        RuleFor(x => x.Input.IdNumber).MaximumLength(50);
        RuleFor(x => x.Input.Iban).MaximumLength(50);
        RuleFor(x => x.Input.Address).MaximumLength(1000);
    }
}

internal sealed class PartyHandlers(IRepository<Party> parties, IReadRepository<PartyGroup> groups, INumberingService numbers, ICustomFieldService fields, ICurrentUser user) :
    IRequestHandler<ListParties, PagedResult<PartyDto>>,
    IRequestHandler<GetParty, PartyDto>,
    IRequestHandler<CreateParty, PartyDto>,
    IRequestHandler<UpdateParty, PartyDto>
{
    public const string EntityType = "party";

    public async Task<PagedResult<PartyDto>> Handle(ListParties q, CancellationToken ct)
    {
        Specification<Party>? spec = null;
        if (!string.IsNullOrWhiteSpace(q.Role))
        {
            if (!Enum.TryParse<PartyRoles>(q.Role, ignoreCase: true, out var role) || role == PartyRoles.None)
                throw new FieldValidationException("role", "Unknown party role.");
            spec = new Specification<Party>(p => (p.Roles & role) == role);
        }
        var page = await parties.PageAsync(q, spec, ct);
        var defs = await fields.GetDefinitionsAsync(EntityType, ct);
        return page.Map(p => ToDto(p, CustomFieldValidator.Project(p.CustomFields, defs)));
    }

    public async Task<PartyDto> Handle(GetParty q, CancellationToken ct)
    {
        var p = await parties.GetAsync(q.Id, ct);
        return ToDto(p, await fields.ReadAsync(EntityType, p, ct));
    }

    public async Task<PartyDto> Handle(CreateParty cmd, CancellationToken ct)
    {
        var p = new Party { Code = await numbers.NextAsync("party", ct: ct) };
        await EnsureGroupAsync(cmd.Input.PartyGroupId, ct);
        Map(cmd.Input, p);
        await fields.ApplyAsync(EntityType, p, cmd.Input.CustomFields, isCreate: true, ct);
        parties.Add(p);
        await parties.SaveChangesAsync(ct);
        return ToDto(p, await fields.ReadAsync(EntityType, p, ct));
    }

    public async Task<PartyDto> Handle(UpdateParty cmd, CancellationToken ct)
    {
        var p = await parties.GetForUpdateAsync(cmd.Id, ct);
        var (idNumber, iban) = (p.IdNumber, p.Iban);
        await EnsureGroupAsync(cmd.Input.PartyGroupId, ct);
        Map(cmd.Input, p);
        // A user who only sees masked values must not overwrite the real ones with the mask.
        if (!user.HasPermission(CorePermissions.SensitiveRead)) { p.IdNumber = idNumber; p.Iban = iban; }
        await fields.ApplyAsync(EntityType, p, cmd.Input.CustomFields, isCreate: false, ct);
        await parties.SaveChangesAsync(ct);
        return ToDto(p, await fields.ReadAsync(EntityType, p, ct));
    }

    private static void Map(PartyInput i, Party p)
    {
        p.Name = i.Name.Trim(); p.Kind = i.Kind; p.Roles = i.Roles; p.Email = i.Email; p.Phone = i.Phone; p.TaxNumber = i.TaxNumber;
        p.IdNumber = i.IdNumber; p.Iban = i.Iban; p.CurrencyCode = string.IsNullOrWhiteSpace(i.CurrencyCode) ? null : i.CurrencyCode.Trim().ToUpperInvariant(); p.PaymentTermsDays = i.PaymentTermsDays;
        p.Address = i.Address; p.IsActive = i.IsActive; p.PartyGroupId = i.PartyGroupId;
    }

    private async Task EnsureGroupAsync(Guid? groupId, CancellationToken ct)
    {
        if (groupId is { } id && !await groups.AnyAsync(g => g.Id == id, ct)) throw new FieldValidationException("partyGroupId", "Party group not found.");
    }

    private PartyDto ToDto(Party p, JsonObject cf)
    {
        var sensitive = user.HasPermission(CorePermissions.SensitiveRead);
        var roles = Enum.GetValues<PartyRoles>().Where(r => r != PartyRoles.None && p.Roles.HasFlag(r)).Select(r => r.ToString()).ToArray();
        return new(p.Id, p.Code, p.Name, p.Kind.ToString(), roles, p.Email, p.Phone, p.TaxNumber,
            sensitive ? p.IdNumber : Masking.Mask(p.IdNumber), sensitive ? p.Iban : Masking.Mask(p.Iban),
            p.CurrencyCode, p.PaymentTermsDays, p.Address, p.IsActive, cf, p.PartyGroupId);
    }
}
