using ERP.Application.Security;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>A request about the records of one type of one pack; its permission (read, manage) depends on the type.</summary>
public abstract record RecordRequest(string Module, string Type) : IRequirePermission
{
    public abstract string RequiredPermission { get; }
}
