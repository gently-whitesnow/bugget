using System.Security.Cryptography;
using System.Text;

namespace Users.BO.TeamInvites;

public static class InviteCryptoHelper
{
    public static string NewTokenRaw()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'); // b64url
    }

    public static byte[] HashToken(string raw, byte[] pepper)
    {
        using var h = new HMACSHA256(pepper);
        return h.ComputeHash(Encoding.UTF8.GetBytes(raw));
    }
}
