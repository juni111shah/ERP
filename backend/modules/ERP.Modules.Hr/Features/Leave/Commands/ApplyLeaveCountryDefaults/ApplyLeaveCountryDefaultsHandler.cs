using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Templates;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ApplyLeaveCountryDefaultsHandler(
    ILeaveTypeRepository types, IPublicHolidayRepository holidays, ILegalEntityRepository legalEntities, LeavePolicyService policy,
    CountryPackRegistry packs, ICurrentUser user, IUnitOfWork uow) : ICommandHandler<ApplyLeaveCountryDefaultsCommand, LeaveDefaultsResult>
{
    public Task<LeaveDefaultsResult> Handle(ApplyLeaveCountryDefaultsCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. The entity (the one asked for, the caller's, else the first) and its country pack.
        var cmd = command;
        if (cmd.Year is < 2000 or > 2100) throw new FieldValidationException("year", "Choose a year between 2000 and 2100.");
        var entityId = cmd.LegalEntityId ?? user.LegalEntityId;
        var entity = entityId is { } id
            ? await legalEntities.FindAsync(id, token) ?? throw new FieldValidationException("legalEntityId", "Legal entity not found.")
            : (await legalEntities.ListAllowedAsync(null, token)).FirstOrDefault() ?? throw new ConflictException("Create a legal entity first.");
        var pack = packs.Find(entity.CountryCode);
        var addedTypes = new List<string>(); var addedHolidays = new List<HolidayDto>();
        if (pack is null) return new LeaveDefaultsResult(entity.CountryCode, cmd.Year, addedTypes, addedHolidays);

        // 2. The pack's leave types that are missing (by code).
        var existingCodes = await types.CodesAsync(token);
        foreach (var t in pack.LeaveDefaults)
        {
            var code = t.Code.Trim().ToUpperInvariant();
            if (!existingCodes.Add(code)) continue;
            var type = new LeaveType
            {
                Code = code, Name = t.Name, IsPaid = t.IsPaid, AnnualDays = t.AnnualDays, CarryForwardCap = t.CarryForwardCap,
                RequiresApproval = t.RequiresApproval, RequiresAttachmentAfterDays = t.RequiresAttachmentAfterDays,
            };
            types.Add(type);
            await policy.SaveTiersAsync(type.Id, (t.Tiers ?? []).Select(x => new LeaveTierInput(x.FromServiceYears, x.Days)).ToList(), token);
            addedTypes.Add(code);
        }

        // 3. The pack's public holidays of the year that are missing (by date), and save.
        var existingDates = await holidays.DatesAsync(token);
        foreach (var h in pack.PublicHolidayTemplates)
        {
            var date = h.ForYear(cmd.Year);
            if (!existingDates.Add(date)) continue;
            var holiday = new PublicHoliday { Date = date, Name = h.Name };
            holidays.Add(holiday);
            addedHolidays.Add(new HolidayDto(holiday.Id, date, h.Name));
        }
        await types.SaveChangesAsync(token);
        return new LeaveDefaultsResult(entity.CountryCode, cmd.Year, addedTypes, addedHolidays);
    }, ct);
}
