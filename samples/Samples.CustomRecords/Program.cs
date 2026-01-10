// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

using Microsoft.Extensions.Logging;

using Samples.Common;

using idunno.AtProto;
using System.Text.Json;

namespace Samples.CustomRecords
{
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
            // Create a new AtProto agent
            using (var agent = new AtProtoAgent(
                service: new("https://api.bsky.app"), // You would substitute your own service URI here
                options: new AtProtoAgentOptions()
                {
                    LoggerFactory = loggerFactory,

                    HttpClientOptions = new HttpClientOptions()
                    {
                        CheckCertificateRevocationList = checkCertificateRevocationList,
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

                // Your code goes here

                const string collection = "blue.idunno.listening.track";
                JsonSerializerOptions jsonSerializerOptions = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(SourceGenerationContext.Default);

                var track = new Track("Shake it off", "Taylor Swift", "1989");

                var createResult = await agent.CreateRecord(
                    record: track,
                    jsonSerializerOptions: jsonSerializerOptions,
                    collection: collection,
                    rKey: TimestampIdentifier.Next(),
                    validate: false,
                    cancellationToken: cancellationToken);

                Debugger.Break();

                if (createResult.Succeeded)
                {
                    await agent.DeleteRecord(createResult.Result.Uri, cancellationToken: cancellationToken);
                }

                Debugger.Break();
            }
        }
    }

}
