// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Jetstream;

using Microsoft.Extensions.Logging;

namespace Samples.Jetstream;

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

        // Resolves and verifies handles, caching them. The jetstream invalidates a cached handle when it sees an identity event for its DID.
        using var didHandleCache = new DidHandleCache(new DidHandleCacheOptions { LoggerFactory = loggerFactory });
        await using var jetStream = new AtProtoJetstream(
            options: new JetstreamOptions
            {
                ProtocolVersion = JetstreamProtocolVersion.V2,
                UseCompression = true,
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
                    await foreach (JetstreamEvent evt in jetStream.StreamAsync(
                        cursor: cursor, cancellationToken: cancellationToken, maximumReconnectAttempts: maximumRetries))
                    {
                        await PrintEventAsync(evt, didHandleCache, cancellationToken).ConfigureAwait(false);
                        cursor = evt.Sequence;
                    }
                }
                catch (JetstreamConnectionException ex) when (ex.ErrorDetail?.Error == "CursorTooOld")
                {
                    Console.WriteLine("RECONNECT: Cursor is too old; resuming from now (events may be missing).");
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
            Console.Error.WriteLine($"RECONNECT: Gave up after {maximumRetries} attempts: {ex.Message}");
            return 1;
        }

        return 0;
    }

    private static async Task PrintEventAsync(
        JetstreamEvent evt,
        DidHandleCache didHandleCache,
        CancellationToken cancellationToken)
    {
        string timeStamp = $"{evt.DateTimeOffset.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture)} (#{evt.Sequence})";

        switch (evt)
        {
            case JetstreamCommitEvent commitEvent:
                {
                    // Commits are too frequent to resolve every DID, so only use a handle an earlier account event has cached.
                    // A handle which could not be verified is shown as handle.invalid.
                    string eventBelongsTo = didHandleCache.TryGetCachedHandle(commitEvent.Did, out Handle? handle)
                        ? $"{commitEvent.Did}/({handle})"
                        : commitEvent.Did;

                    Console.WriteLine($"COMMIT    : {eventBelongsTo} executed a {commitEvent.Commit.Operation} in {commitEvent.Commit.Collection} at {timeStamp}");
                    break;
                }

            case JetstreamAccountEvent accountEvent:
                {
                    Handle handle = await didHandleCache.ResolveHandleAsync(accountEvent.Did, cancellationToken).ConfigureAwait(false);
                    string eventBelongsTo = $"{accountEvent.Did}/({handle})";

                    if (accountEvent.Account.Active)
                    {
                        Console.WriteLine($"ACCOUNT   : {eventBelongsTo} activated at {timeStamp}");
                    }
                    else if (accountEvent.Account.Status == AccountStatus.Deactivated)
                    {
                        Console.WriteLine($"ACCOUNT   : {eventBelongsTo} deactivated at {timeStamp}");
                    }
                    else if (accountEvent.Account.Status == AccountStatus.Deleted)
                    {
                        Console.WriteLine($"ACCOUNT   : {eventBelongsTo} deleted at {timeStamp}");
                    }
                    else
                    {
                        Console.WriteLine($"ACCOUNT   : {eventBelongsTo} was {accountEvent.Account.Status.ToString()!.ToLowerInvariant()} at {timeStamp}");
                    }
                    break;
                }

            case JetstreamIdentityEvent identityEvent:
                {
                    // The jetstream has already invalidated any cached handle. The handle in an identity event is not verified,
                    // so it is only printed; the next account event resolves and verifies the handle instead.

                    if (identityEvent.Identity.Handle is not null)
                    {
                        Console.WriteLine($"IDENTITY  : {identityEvent.Did} reported the unverified handle {identityEvent.Identity.Handle} at {timeStamp}");
                    }
                    else
                    {
                        Console.WriteLine($"IDENTITY  : {identityEvent.Did} at {timeStamp}");
                    }
                    break;
                }

            case JetstreamSyncEvent syncEvent:
                Console.WriteLine($"SYNC      : {syncEvent.Did} needs resyncing from revision {syncEvent.Sync.Rev} at {timeStamp}");
                break;
        }
    }
}
