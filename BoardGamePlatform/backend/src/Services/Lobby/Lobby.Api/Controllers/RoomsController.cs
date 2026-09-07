using Lobby.Application.Commands.CloseRoom;
using Lobby.Application.Commands.CreateRoom;
using Lobby.Application.Commands.JoinRoom;
using Lobby.Application.Commands.KickPlayer;
using Lobby.Application.Commands.LeaveRoom;
using Lobby.Application.Commands.SetReady;
using Lobby.Application.Commands.StartGame;
using Lobby.Application.Commands.TransferHost;
using Lobby.Application.Dtos;
using Lobby.Application.Queries.GetRoom;
using Lobby.Application.Queries.GetRoomList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lobby.Api.Controllers;

/// <summary>
/// Manages lobby rooms and waiting room lifecycle.
/// </summary>
[ApiController]
[Route("api/lobby")]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoomsController"/> class.
    /// </summary>
    public RoomsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new room with the current user as host.
    /// </summary>
    /// <param name="command">Room details.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The created room.</response>
    [HttpPost("rooms")]
    [ProducesResponseType(typeof(LobbyRoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Lists rooms currently open for joining.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The list of open rooms.</response>
    [HttpGet("rooms")]
    [ProducesResponseType(typeof(List<LobbyRoomDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRoomListQuery(), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Gets a single room by id.
    /// </summary>
    /// <param name="id">The room id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The room.</response>
    /// <response code="404">Room not found.</response>
    [HttpGet("rooms/{id:guid}")]
    [ProducesResponseType(typeof(LobbyRoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRoomQuery(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Joins the current user to a room.
    /// </summary>
    /// <param name="id">The room id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated room.</response>
    [HttpPost("rooms/{id:guid}/join")]
    [ProducesResponseType(typeof(LobbyRoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Join(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new JoinRoomCommand(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Removes the current user from a room.
    /// </summary>
    /// <param name="id">The room id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated room.</response>
    [HttpPost("rooms/{id:guid}/leave")]
    [ProducesResponseType(typeof(LobbyRoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Leave(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LeaveRoomCommand(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Marks the current user as ready.
    /// </summary>
    /// <param name="id">The room id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated room.</response>
    [HttpPost("rooms/{id:guid}/ready")]
    [ProducesResponseType(typeof(LobbyRoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ready(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetReadyCommand(id, true), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Marks the current user as not ready.
    /// </summary>
    /// <param name="id">The room id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated room.</response>
    [HttpPost("rooms/{id:guid}/unready")]
    [ProducesResponseType(typeof(LobbyRoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unready(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetReadyCommand(id, false), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Kicks a player from a room (host only).
    /// </summary>
    /// <param name="id">The room id.</param>
    /// <param name="playerId">The player to kick.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated room.</response>
    [HttpPost("rooms/{id:guid}/kick/{playerId:guid}")]
    [ProducesResponseType(typeof(LobbyRoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Kick(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new KickPlayerCommand(id, playerId), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Transfers the host role to another member (host only).
    /// </summary>
    /// <param name="id">The room id.</param>
    /// <param name="playerId">The player to transfer host to.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated room.</response>
    [HttpPost("rooms/{id:guid}/transfer-host/{playerId:guid}")]
    [ProducesResponseType(typeof(LobbyRoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> TransferHost(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new TransferHostCommand(id, playerId), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Starts the game from a waiting room (host only).
    /// </summary>
    /// <param name="id">The room id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The updated room.</response>
    [HttpPost("rooms/{id:guid}/start")]
    [ProducesResponseType(typeof(LobbyRoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new StartGameCommand(id), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Closes a waiting room (host only).
    /// </summary>
    /// <param name="id">The room id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">The closed room.</response>
    [HttpPost("rooms/{id:guid}/close")]
    [ProducesResponseType(typeof(LobbyRoomDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CloseRoomCommand(id), cancellationToken);
        return Ok(result.Value);
    }
}