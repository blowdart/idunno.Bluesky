// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.Embed;

using Samples.Common;

using OpenGraphNet;
using OpenGraphNet.Metadata;
using System.Diagnostics;

namespace Samples.EmbeddedCard
{
    public sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var parser = Helpers.ConfigureCommandLine(
                args,
                "BlueskyAgent Embedded Card Sample",
                PerformOperations);

            return await parser.InvokeAsync();
        }

        static async Task PerformOperations(string? handle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(handle);
            ArgumentException.ThrowIfNullOrEmpty(password);

            // Uncomment the next line to route all requests through Fiddler Everywhere
            // proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // If a proxy is being used turn off certificate revocation checks.
            //
            // WARNING: this setting can introduce security vulnerabilities.
            // The assumption in these samples is that any proxy is a debugging proxy,
            // which tend to not support CRLs in the proxy HTTPS certificates they generate.
            bool checkCertificateRevocationList = true;
            if (proxyUri is not null)
            {
                checkCertificateRevocationList = false;
            }

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

            // Create a new BlueSkyAgent
            using (var agent = new BlueskyAgent(
              new BlueskyAgentOptions()
              {
                  LoggerFactory = loggerFactory,

                  HttpClientOptions = new HttpClientOptions()
                  {
                      CheckCertificateRevocationList = checkCertificateRevocationList,
                      ProxyUri = proxyUri
                  }
              }))
            {
                var loginResult = await agent.Login(handle, password, authCode, cancellationToken: cancellationToken);
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

                Uri pageUri = new ("https://en.wikipedia.org/wiki/Baked_beans");

                OpenGraph graph = await OpenGraph.ParseUrlAsync(pageUri, cancellationToken: cancellationToken);

                // Check to see if there's a different URI specified in the graph metadata.
                if (graph.Url is not null)
                {
                    pageUri = graph.Url;
                }

                if (!string.IsNullOrEmpty(graph.Title) && pageUri is not null)
                {
                    string? description = graph.Metadata["og:description"].Value();

                    Blob? thumbnailBlob = null;

                    // Now see if there's a thumbnail
                    if (graph.Image is not null)
                    {
                        // Try to grab the image, then upload it as a blob.
                        try
                        {
                            var downloadHttpClient = agent.HttpClient;

                            using (HttpResponseMessage response = await downloadHttpClient.GetAsync(graph.Image, cancellationToken: cancellationToken))
                            {
                                response.EnsureSuccessStatusCode();

                                var responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken: cancellationToken);

                                if (responseBody is not null &&
                                    response.Content is not null &&
                                    response.Content.Headers is not null &&
                                    response.Content.Headers.ContentType is not null &&
                                    response.Content.Headers.ContentType.MediaType is not null)
                                {
                                    AtProtoHttpResult<Blob> uploadResult = await
                                        agent.UploadBlob(responseBody, response.Content.Headers.ContentType.MediaType, cancellationToken: cancellationToken);

                                    if (uploadResult.Succeeded)
                                    {
                                        thumbnailBlob = uploadResult.Result;
                                    }
                                }
                            }
                        }
                        catch (HttpRequestException) { } // Ignore any exceptions from trying to get the thumbnail and upload the image.

                        EmbeddedExternal embeddedExternal = new(pageUri, graph.Title, description, thumbnailBlob);

                        // Embed with a PostBuilder
                        var postBuilder = new PostBuilder("Embedded record test");
                        postBuilder.EmbedRecord(embeddedExternal);

                        var postBuilderResult = await agent.Post(postBuilder, cancellationToken: cancellationToken);

                        // Post an embedded card directly. Embedded card posts don't require post text.
                        var postResult = await agent.Post(externalCard: embeddedExternal, cancellationToken: cancellationToken);

                        Debugger.Break();

                        if (postBuilderResult.Succeeded)
                        {
                            await agent.DeletePost(postBuilderResult.Result.StrongReference, cancellationToken: cancellationToken);
                        }

                        if (postResult.Succeeded)
                        {
                            await agent.DeletePost(postResult.Result.StrongReference, cancellationToken: cancellationToken);
                        }
                    }
                }
            }
            return;
        }
    }
}
