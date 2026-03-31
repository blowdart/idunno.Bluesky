// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Reflection.Metadata;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class SecurityHelpersTests
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

        Assert.False(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CloudMetadataEndpointShouldFailValidation()
    {
        var metadataUri = new Uri("https://169.254.169.254");

        Assert.False(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: metadataUri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("fe80::1")]
    [InlineData("fe80::1ff:fe23:4567:890a")]
    [InlineData("fe80::5710:b5c:4c18:21d6%19")]
    public async Task IpV6LinkLocalAddressesShouldFailValidation(string host)
    {
        var uri = new Uri($"https://[{host}]");
        Assert.False(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LocalhostShouldFailValidation()
    {
        var uri = new Uri("https://localhost");
        Assert.False(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ipv4LoopbackShouldFailValidation()
    {
        var uri = new Uri("https://127.0.0.1");
        Assert.False(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IpV6LoopbackShouldFailValidation()
    {
        var uri = new Uri("https://[::1]");
        Assert.False(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowLoopback: false,
            allowInsecureProtocols: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }


    [Theory]
    [InlineData("11.0.0.1")]
    [InlineData("172.1.255.255")]
    [InlineData("172.32.0.0")]
    [InlineData("192.167.255.255")]
    [InlineData("192.169.0.0")]
    [InlineData("[2601:600:9c00:4b:b53b:b141:2378:66a]")]
    public async Task ValidIPUriShouldPassValidation(string host)
    {
        var uri = new Uri($"https://{host}");
        Assert.True(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("example.org")]
    [InlineData("porcini.us-east.host.bsky.network")]
    [InlineData("chaga.us-west.host.bsky.network/")]
    [InlineData("bsky.social")]
    public async Task ValidDnsEntriesShouldPassValidation(string host)
    {
        var uri = new Uri($"https://{host}");
        Assert.True(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.org")]
    [InlineData("gopher://example.org")]
    [InlineData("ws://example.org")]
    [InlineData("javascript:alert(1)")]
    public async Task NonHttpSchemesShouldFailValidation(string uriString)
    {
        var uri = new Uri(uriString);
        Assert.False(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("100.64.0.0")]     // CGNAT start
    [InlineData("100.127.255.255")] // CGNAT end
    [InlineData("0.0.0.1")]        // "this network"
    [InlineData("198.18.0.0")]     // benchmarking start
    [InlineData("198.19.255.255")] // benchmarking end
    [InlineData("192.0.2.1")]      // TEST-NET-1
    [InlineData("198.51.100.1")]   // TEST-NET-2
    [InlineData("203.0.113.1")]    // TEST-NET-3
    [InlineData("192.0.0.1")]      // IETF protocol assignments
    [InlineData("224.0.0.1")]      // multicast
    [InlineData("240.0.0.1")]      // reserved
    [InlineData("255.255.255.255")] // broadcast
    public async Task AdditionalBlockedIpv4RangesShouldFailValidation(string host)
    {
        var uri = new Uri($"https://{host}");
        Assert.False(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("[2001:db8::1]")]     // documentation prefix
    [InlineData("[2001:db8:ffff::1]")] // documentation prefix end
    public async Task Ipv6DocumentationRangeShouldFailValidation(string host)
    {
        var uri = new Uri($"https://{host}");
        Assert.False(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task KnownPublicIpShouldPassValidation()
    {
        // 1.1.1.1 is Cloudflare's public DNS resolver - a well-known public IP.
        var uri = new Uri("https://1.1.1.1");
        Assert.True(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task KnownPublicIpShouldOnHttpPassValidationIfAllowInsecureProtocolsIsTrue()
    {
        // 1.1.1.1 is Cloudflare's public DNS resolver - a well-known public IP.
        var uri = new Uri("http://1.1.1.1");
        Assert.True(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: true,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task KnownPublicIpShouldOnHttpFailValidationIfAllowInsecureProtocolsIsFalse()
    {
        // 1.1.1.1 is Cloudflare's public DNS resolver - a well-known public IP.
        var uri = new Uri("http://1.1.1.1");
        Assert.False(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: false,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    public async Task LoopbackHttpsShouldBeAllowedIfAllowLoopbackIsTrue(string hostName)
    {
        var uri = new Uri($"https://{hostName}");
        Assert.True(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: false,
            allowLoopback: true,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    public async Task LoopbackHttpShouldBeAllowedIfAllowLoopbackAndAllowInsecureProtocolsIsTrue(string hostName)
    {
        var uri = new Uri($"http://{hostName}");
        Assert.True(await SecurityHelpers.DefaultDiscoveryUriValidator(
            uri: uri,
            allowInsecureProtocols: true,
            allowLoopback: true,
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken));
    }
}
