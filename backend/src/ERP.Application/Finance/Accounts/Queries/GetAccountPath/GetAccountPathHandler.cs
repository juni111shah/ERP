using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetAccountPathHandler(IAccountRepository accounts, IAccountSubcategoryRules rules) : IQueryHandler<GetAccountPathQuery, IReadOnlyList<AccountNodeDto>>
{
    public async Task<IReadOnlyList<AccountNodeDto>> Handle(GetAccountPathQuery q, CancellationToken ct)
    {
        // Walk up from the account to its top-level ancestor (a loop in the data stops the walk).
        var chain = new List<AccountNodeDto>();
        var seen = new HashSet<Guid>();
        for (Guid? current = q.Id; current is { } id && seen.Add(id);)
        {
            var node = await accounts.FindNodeAsync(id, ct);
            if (node is null)
            {
                if (chain.Count == 0) throw new NotFoundException("The account was not found.");
                break;
            }
            chain.Insert(0, node with { Subcategory = rules.Classify(node.Type, node.Code, node.ReportingGroup, node.SystemRole) });
            current = node.ParentId;
        }
        return chain;
    }
}
