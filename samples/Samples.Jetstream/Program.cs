// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Jetstream;

using Microsoft.Extensions.Caching.Memory;
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

        using var didHandleCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        await using var jetStream = new AtProtoJetstream(
            options: new JetstreamOptions
            {
                ProtocolVersion = JetstreamProtocolVersion.V2,
                UseCompression = true,
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
                    await foreach (JetstreamEvent evt in jetStream.StreamAsync(
                        cursor: cursor, cancellationToken: cancellationToken, maximumReconnectAttempts: maximumRetries))
                    {
                        await PrintEventAsync(evt, didHandleCache, loggerFactory).ConfigureAwait(false);
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
        MemoryCache didHandleCache,
        ILoggerFactory loggerFactory)
    {
        string timeStamp = $"{evt.DateTimeOffset.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture)} (#{evt.Sequence})";

        switch (evt)
        {
            case JetstreamCommitEvent commitEvent:
                {
                    string eventBelongsTo = commitEvent.Did;

                    if (didHandleCache.TryGetValue(commitEvent.Did, out string? handle))
                    {
                        eventBelongsTo += $"/({handle})";
                    }

                    Console.WriteLine($"COMMIT    : {eventBelongsTo} executed a {commitEvent.Commit.Operation} in {commitEvent.Commit.Collection} at {timeStamp}");
                    break;
                }

            case JetstreamAccountEvent accountEvent:
                {
                    string eventBelongsTo = accountEvent.Did;

                    if (didHandleCache.TryGetValue(accountEvent.Did, out string? handle))
                    {
                        eventBelongsTo += $"/({handle})";
                    }
                    else
                    {
                        DidDocument? didDoc = await Resolution.ResolveDidDocument(
                            accountEvent.Did,
                            loggerFactory: loggerFactory).ConfigureAwait(false);

                        if (didDoc is not null)
                        {
                            foreach (string alsoKnownAs in didDoc.AlsoKnownAs)
                            {
                                if (alsoKnownAs.StartsWith("at://", StringComparison.InvariantCulture))
                                {
                                    didHandleCache.Set(accountEvent.Did, alsoKnownAs[5..], new MemoryCacheEntryOptions
                                    {
                                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                                        Size = 1
                                    });
                                    eventBelongsTo += "/";
                                    eventBelongsTo += alsoKnownAs[5..];
                                    break;
                                }
                            }
                        }
                    }

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
                    if (identityEvent.Identity.Handle is not null)
                    {
                        Console.WriteLine($"IDENTITY  : {identityEvent.Did} changed handle to {identityEvent.Identity.Handle} at {timeStamp}");

                        didHandleCache.Set(identityEvent.Did, identityEvent.Identity.Handle, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                            Size = 1
                        });
                    }
                    else
                    {
                        didHandleCache.Remove(identityEvent.Did);
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
