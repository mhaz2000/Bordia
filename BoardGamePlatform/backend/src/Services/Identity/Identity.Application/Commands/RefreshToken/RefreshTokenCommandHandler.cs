using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Results;
using Identity.Application.Common;
using Identity.Application.Dtos;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Commands.RefreshToken;

/// <summary>
/// Handler that rotates a refresh token and returns a new token pair.
/// </summary>
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TokenIssuer _tokenIssuer;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshTokenCommandHandler"/> class.
    /// </summary>
    public RefreshTokenCommandHandler(
        IdentityDbContext dbContext,
        IUnitOfWork unitOfWork,
        TokenIssuer tokenIssuer,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _tokenIssuer = tokenIssuer;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<AuthResponseDto>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var refreshToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(
            t => t.Token == request.RefreshToken && !t.IsDeleted,
            cancellationToken);

        // Note: tokens are one-time-use. A revoked token means replay — reject regardless.
        if (refreshToken is null || refreshToken.RevokedAt is not null)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        if (!refreshToken.IsActive)
        {
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.Id == refreshToken.UserId,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Account is no longer active.");
        }

        var response = _tokenIssuer.Issue(user, out var newRefreshToken);
        response.User = _mapper.Map<UserProfileDto>(user);

        refreshToken.Revoke(newRefreshToken.Token);
        _dbContext.RefreshTokens.Add(newRefreshToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AuthResponseDto>.Success(response);
    }
}