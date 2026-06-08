using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace TaskManager.Api.Auth;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string encoded);
}

// Argon2id with parameters appropriate for an interactive login (OWASP 2024
// guidance: m=64MiB, t=3, p=4 as a starting point, tune to ~0.5s per hash on
// production hardware). Output is a self-describing string that carries its
// parameters so we can upgrade them later without breaking existing users.
public class Argon2idPasswordHasher : IPasswordHasher
{
    private const int MemorySizeKb = 64 * 1024; // 64 MiB
    private const int Iterations = 3;
    private const int DegreeOfParallelism = 4;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt);
        return $"$argon2id$v=19$m={MemorySizeKb},t={Iterations},p={DegreeOfParallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encoded)
    {
        // Parse "$argon2id$v=19$m=...,t=...,p=...$<salt>$<hash>"
        var parts = encoded.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || parts[0] != "argon2id") return false;

        var paramPairs = parts[2].Split(',');
        if (paramPairs.Length != 3) return false;
        if (!TryParseInt(paramPairs[0], "m=", out var m) ||
            !TryParseInt(paramPairs[1], "t=", out var t) ||
            !TryParseInt(paramPairs[2], "p=", out var p)) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException) { return false; }

        var actual = Derive(password, salt, m, t, p, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool TryParseInt(string s, string prefix, out int value)
    {
        value = 0;
        return s.StartsWith(prefix) && int.TryParse(s.AsSpan(prefix.Length), out value);
    }

    private static byte[] Derive(string password, byte[] salt,
        int memoryKb = MemorySizeKb, int iterations = Iterations,
        int parallelism = DegreeOfParallelism, int hashSize = HashSize)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKb,
        };
        return argon.GetBytes(hashSize);
    }
}
