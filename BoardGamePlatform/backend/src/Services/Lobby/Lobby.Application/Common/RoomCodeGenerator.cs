using System.Security.Cryptography;

namespace Lobby.Application.Common;

/// <summary>
/// Generates short, human-friendly, unique room codes.
/// </summary>
public static class RoomCodeGenerator
{
    // No I, O, 0, 1 to avoid confusion between letters and digits.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int Length = 6;

    /// <summary>
    /// Generates a random 6-character room code.
    /// </summary>
    public static string Generate() =>
        string.Concat(Enumerable.Range(0, Length)
            .Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]));
}