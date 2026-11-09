using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class CreateBillHandler(IBillRepository bills, BillDrafts drafts, ILegalEntityGuard guard) : ICommandHandler<CreateBillCommand, BillDto>
{
    public async Task<BillDto> Handle(CreateBillCommand command, CancellationToken ct)
    {
        // 1. A draft in the legal entity's currency, numbered DRAFT-... until it is posted.
        var entity = await guard.RequireAsync(command.Input.LegalEntityId, ct);
        var bill = new Bill { LegalEntityId = entity.Id, Status = DocumentStatus.Draft, CurrencyCode = entity.BaseCurrency };
        bill.Number = "DRAFT-" + bill.Id.ToString("N")[^8..];

        // 2. Price the lines from the input and save.
        bill.Lines.AddRange(await drafts.ApplyAsync(bill, command.Input, ct));
        bills.Add(bill);
        await drafts.SaveAsync(bill, ct);
        return BillDto.From(bill);
    }
}
