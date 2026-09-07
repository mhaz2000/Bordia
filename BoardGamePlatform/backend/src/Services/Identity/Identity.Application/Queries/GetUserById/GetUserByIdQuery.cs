using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Identity.Application.Dtos;

namespace Identity.Application.Queries.GetUserById;

/// <summary>
/// Query that fetches a user profile by id.
/// </summary>
public record GetUserByIdQuery(Guid UserId) : IQuery<UserProfileDto>;