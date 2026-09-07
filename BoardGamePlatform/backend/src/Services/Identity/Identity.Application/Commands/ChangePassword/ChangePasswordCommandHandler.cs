using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using Identity.Application.Common;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Commands.ChangePassword;

/// <summary>
/// Handler that changes the current user's password.
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _passwordHasher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangePasswordCommandHandler"/> class.
    /// </summary>
    public ChangePasswordCommandHandler(
        IdentityDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.Id == userId,
            cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(nameof(User), userId);
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Current password is incorrect.");
        }

        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}