using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using Identity.Application.Dtos;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// Handler for <see cref="GetCurrentUserQuery"/>.
/// </summary>
public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserProfileDto>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCurrentUserQueryHandler"/> class.
    /// </summary>
    public GetCurrentUserQueryHandler(
        IdentityDbContext dbContext,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<UserProfileDto>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.Id == userId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        return Result<UserProfileDto>.Success(_mapper.Map<UserProfileDto>(user));
    }
}