// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Test;

/// <summary>
/// Tests for the checks applied to a service endpoint read from a DID document.
/// </summary>
/// <remarks>
/// <para>
/// A DID document is served by whoever controls the DID, so the service endpoints it carries are chosen by a third
/// party. Requests to a PDS carry access credentials, so an endpoint which would send them in clear text is rejected.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class ServiceEndpointTests
{
    [Theory]
    [InlineData("https://pds.example.com/")]
    [InlineData("https://pds.example.com:8443/")]
    [InlineData("https://127.0.0.1/")]
    // http is supported against loopback, for testing and development.
    [InlineData("http://localhost:1234/")]
    [InlineData("http://127.0.0.1:1234/")]
    [InlineData("http://[::1]:1234/")]
    public void SupportedServiceEndpointsAreAccepted(string serviceEndpoint)
    {
        Assert.True(Resolution.IsSupportedServiceEndpoint(new Uri(serviceEndpoint)));
    }

    [Theory]
    // Credentials must not be sent to a remote host in clear text.
    [InlineData("http://pds.example.com/")]
    [InlineData("http://169.254.169.254/")]
    [InlineData("http://10.0.0.1/")]
    // Nor should a scheme which is not an HTTP scheme at all be followed.
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/")]
    [InlineData("ws://example.com/")]
    public void UnsupportedServiceEndpointsAreRejected(string serviceEndpoint)
    {
        Assert.False(Resolution.IsSupportedServiceEndpoint(new Uri(serviceEndpoint)));
    }
}
