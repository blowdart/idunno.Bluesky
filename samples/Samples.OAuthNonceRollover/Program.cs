// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

using Microsoft.Extensions.Logging;

using Samples.Common;
using System.Security.Cryptography;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.Record;
using Microsoft.Extensions.Validation;

namespace Samples.OAuthNonceRollover
{
    public sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = Encoding.UTF8;

            var parser = Helpers.ConfigureCommandLine(
                args,
                "BlueskyAgent DPoP Test Bed",
                PerformOperations);

            return await parser.InvokeAsync();
        }

        static async Task PerformOperations(string? handle, Uri? proxyUri, CancellationToken cancellationToken = default)
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
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Warning))
            using (var agent = new BlueskyAgent(
                options: new BlueskyAgentOptions()
                {
                    LoggerFactory = loggerFactory,

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
                    Console.WriteLine();
                    Console.WriteLine($"Credentials issued for   : {agent.Credentials.Service}");
                    Console.WriteLine();

                    string accessCredentialsHash;

                    accessCredentialsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Credentials.AccessJwt)));
                    Console.WriteLine($"Access JWT hash          : {accessCredentialsHash}");
                    Console.WriteLine($"Access JWT expiry        : {agent.Credentials.ExpiresOn:G}");

                    DPoPAccessCredentials? accessCredentials = agent.Credentials as DPoPAccessCredentials;
                    Console.WriteLine($"DPoP Nonce               : {accessCredentials?.DPoPNonce}");
                    Console.WriteLine();
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

                bool credentialsUpdatedEventFired = false;

                agent.CredentialsUpdated += (sender, eventArgs) =>
                {
                    string updatedAccessCredentialsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Credentials.AccessJwt)));
                    Console.WriteLine($"Credentials updated      : {updatedAccessCredentialsHash}");
                    Console.WriteLine($"Credentials updated      : {agent.Credentials.ExpiresOn:G}");

                    DPoPAccessCredentials? accessCredentials = agent.Credentials as DPoPAccessCredentials;
                    Console.WriteLine($"DPoP Nonce               : {accessCredentials?.DPoPNonce}");

                    credentialsUpdatedEventFired = true;
                };

                string? initialNonce = agent.Credentials is DPoPAccessCredentials dPoPAccessCredentials ? dPoPAccessCredentials.DPoPNonce : null;

                if (initialNonce is null)
                {
                    ConsoleColor oldColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Initial credentials have no DPoP nonce.");
                    Console.ForegroundColor = oldColor;
                    return;
                }

                Console.WriteLine("Making initial request...");
                AtProtoHttpResult<int> getNotificationCount = await agent.GetNotificationUnreadCount(cancellationToken: cancellationToken);
                getNotificationCount.EnsureSucceeded();

                Console.WriteLine($"Immediately making another request, no nonce rotation should occur.");
                getNotificationCount = await agent.GetNotificationUnreadCount(cancellationToken: cancellationToken);
                getNotificationCount.EnsureSucceeded();
                if (credentialsUpdatedEventFired)
                {
                    ConsoleColor oldColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("CredentialsUpdated event unexpectedly fired.");
                    Console.ForegroundColor = oldColor;
                    return;
                }

                Console.WriteLine("Waiting for 90 seconds for the nonce to rotate.");
                Thread.Sleep(TimeSpan.FromSeconds(90));
                Console.WriteLine("Waking up and making another request...");
                getNotificationCount = getNotificationCount = await agent.GetNotificationUnreadCount(cancellationToken: cancellationToken);
                getNotificationCount.EnsureSucceeded();

                if (!credentialsUpdatedEventFired)
                {
                    ConsoleColor oldColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("CredentialsUpdated event did not fire as expected.");
                    Console.ForegroundColor = oldColor;
                    return;
                }
                credentialsUpdatedEventFired = false;

                string? autoUpdatedNonce = agent.Credentials is DPoPAccessCredentials autoUpdatedCredentials ? autoUpdatedCredentials.DPoPNonce : null;

                if (autoUpdatedNonce is null)
                {
                    ConsoleColor oldColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Auto updated credentials have no DPoP nonce.");
                    Console.ForegroundColor = oldColor;
                    return;
                }

                if (autoUpdatedNonce == initialNonce)
                {
                    ConsoleColor oldColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Nonce did not rotate as expected.");
                    Console.ForegroundColor = oldColor;
                    return;
                }

                Debugger.Break();

                Console.WriteLine("Logging out");
                await agent.Logout(cancellationToken: cancellationToken);
                Debugger.Break();
            }
        }
    }
}
