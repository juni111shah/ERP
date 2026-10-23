using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using FluentValidation;
using MediatR;

namespace ERP.Application.Organizations;

public sealed record LegalEntityInput(
    string Name, string? LegalName, string CountryCode, string BaseCurrency, string? TaxRegistrationNo, bool IsTaxRegistered,
    string? RegistrationNo, string? Address, string? Email, string? Phone, bool IsActive = true, JsonObject? CustomFields = null);

public sealed record LegalEntityDto(
    Guid Id, string Code, string Name, string? LegalName, string CountryCode, string BaseCurrency, string? TaxRegistrationNo,
    bool IsTaxRegistered, string? RegistrationNo, string? Address, string? Email, string? Phone, DateOnly? BooksLockedTo, bool IsActive,
    JsonObject CustomFields)
{
    public static LegalEntityDto From(LegalEntity e, JsonObject customFields) => new(
        e.Id, e.Code, e.Name, e.LegalName, e.CountryCode, e.BaseCurrency, e.TaxRegistrationNo, e.IsTaxRegistered, e.RegistrationNo,
        e.Address, e.Email, e.Phone, e.BooksLockedTo, e.IsActive, customFields);
}

public sealed record BusinessUnitInput(Guid LegalEntityId, string Code, string Name, string? Address, bool IsActive = true, JsonObject? CustomFields = null);

public sealed record BusinessUnitDto(Guid Id, Guid LegalEntityId, string Code, string Name, string? Address, bool IsActive, JsonObject CustomFields)
{
    public static BusinessUnitDto From(BusinessUnit b, JsonObject customFields) => new(b.Id, b.LegalEntityId, b.Code, b.Name, b.Address, b.IsActive, customFields);
}

public sealed record CreateLegalEntity(LegalEntityInput Input) : ICommand<LegalEntityDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.EntitiesWrite;
}

public sealed record UpdateLegalEntity(Guid Id, LegalEntityInput Input) : ICommand<LegalEntityDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.EntitiesWrite;
}

public sealed record CreateBusinessUnit(BusinessUnitInput Input) : ICommand<BusinessUnitDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BusinessUnitsWrite;
}

public sealed record UpdateBusinessUnit(Guid Id, BusinessUnitInput Input) : ICommand<BusinessUnitDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BusinessUnitsWrite;
}

public sealed class UpdateBusinessUnitValidator : AbstractValidator<UpdateBusinessUnit>
{
    public UpdateBusinessUnitValidator()
    {
        RuleFor(x => x.Input.Code).NotEmpty().MaximumLength(20).WithMessage("Code and name are required.");
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("Code and name are required.");
    }
}

public sealed class LegalEntityInputValidator : AbstractValidator<LegalEntityInput>
{
    public LegalEntityInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2).WithMessage("Use a 2-letter ISO country code.");
        RuleFor(x => x.BaseCurrency).NotEmpty().Length(3).WithMessage("Use a 3-letter ISO currency code.");
        RuleFor(x => x.Address).MaximumLength(1000);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
    }
}

public sealed class CreateLegalEntityValidator : AbstractValidator<CreateLegalEntity>
{
    public CreateLegalEntityValidator() => RuleFor(x => x.Input).SetValidator(new LegalEntityInputValidator());
}

public sealed class UpdateLegalEntityValidator : AbstractValidator<UpdateLegalEntity>
{
    public UpdateLegalEntityValidator() => RuleFor(x => x.Input).SetValidator(new LegalEntityInputValidator());
}

public sealed class CreateBusinessUnitValidator : AbstractValidator<CreateBusinessUnit>
{
    public CreateBusinessUnitValidator()
    {
        RuleFor(x => x.Input.Code).NotEmpty().MaximumLength(20).WithMessage("Code and name are required.");
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("Code and name are required.");
    }
}

internal sealed class OrganizationHandlers(
    IRepository<LegalEntity> legalEntities, IRepository<BusinessUnit> businessUnits, INumberingService numbers,
    ICustomFieldService fields, ILegalEntityGuard legalEntityGuard) :
    IRequestHandler<CreateLegalEntity, LegalEntityDto>,
    IRequestHandler<UpdateLegalEntity, LegalEntityDto>,
    IRequestHandler<CreateBusinessUnit, BusinessUnitDto>,
    IRequestHandler<UpdateBusinessUnit, BusinessUnitDto>
{
    private const string LegalEntityType = "legalentity";
    private const string BusinessUnitType = "businessunit";

    public async Task<LegalEntityDto> Handle(CreateLegalEntity cmd, CancellationToken ct)
    {
        var entity = new LegalEntity { Code = await numbers.NextAsync(LegalEntityType, ct: ct) };
        Map(cmd.Input, entity);
        await fields.ApplyAsync(LegalEntityType, entity, cmd.Input.CustomFields, isCreate: true, ct);
        legalEntities.Add(entity);
        await legalEntities.SaveChangesAsync(ct);
        return LegalEntityDto.From(entity, await fields.ReadAsync(LegalEntityType, entity, ct));
    }

    public async Task<LegalEntityDto> Handle(UpdateLegalEntity cmd, CancellationToken ct)
    {
        var entity = await legalEntities.GetForUpdateAsync(cmd.Id, ct);
        Map(cmd.Input, entity);
        await fields.ApplyAsync(LegalEntityType, entity, cmd.Input.CustomFields, isCreate: false, ct);
        await legalEntities.SaveChangesAsync(ct);
        return LegalEntityDto.From(entity, await fields.ReadAsync(LegalEntityType, entity, ct));
    }

    public async Task<BusinessUnitDto> Handle(CreateBusinessUnit cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        await legalEntityGuard.RequireAsync(i.LegalEntityId, ct);
        var unit = new BusinessUnit { LegalEntityId = i.LegalEntityId, Code = i.Code.Trim(), Name = i.Name.Trim(), Address = i.Address, IsActive = i.IsActive };
        await fields.ApplyAsync(BusinessUnitType, unit, i.CustomFields, isCreate: true, ct);
        businessUnits.Add(unit);
        await businessUnits.SaveChangesAsync(ct);
        return BusinessUnitDto.From(unit, await fields.ReadAsync(BusinessUnitType, unit, ct));
    }

    public async Task<BusinessUnitDto> Handle(UpdateBusinessUnit cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var unit = await businessUnits.GetForUpdateAsync(cmd.Id, ct);
        if (i.LegalEntityId != unit.LegalEntityId) throw new FieldValidationException("legalEntityId", "A business unit cannot move to another legal entity.");
        var code = i.Code.Trim();
        if (code != unit.Code && await businessUnits.AnyAsync(b => b.LegalEntityId == unit.LegalEntityId && b.Code == code, ct))
            throw new ConflictException($"A business unit with code '{code}' exists in this legal entity.");
        unit.Code = code; unit.Name = i.Name.Trim(); unit.Address = i.Address; unit.IsActive = i.IsActive;
        await fields.ApplyAsync(BusinessUnitType, unit, i.CustomFields, isCreate: false, ct);
        await businessUnits.SaveChangesAsync(ct);
        return BusinessUnitDto.From(unit, await fields.ReadAsync(BusinessUnitType, unit, ct));
    }

    private static void Map(LegalEntityInput i, LegalEntity e)
    {
        e.Name = i.Name.Trim(); e.LegalName = i.LegalName; e.CountryCode = i.CountryCode.ToUpperInvariant(); e.BaseCurrency = i.BaseCurrency.ToUpperInvariant();
        e.TaxRegistrationNo = i.TaxRegistrationNo; e.IsTaxRegistered = i.IsTaxRegistered; e.RegistrationNo = i.RegistrationNo; e.Address = i.Address;
        e.Email = i.Email; e.Phone = i.Phone; e.IsActive = i.IsActive;
    }
}
