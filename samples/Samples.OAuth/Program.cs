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

using Microsoft.IdentityModel.JsonWebTokens;

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
            ArgumentException.ThrowIfNullOrEmpty(handle);

            // Uncomment the next line to route all requests through Fiddler Everywhere
            proxyUri = new Uri("http://localhost:8866");

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

            using (var agent = new BlueskyAgent(proxyUri : proxyUri, checkCertificateRevocationList: checkCertificateRevocationList, loggerFactory: loggerFactory))
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

                Console.WriteLine($"Username:              {handle}");
                Console.WriteLine($"DID:                   {did}");
                Console.WriteLine($"PDS:                   {pds}");
                Console.WriteLine($"Authorization Server:  {authorizationServer}");

                Debugger.Break();

                await using var callbackServer = new CallbackServer(CallbackServer.GetRandomUnusedPort(), loggerFactory: loggerFactory);
                {
                    OAuthClient loginClient = agent.CreateOAuthClient();

                    Uri startUri = await loginClient.CreateOAuth2StartUri(pds, clientId, authorizationServer, callbackServer.Uri, cancellationToken: cancellationToken);

                    Console.WriteLine($"Login URI           : {startUri}");

                    Console.WriteLine($"Opening browser");

                    OAuthClient.OpenBrowser(startUri);

                    Console.WriteLine($"Awaiting callback on {callbackServer.Uri}");

                    string queryString = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(queryString))
                    {
                        AccessCredentials? credentials = await loginClient.ProcessOAuth2Response(queryString, cancellationToken: cancellationToken);

                        if (credentials is not null)
                        {
                            Console.WriteLine($"Access JWT expires on: {credentials.AccessJwtExpiresOn:G}");
                        }

                        Debugger.Break();
                    }
                }

            }
        }
    }
}
