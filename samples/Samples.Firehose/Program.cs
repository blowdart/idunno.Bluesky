// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Firehose;
using idunno.AtProto.Sync;

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

        // Resolves and verifies handles, caching them. The firehose invalidates a cached handle when it sees an #identity event for its DID.
        using var didHandleCache = new DidHandleCache(new DidHandleCacheOptions { LoggerFactory = loggerFactory });
        await using var firehose = new AtProtoFirehose(
            options: new FirehoseOptions
            {
                LoggerFactory = loggerFactory,
                DidHandleResolver = didHandleCache
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
                        await PrintEventAsync(evt, didHandleCache, cancellationToken).ConfigureAwait(false);

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
        DidHandleCache didHandleCache,
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
                    // Commits are too frequent to resolve every DID, so account events resolve and cache the handle for later commits.
                    Handle handle = await didHandleCache.ResolveHandleAsync(accountEvent.Did, cancellationToken).ConfigureAwait(false);
                    string eventBelongsTo = $"{accountEvent.Did}/({handle})";

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

                    // The firehose has already invalidated any cached handle. The handle in an identity event is not verified,
                    // so it is only printed; the next account event resolves and verifies the handle instead.

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

    // Only uses a handle which is already cached, rather than resolving one for each commit. A handle which could not be verified is shown as handle.invalid.
    private static string DescribeDid(Did did, DidHandleCache didHandleCache) =>
        didHandleCache.TryGetCachedHandle(did, out Handle? handle) ? $"{did}/({handle})" : did;

    private static string FormatTime(DateTimeOffset time) =>
        time.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);
}