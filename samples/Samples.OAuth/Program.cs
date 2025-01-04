// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.OAuth;

using idunno.Bluesky;
using idunno.AtProto.Authentication;
using Samples.Common;

namespace Samples.OAuth
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

            // Uncomment the next line to route all requests through Fiddler Everywhere
            proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // Get an HttpClient configured to use a proxy, if proxyUri is not null.
            using (HttpClient? httpClient = Helpers.CreateOptionalHttpClient(proxyUri))

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

            using (var agent = new BlueskyAgent(httpClient: httpClient, loggerFactory: loggerFactory))
            {
                Did? did = await agent.ResolveHandle(handle, cancellationToken);

                if (did is null)
                {
                    Console.WriteLine("Could not resolve DID");
                    return;
                }

                Uri? pds = await agent.ResolvePds(did, cancellationToken);

                if (pds is null)
                {
                    Console.WriteLine($"Could not resolve PDS for {did}.");
                    return;
                }

                Uri? authorizationServer = await agent.ResolveAuthorizationServer(pds, cancellationToken);

                if (authorizationServer is null)
                {
                    Console.WriteLine($"Could not discover authorization server for {pds}.");
                    return;
                }

                string clientId = "http://localhost";

                Console.WriteLine($"Username:             {handle}");
                Console.WriteLine($"DID:                  {did}");
                Console.WriteLine($"PDS:                  {pds}");
                Console.WriteLine($"Authorization Server: {authorizationServer}");

                Debugger.Break();

                await using var callbackServer = new CallbackServer(CallbackServer.GetRandomUnusedPort(), loggerFactory: loggerFactory);
                {
                    var loginClient = new OAuthClient(loggerFactory);

                    Uri startUri = await loginClient.CreateOAuth2StartUri(authorizationServer, clientId, callbackServer.Uri, cancellationToken: cancellationToken);

                    Console.WriteLine($"Login URI           : {startUri}");

                    Console.WriteLine($"Awaiting callback on {callbackServer.Uri}");
                    string queryString = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);



                    if (!string.IsNullOrEmpty(queryString))
                    {
                        Console.WriteLine($"Got {queryString}");

                        var loginResult = await loginClient.ProcessOAuth2Response(queryString, cancellationToken: cancellationToken);

                        Debugger.Break();
                    }
                }

            }
        }
    }
}
