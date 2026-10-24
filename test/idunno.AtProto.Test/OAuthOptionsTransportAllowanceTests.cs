// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class OAuthOptionsTransportAllowanceTests
{
    [Fact]
    public void InsecureProtocolsAndLoopbackAreNotAllowedByDefault()
    {
        OAuthOptions options = new("https://client.test/clientMetadata.json", new Uri("https://client.test/callback"));

        Assert.False(options.AllowInsecureProtocols);
        Assert.False(options.AllowLoopback);
        Assert.False(options.AllowInsecureProtocolsOnTheWire);
        Assert.False(options.AllowLoopbackOnTheWire);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://client.test/callback")]
    [InlineData("https://127.0.0.1/callback")]
    public void AllowInsecureProtocolsAllowsHttpOnTheWireWhateverTheReturnUriIs(string? returnUri)
    {
        OAuthOptions options = new("https://client.test/clientMetadata.json", returnUri is null ? null : new Uri(returnUri))
        {
            AllowInsecureProtocols = true
        };

        Assert.True(options.AllowInsecureProtocolsOnTheWire);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://client.test/callback")]
    [InlineData("http://client.test/callback")]
    public void AllowLoopbackAllowsLoopbackOnTheWireWhateverTheReturnUriIs(string? returnUri)
    {
        OAuthOptions options = new("https://client.test/clientMetadata.json", returnUri is null ? null : new Uri(returnUri))
        {
            AllowLoopback = true
        };

        Assert.True(options.AllowLoopbackOnTheWire);
    }

    [Theory]
    [InlineData("http://client.test/callback")]
    [InlineData("http://127.0.0.1/callback")]
    [InlineData("http://127.0.0.1:5000/callback")]
    [InlineData("http://[::1]/callback")]
    [InlineData("http://[::1]:5000/callback")]
    [InlineData("http://localhost:5000/callback")]
    [InlineData("https://localhost:5001/callback")]
    public void ProductionCallbacksDoNotGrantRemoteTransportAllowances(string returnUri)
    {
        OAuthOptions options = new("https://client.test/clientMetadata.json", new Uri(returnUri));

        Assert.False(options.AllowInsecureProtocols);
        Assert.False(options.AllowLoopback);
        Assert.False(options.AllowInsecureProtocolsOnTheWire);
        Assert.False(options.AllowLoopbackOnTheWire);
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData("https://client.test/callback", false, false)]
    [InlineData("http://client.test/callback", true, false)]
    [InlineData("http://127.0.0.1:5000/callback", true, true)]
    [InlineData("https://localhost:5001/callback", false, true)]
    [InlineData("http://[::1]:5000/callback", true, true)]
    public void LocalhostDevelopmentClientRetainsCallbackTransportAllowances(string? returnUri, bool allowHttp, bool allowLoopback)
    {
        OAuthOptions options = new("http://localhost?scope=atproto", returnUri is null ? null : new Uri(returnUri));

        Assert.False(options.AllowInsecureProtocols);
        Assert.False(options.AllowLoopback);
        Assert.Equal(allowHttp, options.AllowInsecureProtocolsOnTheWire);
        Assert.Equal(allowLoopback, options.AllowLoopbackOnTheWire);
    }

    [Theory]
    [InlineData("https://localhost")]
    [InlineData("http://localhost.example")]
    [InlineData("http://localhost:8080")]
    [InlineData("http://localhost/metadata")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://[::1]")]
    [InlineData("http://user:password@localhost")]
    [InlineData("http://localhost#fragment")]
    [InlineData("invalid")]
    public void OnlyTheLocalhostDevelopmentClientGetsImplicitTransportAllowances(string clientId)
    {
        OAuthOptions options = new(clientId, new Uri("http://127.0.0.1/callback"));

        Assert.False(options.AllowInsecureProtocolsOnTheWire);
        Assert.False(options.AllowLoopbackOnTheWire);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void NativeCallbacksKeepExplicitTransportFlagsIndependent(bool allowHttp, bool allowLoopback)
    {
        OAuthOptions options = new("https://client.test/clientMetadata.json", new Uri("http://127.0.0.1/callback"))
        {
            AllowInsecureProtocols = allowHttp,
            AllowLoopback = allowLoopback
        };

        Assert.Equal(allowHttp, options.AllowInsecureProtocolsOnTheWire);
        Assert.Equal(allowLoopback, options.AllowLoopbackOnTheWire);
    }

    [Fact]
    public void SwitchingFromDevelopmentToProductionClientDoesNotRetainImplicitAllowances()
    {
        OAuthOptions options = new("http://localhost", new Uri("http://127.0.0.1/callback"));
        Assert.True(options.AllowInsecureProtocolsOnTheWire);
        Assert.True(options.AllowLoopbackOnTheWire);

        options.ClientId = "https://client.test/clientMetadata.json";

        Assert.False(options.AllowInsecureProtocolsOnTheWire);
        Assert.False(options.AllowLoopbackOnTheWire);
    }
}
