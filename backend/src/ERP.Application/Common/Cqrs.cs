using MediatR;

namespace ERP.Application.Common;

/// <summary>A request that only reads. Handlers use <see cref="Abstractions.IReadRepository{T}"/> and never save.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;

/// <summary>A request that changes state. Handlers use <see cref="Abstractions.IRepository{T}"/> / <see cref="Abstractions.IUnitOfWork"/>.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>A command with no payload to return (the endpoint answers 204).</summary>
public interface ICommand : ICommand<Unit>;
