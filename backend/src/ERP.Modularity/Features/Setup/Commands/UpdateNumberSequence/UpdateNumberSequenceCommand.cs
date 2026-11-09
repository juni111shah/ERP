using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modularity.Features;

/// <summary>Changes the prefix, pattern and yearly reset of a document type's numbering (every year and legal entity). The next value is kept.</summary>
public sealed record UpdateNumberSequenceCommand(string DocumentType, NumberSequenceInput Input) : ICommand<NumberSequenceView>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
