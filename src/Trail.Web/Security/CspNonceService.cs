using System.Security.Cryptography;

namespace Trail.Web.Security;

public sealed class CspNonceService : ICspNonceService
{
    private string? _nonce;

    public string GetNonce()
    {
        return _nonce ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    }
}
