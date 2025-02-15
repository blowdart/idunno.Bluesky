// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.AtProto.Server;

using Samples.Common;

namespace Samples.AtProto
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
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(null))

            // Create a new AtProtoAgent
            using (var agent = new AtProtoAgent(
                service: new Uri("https://bsky.social"),
                options: new AtProtoAgentOptions()
                {
                    LoggerFactory = loggerFactory,
                    HttpClientOptions = new HttpClientOptions()
                    {
                        ProxyUri = proxyUri,
                        CheckCertificateRevocationList = checkCertificateRevocationList
                    }
                }))
            {
                AtProtoHttpResult<ServerDescription> blueskyServerDescription = await agent.DescribeServer(new Uri("https://bsky.social"), cancellationToken);
                DescribeServer(blueskyServerDescription.Result!);

                var did = await agent.ResolveHandle(handle, cancellationToken);

                if (did is null)
                {
                    Console.WriteLine("Could not resolve DID");
                    return;
                }

                var pds = await agent.ResolvePds(did, cancellationToken);

                if (pds is null)
                {
                    Console.WriteLine($"Could not resolve PDS for {did}.");
                    return;
                }

                AtProtoHttpResult<ServerDescription> pdsDescriptionResult = await agent.DescribeServer(pds, cancellationToken);
                if (pdsDescriptionResult.Succeeded)
                {
                    DescribeServer(pdsDescriptionResult.Result!);
                }
                else
                {
                    Console.WriteLine($"Could get server description.");
                    return;
                }

                var repoDescriptionResult = await agent.DescribeRepo(did, cancellationToken: cancellationToken);
                if (repoDescriptionResult.Succeeded)
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

                if (listRecordsResult.Succeeded)
                {
                    foreach (var record in listRecordsResult.Result)
                    {
                        Console.WriteLine($"AT URI : {record.Uri}");
                        Console.WriteLine($"CID    : {record.Cid}");

                        if (record.Value is not null && record.Value.ExtensionData is not null)
                        {
                            Console.WriteLine($"Values  :");

                            foreach (var jsonKeyValue in record.Value.ExtensionData)
                            {
                                Console.WriteLine($"  {jsonKeyValue.Key} : {jsonKeyValue.Value}");
                            }
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
                        }
                    }
                }

                await agent.Logout(cancellationToken);
            }

            return;
        }

        static void DescribeServer(ServerDescription serverDescription)
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

        static void DescribeRepo(RepoDescription repoDescription)
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
