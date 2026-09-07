using Game.Application.Commands.CreateGameSession;
using Game.Application.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers;

/// <summary>
/// Creates game sessions. Invoked by the Lobby service when a room starts a game.
/// </summary>
[ApiController]
[Route("api/game")]
[AllowAnonymous]
public class SessionsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionsController"/> class.
    /// </summary>
    public SessionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new game session from a waiting room.
    /// </summary>
    /// <param name="command">The session details.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The created session.</response>
    /// <response code="400">The session is invalid.</response>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(GameSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        [FromBody] CreateGameSessionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result.Value);
    }
}