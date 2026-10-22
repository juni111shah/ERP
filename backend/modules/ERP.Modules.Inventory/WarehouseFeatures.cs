using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using FluentValidation;
using MediatR;

namespace ERP.Modules.Inventory;

public sealed record WarehouseDto(Guid Id, string Code, string Name, Guid LegalEntityId);

public sealed record CreateWarehouse(WarehouseInput Input) : ICommand<WarehouseDto>, IRequirePermission
{
    public string RequiredPermission => InventoryModule.WarehousesWrite;
}

public sealed class CreateWarehouseValidator : AbstractValidator<CreateWarehouse>
{
    public CreateWarehouseValidator()
    {
        RuleFor(x => x.Input.Code).NotEmpty().MaximumLength(20).WithMessage("Code and name are required.");
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("Code and name are required.");
    }
}

internal sealed class WarehouseHandlers(IRepository<Warehouse> warehouses, IReadRepository<LegalEntity> legalEntities, ICurrentUser user) :
    IRequestHandler<CreateWarehouse, WarehouseDto>
{
    public async Task<WarehouseDto> Handle(CreateWarehouse cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        if (!await legalEntities.AnyAsync(e => e.Id == i.LegalEntityId, ct) || (user.AllowedLegalEntityIds is { } allowed && !allowed.Contains(i.LegalEntityId)))
            throw new FieldValidationException("legalEntityId", "Legal entity not found.");
        var warehouse = new Warehouse { Code = i.Code.Trim(), Name = i.Name.Trim(), LegalEntityId = i.LegalEntityId, Address = i.Address };
        warehouses.Add(warehouse);
        await warehouses.SaveChangesAsync(ct);
        return new(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.LegalEntityId);
    }
}
