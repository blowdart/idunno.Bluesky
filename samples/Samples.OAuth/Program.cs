// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using idunno.Bluesky;

using Samples.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

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

            using (var agent = new AtProtoAgent(
                service: new Uri("https://api.bsky.app"),
                loggerFactory: loggerFactory,
                options: new AtProtoAgentOptions()
                {
                    HttpClientOptions = new HttpClientOptions()
                    {
                        CheckCertificateRevocationList = checkCertificateRevocationList,
                        ProxyUri = proxyUri
                    },

                    OAuthOptions = new OAuthOptions()
                    {
                        ClientId = "http://localhost",
                        Scopes = ["atproto", "transition:generic"]
                    }
                }))
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

                Uri? authorizationServer = await agent.ResolveAuthorizationServer(handle, cancellationToken);

                if (authorizationServer is null)
                {
                    Console.WriteLine($"Could not discover authorization server for {pds}.");
                    return;
                }

                Console.WriteLine($"Username:               {handle}");
                Console.WriteLine($"DID:                    {did}");
                Console.WriteLine($"PDS:                    {pds}");
                Console.WriteLine($"Authorization Server:   {authorizationServer}");

                OAuthLoginState? oAuthLoginState = null;
                string callbackData;

                await using var callbackServer = new idunno.AtProto.OAuthCallback.CallbackServer(
                    idunno.AtProto.OAuthCallback.CallbackServer.GetRandomUnusedPort(),
                    loggerFactory: loggerFactory);
                {
                    OAuthClient uriBuilderOAuthClient = agent.CreateOAuthClient();

                    Uri startUri = await agent.BuildOAuth2LoginUri(uriBuilderOAuthClient, handle, returnUri: callbackServer.Uri, cancellationToken: cancellationToken);

                    // Save state to use when processing the response, mimicking what we'd do in a web application.

                    if (uriBuilderOAuthClient.State is null)
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("OAuthClient state is null after building login uri.");
                        Console.ForegroundColor = oldColor;
                        return;
                    }

                    // If you need a primary key you can extract the state parameter from the Uri
                    // string stateKey = QueryHelpers.ParseQuery(startUri.Query)["state"]!;
                    oAuthLoginState = uriBuilderOAuthClient.State;

                    Console.WriteLine($"Login URI           : {startUri}");

                    OAuthClient.OpenBrowser(startUri);

                    Console.WriteLine($"Awaiting callback on {callbackServer.Uri}");

                    callbackData = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                if (string.IsNullOrEmpty(callbackData))
                {
                    ConsoleColor oldColor = Console.ForegroundColor;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Received no login response");
                    Console.ForegroundColor = oldColor;
                    return;
                }

                OAuthClient oAuthClient = agent.CreateOAuthClient(oAuthLoginState);
                await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken);

                Debugger.Break();

                if (agent.IsAuthenticated)
                {
                    Console.WriteLine($"Credentials issued for: {agent.Credentials.Service}");
                    Console.WriteLine($"Access JWT expires on:  {agent.Credentials.ExpiresOn:G}");
                    Console.WriteLine();

                    string accessCredentialsHash;

                    accessCredentialsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Credentials.AccessJwt)));
                    Console.WriteLine($"Access JWT hash      :  {agent.Credentials.ExpiresOn:G}");


                    //                    await agent.CreateRecord(new Post($"hello via oauth, token hash {accessCredentialsHash}"), CollectionNsid.Post, cancellationToken: cancellationToken);

                    await agent.RefreshCredentials(cancellationToken: cancellationToken);

                    accessCredentialsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Credentials.AccessJwt)));
                    Console.WriteLine($"Refreshed JWT hash   :  {agent.Credentials.ExpiresOn:G}");

                    //                    await agent.CreateRecord(new Post($"hello via oauth refresh, token hash {accessCredentialsHash}"), CollectionNsid.Post, cancellationToken: cancellationToken);
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

                await agent.Logout(cancellationToken: cancellationToken);
            }
        }
    }
}
