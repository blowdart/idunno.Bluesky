// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.CommandLine.Help;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky;

namespace Samples.BulkDelete;

public sealed class Program
{
    static async Task<int> Main(string[] args)
    {
        // Necessary to render emojis.
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var handleOption = new Option<string?>(
            name: "--handle",
            description: "The handle to use when authenticating to the PDS.",
            getDefaultValue: () => Environment.GetEnvironmentVariable("_BlueskyHandle"));

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "The password to use when authenticating to Bluesky.",
            getDefaultValue: () => Environment.GetEnvironmentVariable("_BlueskyPassword")!);

        var authCodeOption = new Option<string?>(
            name: "--authCode",
            description: "The authorization code for the account to used when authenticating to Bluesky");

        var olderThanOption = new Option<double?>(
            name: "--olderThan",
            description: "Delete records older than the specified number of days.");

        var proxyOption = new Option<Uri?>(
            name: "--proxy",
            description: "The URI of a web proxy to use.")
        {
            ArgumentHelpName = "Uri"
        };

        var whatIfOption = new Option<bool>(
            name: "--whatIf",
            description: "If set to true deletions do not occur, output shows items that would be affected.",
            getDefaultValue: () => false);

        var rootCommand = new RootCommand("Delete Bluesky records.")
        {
            handleOption,
            passwordOption,
            authCodeOption,
            olderThanOption,
            proxyOption,
            whatIfOption
        };

        Parser parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseHelp(ctx =>
            {
                ctx.HelpBuilder.CustomizeSymbol(handleOption,
                    firstColumnText: "--handle <string>",
                    secondColumnText: "The handle to use when authenticating to Bluesky.\n" +
                                      "If a handle is not specified the handle will be\n" +
                                      "read from the _BlueskyHandle environment variable.");
                ctx.HelpBuilder.CustomizeSymbol(passwordOption,
                    firstColumnText: "--password <string>",
                    secondColumnText: "The password to use when authenticating to Bluesky.\n" +
                                      "If a password is not specified the password will be\n" +
                                      "read from the _BlueskyPassword environment variable.");
                ctx.HelpBuilder.CustomizeSymbol(olderThanOption,
                    firstColumnText: "--olderThan <days>",
                    secondColumnText: "Limits record deletion to records under than the " +
                                      "specified number of days.");
                   
            })
            .Build();

        int returnCode = 0;

        rootCommand.SetHandler(async (context) =>
        {
            CancellationToken cancellationToken = context.GetCancellationToken();

            returnCode = await DeleteRecords(
                context.ParseResult.GetValueForOption(handleOption),
                context.ParseResult.GetValueForOption(passwordOption),
                context.ParseResult.GetValueForOption(authCodeOption),
                context.ParseResult.GetValueForOption(olderThanOption),
                context.ParseResult.GetValueForOption(proxyOption),
                context.ParseResult.GetValueForOption(whatIfOption),
                cancellationToken);
        });

        await parser.InvokeAsync(args);

        return returnCode;
    }

    static async Task<int> DeleteRecords(
        string? handle,
        string? password,
        string? authCode,
        double? olderThan,
        Uri? proxyUri,
        bool whatIf,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(handle);
        ArgumentNullException.ThrowIfNullOrEmpty(password);

        DateTimeOffset runStartedAt = DateTimeOffset.UtcNow;

        IReadOnlyDictionary<Nsid, string> feedCollections = new Dictionary<Nsid, string>()
        {
            { "app.bsky.feed.post", "Posts" },
            { "app.bsky.feed.like", "Likes" },
            { "app.bsky.feed.repost", "Reposts"},
            { "app.bsky.feed.threadgate", "Thread Gates" },
            { "app.bsky.feed.postgate", "Post Gates"}
        };

        using (HttpClient? httpClient = CreateOptionalHttpClient(proxyUri))
        using (ILoggerFactory? loggerFactory = ConfigureConsoleLogging(LogLevel.Error))
        using (var agent = new BlueskyAgent(httpClient: httpClient, loggerFactory: loggerFactory))
        {
            AtProtoHttpResult<bool> loginResult =
                await agent.Login(handle, password!, authCode, cancellationToken: cancellationToken);
            if (!loginResult.Succeeded &&
                loginResult.AtErrorDetail is not null &&
                string.Equals(loginResult.AtErrorDetail.Error!, "AuthFactorTokenRequired", StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleColor oldColor = Console.ForegroundColor;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Login requires an authentication code.");
                    Console.WriteLine("Check your email and use --authCode to specify the authentication code.");
                    Console.ForegroundColor = oldColor;

                    return 1;
                }

            if (!loginResult.Succeeded)
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

                    return 2;
                }
            }

            long totalRecordsDeleted = 0;

            foreach (Nsid collection in feedCollections.Keys)
            {
                Console.WriteLine($"Processing {feedCollections[collection]}.\n");

                long collectionRecordsProcessed = 0;
                int recordLimit = 10;

                AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRecord>> listRecordsResult =
                    await agent.ListRecords<AtProtoRecord>(collection, recordLimit, cancellationToken: cancellationToken).ConfigureAwait(false);

                do
                {
                    if (listRecordsResult.Succeeded)
                    {
                        Console.Write($"Processing {collection} records {collectionRecordsProcessed + 1} - ");
                        Console.WriteLine($"{collectionRecordsProcessed + listRecordsResult.Result.Count}");

                        foreach (AtProtoRecord record in listRecordsResult.Result)
                        {
                            const string createdAtKey = "createdAt";
                            const string textKey = "text";

                            if (record.Value is not null &&
                                record.Value.ExtensionData is not null &&
                                record.Value.ExtensionData.ContainsKey(createdAtKey))
                            {
                                if (DateTimeOffset.TryParse(record.Value.ExtensionData[createdAtKey].ToString(), out DateTimeOffset createdAt))
                                {
                                    if (!whatIf)
                                    {
                                        if (olderThan is null || createdAt < runStartedAt.AddDays((double)-olderThan))
                                        {
                                            Console.WriteLine($"Deleting {record.StrongReference.Uri} {createdAt:G}.");
                                            var deleteResult = await agent.DeleteRecord(
                                                    collection,
                                                    record.Uri.RecordKey!,
                                                    cancellationToken: cancellationToken).ConfigureAwait(false);

                                            if (!deleteResult.Succeeded)
                                            {
                                                ConsoleColor revertToColor = Console.ForegroundColor;
                                                Console.ForegroundColor = ConsoleColor.Red;
                                                Console.WriteLine($"Deletion of {record.StrongReference.Uri} failed.");
                                                Console.ForegroundColor = revertToColor;
                                            }
                                            else
                                            {
                                                totalRecordsDeleted++;
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Skipping {record.StrongReference.Uri} {createdAt:G} - not old enough.");
                                        }
                                    }
                                    else
                                    {
                                        if (olderThan is null || createdAt < runStartedAt.AddDays((double)-olderThan))
                                        {
                                            Console.WriteLine($"Would delete {record.StrongReference.Uri} {createdAt:G}.");
                                            totalRecordsDeleted++;
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Would skip {record.StrongReference.Uri} {createdAt:G} - not old enough.");
                                        }
                                    }

                                    if (record.Value.ExtensionData.TryGetValue(textKey, out JsonElement value) &&
                                        !string.IsNullOrEmpty(value.ToString()))
                                    {
                                        Console.WriteLine($"{value}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Skipped {record.StrongReference} - no {createdAtKey} value.");
                                }
                            }

                            collectionRecordsProcessed++;
                        }

                        listRecordsResult = await agent.ListRecords<AtProtoRecord>(collection, recordLimit, listRecordsResult.Result.Cursor, cancellationToken: cancellationToken);
                    }
                } while (listRecordsResult.Succeeded && !string.IsNullOrEmpty(listRecordsResult.Result.Cursor));

                if (collectionRecordsProcessed == 0)
                {
                    Console.WriteLine($"No records found.");
                }

                Console.WriteLine();
            }

            if (!whatIf)
            {
                Console.WriteLine($"{totalRecordsDeleted} records deleted.");
            }
            else
            {
                Console.WriteLine($"{totalRecordsDeleted} records would be deleted.");
            }
        }

        return 0;
    }

    public static ILoggerFactory? ConfigureConsoleLogging(LogLevel? level)
    {
        if (level is null)
        {
            return null;
        }

        return LoggerFactory.Create(configure =>
        {
            configure.AddConsole();
            configure.SetMinimumLevel((LogLevel)level);
        });
    }

    public static HttpClient? CreateOptionalHttpClient(Uri? proxyUri)
    {
        if (proxyUri is not null)
        {
            return Agent.CreateConfiguredHttpClient(proxyUri);
        }
        else
        {
            return null;
        }
    }
}
