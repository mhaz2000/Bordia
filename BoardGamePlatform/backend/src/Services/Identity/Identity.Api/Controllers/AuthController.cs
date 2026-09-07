using Identity.Application.Commands.Login;
using Identity.Application.Commands.Logout;
using Identity.Application.Commands.RefreshToken;
using Identity.Application.Commands.Register;
using Identity.Application.Dtos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

/// <summary>
/// Handles registration and authentication flows: register, login, refresh, logout.
/// </summary>
[ApiController]
[Route("api/identity")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registers a new user account and returns a token pair.
    /// </summary>
    /// <param name="request">Registration details.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">Registration succeeded; a token pair is returned.</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Authenticates a user and returns a token pair.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">Login succeeded; a token pair is returned.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Rotates an expired access token using the refresh token.
    /// </summary>
    /// <param name="request">The refresh token to redeem.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">A new token pair is returned.</response>
    /// <response code="401">The refresh token is invalid or expired.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Revokes the supplied refresh token, ending the session.
    /// </summary>
    /// <param name="request">The refresh token to revoke.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">Logout succeeded.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutCommand request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(request, cancellationToken);
        return Ok();
    }
}