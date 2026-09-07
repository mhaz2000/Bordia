using AutoMapper;
using Identity.Application.Dtos;
using Identity.Domain.Entities;

namespace Identity.Application.Mapping;

/// <summary>
/// AutoMapper profile mapping identity domain entities to DTOs.
/// </summary>
public class IdentityProfile : Profile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityProfile"/> class.
    /// </summary>
    public IdentityProfile()
    {
        CreateMap<User, UserProfileDto>();
    }
}