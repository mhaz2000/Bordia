namespace BuildingBlocks.Application;

/// <summary>
/// Provides access to the currently authenticated user's context.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// The id of the current user, or null if unauthenticated.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// The email of the current user, or null if unauthenticated.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// The display name of the current user, or null if unauthenticated.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// Indicates whether the current request is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}
