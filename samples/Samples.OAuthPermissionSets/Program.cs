// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.OAuthCallback;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.Authentication;

using Microsoft.Extensions.Logging;

using Samples.Common;

namespace Samples.OAuthPermissionSets;

public sealed class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var parser = Helpers.ConfigureCommandLine(
            args,
            "BlueskyAgent OAuth Permission Sets Sample",
            PerformOperations);

        return await parser.InvokeAsync();
    }

    static async Task PerformOperations(string? handle, Uri? proxyUri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(handle);

        using ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug);
        using var agent = new BlueskyAgent(
            options: new BlueskyAgentOptions()
            {
                LoggerFactory = loggerFactory,
                HttpClientOptions = new HttpClientOptions()
                {
                    ProxyUri = proxyUri
                },
                OAuthOptions = new OAuthOptions()
                {
                    ClientId = "http://localhost",
                    Scopes = ["atproto"],
                    // These published sets allow creation and deletion, without granting update access.
                    PermissionSets = [BlueskyOAuthPermissionSets.CreatePosts, BlueskyOAuthPermissionSets.DeleteContent]
                }
            });

        Did? did = await agent.ResolveHandle(handle, cancellationToken);
        if (did is null)
        {
            Console.WriteLine("Could not resolve DID.");
            return;
        }

        Uri? pds = await agent.ResolvePds(did, cancellationToken);
        if (pds is null)
        {
            Console.WriteLine($"Could not resolve PDS for {did}.");
            return;
        }

        Uri? authorizationServer = await agent.ResolveAuthorizationServer(handle, cancellationToken);
        if (authorizationServer is null)
        {
            Console.WriteLine($"Could not discover authorization server for {pds}.");
            return;
        }

        Console.WriteLine($"Username:               {handle}");
        Console.WriteLine($"DID:                    {did}");
        Console.WriteLine($"PDS:                    {pds}");
        Console.WriteLine($"Authorization Server:   {authorizationServer}");
        Console.WriteLine("This sample creates a public post, refreshes credentials, then deletes the post.");

        OAuthLoginState oAuthLoginState;
        string callbackData;

        await using (var callbackServer = new CallbackServer(
            CallbackServer.GetRandomUnusedPort(),
            loggerFactory: loggerFactory))
        {
            OAuthClient uriBuilderOAuthClient = agent.CreateOAuthClient();
            Uri startUri = await agent.BuildOAuth2LoginUri(
                oAuthClient: uriBuilderOAuthClient,
                handle: handle,
                returnUri: callbackServer.Uri,
                allowLoopback: true,
                cancellationToken: cancellationToken);

            if (uriBuilderOAuthClient.State is null)
            {
                Console.WriteLine("OAuthClient state is null after building the login URI.");
                return;
            }

            // Restore the saved state when processing the callback, as a web application would.
            oAuthLoginState = uriBuilderOAuthClient.State;
            Console.WriteLine($"Login URI: {startUri}");
            OAuthClient.OpenBrowser(startUri);
            Console.WriteLine($"Awaiting callback on {callbackServer.Uri}");
            callbackData = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrEmpty(callbackData))
        {
            Console.WriteLine("Received no login response.");
            return;
        }

        OAuthClient oAuthClient = agent.CreateOAuthClient(oAuthLoginState);
        if (!await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken) || !agent.IsAuthenticated)
        {
            Console.WriteLine("Could not login with OAuth credentials.");
            return;
        }

        try
        {
            Console.WriteLine($"Credentials issued for: {agent.Credentials.Service}");

            AtProtoHttpResult<CreateRecordResult> createPostResult = await agent.Post(
                "Hello OAuth Permission Sets",
                cancellationToken: cancellationToken);

            if (!createPostResult.Succeeded)
            {
                Console.WriteLine($"Could not create the post: {createPostResult.StatusCode}, {createPostResult.AtErrorDetail}");
                return;
            }

            StrongReference post = createPostResult.Result.StrongReference;
            Console.WriteLine($"Post created: {post.Uri}");
            Console.WriteLine($"Access JWT hash: {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Credentials.AccessJwt)))}");
            Console.WriteLine($"Access JWT expires on: {agent.Credentials.ExpiresOn:G}");

            if (!await agent.RefreshCredentials(cancellationToken: cancellationToken))
            {
                Console.WriteLine($"Could not refresh credentials. The post remains at {post.Uri}; delete it manually.");
                return;
            }

            if (!agent.IsAuthenticated)
            {
                Console.WriteLine($"The session ended during refresh. The post remains at {post.Uri}; delete it manually.");
                return;
            }

            Console.WriteLine($"Refreshed JWT hash: {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Credentials.AccessJwt)))}");
            Console.WriteLine($"Refreshed JWT expires on: {agent.Credentials.ExpiresOn:G}");

            AtProtoHttpResult<DeleteResult> deletePostResult = await agent.DeletePost(post, cancellationToken: cancellationToken);
            if (!deletePostResult.Succeeded)
            {
                Console.WriteLine($"Could not delete the post: {deletePostResult.StatusCode}, {deletePostResult.AtErrorDetail}");
                Console.WriteLine($"The post remains at {post.Uri}; delete it manually.");
                return;
            }

            Console.WriteLine($"Post deleted after refreshing credentials: {post.Uri}");
        }
        finally
        {
            await agent.Logout(cancellationToken: CancellationToken.None);
        }
    }
}
