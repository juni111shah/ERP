using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class CreateRequisitionHandler(
    IPurchaseRequisitionRepository requisitions,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields) : ICommandHandler<CreateRequisitionCommand, RequisitionDto>
{
    public async Task<RequisitionDto> Handle(CreateRequisitionCommand cmd, CancellationToken ct)
    {
        // 1. The header, in a legal entity the caller works in.
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var req = new PurchaseRequisition
        {
            LegalEntityId = entity.Id,
            RequesterName = i.RequesterName.Trim(),
            Department = i.Department?.Trim(),
            RequisitionDate = i.RequisitionDate,
            RequiredDate = i.RequiredDate,
            Reason = i.Reason,
            Status = PurchaseRequisitionStatus.Draft,
        };

        // 2. A free number (the next in the sequence unless one is given).
        req.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.PurchaseRequisition, entity.Id, i.RequisitionDate, ct)
            : i.Number.Trim();

        if (await requisitions.AnyAsync(x => x.Number == req.Number, ct))
            throw new ConflictException($"Requisition number '{req.Number}' already exists.");

        // 3. Lines, totals and custom fields, then save.
        RequisitionLines.PopulateLinesAndTotals(req, i.Lines);
        await fields.ApplyAsync(Docs.PurchaseRequisition, req, i.CustomFields, isCreate: true, ct);

        requisitions.Add(req);
        await requisitions.SaveChangesAsync(ct);
        return await RequisitionDto.FromAsync(req, fields, ct);
    }
}
