using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
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
    DateTimeOffset CreatedAt,
    Guid? CardAccountId = null,
    Guid? VarianceAccountId = null,
    Guid? ExpenseAccountId = null);

public record CreateRegisterCommand(
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid? WarehouseId,
    Guid? DefaultCashAccountId,
    bool IsActive = true,
    string? ReceiptHeader = null,
    string? ReceiptFooter = null,
    string? CustomFields = null,
    Guid? CardAccountId = null,
    Guid? VarianceAccountId = null,
    Guid? ExpenseAccountId = null) : ICommand<PosRegisterDto>;

/// <summary>
/// The update replaces the whole record, except the card, variance and expense accounts: left out they keep their value, because older
/// clients do not send them, and an empty GUID clears them.
/// </summary>
public record UpdateRegisterCommand(
    Guid Id,
    string Name,
    Guid? WarehouseId,
    Guid? DefaultCashAccountId,
    bool IsActive,
    string? ReceiptHeader,
    string? ReceiptFooter,
    string? CustomFields,
    Guid? CardAccountId = null,
    Guid? VarianceAccountId = null,
    Guid? ExpenseAccountId = null) : ICommand<PosRegisterDto>;

public record DeleteRegisterCommand(Guid Id) : ICommand<Unit>;

public record GetRegisterByIdQuery(Guid Id) : IQuery<PosRegisterDto?>;

public class CreateRegisterValidator : AbstractValidator<CreateRegisterCommand>
{
    public CreateRegisterValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.ReceiptHeader).MaximumLength(500);
        RuleFor(x => x.ReceiptFooter).MaximumLength(500);
    }
}

public class UpdateRegisterValidator : AbstractValidator<UpdateRegisterCommand>
{
    public UpdateRegisterValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ReceiptHeader).MaximumLength(500);
        RuleFor(x => x.ReceiptFooter).MaximumLength(500);
    }
}

public class RegisterHandlers :
    IRequestHandler<CreateRegisterCommand, PosRegisterDto>,
    IRequestHandler<UpdateRegisterCommand, PosRegisterDto>,
    IRequestHandler<DeleteRegisterCommand, Unit>,
    IRequestHandler<GetRegisterByIdQuery, PosRegisterDto?>
{
    private readonly IRepository<PosRegister> _registers;
    private readonly IRepository<PosSession> _sessions;
    private readonly IReadRepository<Account> _ledgerAccounts;
    private readonly IStockPostingService _stock;

    public RegisterHandlers(IRepository<PosRegister> registers, IRepository<PosSession> sessions, IReadRepository<Account> ledgerAccounts, IStockPostingService stock)
    {
        _registers = registers;
        _sessions = sessions;
        _ledgerAccounts = ledgerAccounts;
        _stock = stock;
    }

    private async Task EnsureNoOpenSessionAsync(Guid registerId, string action, CancellationToken ct)
    {
        if (await _sessions.AnyAsync(s => s.RegisterId == registerId && s.Status == PosSessionStatus.Open, ct))
            throw new ConflictException($"Close the open session before you {action} this register.");
    }

    private async Task EnsureSetupAsync(Guid legalEntityId, Guid? warehouseId, Guid? cash, Guid? card, Guid? variance, Guid? expense, CancellationToken ct)
    {
        if (warehouseId is { } w && !await _stock.IsWarehouseUsableAsync(w, legalEntityId, ct))
            throw new FieldValidationException("warehouseId", "Choose an active warehouse of this legal entity.");
        await EnsureAccountAsync("defaultCashAccountId", cash, ct);
        await EnsureAccountAsync("cardAccountId", card, ct);
        await EnsureAccountAsync("varianceAccountId", variance, ct);
        await EnsureAccountAsync("expenseAccountId", expense, ct);
    }

    /// <summary>Caught here, not at the first sale: a register's own till account is often a plain asset account, so only "can it be posted to" is asked.</summary>
    private async Task EnsureAccountAsync(string field, Guid? id, CancellationToken ct)
    {
        if (id is null || id == Guid.Empty) return;
        var account = await _ledgerAccounts.FindAsync(id.Value, ct);
        if (account is null || !account.IsPostable || !account.IsActive) throw new FieldValidationException(field, "Choose an active account that can be posted to.");
    }

    private static Guid? Merge(Guid? incoming, Guid? current) => incoming is null ? current : incoming == Guid.Empty ? null : incoming;

    public async Task<PosRegisterDto?> Handle(GetRegisterByIdQuery request, CancellationToken cancellationToken)
    {
        var reg = await _registers.FindAsync(request.Id, cancellationToken);
        return reg is null ? null : Map(reg);
    }

    public async Task<PosRegisterDto> Handle(CreateRegisterCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        if (await _registers.AnyAsync(r => r.Code == code, cancellationToken)) throw new ConflictException($"A register with code {code} already exists.");
        await EnsureSetupAsync(request.LegalEntityId, request.WarehouseId, request.DefaultCashAccountId, request.CardAccountId, request.VarianceAccountId, request.ExpenseAccountId, cancellationToken);

        var register = new PosRegister
        {
            LegalEntityId = request.LegalEntityId,
            Code = code,
            Name = request.Name.Trim(),
            WarehouseId = request.WarehouseId,
            DefaultCashAccountId = request.DefaultCashAccountId,
            CardAccountId = Merge(request.CardAccountId, null),
            VarianceAccountId = Merge(request.VarianceAccountId, null),
            ExpenseAccountId = Merge(request.ExpenseAccountId, null),
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
        if (!request.IsActive && register.IsActive) await EnsureNoOpenSessionAsync(register.Id, "deactivate", cancellationToken);
        await EnsureSetupAsync(register.LegalEntityId, request.WarehouseId, request.DefaultCashAccountId, request.CardAccountId, request.VarianceAccountId, request.ExpenseAccountId, cancellationToken);

        register.Name = request.Name.Trim();
        register.WarehouseId = request.WarehouseId;
        register.DefaultCashAccountId = request.DefaultCashAccountId;
        register.CardAccountId = Merge(request.CardAccountId, register.CardAccountId);
        register.VarianceAccountId = Merge(request.VarianceAccountId, register.VarianceAccountId);
        register.ExpenseAccountId = Merge(request.ExpenseAccountId, register.ExpenseAccountId);
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
        await EnsureNoOpenSessionAsync(register.Id, "delete", cancellationToken);
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
        r.CreatedAt,
        r.CardAccountId,
        r.VarianceAccountId,
        r.ExpenseAccountId);
}
