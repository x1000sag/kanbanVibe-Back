using Microsoft.AspNetCore.Identity;

namespace Account.Infrastructure;

public sealed class AccountPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    // Учебный хэш для отсутствующего ника — пароль "timing-pad-value", не принадлежит учётке.
    private const string DummyHash =
        "AQAAAAIAAYagAAAAENVJWOTGYUopNC2yZM3JE625TRkO76DJ8sdJD/UQtUY/wfNwSIVchBRuOPx5DeQyhg==";

    public string Hash(string password) => _hasher.HashPassword(new object(), password);

    public bool Verify(string password, string hash) =>
        _hasher.VerifyHashedPassword(new object(), hash, password) != PasswordVerificationResult.Failed;

    public void VerifyDummy(string password)
    {
        _ = _hasher.VerifyHashedPassword(new object(), DummyHash, password);
    }
}