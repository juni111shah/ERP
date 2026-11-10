using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.IndustryPacks.Domain;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class GetPackViewHandler(ICurrentUser user) : IQueryHandler<GetPackViewQuery, PackView>
{
    public Task<PackView> Handle(GetPackViewQuery q, CancellationToken ct)
    {
        var pack = PackCatalog.Require(q.Module);
        var types = pack.Types.Where(t => user.HasPermission(PackModule.ReadPermission(pack.Key, t.Key))).Select(t => new RecordTypeView(
            t.Key, t.Label, t.Singular, t.Icon, VisibleFields(pack.Key, t), t.Statuses, t.PartyRole, t.PartyLabel, t.ParentType, t.ParentLabel, t.HasLines, t.LinesLabel, t.HasAmount, t.HasDueDate,
            t.Billing?.Invoice == true, t.Billing?.Bill == true, t.Billing?.PerLine == true, t.Billing?.WhenStatus, t.ConsumesStock, t.TracksProgress, t.Sensitive, t.TitleLabel)).ToList();
        return Task.FromResult(new PackView(pack.Key, pack.Name, pack.Description, types));
    }

    private IReadOnlyList<RecordFieldDef> VisibleFields(string module, RecordTypeDef def)
        => user.HasPermission(PackModule.SensitivePermission(module, def.Key)) ? def.Fields : def.Fields.Where(f => !f.Sensitive).ToList();
}
