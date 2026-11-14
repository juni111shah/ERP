using ERP.Application.Security;
using ERP.Application.Common;

namespace ERP.Application.Imports;

/// <summary>DryRun checks the file (columns, required values, types, and the rules of every row up to the point of saving) without keeping anything.</summary>
public sealed record RunImportCommand(string Key, string Csv, bool DryRun) : ICommand<ImportResultDto>, IAuthorizesInHandler;
