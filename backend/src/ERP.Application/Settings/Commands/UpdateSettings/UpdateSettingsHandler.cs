using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using MediatR;

namespace ERP.Application.Settings;

internal sealed class UpdateSettingsHandler(ISettingsService settings, IModuleAccess modules, ICurrentUser user, IUnitOfWork unitOfWork) : ICommandHandler<UpdateSettingsCommand>
{
    public async Task<Unit> Handle(UpdateSettingsCommand command, CancellationToken ct)
    {
        foreach (var (key, value) in command.Values)
        {
            // 1. The caller may write this key: core keys need core.settings.write, a module's keys the module's own write permission too.
            var module = key.Split('.')[0];
            if (module != "core")
            {
                if (!await modules.IsEnabledAsync(module, ct)) throw new FieldValidationException(key, "That module is not enabled.");
                if (!user.HasPermission(CorePermissions.SettingsWrite) && !user.HasPermission($"{module}.settings.write")) throw new ForbiddenException();
            }
            else if (!user.HasPermission(CorePermissions.SettingsWrite)) throw new ForbiddenException();

            // 2. The value is valid for the key.
            if (key == ERP.Application.Finance.AccountNumbering.SettingKey && !ERP.Application.Finance.AccountNumbering.IsValidJson(value.GetRawText()))
                throw new FieldValidationException(key, "Invalid numbering mask.");
            await settings.SetRawAsync(key, value.GetRawText(), ct: ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
