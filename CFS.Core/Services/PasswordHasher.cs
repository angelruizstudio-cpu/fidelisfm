using System.Security.Cryptography;

namespace CFS.Core.Services;

/// <summary>
/// Single source of truth for password hashing (PBKDF2-SHA256).
///
/// The iteration count is <b>stored alongside every hash</b> so existing passwords keep verifying
/// with the count they were created under, while new or updated passwords use the current
/// (stronger) count. This lets us raise the cost over time without needing the plaintext to rehash.
///
/// Never lower <see cref="CurrentIterations"/>: doing so would weaken every newly-set password and
/// break the <see cref="NeedsRehash"/> upgrade path.
/// </summary>
public static class PasswordHasher
{
    /// <summary>OWASP-recommended iteration count for PBKDF2-SHA256.</summary>
    public const int CurrentIterations = 600_000;

    /// <summary>Iteration count used before versioning existed; the value backfilled by migration.</summary>
    public const int LegacyIterations = 100_000;

    private const int SaltSize = 16;
    private const int HashSize = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Hashes a new password at <see cref="CurrentIterations"/>. Returns the random salt, the hash,
    /// and the iteration count used — all three must be persisted together.
    /// </summary>
    public static (byte[] Salt, byte[] Hash, int Iterations) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, CurrentIterations, Algorithm, HashSize);
        return (salt, hash, CurrentIterations);
    }

    /// <summary>
    /// Verifies a password against a stored hash using the <b>stored</b> iteration count (never the
    /// current one), in constant time. Returns false for missing salt/hash or a non-positive count.
    /// </summary>
    public static bool Verify(string password, byte[]? salt, byte[]? hash, int iterations)
    {
        if (salt is not { Length: > 0 } || hash is not { Length: > 0 } || iterations <= 0)
        {
            return false;
        }

        var calculated = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, hash.Length);
        return CryptographicOperations.FixedTimeEquals(calculated, hash);
    }

    /// <summary>True when a stored hash was created with fewer iterations than we now require.</summary>
    public static bool NeedsRehash(int iterations) => iterations < CurrentIterations;
}
