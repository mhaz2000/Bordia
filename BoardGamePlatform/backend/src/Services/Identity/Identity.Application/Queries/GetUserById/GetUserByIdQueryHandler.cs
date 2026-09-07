using AutoMapper;
using BuildingBlocks.Domain.Exceptions;
using BuildingBlocks.Domain.Results;
using Identity.Application.Dtos;
using Identity.Application.Persistence;
using Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Identity.Application.Queries.GetUserById;

/// <summary>
/// Handler for <see cref="GetUserByIdQuery"/>.
/// </summary>
public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserProfileDto>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserByIdQueryHandler"/> class.
    /// </summary>
    public GetUserByIdQueryHandler(
        IdentityDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Result<UserProfileDto>> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.Id == request.UserId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        return Result<UserProfileDto>.Success(_mapper.Map<UserProfileDto>(user));
    }
}