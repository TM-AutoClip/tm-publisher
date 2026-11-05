using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace TMPublisher.Platforms.Connections;

/// <summary>PKCE (RFC 7636, S256): a random verifier and its SHA-256 challenge.</summary>
public sealed record Pkce(string Verifier, string Challenge)
{
    /// <summary>A new pair.</summary>
    public static Pkce Create()
    {
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return new Pkce(verifier, ChallengeOf(verifier));
    }

    /// <summary>S256 challenge of a verifier.</summary>
    public static string ChallengeOf(string verifier) => Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>A random state value (protects the return address against forged callbacks).</summary>
    public static string NewState() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(24));
}
