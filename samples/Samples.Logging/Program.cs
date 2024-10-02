// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Net;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Server;

namespace Samples.Logging
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
                getDefaultValue: () => Environment.GetEnvironmentVariable("_BlueskyUserName")!);

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

                returnCode = await DemonstrateLogging(
                    context.ParseResult.GetValueForOption(handleOption),
                    context.ParseResult.GetValueForOption(passwordOption),
                    context.ParseResult.GetValueForOption(proxyOption),
                    cancellationToken);

            });

            await parser.InvokeAsync(args);

            return returnCode;
        }

        static async Task<int> DemonstrateLogging(string? handle, string? password, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(handle);
            ArgumentNullException.ThrowIfNullOrEmpty(password);

            HttpClient? httpClient = null;

            if (proxyUri is not null)
            {
                var proxyClientHandler = new HttpClientHandler
                {
                    Proxy = new WebProxy
                    {
                        Address = proxyUri,
                        BypassProxyOnLocal = true,
                        UseDefaultCredentials = true
                    },
                    UseProxy = true,
                    CheckCertificateRevocationList = false
                };

                httpClient = new HttpClient(handler: proxyClientHandler, disposeHandler: true);
            }

            // Setup console logging, and configure the log level to Information
            using ILoggerFactory loggerFactory = LoggerFactory.Create(configure =>
            {
                configure.AddConsole();
                configure.SetMinimumLevel(LogLevel.Debug);
            });
            using var agentWithLogging = new AtProtoAgent(new("https://bsky.social"), loggerFactory: loggerFactory);
            {
                _ = await agentWithLogging.Login(new Credentials(handle, password), cancellationToken: cancellationToken).ConfigureAwait(false);
                await agentWithLogging.Logout(cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            // Now do it again without a logger to demonstrate no bad side effects
            using var agentWithoutLogging = new AtProtoAgent(new("https://bsky.social"));
            {
                _ = await agentWithoutLogging.Login(new Credentials(handle, password), cancellationToken: cancellationToken).ConfigureAwait(false);
                await agentWithoutLogging.Logout(cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return 0;
        }
    }
}
