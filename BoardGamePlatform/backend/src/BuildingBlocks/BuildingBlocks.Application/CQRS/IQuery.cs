using BuildingBlocks.Domain.Results;
using MediatR;

namespace BuildingBlocks.Application.CQRS;

/// <summary>
/// Marker interface for queries that return a value.
/// </summary>
/// <typeparam name="TResult">The type of the result value.</typeparam>
public interface IQuery<TResult> : IRequest<Result<TResult>>
{
}
