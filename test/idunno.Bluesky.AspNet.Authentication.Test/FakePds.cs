// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Stands in for the PDS and authorization server a sign out revokes credentials at.
/// </summary>
/// <remarks>
/// <para>
///   Revoking credentials is three round trips rather than one. The agent reads the protected resource metadata to
///   find the authorization server, reads the authorization server metadata to find its revocation endpoint, and then
///   posts to that endpoint once for the refresh token and once for the access token. All three are served here so a
///   test can assert on what the handler actually sent rather than on a stub being called.
/// </para>
/// <para>
///   The client this hands out routes every request to this server whatever host it was addressed to, so the metadata
///   can point at the real Bluesky host names the test credentials carry.
/// </para>
/// </remarks>
internal sealed class FakePds : IAsyncDisposable
{
    private const string RevocationPath = "/oauth/revoke";

    private readonly IHost _host;
    private readonly List<string> _requests = [];
    private readonly List<string> _revokedTokenTypeHints = [];
    private readonly object _lock = new();

    private FakePds(IHost host) => _host = host;

    /// <summary>
    /// Every request this server received, as a method and path, in the order they arrived.
    /// </summary>
    internal IReadOnlyList<string> Requests
    {
        get
        {
            lock (_lock)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>
    /// The token type hints the revocation endpoint was posted, in the order they arrived.
    /// </summary>
    internal IReadOnlyList<string> RevokedTokenTypeHints
    {
        get
        {
            lock (_lock)
            {
                return [.. _revokedTokenTypeHints];
            }
        }
    }

    /// <summary>
    /// An <see cref="IHttpClientFactory"/> whose clients reach this server.
    /// </summary>
    internal IHttpClientFactory HttpClientFactory => new FakePdsHttpClientFactory(_host.GetTestServer());

    /// <summary>
    /// Creates a fake PDS.
    /// </summary>
    /// <param name="revocationStatusCode">The status code the revocation endpoint should answer with.</param>
    /// <param name="serveAuthorizationServerMetadata">
    ///   Whether the authorization server should serve its metadata. Set this to <see langword="false"/> to exercise a
    ///   revocation which fails before the handler reaches the revocation endpoint.
    /// </param>
    /// <param name="profile">The handle and DID the profile endpoint should report, if it should serve a profile at all.</param>
    /// <param name="directoryHandle">
    ///   The handle the directory should report the profile's DID as also being known as. Set this to something other
    ///   than the profile handle, or leave it unset, to exercise a handle which does not verify.
    /// </param>
    internal static async Task<FakePds> Create(
        int revocationStatusCode = StatusCodes.Status200OK,
        bool serveAuthorizationServerMetadata = true,
        (string Handle, Did Did)? profile = null,
        string? directoryHandle = null)
    {
        FakePds? pds = null;

        IHostBuilder hostBuilder = new HostBuilder().ConfigureWebHost(webHostBuilder =>
            webHostBuilder
                .UseTestServer()
                .Configure(app => app.Run(async context =>
                {
                    pds!.RecordRequest($"{context.Request.Method} {context.Request.Path}");

                    if (profile is (string profileHandle, Did profileDid))
                    {
                        // The directory lookup is addressed to the PLC directory by DID, and the handle owner's
                        // declaration to the handle's own host, so both are matched on path rather than on host.
                        if (context.Request.Path == $"/{profileDid}")
                        {
                            string alsoKnownAs = directoryHandle is null
                                ? "[]"
                                : $$"""["at://{{directoryHandle}}"]""";

                            await WriteJson(
                                context,
                                $$"""{"id":"{{profileDid}}","alsoKnownAs":{{alsoKnownAs}}}""");
                            return;
                        }

                        if (context.Request.Path == "/.well-known/atproto-did")
                        {
                            context.Response.ContentType = "text/plain";
                            await context.Response.WriteAsync(profileDid.Value);
                            return;
                        }

                        if (context.Request.Path == "/xrpc/app.bsky.actor.getProfile")
                        {
                            await WriteJson(
                                context,
                                $$"""{"did":"{{profileDid}}","handle":"{{profileHandle}}"}""");
                            return;
                        }
                    }

                    switch (context.Request.Path)
                    {
                        case "/.well-known/oauth-protected-resource":
                            await WriteJson(context, $$"""{"authorization_servers":["https://{{context.Request.Host.Host}}/"]}""");
                            break;

                        case "/.well-known/oauth-authorization-server" when serveAuthorizationServerMetadata:
                            await WriteJson(
                                context,
                                $$"""{"revocation_endpoint":"https://{{context.Request.Host.Host}}{{RevocationPath}}"}""");
                            break;

                        case RevocationPath:
                            pds!.RecordRevocation(await ReadTokenTypeHint(context));
                            context.Response.StatusCode = revocationStatusCode;
                            break;

                        default:
                            context.Response.StatusCode = StatusCodes.Status404NotFound;
                            break;
                    }
                })));

        pds = new FakePds(await hostBuilder.StartAsync());

        return pds;
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private void RecordRequest(string request)
    {
        lock (_lock)
        {
            _requests.Add(request);
        }
    }

    private void RecordRevocation(string tokenTypeHint)
    {
        lock (_lock)
        {
            _revokedTokenTypeHints.Add(tokenTypeHint);
        }
    }

    private static async Task<string> ReadTokenTypeHint(HttpContext context)
    {
        IFormCollection form = await context.Request.ReadFormAsync();

        return form["token_type_hint"].ToString();
    }

    private static async Task WriteJson(HttpContext context, string json)
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(json);
    }

    private sealed class FakePdsHttpClientFactory(TestServer testServer) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => testServer.CreateClient();
    }
}
