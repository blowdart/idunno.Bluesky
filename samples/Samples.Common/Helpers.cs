// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;

using Microsoft.Extensions.Logging;

namespace Samples.Common
{
    /// <summary>
    /// Common helper classes for samples.
    /// </summary>
    public class Helpers
    {
        static readonly Option<string?> s_handleOption = new(
                name: "--handle",
                description: "The handle to use when authenticating to the PDS.",
                getDefaultValue: () => Environment.GetEnvironmentVariable("_BlueskyHandle"));

        static readonly Option<string?> s_passwordOption = new(
            name: "--password",
            description: "The password to use when authenticating to the PDS.",
            getDefaultValue: () => Environment.GetEnvironmentVariable("_BlueskyPassword")!);

        static readonly Option<string?> s_authCodeOption = new(
            name: "--authCode",
            description: "The authorization code for the account to used when authenticating to the PDS");

        static readonly Option<Uri?> s_proxyOption = new(
            name: "--proxy",
            description: "The URI of a web proxy to use.")
        {
            ArgumentHelpName = "Uri"
        };

        /// <summary>
        /// Returns a command line parser configured with the common command line parameters,
        /// which will call <paramref name="runCode"/> when the <see cref="Parser"/>'s InvokeAsync is called.
        /// </summary>
        /// <param name="runCode">The function to run when InvokeAsync is called on the parser.</param>
        /// <returns>A preconfigured command line <see cref="Parser"/>.</returns>
        public static Parser ConfigureCommandLine(Func<string?, string?, string?,Uri?, CancellationToken, Task> runCode)
        {
            var rootCommand = new RootCommand("Perform various AT Proto operations.")
            {
                s_handleOption,
                s_passwordOption,
                s_authCodeOption,
                s_proxyOption,
            };

            Parser parser = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseHelp(ctx =>
                {
                    ctx.HelpBuilder.CustomizeSymbol(s_handleOption,
                        firstColumnText: "--handle <string>",
                        secondColumnText: "The handle to use when authenticating to Bluesky.\n" +
                                        "If a username is not specified the username will be\n" +
                                        "read from the _BlueskyHandle environment variable.");
                    ctx.HelpBuilder.CustomizeSymbol(s_passwordOption,
                        firstColumnText: "--password <string>",
                        secondColumnText: "The password to use when authenticating to Bluesky.\n" +
                                        "If a password is not specified the password will be\n" +
                                        "read from the _BlueskyPassword environment variable.");
                })
            .Build();

            rootCommand.SetHandler(async (context) =>
            {
                CancellationToken cancellationToken = context.GetCancellationToken();

                 await runCode(
                    context.ParseResult.GetValueForOption(s_handleOption),
                    context.ParseResult.GetValueForOption(s_passwordOption),
                    context.ParseResult.GetValueForOption(s_authCodeOption),
                    context.ParseResult.GetValueForOption(s_proxyOption),
                    cancellationToken);
            });

            return parser;
        }

        /// <summary>
        /// Adds a console logger with the specified <paramref name="level"/>.
        /// </summary>
        /// <param name="level">The <see cref="LogLevel"/> to configure the logger with.</param>
        /// <returns>An <see cref="ILoggerFactory"/> configured to log to the console at the specified <paramref name="level"/></returns>
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
