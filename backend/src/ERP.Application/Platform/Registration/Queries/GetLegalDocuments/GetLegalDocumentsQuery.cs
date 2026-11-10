using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The terms of service and privacy policy, with the company's details filled in.</summary>
public sealed record GetLegalDocumentsQuery : IQuery<LegalDocuments>;
