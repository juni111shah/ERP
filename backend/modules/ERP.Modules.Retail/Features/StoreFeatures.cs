using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Retail.Domain;
using FluentValidation;
using MediatR;

namespace ERP.Modules.Retail.Features;

public record RetailStoreDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid? WarehouseId,
    string? StoreManagerName,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record CreateStoreCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid? WarehouseId,
    string? StoreManagerName,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    string? CustomFields) : ICommand<RetailStoreDto>;

public record UpdateStoreCommand(
    Guid Id,
    string Name,
    Guid? WarehouseId,
    string? StoreManagerName,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    string? CustomFields) : ICommand<RetailStoreDto>;

public record DeleteStoreCommand(Guid Id) : ICommand<Unit>;

public record GetStoreByIdQuery(Guid Id) : IQuery<RetailStoreDto?>;

public class CreateStoreValidator : AbstractValidator<CreateStoreCommand>
{
    public CreateStoreValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LegalEntityId).NotEmpty();
    }
}

public class UpdateStoreValidator : AbstractValidator<UpdateStoreCommand>
{
    public UpdateStoreValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class StoreHandlers :
    IRequestHandler<CreateStoreCommand, RetailStoreDto>,
    IRequestHandler<UpdateStoreCommand, RetailStoreDto>,
    IRequestHandler<DeleteStoreCommand, Unit>,
    IRequestHandler<GetStoreByIdQuery, RetailStoreDto?>
{
    private readonly IRepository<RetailStore> _stores;

    public StoreHandlers(IRepository<RetailStore> stores)
    {
        _stores = stores;
    }

    public async Task<RetailStoreDto?> Handle(GetStoreByIdQuery request, CancellationToken cancellationToken)
    {
        var store = await _stores.FindAsync(request.Id, cancellationToken);
        return store is null ? null : Map(store);
    }

    public async Task<RetailStoreDto> Handle(CreateStoreCommand request, CancellationToken cancellationToken)
    {
        var store = new RetailStore
        {
            LegalEntityId = request.LegalEntityId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            WarehouseId = request.WarehouseId,
            StoreManagerName = request.StoreManagerName?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            IsActive = request.IsActive,
            CustomFields = request.CustomFields
        };

        _stores.Add(store);
        await _stores.SaveChangesAsync(cancellationToken);
        return Map(store);
    }

    public async Task<RetailStoreDto> Handle(UpdateStoreCommand request, CancellationToken cancellationToken)
    {
        var store = await _stores.GetForUpdateAsync(request.Id, cancellationToken);

        store.Name = request.Name.Trim();
        store.WarehouseId = request.WarehouseId;
        store.StoreManagerName = request.StoreManagerName?.Trim();
        store.Phone = request.Phone?.Trim();
        store.Email = request.Email?.Trim();
        store.Address = request.Address?.Trim();
        store.IsActive = request.IsActive;
        store.CustomFields = request.CustomFields;

        await _stores.SaveChangesAsync(cancellationToken);
        return Map(store);
    }

    public async Task<Unit> Handle(DeleteStoreCommand request, CancellationToken cancellationToken)
    {
        var store = await _stores.GetForUpdateAsync(request.Id, cancellationToken);
        _stores.Remove(store);
        await _stores.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private static RetailStoreDto Map(RetailStore s) => new(
        s.Id,
        s.LegalEntityId,
        s.Code,
        s.Name,
        s.WarehouseId,
        s.StoreManagerName,
        s.Phone,
        s.Email,
        s.Address,
        s.IsActive,
        s.CustomFields,
        s.CreatedAt);
}
