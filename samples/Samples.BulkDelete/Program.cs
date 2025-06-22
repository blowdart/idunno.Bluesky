// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
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

        var olderThanOption = new Option<double?>("--olderThan", "-o", "/o")
        {
            Description = "Delete records older than the specified number of days."
        };
        olderThanOption.Validators.Add(result =>
        {
            if (result.GetValue(olderThanOption) < 1)
            {
                result.AddError("Must be greater than 0");
            }
        });

        var proxyOption = new Option<Uri?>("--proxy")
        {
            Description = "The URI of a web proxy to use.",
            HelpName = "Uri"
        };

        var whatIfOption = new Option<bool>("--whatIf", "-w", "/w")
        {
            Description = "If set to true deletions do not occur, output shows items that would be affected.",
            DefaultValueFactory = defaultValue => false
        };

        var rootCommand = new RootCommand("Delete Bluesky records.")
        {
            handleOption,
            passwordOption,
            authCodeOption,
            olderThanOption,
            proxyOption,
            whatIfOption
        };

        for (int i = 0; i < rootCommand.Options.Count; i++)
        {
            if (rootCommand.Options[i] is HelpOption defaultHelpOption)
            {
                defaultHelpOption.Action = new CustomHelpAction((HelpAction)defaultHelpOption.Action!);
                break;
            }
        }

        rootCommand.SetAction((parseResult, cancellationToken) =>
        {
            return DeleteRecords(
                parseResult.GetValue(handleOption),
                parseResult.GetValue(passwordOption),
                parseResult.GetValue(authCodeOption),
                parseResult.GetValue(olderThanOption),
                parseResult.GetValue(proxyOption),
                parseResult.GetValue(whatIfOption),
                cancellationToken);
        });

        ParseResult parseResult = rootCommand.Parse(args);

        return await parseResult.InvokeAsync();
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
        ArgumentException.ThrowIfNullOrEmpty(handle);
        ArgumentException.ThrowIfNullOrEmpty(password);

        DateTimeOffset runStartedAt = DateTimeOffset.UtcNow;

        IReadOnlyDictionary<Nsid, string> feedCollections = new Dictionary<Nsid, string>()
        {
            { "app.bsky.feed.post", "Posts" },
            { "app.bsky.feed.like", "Likes" },
            { "app.bsky.feed.repost", "Reposts"},
            { "app.bsky.feed.threadgate", "Thread Gates" },
            { "app.bsky.feed.postgate", "Post Gates"}
        };

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

        using (ILoggerFactory? loggerFactory = ConfigureConsoleLogging(LogLevel.Error))
        using (var agent = new BlueskyAgent(
          new BlueskyAgentOptions()
          {
              LoggerFactory = loggerFactory,

              HttpClientOptions = new HttpClientOptions()
              {
                  CheckCertificateRevocationList = checkCertificateRevocationList,
                  ProxyUri = proxyUri
              }
          }))
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

                AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<AtProtoRecord>>> listRecordsResult =
                    await agent.ListRecords<AtProtoRecord>(collection, recordLimit, cancellationToken: cancellationToken).ConfigureAwait(false);

                do
                {
                    if (listRecordsResult.Succeeded && !cancellationToken.IsCancellationRequested)
                    {
                        Console.Write($"Processing {collection} records {collectionRecordsProcessed + 1} - ");
                        Console.WriteLine($"{collectionRecordsProcessed + listRecordsResult.Result.Count}");

                        foreach (AtProtoRepositoryRecord<AtProtoRecord> record in listRecordsResult.Result)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

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

                        if (!cancellationToken.IsCancellationRequested && !string.IsNullOrEmpty(listRecordsResult.Result.Cursor))
                        {
                            listRecordsResult = await agent.ListRecords<AtProtoRecord>(collection, recordLimit, listRecordsResult.Result.Cursor, cancellationToken: cancellationToken);
                        }
                    }
                } while (!cancellationToken.IsCancellationRequested &&
                         listRecordsResult.Succeeded &&
                         !string.IsNullOrEmpty(listRecordsResult.Result.Cursor));

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

    internal sealed class CustomHelpAction(HelpAction action) : SynchronousCommandLineAction
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
