using Identity.Application.Common;

namespace Identity.Infrastructure.Security;

/// <summary>
/// BCrypt-based password hasher.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    /// <inheritdoc />
    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password);

    /// <inheritdoc />
    public bool Verify(string password, string passwordHash)
        => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}