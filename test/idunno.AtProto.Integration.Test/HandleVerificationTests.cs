// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class HandleVerificationTests
{
    private const string Did = "did:plc:identifier";
    private const string OtherDid = "did:plc:someoneelse";

    private static string DidDocumentDeclaring(params string[] alsoKnownAs)
    {
        string entries = string.Join(", ", alsoKnownAs.Select(entry => $"\"{entry}\""));

        return $$"""
            {
                "@context": [ "https://www.w3.org/ns/did/v1" ],
                "id": "{{Did}}",
                "alsoKnownAs": [ {{entries}} ]
            }
            """;
    }

    /// <summary>
    /// Serves a PLC directory document for <see cref="Did"/> and a well known handle resolution response for the
    /// default test domain.
    /// </summary>
    private static TestServer CreateServer(string didDocument, string? wellKnownDid)
    {
        return TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == "plc.directory" && request.Path.Value == $"/{Did}")
            {
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync(didDocument);
                return;
            }

            if (request.Path.Value == "/.well-known/atproto-did" &&
                request.Host.Host == TestServerBuilder.DefaultDomainName &&
                wellKnownDid is not null)
            {
                response.StatusCode = 200;
                response.ContentType = "text/plain";
                await response.WriteAsync(wellKnownDid);
                return;
            }

            response.StatusCode = 404;
        });
    }

    [Fact]
    public async Task VerifyHandleReturnsTrueWhenBothDirectionsAgree()
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);

        using TestServer testServer = CreateServer(DidDocumentDeclaring($"at://{TestServerBuilder.DefaultDomainName}"), Did);
        using HttpClient httpClient = testServer.CreateClient();

        Assert.True(await Resolution.VerifyHandle(
            handle,
            new Did(Did),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifyHandleReturnsFalseWhenTheDidDocumentDoesNotDeclareTheHandle()
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);

        using TestServer testServer = CreateServer(DidDocumentDeclaring("at://someone.else.invalid"), Did);
        using HttpClient httpClient = testServer.CreateClient();

        Assert.False(await Resolution.VerifyHandle(
            handle,
            new Did(Did),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifyHandleReturnsFalseWhenTheHandleResolvesToADifferentDid()
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);

        using TestServer testServer = CreateServer(DidDocumentDeclaring($"at://{TestServerBuilder.DefaultDomainName}"), OtherDid);
        using HttpClient httpClient = testServer.CreateClient();

        Assert.False(await Resolution.VerifyHandle(
            handle,
            new Did(Did),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifyHandleReturnsFalseWhenTheHandleDoesNotResolveAtAll()
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);

        using TestServer testServer = CreateServer(DidDocumentDeclaring($"at://{TestServerBuilder.DefaultDomainName}"), wellKnownDid: null);
        using HttpClient httpClient = testServer.CreateClient();

        Assert.False(await Resolution.VerifyHandle(
            handle,
            new Did(Did),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResolveVerifiedHandleReturnsTheHandleWhenBothDirectionsAgree()
    {
        using TestServer testServer = CreateServer(DidDocumentDeclaring($"at://{TestServerBuilder.DefaultDomainName}"), Did);
        using HttpClient httpClient = testServer.CreateClient();

        Handle handle = await Resolution.ResolveVerifiedHandle(
            new Did(Did),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(handle.IsValid);
        Assert.Equal(TestServerBuilder.DefaultDomainName, handle.Value);
    }

    [Fact]
    public async Task ResolveVerifiedHandleReturnsTheFirstDeclaredHandleWhichResolvesBack()
    {
        using TestServer testServer = CreateServer(
            DidDocumentDeclaring("at://someone.else.invalid", $"at://{TestServerBuilder.DefaultDomainName}"),
            Did);
        using HttpClient httpClient = testServer.CreateClient();

        Handle handle = await Resolution.ResolveVerifiedHandle(
            new Did(Did),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(handle.IsValid);
        Assert.Equal(TestServerBuilder.DefaultDomainName, handle.Value);
    }

    [Fact]
    public async Task ResolveVerifiedHandleReturnsAnInvalidHandleWhenTheDeclaredHandleResolvesElsewhere()
    {
        using TestServer testServer = CreateServer(DidDocumentDeclaring($"at://{TestServerBuilder.DefaultDomainName}"), OtherDid);
        using HttpClient httpClient = testServer.CreateClient();

        Handle handle = await Resolution.ResolveVerifiedHandle(
            new Did(Did),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(handle.IsValid);
        Assert.Equal(Handle.Invalid, handle);
    }

    [Theory]
    [InlineData("https://example.invalid")]
    [InlineData("mailto:someone@example.invalid")]
    [InlineData("at://")]
    public async Task ResolveVerifiedHandleReturnsAnInvalidHandleWhenNoUsableHandleIsDeclared(string alsoKnownAs)
    {
        using TestServer testServer = CreateServer(DidDocumentDeclaring(alsoKnownAs), Did);
        using HttpClient httpClient = testServer.CreateClient();

        Handle handle = await Resolution.ResolveVerifiedHandle(
            new Did(Did),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Handle.Invalid, handle);
    }

    [Theory]
    [InlineData("at://{0}/app.bsky.feed.post/rkey")]
    [InlineData("at://{0}?query=value")]
    [InlineData("at://{0}#fragment")]
    public async Task ResolveVerifiedHandleIgnoresAnythingAfterTheAuthority(string template)
    {
        string alsoKnownAs = string.Format(CultureInfo.InvariantCulture, template, TestServerBuilder.DefaultDomainName);

        using TestServer testServer = CreateServer(DidDocumentDeclaring(alsoKnownAs), Did);
        using HttpClient httpClient = testServer.CreateClient();

        Handle handle = await Resolution.ResolveVerifiedHandle(
            new Did(Did),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(handle.IsValid);
        Assert.Equal(TestServerBuilder.DefaultDomainName, handle.Value);
    }
}
