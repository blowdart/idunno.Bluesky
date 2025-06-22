// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

using Microsoft.Extensions.Logging;

using Samples.Common;

using idunno.AtProto;


namespace Samples.LoginDiscovery
{
   public sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var attemptLoginOption = new Option<bool>("--login", "-l", "/l")
            {
                Description = "Attempt to perform a login on the discovered PDS.",
                DefaultValueFactory = defaultValue => false
            };

            var handleOption = new Option<string?>("--handle", "-u", "/u")
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

            var passwordOption = new Option<string?>("--password", "-p", "/p")
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

            var authCodeOption = new Option<string?>("--authCode", "-a", "/a")
            {
                Description = "The authorization code for the account to used when authenticating to Bluesky"
            };

            var proxyOption = new Option<Uri?>("--proxy")
            {
                Description = "The URI of a web proxy to use.",
                HelpName = "Uri"
            };

            var rootCommand = new RootCommand("Demonstrate how Bluesky Login discovery works.")
            {
                attemptLoginOption,
                handleOption,
                passwordOption,
                authCodeOption,
                proxyOption,
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
                return ResolveHandleAndLogin(
                    parseResult.GetValue(attemptLoginOption),
                    parseResult.GetValue(handleOption),
                    parseResult.GetValue(passwordOption),
                    parseResult.GetValue(authCodeOption),
                    parseResult.GetValue(proxyOption),
                    cancellationToken);
            });

            var parseResult = rootCommand.Parse(args);

            return await parseResult.InvokeAsync();
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
    }
}
