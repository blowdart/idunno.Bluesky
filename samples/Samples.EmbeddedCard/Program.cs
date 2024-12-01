// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;

using Microsoft.Extensions.Logging;

using idunno.Bluesky;

using X.Web.MetaExtractor;
using X.Web.MetaExtractor.ContentLoaders;
using X.Web.MetaExtractor.LanguageDetectors;

using Samples.Common;
using X.Web.MetaExtractor.ContentLoaders.HttpClient;
using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;
using System.Diagnostics;

namespace Samples.EmbeddedCard
{
    public sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var parser = Helpers.ConfigureCommandLine(PerformOperations);
            await parser.InvokeAsync(args);

            return 0;
        }

        static async Task PerformOperations(string? handle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(handle);
            ArgumentNullException.ThrowIfNullOrEmpty(password);

            // Uncomment the next line to route all requests through Fiddler Everywhere
            proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // Get an HttpClient configured to use a proxy, if proxyUri is not null.
            using (HttpClient? httpClient = Helpers.CreateOptionalHttpClient(proxyUri))

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

            // Create a new BlueSkyAgent
            using (var agent = new BlueskyAgent(httpClient: httpClient, loggerFactory: loggerFactory))
            {
                // Test code goes here.

                // Delete if your test code does not require authentication
                // START-AUTHENTICATION
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
                // END-AUTHENTICATION

                string targetUri = "https://en.wikipedia.org/wiki/Baked_beans";
                Uri page = new (targetUri);

                Extractor metadataExtractor = new ();
                var pageMetadata = await metadataExtractor.ExtractAsync(page);

                string? title = pageMetadata.Title;
                string? pageUri = pageMetadata.Url ?? targetUri;
                string? description = pageMetadata.Description;

                if (!string.IsNullOrEmpty(pageUri) && !string.IsNullOrEmpty(title))
                {
                    // We have the minimum needed to embed a card.
                    Blob? thumbnailBlob = null;

                    // Now see if there's a thumbnail
                    string? thumbnailUri = pageMetadata.MetaTags.Where(o => o.Key == "og:image").Select(o => o.Value).FirstOrDefault();
                    if (!string.IsNullOrEmpty(thumbnailUri))
                    {
                        // Try to grab the image, then upload it as a blob.
                        try
                        {
                            var downloadHttpClient = httpClient ?? new HttpClient();

                            using (HttpResponseMessage response = await downloadHttpClient.GetAsync(thumbnailUri, cancellationToken: cancellationToken))
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

                        EmbeddedExternal embeddedExternal = new(pageUri, title, description, thumbnailBlob);

                        // Embed with a PostBuilder
                        var postBuilder = new PostBuilder("Embedded record test");
                        postBuilder.EmbedRecord(embeddedExternal);

                        var postBuilderResult = await agent.Post(postBuilder, cancellationToken: cancellationToken);

                        // Post an embedded card directly. Embedded card posts don't require post text.
                        var postResult = await agent.Post(externalCard: embeddedExternal, cancellationToken: cancellationToken);

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
