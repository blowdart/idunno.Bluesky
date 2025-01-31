// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Net;
using System.Reflection;
using System.Text;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky;

using Samples.Common;

namespace Samples.TokenRefresh
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

            // Create a new BlueSkyAgent
            using (var agent = new BlueskyAgent(proxyUri: proxyUri, checkCertificateRevocationList: checkCertificateRevocationList, loggerFactory: loggerFactory))
            {
                agent.SessionCreated += (sender, e) =>
                {
                    Console.WriteLine($"EVENT: Session created for : {e.Handle} ({e.Did}) on {e.Service}");
                };

                agent.CredentialsUpdated += (sender, e) =>
                {
                    Console.WriteLine($"EVENT: Session refreshed for : {e.Did}");
                };

                agent.SessionEnded += (sender, e) =>
                {
                    Console.WriteLine($"EVENT: Logged out from {e.Service}: {e.Did}");
                };

                agent.TokenRefreshFailed += (sender, e) =>
                {
                    if (e.SessionErrors != SessionConfigurationErrorType.None)
                    {
                        Console.WriteLine($"EVENT: Session was not in the right state to be refreshed:");

                        if (e.SessionErrors.HasFlag(SessionConfigurationErrorType.NullSession))
                        {
                            Console.WriteLine("\tSession was null.");
                        }

                        if (e.SessionErrors.HasFlag(SessionConfigurationErrorType.MissingAccessToken))
                        {
                            Console.WriteLine("\tSession was missing the access token.");
                        }

                        if (e.SessionErrors.HasFlag(SessionConfigurationErrorType.MissingRefreshToken))
                        {
                            Console.WriteLine("\tSession was missing the access token.");
                        }

                        if (e.SessionErrors.HasFlag(SessionConfigurationErrorType.MissingService))
                        {
                            Console.WriteLine("\tSession was missing the service URI it was created on.");
                        }

                        if (e.SessionErrors.HasFlag(SessionConfigurationErrorType.MissingDid))
                        {
                            Console.WriteLine("\tSession missing the DID of the actor it was created for.");
                        }
                    }

                    if (e.SessionErrors == SessionConfigurationErrorType.None && e.StatusCode != HttpStatusCode.OK)
                    {
                        Console.WriteLine($"EVENT: Refresh API returned {e.StatusCode}");

                        if (e.Error is not null)
                        {
                            Console.WriteLine($"\t{e.Error.Error}");

                            if (!string.IsNullOrEmpty(e.Error.Message))
                            {
                                Console.WriteLine($"\t{e.Error.Message}");
                            }
                        }
                    }
                };


                // START-AUTHENTICATION
                var loginResult = await agent.Login(handle, password, authCode, cancellationToken: cancellationToken);
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
                
                if (agent.IsAuthenticated && agent.Session.AccessJwt is not null && agent.Session.RefreshJwt is not null)
                {
                    // First let's go manually

                    await agent.RefreshSession(cancellationToken);

                    Console.WriteLine($"Access JWT SHA256 {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Session.AccessJwt)))}");

                    // OK now let's be horrible via reflection.
                    Type timer = typeof(System.Timers.Timer);
                    Type agentType = typeof(AtProtoAgent);
                    FieldInfo? sessionRefreshTimerField = agentType.GetField("_sessionRefreshTimer", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (sessionRefreshTimerField is not null)
                    {
                        var sessionRefreshTimerInstance = sessionRefreshTimerField.GetValue(agent);

                        if (sessionRefreshTimerInstance is not null)
                        {
                            var sessionRefreshTimer = (System.Timers.Timer)sessionRefreshTimerInstance;

                            var oldInterval = sessionRefreshTimer.Interval;

                            Console.WriteLine($"Refresh time is due to fire in {sessionRefreshTimer.Interval} milliseconds");

                            Console.WriteLine("Hacking the refresh timer to fire in 10 seconds");

                            sessionRefreshTimer.Interval = 10000;

                            Console.WriteLine("Waiting for 30 seconds, you should see refreshes happening via console logging...");

                            await Task.Delay(new TimeSpan(0, 0, 30), cancellationToken);

                            Console.WriteLine($"Access JWT SHA256 {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Session.AccessJwt)))}");

                            sessionRefreshTimer.Interval = oldInterval;
                        }
                    }
                }
            }
        }
    }
}
