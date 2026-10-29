using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using FluentValidation;
using MediatR;

namespace ERP.Modules.Pos.Features;

public record PosRegisterDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid? WarehouseId,
    Guid? DefaultCashAccountId,
    bool IsActive,
    string? ReceiptHeader,
    string? ReceiptFooter,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record CreateRegisterCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid? WarehouseId,
    Guid? DefaultCashAccountId,
    bool IsActive,
    string? ReceiptHeader,
    string? ReceiptFooter,
    string? CustomFields) : ICommand<PosRegisterDto>;

public record UpdateRegisterCommand(
    Guid Id,
    string Name,
    Guid? WarehouseId,
    Guid? DefaultCashAccountId,
    bool IsActive,
    string? ReceiptHeader,
    string? ReceiptFooter,
    string? CustomFields) : ICommand<PosRegisterDto>;

public record DeleteRegisterCommand(Guid Id) : ICommand<Unit>;

public record GetRegisterByIdQuery(Guid Id) : IQuery<PosRegisterDto?>;

public class CreateRegisterValidator : AbstractValidator<CreateRegisterCommand>
{
    public CreateRegisterValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LegalEntityId).NotEmpty();
    }
}

public class UpdateRegisterValidator : AbstractValidator<UpdateRegisterCommand>
{
    public UpdateRegisterValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class RegisterHandlers :
    IRequestHandler<CreateRegisterCommand, PosRegisterDto>,
    IRequestHandler<UpdateRegisterCommand, PosRegisterDto>,
    IRequestHandler<DeleteRegisterCommand, Unit>,
    IRequestHandler<GetRegisterByIdQuery, PosRegisterDto?>
{
    private readonly IRepository<PosRegister> _registers;

    public RegisterHandlers(IRepository<PosRegister> registers)
    {
        _registers = registers;
    }

    public async Task<PosRegisterDto?> Handle(GetRegisterByIdQuery request, CancellationToken cancellationToken)
    {
        var reg = await _registers.FindAsync(request.Id, cancellationToken);
        return reg is null ? null : Map(reg);
    }

    public async Task<PosRegisterDto> Handle(CreateRegisterCommand request, CancellationToken cancellationToken)
    {
        var register = new PosRegister
        {
            LegalEntityId = request.LegalEntityId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            WarehouseId = request.WarehouseId,
            DefaultCashAccountId = request.DefaultCashAccountId,
            IsActive = request.IsActive,
            ReceiptHeader = request.ReceiptHeader,
            ReceiptFooter = request.ReceiptFooter,
            CustomFields = request.CustomFields
        };

        _registers.Add(register);
        await _registers.SaveChangesAsync(cancellationToken);
        return Map(register);
    }

    public async Task<PosRegisterDto> Handle(UpdateRegisterCommand request, CancellationToken cancellationToken)
    {
        var register = await _registers.GetForUpdateAsync(request.Id, cancellationToken);

        register.Name = request.Name.Trim();
        register.WarehouseId = request.WarehouseId;
        register.DefaultCashAccountId = request.DefaultCashAccountId;
        register.IsActive = request.IsActive;
        register.ReceiptHeader = request.ReceiptHeader;
        register.ReceiptFooter = request.ReceiptFooter;
        register.CustomFields = request.CustomFields;

        await _registers.SaveChangesAsync(cancellationToken);
        return Map(register);
    }

    public async Task<Unit> Handle(DeleteRegisterCommand request, CancellationToken cancellationToken)
    {
        var register = await _registers.GetForUpdateAsync(request.Id, cancellationToken);
        _registers.Remove(register);
        await _registers.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private static PosRegisterDto Map(PosRegister r) => new(
        r.Id,
        r.LegalEntityId,
        r.Code,
        r.Name,
        r.WarehouseId,
        r.DefaultCashAccountId,
        r.IsActive,
        r.ReceiptHeader,
        r.ReceiptFooter,
        r.CustomFields,
        r.CreatedAt);
}
