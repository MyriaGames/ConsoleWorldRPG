using Myria.ConsoleApp.Services;
using Xunit;

namespace Myria.Console.Tests;

/// <summary>Security review 2026-11-26, A04 F1: the official server never falls back to trust-on-first-use.</summary>
public class CertificateTrustTests
{
    [Fact]
    public void OfficialServer_IsCaOnly() =>
        Assert.Contains("myria.myria-games.duckdns.org", ConsoleHubClient.CaOnlyHosts);

    [Fact]
    public void CaOnlyHost_MatchesTheDefaultServerAddress() =>
        Assert.Contains(new Uri(ConsoleHubClient.DefaultAuthBaseUrl).Host, ConsoleHubClient.CaOnlyHosts);
}
