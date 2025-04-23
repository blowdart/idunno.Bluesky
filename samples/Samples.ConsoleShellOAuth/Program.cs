// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.OAuthCallback;

using idunno.Bluesky;
using idunno.Bluesky.Record;

using Samples.Common;
using idunno.AtProto.Repo;

namespace Samples.ConsoleShellOAuth
{
    public sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = Encoding.UTF8;

            var parser = Helpers.ConfigureCommandLine(PerformOperations);
            await parser.InvokeAsync(args);

            return 0;
        }

        static async Task PerformOperations(string? loginHandle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(loginHandle);

            loginHandle = "blowdart.me";

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

                await using var callbackServer = new CallbackServer(
                    CallbackServer.GetRandomUnusedPort(),
                    loggerFactory: loggerFactory);
                {
                    string callbackData;

                    OAuthClient oAuthClient = agent.CreateOAuthClient();

                    Uri startUri = await agent.BuildOAuth2LoginUri(oAuthClient, loginHandle, returnUri: callbackServer.Uri, cancellationToken: cancellationToken);

                    if (oAuthClient.State is null)
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("OAuthClient state is null after building login uri.");
                        Console.ForegroundColor = oldColor;
                        return;
                    }

                    OAuthClient.OpenBrowser(startUri);

                    callbackData = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(callbackData))
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Received no login response");
                        Console.ForegroundColor = oldColor;
                        return;
                    }

                    await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken);

                    if (string.IsNullOrEmpty(callbackData))
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Received no login response");
                        Console.ForegroundColor = oldColor;
                        return;
                    }

                    await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken);
                }

                if (!agent.IsAuthenticated)
                {
                    ConsoleColor oldColor = Console.ForegroundColor;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Could not login with oauth credentials");
                    Console.ForegroundColor = oldColor;
                    return;
                }

                Debugger.Break();

                StrongReference sr = new ("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.verification/3lnhd6cqgys27", "bafyreihgag4aasij5jmccta4vwapnw5qel6phjx5pk6kaga3tbtkoaqism");
                await agent.DeleteRecord(sr, cancellationToken: cancellationToken);

                var validationRecords = await agent.ListRecords<VerificationRecordValue>(CollectionNsid.Verification, cancellationToken: cancellationToken);

                var handle = "jcsalterego.bsky.social";

                var did = await agent.ResolveHandle(handle, cancellationToken: cancellationToken);
                if (did is not null)
                {
                    var profileResult = await agent.GetProfile(did, cancellationToken: cancellationToken);

                    if (profileResult.Succeeded && profileResult.Result.DisplayName is not null)
                    {
                        var verificationRecordValue = new VerificationRecordValue(
                            handle: handle,
                            subject: did,
                            displayName: profileResult.Result.DisplayName,
                            createdAt: DateTimeOffset.Now);

                        var recordCreateResult = await agent.CreateBlueskyRecord(
                            recordValue: verificationRecordValue,
                            collection: CollectionNsid.Verification,
                            validate: false,
                            cancellationToken: cancellationToken);

                        if (recordCreateResult.Succeeded)
                        {
                            Console.WriteLine(recordCreateResult.Result.StrongReference);
                        }

                        Debugger.Break();
                    }
                }

                await agent.Logout(cancellationToken: cancellationToken);
            }
        }
    }
}
