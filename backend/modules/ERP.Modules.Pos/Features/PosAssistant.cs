using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Domain.Organizations;
using ERP.Modules.Pos.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

internal sealed class RegistersAssistantReader(IReadRepository<PosRegister> registers) : IAssistantReader
{
    public string Key => "pos_registers";
    public string Description => "Point of sale registers: code, name and whether active.";
    public string? ModuleKey => PosModule.Key;
    public string Permission => Permissions.RegistersRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = registers.Query();
        if (search is not null) query = query.Where(r => r.Code.Contains(search) || r.Name.Contains(search));
        return await query.OrderBy(r => r.Code).Take(take).Select(r => new { r.Code, r.Name, r.IsActive, Link = "/pos/registers" }).ToListAsync(ct);
    }
}

internal sealed class PosOrdersAssistantReader(IReadRepository<PosOrder> orders, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "pos_orders";
    public string Description => "Recent counter sales, newest first: receipt number, customer, date, total, returned amount, status and payment status.";
    public string? ModuleKey => PosModule.Key;
    public string Permission => Permissions.OrdersRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = orders.Query();
        if (search is not null) query = query.Where(o => o.Number.Contains(search) || o.CustomerName.Contains(search));
        return await query.OrderByDescending(o => o.CreatedAt).Take(take).Select(o => new { o.Number, Customer = o.CustomerName, o.OrderDate, Currency = entities.Query(null).Where(e => e.Id == o.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), o.TotalAmount, o.ReturnedTotal, Status = o.Status.ToString(), PaymentStatus = o.PaymentStatus.ToString(), Link = "/pos/orders/" + o.Id }).ToListAsync(ct);
    }
}

internal static class PosAssistantHelp
{
    public static readonly IReadOnlyList<AssistantHelpArticle> Articles =
    [
        new("Make a sale at the till", "Go to Point of sale > POS Terminal, choose a register and press Open shift. Add the items to the ticket and press Pay.", "/pos/terminal", Permissions.OrdersCreate),
        new("Close a shift", "On Point of sale > POS Terminal press Close shift, enter the cash you counted and confirm with Close shift. Point of sale > Shifts & Drawers lists past shifts and any cash over or short.", "/pos/terminal", Permissions.OrdersCreate),
        new("Return or void a sale", "Go to Point of sale > Counter Orders and open the sale. Return takes goods back; Void reverses the whole sale and only works while its shift is still open.", "/pos/orders", Permissions.OrdersRead),
        new("Add a register", "Go to Point of sale > Registers, press New register, fill it in and press Save.", "/pos/registers", Permissions.RegistersRead),
    ];
}
