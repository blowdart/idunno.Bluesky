// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.OAuthCallback;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.Authentication;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Record;

using Microsoft.Extensions.Logging;

using Samples.Common;

namespace Samples.ProgressiveOAuth;

internal static class Program
{
    /// <summary>
    /// Gets the identity and granular timeline scopes used for the initial read-only authorization.
    /// </summary>
    /// <remarks>
    /// <para>The RPC scope grants access only to the authenticated timeline on Bluesky's app view,
    /// rather than the broader content, notification, and preference access granted by the ViewAll Permission Set.</para>
    /// </remarks>
    static string[] InitialScopes =>
        ["atproto", "rpc:app.bsky.feed.getTimeline?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview"];

    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var parser = Helpers.ConfigureCommandLine(
            args,
            "BlueskyAgent Progressive OAuth Permission Sets Sample",
            PerformOperations);

        return await parser.InvokeAsync();
    }

    static async Task PerformOperations(string? handle, Uri? proxyUri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(handle);

        using ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Information);
        BlueskyAgentOptions options = new()
        {
            LoggerFactory = loggerFactory,
            HttpClientOptions = new HttpClientOptions()
            {
                ProxyUri = proxyUri
            },
            OAuthOptions = CreateOptions()
        };
        using var agent = new BlueskyAgent(options: options);

        Did? did = await agent.ResolveHandle(handle, cancellationToken);
        if (did is null)
        {
            Console.Error.WriteLine("Could not resolve DID.");
            return;
        }

        Console.WriteLine("This sample reads your timeline, attempts a public post with read-only permissions,");
        Console.WriteLine("then requests create/delete Permission Sets, retries the same post, and deletes it.");
        Console.WriteLine("Two browser consent flows are required. No transition scopes are requested.");

        StrongReference? createdPost = null;
        string[] initialScopes = InitialScopes;
        RecordKey recordKey = TimestampIdentifier.Next();
        AtUri postUri = new($"at://{did}/{CollectionNsid.Post}/{recordKey}");
        Post post = new("Hello OAuth Permission Sets");

        try
        {
            if (!await Authorize(agent, handle, initialScopes, did, loggerFactory, cancellationToken) ||
                !agent.IsAuthenticated)
            {
                Console.Error.WriteLine("Could not establish the initial OAuth session.");
                return;
            }

            Did originalDid = agent.Credentials.Did;
            Console.WriteLine($"Authenticated DID: {originalDid}");
            Console.WriteLine("Reading the authenticated user's timeline with only getTimeline permission.");

            AtProtoHttpResult<Timeline> timelineResult = await agent.GetTimeline(
                limit: 5, cancellationToken: cancellationToken);
            PrintResult("GetTimeline", timelineResult);
            if (!timelineResult.Succeeded)
            {
                Console.Error.WriteLine("Timeline reading failed; the write demonstration will not proceed.");
                return;
            }

            Console.WriteLine($"Timeline returned {timelineResult.Result.Count} entries.");
            foreach (FeedViewPost entry in timelineResult.Result)
            {
                Console.WriteLine($"{entry.Post.Author}: {entry.Post.Record.Text}");
                Console.WriteLine($"  {entry.Post.Uri}");
            }

            Console.WriteLine($"Attempting to create public post '{post.Text}' at {postUri} before requesting write permissions.");
            AtProtoHttpResult<CreateRecordResult> firstAttempt = await agent.CreateBlueskyRecord<BlueskyRecord>(
                post, CollectionNsid.Post, rKey: recordKey, cancellationToken: cancellationToken);
            PrintResult("Create post with timeline-only scopes", firstAttempt);

            if (firstAttempt.Succeeded)
            {
                createdPost = firstAttempt.Result.StrongReference;
                Console.WriteLine("The server allowed posting with the initial scopes, possibly granting broader permissions.");
                Console.WriteLine("The negative authorization demonstration cannot be observed on this server.");
                Console.WriteLine("The post will NOT be created again. Requesting delete permission for cleanup.");
            }
            else if (firstAttempt.StatusCode == HttpStatusCode.Forbidden &&
                firstAttempt.AtErrorDetail is ScopeMissingError or InsufficientScope)
            {
                Console.WriteLine("The server rejected the actual write because the session lacks a required scope.");
            }
            else
            {
                Console.Error.WriteLine("This is not a recognized missing-scope response. Stopping without reauthorization or retry.");
                Console.Error.WriteLine($"If the server committed the write despite this response, check {postUri} and delete it manually.");
                return;
            }

            string[] expandedScopes = ExpandedScopes(initialScopes);
            Console.WriteLine("Adding app.bsky.authCreatePosts and app.bsky.authDeleteContent, retaining getTimeline.");
            if (!await Authorize(agent, handle, expandedScopes, originalDid, loggerFactory, cancellationToken) ||
                !agent.IsAuthenticated)
            {
                Console.Error.WriteLine("Scope upgrade failed; the post will not be retried.");
                return;
            }

            Console.WriteLine($"Scope upgrade authorized the same DID: {agent.Credentials.Did}");
            if (createdPost is null)
            {
                // The same record and key are reused, so a retry cannot create a second post.
                AtProtoHttpResult<CreateRecordResult> retry = await agent.CreateBlueskyRecord<BlueskyRecord>(
                    post, CollectionNsid.Post, rKey: recordKey, cancellationToken: cancellationToken);
                PrintResult("Create the same post after scope upgrade", retry);
                if (!retry.Succeeded)
                {
                    Console.Error.WriteLine($"The upgraded write failed. No further retry will be made; check {postUri} if the outcome is uncertain.");
                    return;
                }

                createdPost = retry.Result.StrongReference;
                Console.WriteLine($"Post created successfully after scope upgrade: {createdPost.Uri}");
            }
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine($"Network/HTTP transport failure, not an expected missing-scope response: {exception.Message}");
            Console.Error.WriteLine($"No write will be retried automatically. Check {postUri} if a write was in flight.");
            throw;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine($"The operation was canceled. No write will be retried automatically; check {postUri} if a write was in flight.");
            throw;
        }
        finally
        {
            try
            {
                if (createdPost is not null)
                {
                    Console.WriteLine($"Cleaning up the public post: {createdPost.Uri}");
                    Console.WriteLine("An unsuccessful or interrupted cleanup leaves this post on your account; delete it manually.");

                    if (agent.IsAuthenticated && agent.Credentials.Did == did)
                    {
                        using CancellationTokenSource cleanupTimeout = new(TimeSpan.FromSeconds(30));
                        AtProtoHttpResult<DeleteResult> deleteResult = await agent.DeletePost(
                            createdPost, cancellationToken: cleanupTimeout.Token);
                        PrintResult("Delete post", deleteResult);
                        if (deleteResult.Succeeded)
                        {
                            Console.WriteLine($"Post deleted: {createdPost.Uri}");
                        }
                        else
                        {
                            Console.Error.WriteLine($"Cleanup failed. Delete {createdPost.Uri} manually.");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"No matching authenticated session for cleanup. Delete {createdPost.Uri} manually.");
                    }
                }
            }
            finally
            {
                if (agent.IsAuthenticated)
                {
                    await agent.Logout(cancellationToken: CancellationToken.None);
                }
            }
        }
    }

    /// <summary>
    /// Adds the create-posts and delete-content Permission Sets while retaining the original authorization scopes.
    /// </summary>
    /// <param name="originalScopes">The scopes to preserve during the progressive authorization request.</param>
    /// <returns>A deduplicated collection containing the original scopes and both additional Permission Sets.</returns>
    static string[] ExpandedScopes(IEnumerable<string> originalScopes)
    {
        OAuthOptions options = new()
        {
            Scopes = originalScopes,
            PermissionSets = [BlueskyOAuthPermissionSets.CreatePosts, BlueskyOAuthPermissionSets.DeleteContent]
        };

        return [.. options.GetRequestedScopes()];
    }

    /// <summary>
    /// Creates OAuth options declaring the sample's maximum scopes under one fixed localhost client ID.
    /// </summary>
    /// <returns>The OAuth options shared by the initial and progressive authorization requests.</returns>
    /// <remarks>
    /// <para>The client ID's scope query declares all permissions the sample may request.
    /// Each pushed authorization request supplies its own subset separately, keeping the client ID unchanged
    /// when the sample progresses from timeline access to posting and cleanup.</para>
    /// </remarks>
    static OAuthOptions CreateOptions()
    {
        string[] maximumScopes = ExpandedScopes(InitialScopes);

        return new OAuthOptions(
            $"http://localhost/?scope={Uri.EscapeDataString(string.Join(" ", maximumScopes))}",
            scopes: maximumScopes);
    }

    static async Task<bool> Authorize(
        BlueskyAgent agent,
        Handle handle,
        string[] scopes,
        Did expectedDid,
        ILoggerFactory? loggerFactory,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Requested scopes: {string.Join(" ", scopes)}");

        await using var callbackServer = await CallbackServer.CreateAsync(
            loggerFactory: loggerFactory, cancellationToken: cancellationToken);
        OAuthClient uriBuilder = agent.CreateOAuthClient();
        Uri startUri = await agent.BuildOAuth2LoginUri(
            oAuthClient: uriBuilder,
            handle: handle,
            scopes: scopes,
            returnUri: callbackServer.Uri,
            cancellationToken: cancellationToken);

        if (uriBuilder.State is null)
        {
            throw new OAuthException("OAuth state is missing after building the login URI.");
        }

        // In a web app, store this state and the original DID together in trusted session storage.
        OAuthLoginState savedState = uriBuilder.State;
        OAuthClient.OpenBrowser(startUri);
        Console.WriteLine($"Awaiting callback on {callbackServer.Uri}");
        string callbackData = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken);
        if (string.IsNullOrEmpty(callbackData))
        {
            Console.Error.WriteLine("Received no OAuth response.");
            return false;
        }

        OAuthClient restoredClient = agent.CreateOAuthClient(savedState);
        bool authenticated = await agent.ProcessOAuth2LoginResponse(
            restoredClient, callbackData, expectedDid, cancellationToken);

        return authenticated;
    }

    static void PrintResult<TResult>(string operation, AtProtoHttpResult<TResult> result)
    {
        Console.WriteLine($"{operation}: Succeeded={result.Succeeded}, HTTP {(int)result.StatusCode} ({result.StatusCode})");
        Console.WriteLine($"  AtErrorDetail.Error: {result.AtErrorDetail?.Error ?? "(none)"}");
        Console.WriteLine($"  AtErrorDetail.Message: {result.AtErrorDetail?.Message ?? "(none)"}");
    }
}
