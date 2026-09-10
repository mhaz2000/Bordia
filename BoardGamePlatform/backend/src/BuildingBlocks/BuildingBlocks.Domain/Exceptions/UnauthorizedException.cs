namespace BuildingBlocks.Domain.Exceptions;

/// <summary>
/// Exception thrown when an authenticated caller lacks permission for an
/// operation (host-only actions, non-members, etc.) or when credentials fail.
/// Localized at the request boundary via the error code.
/// </summary>
public class UnauthorizedException : DomainExceptionBase
{
    /// <summary>
    /// Initializes a new <see cref="UnauthorizedException"/> with an error code
    /// and optional positional arguments.
    /// </summary>
    public UnauthorizedException(string code, params object?[] args)
        : base(code, args)
    {
    }
}
