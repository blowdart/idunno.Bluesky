// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using System.Text;

namespace idunno.TrimmingTest
{
    public sealed class Program
    {
        static async Task<int> Main()
        {
            // Necessary to render emojis.
            Console.OutputEncoding = Encoding.UTF8;

            using (var cancellationTokenSource = new CancellationTokenSource())
            {

                Console.CancelKeyPress += (sender, args) =>
                {
                    args.Cancel = true;
                    cancellationTokenSource.Cancel(true);
                };

                CancellationToken cancellationToken = cancellationTokenSource.Token;

                Uri? proxyUri = null;

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

                string? environmentHandle = Environment.GetEnvironmentVariable("_BlueskyHandle");
                Console.Write("Login as: ");
                if (environmentHandle is not null)
                {
                    Console.Write($"[{environmentHandle}] ");
                }
                string? handle = Console.ReadLine();
                if (handle is null && environmentHandle is not null)
                {
                    handle = environmentHandle;
                }
                if (handle is null)
                {
                    return -1;
                }

                // Change the log level in the ConfigureConsoleLogging() to enable logging
                using (ILoggerFactory? loggerFactory = ConfigureConsoleLogging(LogLevel.Debug))
                using (var agent = new AtProtoAgent(new Uri("https://api.bsky.app"),
                    options: new()
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
                    string callbackData;

                    await using var callbackServer = new AtProto.OAuthCallback.CallbackServer(
                        AtProto.OAuthCallback.CallbackServer.GetRandomUnusedPort(),
                        loggerFactory: loggerFactory);
                    {
                        OAuthClient oAuthClient = agent.CreateOAuthClient();

                        Uri startUri = await agent.BuildOAuth2LoginUri(oAuthClient, handle, returnUri: callbackServer.Uri, cancellationToken: cancellationToken);

                        // Save state to use when processing the response, mimicking what we'd do in a web application.

                        if (oAuthClient.State is null)
                        {
                            ConsoleColor oldColor = Console.ForegroundColor;

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("OAuthClient state is null after building login uri.");
                            Console.ForegroundColor = oldColor;
                            return -2;
                        }

                        Console.WriteLine($"Login URI           : {startUri}");

                        OAuthClient.OpenBrowser(startUri);

                        Console.WriteLine($"Awaiting callback on {callbackServer.Uri}");

                        callbackData = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (string.IsNullOrEmpty(callbackData))
                        {
                            ConsoleColor oldColor = Console.ForegroundColor;

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Received no login response");
                            Console.ForegroundColor = oldColor;
                            return -3;
                        }

                        await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken);
                    }

                    if (agent.IsAuthenticated)
                    {
                        //await agent.Post("Posted from a trimmed AOT app", cancellationToken: cancellationToken);

                        await agent.Logout(cancellationToken: cancellationToken);
                    }
                    else
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Could not login with oauth credentials");
                        Console.ForegroundColor = oldColor;
                        return -4 ;
                    }
                }
            }

            return 0;
        }

        public static ILoggerFactory? ConfigureConsoleLogging(LogLevel? level)
        {
            if (level is null)
            {
                return null;
            }

            return LoggerFactory.Create(configure =>
            {
                configure.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "G";
                    options.UseUtcTimestamp = false;
                });
                configure.SetMinimumLevel((LogLevel)level);
            });
        }
    }
}

