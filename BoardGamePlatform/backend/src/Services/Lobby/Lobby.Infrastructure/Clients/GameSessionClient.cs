using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Lobby.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lobby.Infrastructure.Clients;

/// <summary>
/// Configuration for the GameSessionClient.
/// </summary>
public class GameServiceOptions
{
    /// <summary>
    /// Base URL of the Game service API.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// HTTP client that creates game sessions in the Game service.
/// </summary>
public class GameSessionClient : IGameSessionClient
{
    private readonly HttpClient _httpClient;
    private readonly GameServiceOptions _options;
    private readonly ILogger<GameSessionClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameSessionClient"/> class.
    /// </summary>
    public GameSessionClient(
        HttpClient httpClient,
        IOptions<GameServiceOptions> options,
        ILogger<GameSessionClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateGameSessionResult> CreateAsync(
        Guid roomId,
        string gameType,
        IReadOnlyList<GamePlayerRequest> players,
        CancellationToken cancellationToken)
    {
        var request = new CreateGameSessionRequest(roomId, gameType, players
            .Select(p => new CreateGamePlayerRequest(p.UserId, p.DisplayName))
            .ToList());

        var response = await _httpClient.PostAsJsonAsync(
            $"{_options.BaseUrl}/api/game/sessions",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Game service rejected session creation: {Status} {Body}",
                response.StatusCode,
                body);

            throw new HttpRequestException(
                $"Game service returned {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateGameSessionResponse>(
            cancellationToken: cancellationToken);

        return new CreateGameSessionResult(result!.Id);
    }
}

/// <summary>
/// Request payload for creating a game session.
/// </summary>
public class CreateGameSessionRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateGameSessionRequest"/> class.
    /// </summary>
    public CreateGameSessionRequest(
        Guid roomId,
        string gameType,
        IReadOnlyList<CreateGamePlayerRequest> players)
    {
        RoomId = roomId;
        GameType = gameType;
        Players = players;
    }

    /// <summary>
    /// The room id the session starts from.
    /// </summary>
    public Guid RoomId { get; }

    /// <summary>
    /// The game type to play.
    /// </summary>
    public string GameType { get; }

    /// <summary>
    /// The players joining the session.
    /// </summary>
    public IReadOnlyList<CreateGamePlayerRequest> Players { get; }
}

/// <summary>
/// A player in a create-session request.
/// </summary>
public class CreateGamePlayerRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateGamePlayerRequest"/> class.
    /// </summary>
    public CreateGamePlayerRequest(Guid userId, string displayName)
    {
        UserId = userId;
        DisplayName = displayName;
    }

    /// <summary>
    /// The user id.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The display name.
    /// </summary>
    public string DisplayName { get; }
}

/// <summary>
/// Response payload containing the created session id.
/// The Game service returns its <c>GameSessionDto</c>, whose id serializes as "id".
/// </summary>
public class CreateGameSessionResponse
{
    /// <summary>
    /// The created game session id.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
}