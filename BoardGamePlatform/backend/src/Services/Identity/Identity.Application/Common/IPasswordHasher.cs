namespace Identity.Application.Common;

/// <summary>
/// Hashes and verifies user passwords.
/// Implemented in Infrastructure using a slow adaptive hashing algorithm (BCrypt/Argon2).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a plaintext password against a stored hash.
    /// </summary>
    bool Verify(string password, string passwordHash);
}