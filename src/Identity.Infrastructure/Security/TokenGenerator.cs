using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Identity.Application.Abstractions.Security;

namespace Identity.Infrastructure.Security;

public sealed class TokenGenerator : ITokenGenerator
{
    public string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
    public string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
