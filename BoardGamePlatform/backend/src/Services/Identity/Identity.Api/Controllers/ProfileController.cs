using Identity.Application.Commands.ChangePassword;
using Identity.Application.Commands.UpdateProfile;
using Identity.Application.Dtos;
using Identity.Application.Queries.GetCurrentUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

/// <summary>
/// Manages the authenticated user's profile.
/// </summary>
[ApiController]
[Route("api/identity")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileController"/> class.
    /// </summary>
    public ProfileController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the profile of the currently authenticated user.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The current user's profile.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Updates the profile of the currently authenticated user.
    /// </summary>
    /// <param name="request">Updated profile fields.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated profile.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateProfileCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Changes the current user's password.
    /// </summary>
    /// <param name="request">Current and new password.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">Password changed.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(request, cancellationToken);
        return Ok();
    }
}