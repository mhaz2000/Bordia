using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Domain.Localization;
using Identity.Application.Dtos;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Commands.UpdateProfile;

/// <summary>
/// Handler that updates the current user's profile.
/// </summary>
public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Result<UserProfileDto>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateProfileCommandHandler"/> class.
    /// </summary>
    public UpdateProfileCommandHandler(
        IdentityDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<UserProfileDto>> Handle(
        UpdateProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedException(ErrorCodes.Common.NotAuthenticated);
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.Id == userId,
            cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(nameof(User), userId);
        }

        user.UpdateDisplayName(request.DisplayName);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserProfileDto>.Success(_mapper.Map<UserProfileDto>(user));
    }
}