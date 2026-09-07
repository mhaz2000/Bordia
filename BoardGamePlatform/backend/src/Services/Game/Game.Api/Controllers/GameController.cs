using Game.Application.Commands.PauseGame;
using Game.Application.Commands.ProcessGameAction;
using Game.Application.Commands.ReconnectPlayer;
using Game.Application.Commands.ResumeGame;
using Game.Application.Dtos;
using Game.Application.Queries.GetGameSession;
using Game.Application.Queries.GetGameState;
using Game.Application.Queries.GetPlayerGameSessions;
using GameEngine.Core;
using GameEngine.Core.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers;

/// <summary>
/// Manages active game sessions and player actions.
/// </summary>
[ApiController]
[Route("api/game")]
[Authorize]
public class GameController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IEnumerable<IGame> _games;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameController"/> class.
    /// </summary>
    public GameController(IMediator mediator, IEnumerable<IGame> games)
    {
        _mediator = mediator;
        _games = games;
    }

    /// <summary>
    /// Lists the games available on the platform with their player ranges.
    /// </summary>
    /// <response code="200">The available games.</response>
    [AllowAnonymous]
    [HttpGet("games")]
    [ProducesResponseType(typeof(List<GameCatalogItemDto>), StatusCodes.Status200OK)]
    public IActionResult Games()
    {
        var games = _games
            .Select(g => new GameCatalogItemDto(g.GameType, g.MinPlayers, g.MaxPlayers))
            .OrderBy(g => g.GameType)
            .ToList();

        return Ok(games);
    }

    /// <summary>
    /// Gets a game session by id.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The session.</response>
    /// <response code="404">Session not found.</response>
    [HttpGet("sessions/{id:guid}")]
    [ProducesResponseType(typeof(GameSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetGameSessionQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Gets the sessions the current user is a player in.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The user's sessions.</response>
    [HttpGet("sessions/mine")]
    [ProducesResponseType(typeof(List<GameSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPlayerGameSessionsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Gets the current state of a game session.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The game state.</response>
    /// <response code="404">Session not found.</response>
    [HttpGet("sessions/{id:guid}/state")]
    [ProducesResponseType(typeof(GameState), StatusCodes.Status200OK)]
    public async Task<IActionResult> State(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetGameStateQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Submits a player action to the game engine.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="command">The action to process.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated game state.</response>
    /// <response code="404">Session not found.</response>
    /// <response code="409">The action was rejected.</response>
    [HttpPost("sessions/{id:guid}/actions")]
    [ProducesResponseType(typeof(GameState), StatusCodes.Status200OK)]
    public async Task<IActionResult> Action(
        Guid id,
        [FromBody] ProcessGameActionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { SessionId = id }, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Records the current user's connection for a session.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="connectionId">The SignalR connection id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The current game state.</response>
    [HttpPost("sessions/{id:guid}/reconnect")]
    [ProducesResponseType(typeof(GameState), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reconnect(
        Guid id,
        [FromBody] ReconnectPlayerRequest connectionId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ReconnectPlayerCommand(id, connectionId.ConnectionId),
            cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Pauses an active game session.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated session.</response>
    [HttpPost("sessions/{id:guid}/pause")]
    [ProducesResponseType(typeof(GameSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pause(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PauseGameCommand(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Resumes a paused game session.
    /// </summary>
    /// <param name="id">The session id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated session.</response>
    [HttpPost("sessions/{id:guid}/resume")]
    [ProducesResponseType(typeof(GameSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resume(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ResumeGameCommand(id), cancellationToken);
        return Ok(result.Value);
    }
}

/// <summary>
/// Payload carrying the SignalR connection id for a reconnect request.
/// </summary>
public record ReconnectPlayerRequest(string ConnectionId);