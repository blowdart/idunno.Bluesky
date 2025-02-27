﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

using Microsoft.Extensions.Logging;

using idunno.AtProto;

using Samples.Common;

namespace Samples.LoginDiscovery
{
   public sealed class Program
    {
        static async Task<int> Main(string[] args)
        {

            // Necessary to render emojis.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var attemptLoginOption = new Option<bool>(
                name: "--login",
                description: "Attempt to perform a login on the discovered PDS.",
                getDefaultValue: () => false);

            var handleOption = new Option<string>(
                name: "--handle",
                description: "The handle to use when authenticating to the PDS.",
                getDefaultValue: () => Environment.GetEnvironmentVariable("_BlueskyHandle")!);

            var passwordOption = new Option<string>(
                name: "--password",
                description: "The password to use when authenticating to the PDS.",
                getDefaultValue: () => Environment.GetEnvironmentVariable("_BlueskyPassword")!);

            var authCodeOption = new Option<string?>(
                name: "--authCode",
                description: "The authorization code for the account to used when authenticating to the PDS");

            var proxyOption = new Option<Uri?>(
                name: "--proxy",
                description: "The URI of a web proxy to use.")
            {
                ArgumentHelpName = "Uri"
            };

            var rootCommand = new RootCommand("Discover Bluesky Login PDS.")
            {
                attemptLoginOption,
                handleOption,
                passwordOption,
                authCodeOption,
                proxyOption,
            };

            Parser parser = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseHelp(ctx =>
                {
                    ctx.HelpBuilder.CustomizeSymbol(handleOption,
                        firstColumnText: "--handle <string>",
                        secondColumnText: "The handle to use when authenticating to Bluesky.\n" +
                                        "If a handle is not specified the username will be\n" +
                                        "read from the _BlueskyHandle environment variable.");
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

                returnCode = await ResolveHandleAndLogin(
                    context.ParseResult.GetValueForOption(attemptLoginOption),
                    context.ParseResult.GetValueForOption(handleOption),
                    context.ParseResult.GetValueForOption(passwordOption),
                    context.ParseResult.GetValueForOption(authCodeOption),
                    context.ParseResult.GetValueForOption(proxyOption),
                    cancellationToken);
            });

            await parser.InvokeAsync(args);

            return returnCode;
        }

        static async Task<int> ResolveHandleAndLogin(bool attemptLogin, string? handle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
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
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

            // The Login APIs on the BlueskyAgent do all this for you behind the scenes, this sample just shows the steps.
            using (var agent = new AtProtoAgent(new Uri("https://bsky.social"),
                    new AtProtoAgentOptions()
                    {
                        LoggerFactory = loggerFactory,
                        HttpClientOptions = new HttpClientOptions()
                        {
                            ProxyUri = proxyUri,
                            CheckCertificateRevocationList = checkCertificateRevocationList,
                        }
                    }))
            {
                var did = await agent.ResolveHandle(handle, CancellationToken.None);

                if (did is null)
                {
                    Console.WriteLine("Could not resolve DID");
                    return 1;
                }

                var pds = await agent.ResolvePds(did, CancellationToken.None);

                if (pds is null)
                {
                    Console.WriteLine($"Could not resolve PDS for {did}.");
                    return 1;
                }

                var authorizationServer = await agent.ResolveAuthorizationServer(pds, CancellationToken.None);

                if (authorizationServer is null)
                {
                    Console.WriteLine($"Could not discover authorization server for {pds}.");
                    return 1;
                }

                Console.WriteLine($"Handle :     {handle}");
                Console.WriteLine($"Handle DID : {did}");
                Console.WriteLine($"PDS :        {pds}");
                Console.WriteLine();

                if (attemptLogin)
                {
                    Console.WriteLine($"Attempting CreateSession on {pds}.");

                    AtProtoHttpResult<bool> loginResult = await agent.Login(handle, password, authCode, cancellationToken: cancellationToken);

                    if (!loginResult.Succeeded &&
                        string.Equals(loginResult.AtErrorDetail?.Error, "AuthFactorTokenRequired", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Account requires an email authentication code.");
                        Console.WriteLine("Once the code has arrived rerun and use the --authCode parameter to specify it.");

                        return 2;
                    }

                    if (!loginResult.Succeeded)
                    {
                        Console.WriteLine("Login failed");
                        Console.WriteLine($"HTTP Status: {loginResult.StatusCode}.");

                        if (loginResult.AtErrorDetail is not null)
                        {
                            if (loginResult.AtErrorDetail.Error is not null)
                            {
                                Console.WriteLine($"Error :      {loginResult.AtErrorDetail.Error}");
                            }

                            if (loginResult.AtErrorDetail.Message is not null)
                            {
                                Console.WriteLine($"Message :    {loginResult.AtErrorDetail.Message}");
                            }
                        }
                    }

                    if (agent.Credentials is null)
                    {
                        Console.WriteLine("Login worked, but a credentials ended up as null.");
                        return 4;
                    }

                    Console.WriteLine($"Login successful, ended up on {agent.Credentials.Service}.");

                    await agent.Logout(cancellationToken: cancellationToken);
                }
                else
                {
                    Console.WriteLine("Login skipped.");
                }

                return 0;
            }
        }
    }
}
