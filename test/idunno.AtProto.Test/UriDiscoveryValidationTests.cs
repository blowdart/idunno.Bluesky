// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class UriDiscoveryValidationTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.0")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.0")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.0")]
    [InlineData("192.168.255.255")]

    public async Task PrivateIpV4AddressesShouldFailValidation(string host)
    {
        var uri = new Uri($"https://{host}");

        Assert.False(await AtProtoAgent.DefaultDiscoveryUriValidator(uri, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LocalhostShouldFailValidation()
    {
        var uri = new Uri("https://localhost");
        Assert.False(await AtProtoAgent.DefaultDiscoveryUriValidator(uri, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LocalhostIPShouldFailValidation()
    {
        var uri = new Uri("https://127.0.0.1");
        Assert.False(await AtProtoAgent.DefaultDiscoveryUriValidator(uri, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("11.0.0.1")]
    [InlineData("172.1.255.255")]
    [InlineData("172.32.0.0")]
    [InlineData("192.167.255.255")]
    [InlineData("192.169.0.0")]
    public async Task ValidIPUriShouldPassValidation(string host)
    {
        var uri = new Uri($"https://{host}");
        Assert.True(await AtProtoAgent.DefaultDiscoveryUriValidator(uri, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("example.org")]
    [InlineData("porcini.us-east.host.bsky.network")]
    [InlineData("chaga.us-west.host.bsky.network/")]
    [InlineData("bsky.social")]
    public async Task ValidDnsEntriesShouldPassValidation(string host)
    {
        var uri = new Uri($"https://{host}");
        Assert.True(await AtProtoAgent.DefaultDiscoveryUriValidator(uri, TestContext.Current.CancellationToken));
    }
}
