using BuildingBlocks.Domain.Results;
using MediatR;

namespace BuildingBlocks.Application.CQRS;

/// <summary>
/// Marker interface for commands that do not return a value.
/// </summary>
public interface ICommand : IRequest<Result>
{
}

/// <summary>
/// Marker interface for commands that return a value.
/// </summary>
/// <typeparam name="TResult">The type of the result value.</typeparam>
public interface ICommand<TResult> : IRequest<Result<TResult>>
{
}
