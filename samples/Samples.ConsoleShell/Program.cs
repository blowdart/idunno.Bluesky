// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using GermNetwork.Com;
using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.Embed;
using Microsoft.Extensions.Logging;
using Samples.Common;

namespace Samples.ConsoleShell;

public sealed class Program
{
    static async Task<int> Main(string[] args)
    {
        // Necessary to render emojis.
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var parser = Helpers.ConfigureCommandLine(
            args,
            "BlueskyAgent Console Demonstration Template",
            PerformOperations);

        return await parser.InvokeAsync();
    }

    static async Task PerformOperations(string? userHandle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userHandle);
        ArgumentException.ThrowIfNullOrEmpty(password);

        // Uncomment the next line to route all requests through Fiddler Everywhere
        proxyUri = new Uri("http://localhost:8866");

        // Uncomment the next line to route all requests  through Fiddler Classic
        // proxyUri = new Uri("http://localhost:8888");

        // Change the log level in the ConfigureConsoleLogging() to enable logging
        using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

        // Create a new BlueSkyAgent
        using (var agent = new BlueskyAgent(
            options: new BlueskyAgentOptions()
            {
                LoggerFactory = loggerFactory,

                HttpClientOptions = new HttpClientOptions()
                {
                    ProxyUri = proxyUri
                },
            }))
        {
            // Delete if your test code does not require authentication
            // START-AUTHENTICATION
            var loginResult = await agent.Login(userHandle, password, authCode, cancellationToken: cancellationToken);
            if (!loginResult.Succeeded)
            {
                if (loginResult.AtErrorDetail is not null &&
                    string.Equals(loginResult.AtErrorDetail.Error!, "AuthFactorTokenRequired", StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleColor oldColor = Console.ForegroundColor;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Login requires an authentication code.");
                    Console.WriteLine("Check your email and use --authCode to specify the authentication code.");
                    Console.ForegroundColor = oldColor;

                    return;
                }
                else
                {
                    ConsoleColor oldColor = Console.ForegroundColor;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Login failed.");
                    Console.ForegroundColor = oldColor;

                    if (loginResult.AtErrorDetail is not null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;

                        Console.WriteLine($"Server returned {loginResult.AtErrorDetail.Error} / {loginResult.AtErrorDetail.Message}");
                        Console.ForegroundColor = oldColor;

                        return;
                    }
                }
            }
            // END-AUTHENTICATION

            AtProtoHttpResult<AtProtoRepositoryRecord<Declaration>> getRecordResult = await agent.GetRecord<Declaration>(
                new AtUri("at://blowdart.me/com.germnetwork.declaration/self"),
                cancellationToken: cancellationToken);

            var getPostViewResult = await agent.GetPost(new AtUri("at://did:plc:dpajgwmnecpdyjyqzjzm6bnb/app.bsky.feed.post/3mmexpn42dk27"), cancellationToken: cancellationToken);
            getPostViewResult.EnsureSucceeded();
            var getEmbedExternalViewResult = await agent.GetEmbedExternalView(getPostViewResult.Result, cancellationToken: cancellationToken);
            getEmbedExternalViewResult.EnsureSucceeded();
            Debugger.Break();

            var post = new Post("Test new embedding records");
            var embed = new EmbeddedExternal(
                uri: new Uri("https://estrattonbailey.pckt.blog/test-post-bn5bcy2"),
                title: "https://estrattonbailey.pckt.blog/test-post-bn5bcy2",
                description: "Test post content",
                thumbnail: null,
                associatedRefs:
                    [
                        new StrongReference("at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/site.standard.document/3mloolvzj2jsy", "bafyreibhvcdzstnjcktsdaiyjy7f2msthllikx3k3eem2rfqbmgbeniwc4"),
                        new StrongReference("at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/site.standard.publication/3mlooltppoh4a", "bafyreigzwdefal6ueevleagplq64yadnxwv6ci5t6tizu3sshszwfe3e64"),
                    ]
                );
            post.Embed(embed);
            var postResult = await agent.Post(post, cancellationToken: cancellationToken);
            Debugger.Break();

            if (postResult.Succeeded)
            {
                getPostViewResult = await agent.GetPost(postResult.Result.Uri, cancellationToken: cancellationToken);
                getPostViewResult.EnsureSucceeded();
                getEmbedExternalViewResult = await agent.GetEmbedExternalView(getPostViewResult.Result, cancellationToken: cancellationToken);
                getEmbedExternalViewResult.EnsureSucceeded();
                Debugger.Break();


                await agent.DeletePost(postResult.Result.Uri, cancellationToken: cancellationToken);
            }

            Debugger.Break();
        }
    }
}
