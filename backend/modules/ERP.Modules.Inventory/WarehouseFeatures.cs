using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Application.Organizations;
using FluentValidation;
using MediatR;

namespace ERP.Modules.Inventory;

public sealed record WarehouseDto(Guid Id, string Code, string Name, Guid LegalEntityId, string? Address = null, bool IsActive = true);
public sealed record WarehouseUpdateInput(string Name, string? Address, bool IsActive);

public sealed record CreateWarehouse(WarehouseInput Input) : ICommand<WarehouseDto>, IRequirePermission
{
    public string RequiredPermission => InventoryModule.WarehousesWrite;
}

/// <summary>Renames a warehouse, changes its address or deactivates it. The code and legal entity never change (stock movements refer to them).</summary>
public sealed record UpdateWarehouse(Guid Id, WarehouseUpdateInput Input) : ICommand<WarehouseDto>, IRequirePermission
{
    public string RequiredPermission => InventoryModule.WarehousesWrite;
}

public sealed class UpdateWarehouseValidator : AbstractValidator<UpdateWarehouse>
{
    public UpdateWarehouseValidator() => RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("Code and name are required.");
}

public sealed class CreateWarehouseValidator : AbstractValidator<CreateWarehouse>
{
    public CreateWarehouseValidator()
    {
        RuleFor(x => x.Input.Code).NotEmpty().MaximumLength(20).WithMessage("Code and name are required.");
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200).WithMessage("Code and name are required.");
    }
}

internal sealed class WarehouseHandlers(IRepository<Warehouse> warehouses, ILegalEntityGuard legalEntityGuard) :
    IRequestHandler<CreateWarehouse, WarehouseDto>,
    IRequestHandler<UpdateWarehouse, WarehouseDto>
{
    public async Task<WarehouseDto> Handle(CreateWarehouse cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        await legalEntityGuard.RequireAsync(i.LegalEntityId, ct);
        var code = i.Code.Trim();
        if (await warehouses.AnyAsync(w => w.Code == code, ct)) throw new ConflictException($"A warehouse with code '{code}' already exists.");
        var warehouse = new Warehouse { Code = code, Name = i.Name.Trim(), LegalEntityId = i.LegalEntityId, Address = i.Address };
        warehouses.Add(warehouse);
        await warehouses.SaveChangesAsync(ct);
        return new(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.LegalEntityId, warehouse.Address, warehouse.IsActive);
    }

    public async Task<WarehouseDto> Handle(UpdateWarehouse cmd, CancellationToken ct)
    {
        var w = await warehouses.GetForUpdateAsync(cmd.Id, ct);
        w.Name = cmd.Input.Name.Trim(); w.Address = cmd.Input.Address; w.IsActive = cmd.Input.IsActive;
        await warehouses.SaveChangesAsync(ct);
        return new(w.Id, w.Code, w.Name, w.LegalEntityId, w.Address, w.IsActive);
    }
}
