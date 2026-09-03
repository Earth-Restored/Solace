using System.Net;

namespace Solace.IntegrationTests;

public sealed partial class LoginTests
{
    [Test]
    public async Task Login_InlineConnect(CancellationToken cancellationToken)
    {
        using var respones = await _authServerClient.GetAsync("/login.live.com/ppsecure/InlineConnect.srf", cancellationToken);

        await Assert.That(respones.StatusCode).IsEqualTo(HttpStatusCode.OK);

        await Assert.That(await respones.Content.ReadAsStringAsync(cancellationToken)).Contains("""
            href="/login"
            """);
    }
}
