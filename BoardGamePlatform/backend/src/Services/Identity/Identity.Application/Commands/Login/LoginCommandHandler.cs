using AutoMapper;
using BuildingBlocks.Application;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using Identity.Application.Common;
using Identity.Application.Dtos;
using Identity.Application.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Commands.Login;

/// <summary>
/// Handler that authenticates a user and returns a token pair.
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TokenIssuer _tokenIssuer;
    private readonly ILogger<LoginCommandHandler> _logger;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginCommandHandler"/> class.
    /// </summary>
    public LoginCommandHandler(
        IdentityDbContext dbContext,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        TokenIssuer tokenIssuer,
        ILogger<LoginCommandHandler> logger,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _logger = logger;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<AuthResponseDto>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();

        var user = identifier.Contains('@')
            ? await _dbContext.Users.FirstOrDefaultAsync(
                u => u.Email == identifier.ToLowerInvariant(),
                cancellationToken)
            : await _dbContext.Users.FirstOrDefaultAsync(
                u => u.DisplayName.ToLower() == identifier.ToLower(),
                cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning(
                "Failed login attempt for {Identifier}",
                identifier);

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("This account has been deactivated.");
        }

        user.RecordLogin();

        var response = _tokenIssuer.Issue(user, out var refreshToken);
        response.User = _mapper.Map<UserProfileDto>(user);
        _dbContext.RefreshTokens.Add(refreshToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AuthResponseDto>.Success(response);
    }
}