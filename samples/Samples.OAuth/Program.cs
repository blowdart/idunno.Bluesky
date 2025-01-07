// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using IdentityModel.OidcClient;

using idunno.AtProto;
using idunno.AtProto.OAuth;
using idunno.Bluesky;

using Samples.Common;
using IdentityModel;
using Microsoft.IdentityModel.JsonWebTokens;
using idunno.AtProto.Authentication;

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
            ArgumentException.ThrowIfNullOrEmpty(handle);

            // Uncomment the next line to route all requests through Fiddler Everywhere
            proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

            using (var agent = new BlueskyAgent(proxyUri : proxyUri, loggerFactory: loggerFactory))
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
                    OAuthClient loginClient = agent.CreateOAuthClient();

                    Uri startUri = await loginClient.CreateOAuth2StartUri(authorizationServer, clientId, callbackServer.Uri, cancellationToken: cancellationToken);

                    Console.WriteLine($"Login URI           : {startUri}");

                    Console.WriteLine($"Opening browser");

                    OAuthClient.OpenBrowser(startUri);

                    Console.WriteLine($"Awaiting callback on {callbackServer.Uri}");

                    string queryString = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(queryString))
                    {
                        Console.WriteLine($"Got {queryString}");

                        LoginResult loginResult = await loginClient.ProcessOAuth2Response(queryString, cancellationToken: cancellationToken);

                        Console.WriteLine($"Succeeded : {!loginResult.IsError}");

                        if (!loginResult.IsError)
                        {
                            JsonWebToken accessToken = new (loginResult.AccessToken);

                            Uri issuer = new (accessToken.Issuer);

                            if (!issuer.Equals(authorizationServer))
                            {
                                ConsoleColor currentColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Invalid issuer, expected {authorizationServer}, received {accessToken.Issuer}");
                                Console.ForegroundColor = currentColor;
                                return;
                            }

                            if (accessToken.Subject is null || accessToken.Subject != did)
                            {
                                ConsoleColor currentColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Invalid subject, expected {did}, received {accessToken.Subject}");
                                Console.ForegroundColor = currentColor;
                                return;
                            }

                            if (!accessToken.GetClaim("scope").ToString().Contains("atproto"))
                            {
                                ConsoleColor currentColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"AccessToken does not contain atproto in the scope claim");
                                Console.ForegroundColor = currentColor;
                                return;
                            }
                        }

                        Debugger.Break();
                    }
                }

            }
        }
    }
}
