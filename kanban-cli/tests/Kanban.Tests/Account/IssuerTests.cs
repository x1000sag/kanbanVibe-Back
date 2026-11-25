using Account.Infrastructure;
using Xunit;

namespace Kanban.Tests.Account;

public class IssuerTests
{
    [Fact]
    public void Issue_DoesNotThrow()
    {
        var issuer = new AccessTokenIssuer("test-signing-key-32-bytes-minimum!");
        var token = issuer.Issue(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.Parse("2026-11-01T12:00:00Z"));
        Assert.False(string.IsNullOrEmpty(token));
    }
}