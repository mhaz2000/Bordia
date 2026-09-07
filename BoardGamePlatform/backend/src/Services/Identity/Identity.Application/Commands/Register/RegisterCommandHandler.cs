using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using BuildingBlocks.Infrastructure.Outbox;
using Identity.Application.Common;
using Identity.Application.Dtos;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Commands.Register;

/// <summary>
/// Handler that registers a new user and returns a token pair.
/// </summary>
public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponseDto>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TokenIssuer _tokenIssuer;
    private readonly IOutbox _outbox;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterCommandHandler"/> class.
    /// </summary>
    public RegisterCommandHandler(
        IdentityDbContext dbContext,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        TokenIssuer tokenIssuer,
        IOutbox outbox,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _outbox = outbox;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<AuthResponseDto>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var emailExists = await _dbContext.Users.AnyAsync(
            u => u.Email == request.Email.ToLowerInvariant(),
            cancellationToken);

        if (emailExists)
        {
            throw new ConflictException($"An account with email '{request.Email}' already exists.");
        }

        var user = User.Create(
            request.Email,
            _passwordHasher.Hash(request.Password),
            request.DisplayName);

        _dbContext.Users.Add(user);

        var response = _tokenIssuer.Issue(user, out var refreshToken);
        response.User = _mapper.Map<UserProfileDto>(user);
        _dbContext.RefreshTokens.Add(refreshToken);

        await _outbox.AddAsync(new UserRegistered
        {
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AuthResponseDto>.Success(response);
    }
}