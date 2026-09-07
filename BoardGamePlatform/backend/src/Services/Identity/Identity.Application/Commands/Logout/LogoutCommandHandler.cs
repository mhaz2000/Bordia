using BuildingBlocks.Application;
using BuildingBlocks.Domain.Results;
using Identity.Application.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Commands.Logout;

/// <summary>
/// Handler that revokes the supplied refresh token.
/// </summary>
public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutCommandHandler"/> class.
    /// </summary>
    public LogoutCommandHandler(
        IdentityDbContext dbContext,
        IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(
        LogoutCommand request,
        CancellationToken cancellationToken)
    {
        var refreshToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(
            t => t.Token == request.RefreshToken && !t.IsDeleted,
            cancellationToken);

        if (refreshToken is not null && refreshToken.RevokedAt is null)
        {
            refreshToken.Revoke();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}