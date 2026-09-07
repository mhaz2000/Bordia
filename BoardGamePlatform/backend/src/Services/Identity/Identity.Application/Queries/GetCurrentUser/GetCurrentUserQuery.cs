using BuildingBlocks.Application.CQRS;
using BuildingBlocks.Domain.Results;
using Identity.Application.Dtos;

namespace Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Query that fetches the profile of the currently authenticated user.
/// </summary>
public record GetCurrentUserQuery : IQuery<UserProfileDto>;