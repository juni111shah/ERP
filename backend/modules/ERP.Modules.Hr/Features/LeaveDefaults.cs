using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record LeaveDefaultsResult(string CountryCode, int Year, IReadOnlyList<string> LeaveTypesAdded, IReadOnlyList<HolidayDto> HolidaysAdded);

/// <summary>Adds the country pack's leave types (by code) and fixed-date public holidays (by date) that are missing. Safe to repeat.</summary>
public sealed record ApplyLeaveCountryDefaults(int Year, Guid? LegalEntityId) : ICommand<LeaveDefaultsResult>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }

internal sealed class LeaveDefaultsHandler(
    IRepository<LeaveType> types, IRepository<PublicHoliday> holidays, IReadRepository<LegalEntity> legalEntities, LeavePolicyService policy,
    CountryPackRegistry packs, ICurrentUser user, IUnitOfWork uow) : IRequestHandler<ApplyLeaveCountryDefaults, LeaveDefaultsResult>
{
    public Task<LeaveDefaultsResult> Handle(ApplyLeaveCountryDefaults cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        if (cmd.Year is < 2000 or > 2100) throw new FieldValidationException("year", "Choose a year between 2000 and 2100.");
        var entityId = cmd.LegalEntityId ?? user.LegalEntityId;
        var entity = entityId is { } id
            ? await legalEntities.FindAsync(id, token) ?? throw new FieldValidationException("legalEntityId", "Legal entity not found.")
            : (await legalEntities.Query().OrderBy(e => e.Code).ToListAsync(token)).FirstOrDefault() ?? throw new ConflictException("Create a legal entity first.");
        var pack = packs.Find(entity.CountryCode);
        var addedTypes = new List<string>(); var addedHolidays = new List<HolidayDto>();
        if (pack is null) return new LeaveDefaultsResult(entity.CountryCode, cmd.Year, addedTypes, addedHolidays);

        var existingCodes = (await types.Query().Select(t => t.Code).ToListAsync(token)).ToHashSet(StringComparer.OrdinalIgnoreCase);
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

        var existingDates = (await holidays.Query().Select(h => h.Date).ToListAsync(token)).ToHashSet();
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
