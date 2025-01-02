// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Net;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Server;

using Samples.Common;

namespace Samples.SessionEvents
{
    public sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var handleOption = new Option<string>(
                name: "--handle",
                description: "The handle to use when authenticating to the PDS.",
                getDefaultValue: () => Environment.GetEnvironmentVariable("_BlueskyHandle")!);

            var passwordOption = new Option<string>(
                name: "--password",
                description: "The password to use when authenticating to the PDS.",
                getDefaultValue: () => Environment.GetEnvironmentVariable("_BlueskyPassword")!);

            var proxyOption = new Option<Uri?>(
                name: "--proxy",
                description: "The URI of a web proxy to use.")
            {
                ArgumentHelpName = "Uri"
            };

            var rootCommand = new RootCommand("")
            {
                handleOption,
                passwordOption,
                proxyOption
            };

            Parser parser = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseHelp(ctx =>
                {
                    ctx.HelpBuilder.CustomizeSymbol(handleOption,
                        firstColumnText: "--userName <string>",
                        secondColumnText: "The username to use when authenticating to Bluesky.\n" +
                                        "If a username is not specified the username will be\n" +
                                        "read from the _BlueskyUserName environment variable.");
                    ctx.HelpBuilder.CustomizeSymbol(passwordOption,
                        firstColumnText: "--password <string>",
                        secondColumnText: "The password to use when authenticating to Bluesky.\n" +
                                        "If a password is not specified the password will be\n" +
                                        "read from the _BlueskyPassword environment variable.");
                })
                .Build();

            int returnCode = 0;

            rootCommand.SetHandler(async (context) =>
            {
                CancellationToken cancellationToken = context.GetCancellationToken();

                returnCode = await DemonstrateAgentEvents(
                    context.ParseResult.GetValueForOption(handleOption),
                    context.ParseResult.GetValueForOption(passwordOption),
                    context.ParseResult.GetValueForOption(proxyOption),
                    cancellationToken);

            });

            await parser.InvokeAsync(args);

            return returnCode;
        }

        static async Task<int> DemonstrateAgentEvents(string? handle, string? password, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(handle);
            ArgumentNullException.ThrowIfNullOrEmpty(password);

            // This approximates a token store for the purposes of the sample.
            TokenStore? persistedLoginState = null;

            // Uncomment the next line to route all requests through Fiddler Everywhere
            proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // Get an HttpClient configured to use a proxy, if proxyUri is not null.
            using (HttpClient? httpClient = Helpers.CreateOptionalHttpClient(proxyUri))

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

            using (var agent = new AtProtoAgent(new Uri("https://bsky.social"), httpClient, loggerFactory: loggerFactory))
            {
                agent.SessionCreated += (sender, e) =>
                {
                    // Here you would save the DID, refresh token and service to the appropriate secure
                    // storage location for your platform (Windows Credential store for Windows,
                    // Keyring for Apple platforms, and for Linux the user's profile directory and hope
                    // the permissions are correct.
                    //
                    // Saving the access token is optional.
                    //
                    // When your app restarts you can then use the RestoreSession() method on the Agent
                    // to try to restore the session you had.
                    //
                    // If you save the access token RestoreSession() will try to use that first, before
                    // falling back to using the refresh token to create a new authenticated session.
                    persistedLoginState = new TokenStore(e.Did, e.AccessJwt, e.RefreshJwt, e.Service!);

                    Console.WriteLine($"EVENT: Session created for : {e.Handle} ({e.Did}) on {e.Service}");
                };

                agent.SessionRefreshed += (sender, e) =>
                {
                    // Here you update your saved state. Saving the access token is optional.
                    //
                    // When your app restarts you can then use the RestoreSession() method on the Agent
                    // to try to restore the session you had.
                    //
                    // If you save the access token RestoreSession() will try to use that first, before
                    // falling back to using the refresh token to create a new authenticated session.
                    persistedLoginState = new TokenStore(e.Did, e.AccessJwt, e.RefreshJwt, e.Service!);

                    Console.WriteLine($"EVENT: Session refreshed for : {e.Did}");
                };

                agent.SessionEnded += (sender, e) =>
                {
                    // Here you would clear any saved authentication state for the DID.
                    persistedLoginState = null;

                    Console.WriteLine($"EVENT: Logged out from {e.Service}: {e.Did}");
                };

                agent.SessionRefreshFailed += (sender, e) =>
                {
                    // Here you would clear any saved authentication state for the DID because the session
                    // could not refresh and so any tokens you saved will be invalid.
                    persistedLoginState = null;


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

                Console.WriteLine("Logging in");

                var loginResult = await agent.Login(new Credentials(handle, password), cancellationToken: cancellationToken).ConfigureAwait(false);

                Console.WriteLine($"Login result: {loginResult.StatusCode}");

                Debugger.Break();

                Console.WriteLine("Refreshing sessions manually");

                var refreshSessionResult = await agent.RefreshSession(cancellationToken: cancellationToken);

                Console.WriteLine($"Refresh session result: {refreshSessionResult}");

                Debugger.Break();

                Console.WriteLine("Logging out");

                await agent.Logout(cancellationToken: cancellationToken);

                Debugger.Break();

                Console.WriteLine("Refreshing the session once the agent has logged out.");

                // We know this will throw an exception.
                try
                {
                    _ = await agent.RefreshSession(cancellationToken: cancellationToken);
                }
                catch (InvalidSessionException e)
                {
                    Console.WriteLine($"Exception Thrown : {e.Message}");
                }

                // Login again
                _ = await agent.Login(new Credentials(handle, password), cancellationToken: cancellationToken).ConfigureAwait(false);

                // Now mess with the refresh token to cause the refresh call to fail
                if (agent.Session is not null)
                {
                    try
                    {
                        await agent.SetTokens("invalid", "invalid", cancellationToken: cancellationToken);

                        Console.WriteLine("Refreshing the session with an invalid refresh token.");
                        try
                        {
                            _ = await agent.RefreshSession(cancellationToken: cancellationToken);
                        }
                        catch (InvalidSessionException e)
                        {
                            Console.WriteLine($"Exception Thrown : {e.Message}");
                        }
                    }
                    catch (SecurityTokenValidationException e)
                    {
                        Console.WriteLine($"Exception Thrown : {e.Message}");
                    }
                }

                // Now bring back the agent session using the stored state
                if (persistedLoginState is not null)
                {
                    // Note that we're passing in the service URL from the saved token, rather than
                    // allowing the agent to use the default Bluesky servers.

                    // Try an access token first, in case it's still valid. This is optional, you can just
                    // go straight to using the stored refresh token.
                    using (var restoredFromAccessToken = new AtProtoAgent(persistedLoginState.Service, httpClient))
                    {
                        Console.WriteLine("Restoring the session from an access token.");

                        bool restoreResult = await restoredFromAccessToken.ResumeSession(
                            persistedLoginState.Did,
                            persistedLoginState.AccessToken,
                            persistedLoginState.RefreshToken,
                            persistedLoginState.Service,
                            cancellationToken);

                        if (!restoreResult)
                        {
                            Console.WriteLine($"Restore failed.");
                        }

                        Debugger.Break();
                    }

                    // Try to restore a session using the refresh token.
                    using (var restoredFromRefreshToken = new AtProtoAgent(persistedLoginState.Service, httpClient))
                    {
                        Console.WriteLine("Restoring the session from an refresh token.");

                        bool restoreResult = await restoredFromRefreshToken.ResumeSession(
                            persistedLoginState.Did,
                            persistedLoginState.RefreshToken,
                            persistedLoginState.Service,
                            cancellationToken);

                        if (!restoreResult)
                        {
                            Console.WriteLine($"Restore failed.");
                        }
                    }
                }
            }

            return 0;
        }
    }

    /// <summary>
    /// An in-memory store for session information.
    /// </summary>
    sealed record TokenStore
    {
        public TokenStore(Did did, string accessToken, string refreshToken, Uri service)
        {
            Did = did;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            Service = service;
        }

        /// <summary>
        /// The <see cref="Did"/> the tokens belong to.
        /// </summary>
        public Did Did { get; set; }

        /// <summary>
        /// The access token for the current session.
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// A refresh token that is used to update the current session, or to
        /// create a new session.
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// The <see cref="Uri"/> of the service that issued the tokens.
        /// </summary>
        public Uri Service { get; set; }
    }
}
