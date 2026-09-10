using BuildingBlocks.Domain.Localization;

namespace BuildingBlocks.Domain.Exceptions;

/// <summary>
/// Base class for domain exceptions that surface to API clients.
/// Carries a stable <see cref="Code"/> plus positional <see cref="Args"/> so the
/// request pipeline can localize the message (Accept-Language) at the response
/// boundary. The exception <see cref="Exception.Message"/> is always the
/// English template — used for logs and non-localized surfaces (e.g. SignalR).
/// Adding a new code requires registering its templates in
/// <see cref="ErrorCatalog"/>; unknown codes fall back to the raw code text.
/// </summary>
public abstract class DomainExceptionBase : Exception
{
    /// <summary>
    /// Stable, dot-separated error code (e.g. "lobby.roomFull").
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Positional arguments for the localized template.
    /// </summary>
    public object?[] Args { get; }

    protected DomainExceptionBase(string code, params object?[] args)
        : base(ErrorCatalog.English(code, args))
    {
        Code = code;
        Args = args;
    }
}
