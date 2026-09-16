// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.DidPlcDirectory;

namespace idunno.AtProto.Test;

/// <summary>
/// Tests for the restrictions AT Proto places on the <c>did:web</c> method.
/// </summary>
/// <remarks>
/// <para>
/// A DID is chosen by whoever controls the handle or record it was read from, and resolving one causes an outbound
/// request to the host it names, so an unrestricted did:web identifier lets a third party choose the host, port and
/// path this library connects to. See <see href="https://atproto.com/specs/did">the AT Proto DID specification</see>.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class WebDidServiceTests
{
    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("pds.example.com", "https://pds.example.com/")]
    [InlineData("test.invalid", "https://test.invalid/")]
    [InlineData("EXAMPLE.COM", "https://example.com/")]
    // A port is allowed on localhost, for testing and development.
    [InlineData("localhost%3A1234", "https://localhost:1234/")]
    [InlineData("localhost", "https://localhost/")]
    public void SupportedWebDidIdentifiersResolveToTheExpectedService(string identifier, string expectedService)
    {
        Assert.True(DirectoryServer.TryGetWebDidService(identifier, out Uri? service));
        Assert.NotNull(service);
        Assert.Equal(new Uri(expectedService), service);
    }

    [Theory]
    // An IPv4 literal is not a hostname, and would point resolution at a host of the DID author's choosing,
    // including link local addresses such as the cloud metadata endpoint.
    [InlineData("169.254.169.254")]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.1")]
    // A bare name with no dot is an internal host name rather than a public hostname.
    [InlineData("internal-api")]
    [InlineData("metadata")]
    // A port is only allowed on localhost.
    [InlineData("example.com%3A8080")]
    [InlineData("internal-api%3A8080")]
    [InlineData("127.0.0.1%3A8080")]
    // AT Proto does not support path based did:web identifiers.
    [InlineData("example.com:user:alice")]
    [InlineData("example.com:a:b:c:d:e")]
    [InlineData("localhost:3000")]
    // A percent encoded slash would otherwise add a path to the request.
    [InlineData("example.com%2Fpath")]
    // Percent encoding is decoded before the host is validated, so it cannot be used to smuggle in
    // user information, a fragment, a query string or a control character.
    [InlineData("example.com%40evil.invalid")]
    [InlineData("example.com%3Fquery")]
    [InlineData("example.com%23fragment")]
    [InlineData("example.com%0Aevil.invalid")]
    // Neither an empty identifier nor a malformed host is a hostname.
    [InlineData("")]
    [InlineData(".")]
    [InlineData("example.")]
    [InlineData(".example.com")]
    [InlineData("example..com")]
    [InlineData("-example.com")]
    // A port has to be a port number, and port zero is not a port which can be connected to.
    [InlineData("localhost%3A0")]
    [InlineData("localhost%3A99999")]
    [InlineData("localhost%3Aixth")]
    [InlineData("localhost%3A-1")]
    public void UnsupportedWebDidIdentifiersAreRejected(string identifier)
    {
        Assert.False(DirectoryServer.TryGetWebDidService(identifier, out Uri? service));
        Assert.Null(service);
    }

    [Fact]
    public void WebDidIdentifierLongerThanAHostNameIsRejected()
    {
        // Two hundred and fifty three characters is the longest a DNS name can be, but a DID may be far longer.
        string longHostName = string.Concat(Enumerable.Repeat("a23456789.", 30)) + "com";

        Assert.Equal(303, longHostName.Length);
        Assert.False(DirectoryServer.TryGetWebDidService(longHostName, out Uri? service));
        Assert.Null(service);
    }
}
