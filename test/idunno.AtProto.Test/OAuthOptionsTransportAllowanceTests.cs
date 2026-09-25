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

    [Fact]
    public void AnHttpReturnUriAllowsHttpOnTheWireWithoutTheOptionBeingSet()
    {
        OAuthOptions options = new("https://client.test/clientMetadata.json", new Uri("http://client.test/callback"));

        Assert.False(options.AllowInsecureProtocols);
        Assert.True(options.AllowInsecureProtocolsOnTheWire);
        Assert.False(options.AllowLoopbackOnTheWire);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5000/callback")]
    [InlineData("https://localhost:5001/callback")]
    [InlineData("http://[::1]:5000/callback")]
    public void ALoopbackReturnUriAllowsLoopbackOnTheWireWithoutTheOptionBeingSet(string returnUri)
    {
        OAuthOptions options = new("https://client.test/clientMetadata.json", new Uri(returnUri));

        Assert.False(options.AllowLoopback);
        Assert.True(options.AllowLoopbackOnTheWire);
    }
}
