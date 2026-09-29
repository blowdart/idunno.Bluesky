// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Firehose;
using idunno.AtProto.Sync;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Samples.Firehose;

public sealed class Program
{
    static async Task<int> Main()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(configure =>
        {
            configure.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "G";
                options.UseUtcTimestamp = false;
            });
            configure.SetMinimumLevel(LogLevel.Debug);
        });

        using CancellationTokenSource cancellationTokenSource = new();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };
        Console.OutputEncoding = Encoding.UTF8;

        using var didHandleCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        await using var firehose = new AtProtoFirehose(
            options: new FirehoseOptions
            {
                LoggerFactory = loggerFactory
            });

        const int maximumRetries = 5;
        long? cursor = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await foreach (FirehoseEvent evt in firehose.SubscribeReposAsync(
                        cursor: cursor, maximumReconnectAttempts: maximumRetries, cancellationToken: cancellationToken))
                    {
                        await PrintEventAsync(evt, didHandleCache, loggerFactory, cancellationToken).ConfigureAwait(false);

                        if (evt.Sequence is long sequence)
                        {
                            cursor = sequence;
                        }
                    }
                }
                catch (FirehoseConnectionException ex) when (ex.ErrorDetail?.Error == "FutureCursor")
                {
                    Console.WriteLine("RECONNECT : Cursor is ahead of the relay; resuming from now.");
                    cursor = null;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"RECONNECT : Gave up after {maximumRetries} attempts: {ex.Message}");
            return 1;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"PROTOCOL  : The relay broke the event stream protocol: {ex.Message}");
            return 1;
        }

        return 0;
    }

    private static async Task PrintEventAsync(
        FirehoseEvent evt,
        MemoryCache didHandleCache,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        string sequence = evt.Sequence is long value ? $" (#{value})" : string.Empty;

        switch (evt)
        {
            case FirehoseCommitEvent commitEvent:
                {
                    string eventBelongsTo = DescribeDid(commitEvent.Repo, didHandleCache);
                    string timeStamp = FormatTime(commitEvent.Time) + sequence;

                    foreach (FirehoseRepoOperation operation in commitEvent.Operations)
                    {
                        // The commit carries the record itself: for create/update operations, operation.GetRecord()
                        // decodes it to a JsonElement (deletes return null, since there is no record to carry).
                        // This sample only prints the operation, as the firehose carries records from every lexicon in
                        // use, not just Bluesky's. To decode the records you care about into Bluesky types, filter on
                        // operation.Collection and deserialize with idunno.Bluesky's source generated type information.
                        // See https://bluesky.idunno.dev/docs/firehose.html#decodingRecords.
                        Console.WriteLine($"COMMIT    : {eventBelongsTo} executed a {operation.Action} in {operation.Collection} at {timeStamp}");
                    }

                    break;
                }

            case FirehoseAccountEvent accountEvent:
                {
                    string eventBelongsTo = accountEvent.Did;

                    if (didHandleCache.TryGetValue(accountEvent.Did.Value, out string? handle))
                    {
                        eventBelongsTo += $"/({handle})";
                    }
                    else
                    {
                        DidDocument? didDoc = await Resolution.ResolveDidDocument(
                            accountEvent.Did,
                            loggerFactory: loggerFactory,
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (didDoc is not null)
                        {
                            foreach (string alsoKnownAs in didDoc.AlsoKnownAs)
                            {
                                if (alsoKnownAs.StartsWith("at://", StringComparison.InvariantCulture))
                                {
                                    CacheHandle(didHandleCache, accountEvent.Did, alsoKnownAs[5..]);
                                    eventBelongsTo += $"/({alsoKnownAs[5..]})";
                                    break;
                                }
                            }
                        }
                    }

                    string timeStamp = FormatTime(accountEvent.Time) + sequence;

                    if (accountEvent.Active)
                    {
                        Console.WriteLine($"ACCOUNT   : {eventBelongsTo} activated at {timeStamp}");
                    }
                    else if (accountEvent.Status == RepoStatus.Deactivated)
                    {
                        Console.WriteLine($"ACCOUNT   : {eventBelongsTo} deactivated at {timeStamp}");
                    }
                    else if (accountEvent.Status == RepoStatus.Deleted)
                    {
                        Console.WriteLine($"ACCOUNT   : {eventBelongsTo} deleted at {timeStamp}");
                    }
                    else if (accountEvent.Status is RepoStatus status)
                    {
                        Console.WriteLine($"ACCOUNT   : {eventBelongsTo} was {status.ToString().ToLowerInvariant()} at {timeStamp}");
                    }
                    else
                    {
                        Console.WriteLine($"ACCOUNT   : {eventBelongsTo} became inactive at {timeStamp}");
                    }

                    break;
                }

            case FirehoseIdentityEvent identityEvent:
                {
                    string timeStamp = FormatTime(identityEvent.Time) + sequence;

                    // The identity may have changed, so forget the cached handle. The handle in an identity event is not verified,
                    // so it is not cached; the next account event resolves the DID document instead.
                    didHandleCache.Remove(identityEvent.Did.Value);

                    if (identityEvent.Handle is not null)
                    {
                        Console.WriteLine($"IDENTITY  : {identityEvent.Did} reported the unverified handle {identityEvent.Handle} at {timeStamp}");
                    }
                    else
                    {
                        Console.WriteLine($"IDENTITY  : {identityEvent.Did} at {timeStamp}");
                    }

                    break;
                }

            case FirehoseSyncEvent syncEvent:
                Console.WriteLine($"SYNC      : {syncEvent.Did} needs resyncing from revision {syncEvent.Rev} at {FormatTime(syncEvent.Time)}{sequence}");
                break;

            case FirehoseInfoEvent infoEvent when infoEvent.Name == FirehoseInfoEvent.OutdatedCursor:
                Console.WriteLine("INFO      : Cursor is older than the relay retains; resuming from the oldest event it has (events may be missing).");
                break;

            case FirehoseInfoEvent infoEvent:
                Console.WriteLine($"INFO      : {infoEvent.Name} {infoEvent.Message}");
                break;

            case FirehoseInvalidEvent invalidEvent:
                Console.WriteLine($"INVALID   : Ignored an invalid {invalidEvent.Type} event{sequence}: {invalidEvent.Reason}");
                break;

            case FirehoseUnknownEvent unknownEvent:
                Console.WriteLine($"UNKNOWN   : Ignored an unrecognised {unknownEvent.Type} event{sequence}");
                break;
        }
    }

    private static string DescribeDid(Did did, MemoryCache didHandleCache) =>
        didHandleCache.TryGetValue(did.Value, out string? handle) ? $"{did}/({handle})" : did;

    private static void CacheHandle(MemoryCache didHandleCache, Did did, string handle) =>
        didHandleCache.Set(did.Value, handle, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            Size = 1
        });

    private static string FormatTime(DateTimeOffset time) =>
        time.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);
}