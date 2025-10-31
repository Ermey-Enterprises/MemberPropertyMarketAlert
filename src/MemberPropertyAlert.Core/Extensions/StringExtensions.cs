using System.Security.Cryptography;
using System.Text;

namespace MemberPropertyAlert.Core.Extensions;

public static class StringExtensions
{
    public static string ComputeSha256(this string value)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
