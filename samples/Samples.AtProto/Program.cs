// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.AtProto.Server;

namespace Samples.AtProto
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

            var authCodeOption = new Option<string?>(
                name: "--authCode",
                description: "The authorization code for the account to used when authenticating to the PDS");

            var proxyOption = new Option<Uri?>(
                name: "--proxy",
                description: "The URI of a web proxy to use.")
            {
                ArgumentHelpName = "Uri"
            };

            var rootCommand = new RootCommand("Perform various AT Proto operations.")
            {
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

                returnCode = await PerformOperations(
                    context.ParseResult.GetValueForOption(handleOption),
                    context.ParseResult.GetValueForOption(passwordOption),
                    context.ParseResult.GetValueForOption(authCodeOption),
                    context.ParseResult.GetValueForOption(proxyOption),
                    cancellationToken);
            });

            await parser.InvokeAsync(args);

            return returnCode;
        }

        static async Task<int> PerformOperations(string? handle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(handle);
            ArgumentNullException.ThrowIfNullOrEmpty(password);

            // Route through Fiddler Everywhere
            proxyUri = new Uri("http://localhost:8866");

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

            using (var agent = new AtProtoAgent(new Uri("https://bsky.social"), httpClient))
            {
                AtProtoHttpResult<ServerDescription> blueskyServerDescription = await agent.DescribeServer(new Uri("https://bsky.social"), cancellationToken);
                DescribeServer(blueskyServerDescription.Result!);

                var did = await agent.ResolveHandle(handle, cancellationToken);

                if (did is null)
                {
                    Console.WriteLine("Could not resolve DID");
                    return 1;
                }

                var pds = await agent.ResolvePds(did, cancellationToken);

                if (pds is null)
                {
                    Console.WriteLine($"Could not resolve PDS for {did}.");
                    return 2;
                }

                AtProtoHttpResult<ServerDescription> pdsDescriptionResult = await agent.DescribeServer(pds, cancellationToken);
                if (pdsDescriptionResult.SucceededWithResult)
                {
                    DescribeServer(pdsDescriptionResult.Result!);
                }
                else
                {
                    Console.WriteLine($"Could get server description.");
                    return 3;
                }

                var repoDescriptionResult = await agent.DescribeRepo(did, cancellationToken: cancellationToken);
                if (repoDescriptionResult.SucceededWithResult)
                {
                    Console.WriteLine("Anonymous repo description:");
                    DescribeRepo(repoDescriptionResult.Result);
                }

                string? cursor = null;

                var listRecordsResult = await agent.ListRecords<AtProtoRecord> (
                    did,
                    collection: "app.bsky.feed.post",
                    cursor: cursor,
                    service : pds,
                    cancellationToken: cancellationToken);

                if (listRecordsResult.SucceededWithResult)
                {
                    foreach (var record in listRecordsResult.Result)
                    {
                        Console.WriteLine($"AT URI : {record.Uri}");
                        Console.WriteLine($"CID    : {record.Cid}");

                        if (record.Value is not null)
                        {
                            Console.WriteLine($"Value  :");
                            Console.WriteLine($"* Type : {record.Value.Type}");
                            Console.WriteLine($"* Date : {record.Value.CreatedAt.LocalDateTime}");
                        }
                    }
                }

                var loggedIn = await agent.Login(handle, password, authCode, cancellationToken: cancellationToken);
                if (!loggedIn.Succeeded)
                {
                    if (loggedIn.AtErrorDetail is not null &&
                        string.Equals(loggedIn.AtErrorDetail.Error!, "AuthFactorTokenRequired", StringComparison.OrdinalIgnoreCase))
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Login requires an authentication code.");
                        Console.WriteLine("Check your email and use --authCode to specify the authentication code.");
                        Console.ForegroundColor = oldColor;

                        return 3;
                    }
                    else
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Login failed.");
                        Console.ForegroundColor = oldColor;

                        if (loggedIn.AtErrorDetail is not null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;

                            Console.WriteLine($"Server returned {loggedIn.AtErrorDetail.Error} / {loggedIn.AtErrorDetail.Message}");
                            Console.ForegroundColor = oldColor;

                            return 4;
                        }
                    }
                }

                await agent.Logout(cancellationToken);
            }

            httpClient?.Dispose();

            return 0;
        }

        public static void DescribeServer(ServerDescription serverDescription)
        {
            Console.WriteLine($"Server DID   : {serverDescription.Did}");

            if (serverDescription.AvailableUserDomains.Any())
            {
                Console.WriteLine($"User Domains : ");
                foreach (var userDomain in serverDescription.AvailableUserDomains)
                {
                    Console.WriteLine($"  * {userDomain}");
                }
            }
            else
            {
                Console.WriteLine("No user domains available for registration.");
            }

            if (serverDescription.Links is not null)
            {
                Console.WriteLine($"Links        : ");
                if (serverDescription.Links.PrivacyPolicy is not null)
                {
                    Console.WriteLine($"  * Privacy Policy   : {serverDescription.Links.PrivacyPolicy}");
                }
                if (serverDescription.Links.TermsOfService is not null)
                {
                    Console.WriteLine($"  * Terms of Service : {serverDescription.Links.TermsOfService}");
                }
            }

            if (serverDescription.Contact is not null)
            {
                Console.WriteLine($"Contact      : {serverDescription.Contact}");
            }

            if (serverDescription.InviteCodeRequired)
            {
                Console.WriteLine("Invite code required for registration.");
            }

            if (serverDescription.PhoneVerificationRequired)
            {
                Console.WriteLine("Phone verification required for registration.");
            }

            Console.WriteLine();
        }

        public static void DescribeRepo(RepoDescription repoDescription)
        {
            Console.WriteLine($"Repo        : {repoDescription.Did}");
            Console.WriteLine($"Handle      : {repoDescription.Handle}");
            Console.WriteLine($"Did         : {repoDescription.Did}");

            if (repoDescription.Collections.Count != 0)
            {
                Console.WriteLine("Collections :");

                foreach (var collection in repoDescription.Collections)
                {
                    Console.WriteLine($"  * : {collection}");
                }
            }

            if (!repoDescription.HandleIsCorrect)
            {
                Console.WriteLine("Handle does not bi-directionally resolve.");
            }

            Console.WriteLine();
        }
    }
}
