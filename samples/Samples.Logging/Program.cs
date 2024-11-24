// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Server;
using Samples.Common;

namespace Samples.Logging
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
            // proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // Get an HttpClient configured to use a proxy, if proxyUri is not null.
            using (HttpClient? httpClient = Helpers.CreateOptionalHttpClient(proxyUri))

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))
            {
                using (var agentWithLogging = new AtProtoAgent(new("https://bsky.social"), httpClient: httpClient, loggerFactory: loggerFactory))
                {
                    _ = await agentWithLogging.Login(new Credentials(handle, password), cancellationToken: cancellationToken).ConfigureAwait(false);
                    await agentWithLogging.Logout(cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                // Now do it again without a logger to demonstrate no bad side effects
                using var agentWithoutLogging = new AtProtoAgent(new("https://bsky.social"));
                {
                    _ = await agentWithoutLogging.Login(new Credentials(handle, password), cancellationToken: cancellationToken).ConfigureAwait(false);
                    await agentWithoutLogging.Logout(cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }

            return;
        }
    }
}
