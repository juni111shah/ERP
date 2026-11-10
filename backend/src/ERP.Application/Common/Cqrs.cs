using MediatR;

namespace ERP.Application.Common;

// CQRS building blocks. Every HTTP action sends exactly one of these through MediatR:
//
//   Controller ──► Command / Query ──► its Handler ──► Repository ──► Database
//
// A command changes state (create, update, issue, void...). A query only reads. Each request has exactly one handler
// class, named after it (CreateInvoiceCommand → CreateInvoiceHandler), in the same folder, next to its validator.

/// <summary>A request that only reads. Its handler uses repositories to read and never saves.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;

/// <summary>A request that changes state. Its handler loads through repositories, applies the rules and saves.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>A command with nothing to return (the controller usually answers 204 No Content).</summary>
public interface ICommand : ICommand<Unit>;

/// <summary>Handles one query: <c>internal sealed class GetInvoiceByIdHandler : IQueryHandler&lt;GetInvoiceByIdQuery, InvoiceDto&gt;</c>.</summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>;

/// <summary>Handles one command that returns a value: <c>IssueInvoiceHandler : ICommandHandler&lt;IssueInvoiceCommand, InvoiceDto&gt;</c>.</summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>;

/// <summary>Handles one command with nothing to return; <c>Handle</c> returns <see cref="Unit.Value"/>.</summary>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Unit> where TCommand : ICommand<Unit>;
