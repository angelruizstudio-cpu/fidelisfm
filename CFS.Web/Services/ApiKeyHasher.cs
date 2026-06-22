using System.Security.Cryptography;
using System.Text;

namespace CFS.Web.Services;

public static class ApiKeyHasher
{
    public static string GenerateApiKey() => $"cfs_live_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";

    public static string Hash(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey))).ToLowerInvariant();
}
