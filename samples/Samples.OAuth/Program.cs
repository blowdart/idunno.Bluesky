// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.OAuth;

using idunno.Bluesky;

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

                AccessCredentials? accessCredentials = null;

                await using var callbackServer = new CallbackServer(CallbackServer.GetRandomUnusedPort(), loggerFactory: loggerFactory);
                {
                    OAuthClient loginClient = agent.CreateOAuthClient();

                    Uri startUri = await loginClient.CreateOAuth2StartUri(
                        service: pds,
                        clientId: clientId,
                        redirectUri: callbackServer.Uri,
                        authority: authorizationServer,
                        handle: handle,
                        scopes: ["atproto", "transition:generic"],
                        cancellationToken: cancellationToken);

                    Console.WriteLine($"Login URI           : {startUri}");

                    Console.WriteLine($"Opening browser");

                    OAuthClient.OpenBrowser(startUri);

                    Console.WriteLine($"Awaiting callback on {callbackServer.Uri}");

                    string queryString = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(queryString))
                    {
                        accessCredentials = await loginClient.ProcessOAuth2Response(queryString, cancellationToken: cancellationToken);
                    }
                }

                if (accessCredentials is not null)
                {
                    Console.WriteLine($"Access JWT expires on: {accessCredentials.ExpiresOn:G}");
                    Console.WriteLine();

                    //var result = await AtProtoServer.CreateRecord(
                    //    new Post("hello oauth"),
                    //    CollectionNsid.Post,
                    //    accessCredentials.Did!,
                    //    rKey: null,
                    //    validate: null,
                    //    swapCommit: null,
                    //    service: pds,
                    //    accessCredentials: accessCredentials,
                    //    httpClient: agent.HttpClient,
                    //    onCredentialsUpdated: null,
                    //    cancellationToken: cancellationToken);

                    Debugger.Break();

                    bool loginResult = await agent.Login(accessCredentials, cancellationToken);

                    if (loginResult)
                    {
                        await agent.CreateRecord(new Post("hello oauth, via agent"), CollectionNsid.Post, cancellationToken: cancellationToken);

                        Debugger.Break();

                        // Now try refresh operation

                    }
                    else
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Could not login with oauth credentials");
                        Console.ForegroundColor = oldColor;
                        return;
                    }

                    Debugger.Break();
                }

            }
        }
    }
}
