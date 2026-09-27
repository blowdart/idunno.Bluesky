// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;

namespace Samples.JetstreamReplay;

public sealed class Program
{
    private const string CheckpointPath = "jetstream-replay-checkpoint.json";

    public static async Task<int> Main(string[] args)
    {
        Option<string?> keyOption = new("--api-key")
        {
            Description = "Raw Jetstream archive API key (defaults to _JetstreamApiKey)."
        };
        Option<string?> hostOption = new("--host")
        {
            Description = "Jetstream v2 host URI (defaults to wss://jetstream.us-west.bsky.network)."
        };
        RootCommand command = new("Replay bot.idunno.blue posts, likes, follows and account events, then tail live.")
        {
            keyOption,
            hostOption
        };
        command.SetAction((result, cancellationToken) =>
            RunAsync(
                result.GetValue(keyOption) ?? Environment.GetEnvironmentVariable("_JetstreamApiKey"),
                result.GetValue(hostOption), cancellationToken));
        return await command.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    private static async Task RunAsync(string? apiKey, string? host, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Pass --api-key or set _JetstreamApiKey for archive access.");
        }

        Uri? uri = host is null ? null : new Uri(host, UriKind.Absolute);
        if (uri is not null && uri.Scheme is not ("ws" or "wss"))
        {
            throw new ArgumentException("--host must be a ws:// or wss:// Jetstream v2 URI.", nameof(host));
        }

        using AtProtoAgent agent = new(new Uri("https://bsky.social"));
        Did did = await agent.ResolveHandle(new Handle("bot.idunno.blue"), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Could not resolve bot.idunno.blue.");

        using AtProtoJetstream jetstream = new(uri: uri, options: new JetstreamOptions
        {
            ApiKey = apiKey
        });

        SnapshotRequest request = new()
        {
            AfterSeq = 0,
            Dids = [did],
            Collections =
            [
                new CollectionSelector("app.bsky.feed.post"),
                new CollectionSelector("app.bsky.feed.like"),
                new CollectionSelector("app.bsky.graph.follow")
            ],
            Kinds =
            [
                JetStreamEventKind.Commit,
                JetStreamEventKind.Identity,
                JetStreamEventKind.Account,
                JetStreamEventKind.Sync
            ]
        };

        SnapshotCheckpoint? saved = File.Exists(CheckpointPath)
            ? JsonSerializer.Deserialize(
                await File.ReadAllBytesAsync(CheckpointPath, cancellationToken).ConfigureAwait(false),
                ReplayJsonContext.Default.SnapshotCheckpoint)
            : null;

        await foreach (JetstreamEvent evt in jetstream.ReplayAsync(request, saved, SaveCheckpoint, cancellationToken)
            .ConfigureAwait(false))
        {
            switch (evt)
            {
                case JetstreamCommitEvent record:
                    Console.WriteLine(
                        $"{record.Sequence}: {(record.IsSyncBackfill ? "BACKFILL" : record.Commit.Operation)} " +
                        $"{record.Commit.Collection}/{record.Commit.RKey}");
                    break;
                case JetstreamIdentityEvent identity:
                    Console.WriteLine($"{identity.Sequence}: identity {identity.Identity.Handle}");
                    break;
                case JetstreamAccountEvent account:
                    Console.WriteLine($"{account.Sequence}: account active={account.Account.Active}");
                    break;
                case JetstreamSyncEvent sync:
                    Console.WriteLine($"{sync.Sequence}: sync {sync.Sync.Rev}");
                    break;
            }
        }
    }

    private static void SaveCheckpoint(SnapshotCheckpoint checkpoint)
    {
        string pendingPath = CheckpointPath + ".tmp";
        File.WriteAllBytes(pendingPath, JsonSerializer.SerializeToUtf8Bytes(
            checkpoint, ReplayJsonContext.Default.SnapshotCheckpoint));
        File.Move(pendingPath, CheckpointPath, overwrite: true);
    }
}

[JsonSerializable(typeof(SnapshotCheckpoint))]
internal sealed partial class ReplayJsonContext : JsonSerializerContext;
