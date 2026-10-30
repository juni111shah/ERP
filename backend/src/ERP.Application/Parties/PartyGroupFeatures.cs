using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Parties;
using FluentValidation;
using MediatR;

namespace ERP.Application.Parties;

public sealed record PartyGroupInput(string Code, string Name, bool IsActive = true);
public sealed record PartyGroupDto(Guid Id, string Code, string Name, bool IsActive)
{
    public static PartyGroupDto From(PartyGroup g) => new(g.Id, g.Code, g.Name, g.IsActive);
}

public sealed record CreatePartyGroup(PartyGroupInput Input) : ICommand<PartyGroupDto>, IRequirePermission { public string RequiredPermission => CorePermissions.PartyGroupsWrite; }
public sealed record UpdatePartyGroup(Guid Id, PartyGroupInput Input) : ICommand<PartyGroupDto>, IRequirePermission { public string RequiredPermission => CorePermissions.PartyGroupsWrite; }
/// <summary>Soft-deletes a group that no party uses.</summary>
public sealed record DeletePartyGroup(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => CorePermissions.PartyGroupsWrite; }

public sealed class PartyGroupInputValidator : AbstractValidator<PartyGroupInput>
{
    public PartyGroupInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
public sealed class CreatePartyGroupValidator : AbstractValidator<CreatePartyGroup> { public CreatePartyGroupValidator() => RuleFor(x => x.Input).SetValidator(new PartyGroupInputValidator()); }
public sealed class UpdatePartyGroupValidator : AbstractValidator<UpdatePartyGroup> { public UpdatePartyGroupValidator() => RuleFor(x => x.Input).SetValidator(new PartyGroupInputValidator()); }

internal sealed class PartyGroupHandlers(IRepository<PartyGroup> groups, IReadRepository<Party> parties) :
    IRequestHandler<CreatePartyGroup, PartyGroupDto>,
    IRequestHandler<UpdatePartyGroup, PartyGroupDto>,
    IRequestHandler<DeletePartyGroup, Unit>
{
    public async Task<PartyGroupDto> Handle(CreatePartyGroup cmd, CancellationToken ct)
    {
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (await groups.AnyAsync(g => g.Code == code, ct)) throw new ConflictException($"A party group with code '{code}' exists.");
        var group = new PartyGroup { Code = code, Name = cmd.Input.Name.Trim(), IsActive = cmd.Input.IsActive };
        groups.Add(group);
        await groups.SaveChangesAsync(ct);
        return PartyGroupDto.From(group);
    }

    public async Task<PartyGroupDto> Handle(UpdatePartyGroup cmd, CancellationToken ct)
    {
        var group = await groups.GetForUpdateAsync(cmd.Id, ct);
        var code = cmd.Input.Code.Trim().ToUpperInvariant();
        if (code != group.Code && await groups.AnyAsync(g => g.Code == code && g.Id != group.Id, ct)) throw new ConflictException($"A party group with code '{code}' exists.");
        group.Code = code; group.Name = cmd.Input.Name.Trim(); group.IsActive = cmd.Input.IsActive;
        await groups.SaveChangesAsync(ct);
        return PartyGroupDto.From(group);
    }

    public async Task<Unit> Handle(DeletePartyGroup cmd, CancellationToken ct)
    {
        var group = await groups.GetForUpdateAsync(cmd.Id, ct);
        if (await parties.AnyAsync(p => p.PartyGroupId == group.Id, ct)) throw new ConflictException("This group still has parties. Move them to another group first, or deactivate the group.");
        groups.Remove(group);
        await groups.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
