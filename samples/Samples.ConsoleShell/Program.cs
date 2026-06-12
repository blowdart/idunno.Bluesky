// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
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

            ICollection<EmbeddedGalleryImage> uploadedImages = [];
            for (int i = 1; i < 10; i++)
            {
                AtProtoHttpResult<Blob> uploadResult = await agent.UploadBlob(
                        fileName: $"C:\\Users\\BarryDorrans\\Downloads\\{i}.webp",
                        mimeType: "image/webp",
                        cancellationToken: cancellationToken);

                if (uploadResult.Succeeded)
                {
                    AspectRatio aspectRatio = new(198, 138);

                    if (i == 1)
                    {
                        aspectRatio = new(142, 138);
                    }
                    else if (i == 9)
                    {
                        aspectRatio = new(164, 138);
                    }

                    uploadedImages.Add(new EmbeddedGalleryImage(
                        image: uploadResult.Result,
                        altText: "Long cat is long, sideways",
                        aspectRatio: aspectRatio));
                }
            }

            var embeddedGallery = new EmbeddedGallery(uploadedImages);

            var post = new Post("Long cat is long, sideways", embeddedGallery);

            var postResult = await agent.Post(post, cancellationToken: cancellationToken);

            if (postResult.Succeeded)
            {
                Console.WriteLine($"Post created with URI {postResult.Result.Uri}");
                await agent.DeletePost(postResult.Result.Uri, cancellationToken);
            }
            else
            {
                ConsoleColor oldColor = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to create post.");
                Console.ForegroundColor = oldColor;

                if (postResult.AtErrorDetail is not null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;

                    Console.WriteLine($"Server returned {postResult.AtErrorDetail.Error} / {postResult.AtErrorDetail.Message}");
                    Console.ForegroundColor = oldColor;
                }
            }
        }
    }
}