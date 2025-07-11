// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

using Microsoft.Extensions.Logging;

namespace Samples.Common
{
    /// <summary>
    /// Common helper classes for samples.
    /// </summary>
    public class Helpers
    {
        static readonly Option<string?> s_handleOption = new("--handle", "-u", "/u")
        {
            Description = "The handle to use when authenticating to the PDS.",
            DefaultValueFactory = defaultValue =>
            {
                const string environmentVariableName = "_BlueskyHandle";
                string? environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);

                if (string.IsNullOrWhiteSpace(environmentValue))
                {
                    return null;
                }

                return environmentValue;
            },
            Required = true
        };

        static readonly Option<string?> s_passwordOption = new("--password", "-p", "/p")
        {
            Description = "The password to use when authenticating to Bluesky.",
            DefaultValueFactory = defaultValue =>
            {
                const string environmentVariableName = "_BlueskyPassword";
                string? environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);

                if (string.IsNullOrWhiteSpace(environmentValue))
                {
                    return null;
                }

                return environmentValue;
            },
            Required = true,
        };

        static readonly Option<string?> s_authCodeOption = new("--authCode", "-a", "/a")
        {
            Description = "The authorization code for the account to used when authenticating to Bluesky"
        };

        static readonly Option<Uri?> s_proxyOption = new("--proxy")
        {
            Description = "The URI of a web proxy to use.",
            HelpName = "Uri"
        };

        /// <summary>
        /// Returns a command line parser configured with the common command line parameters,
        /// which will call <paramref name="runCode"/> when the <see cref="ParseResult"/>'s InvokeAsync is called.
        /// </summary>
        /// <param name="runCode">The function to run when InvokeAsync is called on the parser.</param>
        /// <returns>A preconfigured command line <see cref="ParseResult"/>.</returns>
        public static ParseResult ConfigureCommandLine(
            string[] args,
            string commandDescription,
            Func<string?, string?, string?,Uri?, CancellationToken, Task> runCode)
        {
            var rootCommand = new RootCommand(commandDescription)
            {
                s_handleOption,
                s_passwordOption,
                s_authCodeOption,
                s_proxyOption,
            };

            for (int i = 0; i < rootCommand.Options.Count; i++)
            {
                if (rootCommand.Options[i] is HelpOption defaultHelpOption)
                {
                    defaultHelpOption.Action = new CustomHelpHandlePasswordAction((HelpAction)defaultHelpOption.Action!);
                    break;
                }
            }

            rootCommand.SetAction((parseResult, cancellationToken) =>
            {
                 return runCode(
                    parseResult.GetValue(s_handleOption),
                    parseResult.GetValue(s_passwordOption),
                    parseResult.GetValue(s_authCodeOption),
                    parseResult.GetValue(s_proxyOption),
                    cancellationToken);
            });

            return rootCommand.Parse(args);
        }

        /// <summary>
        /// Returns a command line parser configured with the common command line parameters,
        /// which will call <paramref name="runCode"/> when the <see cref="ParseResult"/>'s InvokeAsync is called.
        /// </summary>
        /// <param name="runCode">The function to run when InvokeAsync is called on the parser.</param>
        /// <returns>A preconfigured command line <see cref="ParseResult"/>.</returns>
        public static ParseResult ConfigureCommandLine(
            string[] args,
            string commandDescription,
            Func<string?, Uri?, CancellationToken, Task> runCode)
        {
            var rootCommand = new RootCommand(commandDescription)
            {
                s_handleOption,
                s_proxyOption,
            };

            for (int i = 0; i < rootCommand.Options.Count; i++)
            {
                if (rootCommand.Options[i] is HelpOption defaultHelpOption)
                {
                    defaultHelpOption.Action = new CustomHelpHandlePasswordAction((HelpAction)defaultHelpOption.Action!);
                    break;
                }
            }

            rootCommand.SetAction((parseResult, cancellationToken) =>
            {
                return runCode(
                   parseResult.GetValue(s_handleOption),
                   parseResult.GetValue(s_proxyOption),
                   cancellationToken);
            });

            return rootCommand.Parse(args);
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

        internal sealed class CustomHelpHandlePasswordAction(HelpAction action) : SynchronousCommandLineAction
        {
            public override int Invoke(ParseResult parseResult)
            {
                int result = action.Invoke(parseResult);

                Console.WriteLine("If a handle is not specified the default value will be read from the _BlueskyHandle environment variable.");
                Console.WriteLine("If a password is not specified the default value will be read from the _BlueskyPassword environment variable.");

                return result;
            }
        }

        internal sealed class CustomHelpHandleOnlyAction(HelpAction action) : SynchronousCommandLineAction
        {
            public override int Invoke(ParseResult parseResult)
            {
                int result = action.Invoke(parseResult);

                Console.WriteLine("If a handle is not specified the default value will be read from the _BlueskyHandle environment variable.");

                return result;
            }
        }
    }
}
