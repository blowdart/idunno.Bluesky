// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
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
            Console.OutputEncoding = Encoding.UTF8;

            var parser = Helpers.ConfigureCommandLine(PerformOperations);
            await parser.InvokeAsync(args);

            return 0;
        }

        static async Task PerformOperations(string? handle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(handle);
            ArgumentException.ThrowIfNullOrEmpty(password);

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
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Error))

            // Create a new BlueSkyAgent
            using (var agent = new BlueskyAgent(
                options: new BlueskyAgentOptions()
                {
                    LoggerFactory = loggerFactory,

                    HttpClientOptions = new HttpClientOptions()
                    {
                        CheckCertificateRevocationList = checkCertificateRevocationList,
                        ProxyUri = proxyUri
                    },
                }))
            {
                agent.Authenticated += (sender, e) =>
                {
                    Console.WriteLine($"EVENT: {e.AccessCredentials.Did} authenticated on {e.AccessCredentials.Service}");
                };

                agent.CredentialsUpdated += (sender, e) =>
                {
                    Console.WriteLine($"EVENT: Credentials updated for : {e.Did}");
                };

                agent.Unauthenticated += (sender, e) =>
                {
                    Console.WriteLine($"EVENT: {e.Did} unauthenticated on {e.Service}");
                };

                agent.TokenRefreshFailed += (sender, e) =>
                {
                    Console.WriteLine($"EVENT: Token Refresh failed API returned {e.StatusCode}");

                    if (e.Error is not null)
                    {
                        Console.WriteLine($"       {e.Error.Error}");

                        if (!string.IsNullOrEmpty(e.Error.Message))
                        {
                            Console.WriteLine($"       {e.Error.Message}");
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
                
                if (agent.IsAuthenticated)
                {
                    // First let's go manually

                    Console.WriteLine($"Authenticated : Access JWT SHA256 {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Credentials.AccessJwt)))}");

                    await agent.RefreshCredentials(cancellationToken);

                    Console.WriteLine($"Refreshed :     Access JWT SHA256 {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Credentials.AccessJwt)))}");

                    // OK now let's be horrible via reflection.
                    Type timer = typeof(System.Timers.Timer);
                    Type agentType = typeof(AtProtoAgent);
                    FieldInfo? credentialRefreshTimerField = agentType.GetField("_credentialRefreshTimer", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (credentialRefreshTimerField is not null)
                    {
                        var credentialRefreshTimerInstance = credentialRefreshTimerField.GetValue(agent);

                        if (credentialRefreshTimerInstance is not null)
                        {
                            var credentialRefreshTimer = (System.Timers.Timer)credentialRefreshTimerInstance;

                            var oldInterval = credentialRefreshTimer.Interval;

                            Console.WriteLine($"Refresh time is due to fire in {credentialRefreshTimer.Interval} milliseconds");

                            Console.WriteLine("Hacking the refresh timer to fire in 10 seconds");

                            credentialRefreshTimer.Interval = 10000;

                            Console.WriteLine("Waiting for 30 seconds, you should see refreshes happening via console logging...");

                            await Task.Delay(new TimeSpan(0, 0, 30), cancellationToken);

                            Console.WriteLine($"Final     :     Access JWT SHA256 {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(agent.Credentials.AccessJwt)))}");

                            credentialRefreshTimer.Interval = oldInterval;
                        }
                    }
                }
            }
        }
    }
}
