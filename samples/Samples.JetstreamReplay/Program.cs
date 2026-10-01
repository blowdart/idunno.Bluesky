// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.Feed;

namespace Samples.JetstreamReplay;

public sealed class Program
{
    private const string CheckpointPath = "jetstream-replay-checkpoint.json";
    private const int RecentPostLimit = 1000;

    // Bluesky's public options provide source-generated metadata and converters; use their type info for native AOT.
    private static readonly JsonSerializerOptions s_blueskyJsonOptions = BlueskyJsonSerializerOptions.Options;

    public static async Task<int> Main(string[] args)
    {
        Option<string?> keyOption = new("--api-key")
        {
            Description = "Raw Jetstream archive API key (defaults to reading from the _JetstreamApiKey environment variable).",
            DefaultValueFactory = result =>
            {
                // Help probes defaults without a parent; do not render an API key in --help output.
                if (result.Parent is null)
                {
                    return null;
                }

                string? key = Environment.GetEnvironmentVariable("_JetstreamApiKey");
                return string.IsNullOrWhiteSpace(key) ? null : key;
            },
            Required = true
        };
        Option<string?> hostOption = new("--host")
        {
            Description = "Jetstream v2 host URI (defaults to wss://jetstream.us-west.bsky.network)."
        };
        Option<string?> handleOption = new("--handle", "-u", "/u")
        {
            Description = "Handle to replay (defaults to the _BlueskyHandle environment variable).",
            DefaultValueFactory = _ =>
            {
                string? defaultHandle = Environment.GetEnvironmentVariable("_BlueskyHandle");
                return string.IsNullOrWhiteSpace(defaultHandle) ? null : defaultHandle;
            },
            Required = true
        };
        keyOption.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValue(keyOption)))
            {
                result.AddError("Pass --api-key or set _JetstreamApiKey for archive access.");
            }
        });
        handleOption.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValue(handleOption)))
            {
                result.AddError("Pass --handle or set _BlueskyHandle to select an account.");
            }
        });
        RootCommand command = new("Replay a handle's posts, likes, follows and account events, then tail live.")
        {
            keyOption,
            hostOption,
            handleOption
        };
        command.SetAction((result, cancellationToken) =>
            RunAsync(
                apiKey: result.GetValue(keyOption),
                host: result.GetValue(hostOption),
                handle: result.GetValue(handleOption),
                cancellationToken: cancellationToken));
        return await command.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    private static async Task RunAsync(string? apiKey, string? host, string? handle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);

        Uri? uri = host is null ? null : new Uri(host, UriKind.Absolute);
        if (uri is not null && uri.Scheme is not ("ws" or "wss"))
        {
            throw new ArgumentException("--host must be a ws:// or wss:// Jetstream v2 URI.", nameof(host));
        }

        Did did = await IdentityResolution.ResolveHandleAsync(new Handle(handle), cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not resolve {handle}.");

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

        RecentPosts recentPosts = new(RecentPostLimit);
        await foreach (JetstreamEvent evt in jetstream.ReplayAsync(request, saved, SaveCheckpoint, cancellationToken)
            .ConfigureAwait(false))
        {
            switch (evt)
            {
                case JetstreamCommitEvent record:
                    PrintCommit(record, recentPosts);
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

    private static void PrintCommit(JetstreamCommitEvent evt, RecentPosts recentPosts)
    {
        JetstreamCommit commit = evt.Commit;
        string operation = evt.IsSyncBackfill ? $"{commit.Operation} (BACKFILL)" : commit.Operation.ToString();
        string collection = commit.Collection.ToString();
        Console.WriteLine($"{evt.Sequence}: {operation} in {collection} ({commit.RKey})");

        AtUri recordUri = new($"at://{evt.Did}/{commit.Collection}/{commit.RKey}");
        if (commit.Operation == JetstreamCommitOperation.Delete)
        {
            RecentPost? original = null;
            if (collection == "app.bsky.feed.post")
            {
                recentPosts.TryRemove(recordUri.ToString(), out original);
            }

            DateTimeOffset deletedAt = evt.WitnessedAt ?? evt.DateTimeOffset;
            if ((commit.Cid ?? original?.Cid) is Cid cid)
            {
                Console.WriteLine($"  Deleted {new StrongReference(recordUri, cid)} at {deletedAt.ToLocalTime():G}");
            }
            else
            {
                Console.WriteLine($"  Deleted {recordUri} at {deletedAt.ToLocalTime():G} (CID unavailable; no strong reference)");
            }

            if (original is not null)
            {
                Console.WriteLine($"  Original post: {(string.IsNullOrWhiteSpace(original.Text) ? "No Text" : original.Text)}");
            }

            return;
        }

        if (collection is not ("app.bsky.feed.post" or "app.bsky.feed.like"))
        {
            Console.WriteLine($"  Committed at: {(evt.WitnessedAt ?? evt.DateTimeOffset).ToLocalTime():G}");
            return;
        }

        if (commit.Record is not JsonElement value)
        {
            if (collection == "app.bsky.feed.post")
            {
                recentPosts.TryRemove(recordUri.ToString(), out _);
            }

            Console.Error.WriteLine($"Cannot decode {recordUri} " +
                $"at Jetstream sequence {evt.Sequence}: the commit has no record.");
            Console.WriteLine($"  Observed at: {(evt.WitnessedAt ?? evt.DateTimeOffset).ToLocalTime():G}");
            return;
        }

        if (collection == "app.bsky.feed.post")
        {
            string? text;
            try
            {
                Post post = JsonSerializer.Deserialize(value, (JsonTypeInfo<Post>)s_blueskyJsonOptions.GetTypeInfo(typeof(Post)))
                    ?? throw new JsonException("Could not deserialize value to Post");
                text = post.Text;
                Console.WriteLine($"  Post: {(string.IsNullOrWhiteSpace(text) ? "No Text" : text)}");
                Console.WriteLine($"  Posted at: {post.CreatedAt.ToLocalTime():G}");
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
                WarnInvalidRecord(evt, exception);
                text = GetString(value, "text");
                Console.WriteLine($"  Post: {(string.IsNullOrWhiteSpace(text) ? "No Text" : text)}");
                PrintRecordTime(value, evt, "Posted");
            }

            recentPosts.Remember(recordUri.ToString(), new RecentPost(text, commit.Cid));
        }
        else
        {
            try
            {
                Like like = JsonSerializer.Deserialize(value, (JsonTypeInfo<Like>)s_blueskyJsonOptions.GetTypeInfo(typeof(Like)))
                    ?? throw new JsonException("Could not deserialize value to Like");
                Console.WriteLine($"  Liked {like.Subject} at {like.CreatedAt.ToLocalTime():G}");
                if (like.Via is not null)
                {
                    Console.WriteLine($"  Via: {like.Via}");
                }
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
                WarnInvalidRecord(evt, exception);
                JsonElement subject = value.ValueKind == JsonValueKind.Object &&
                    value.TryGetProperty("subject", out JsonElement rawSubject) ? rawSubject : default;
                string? uri = GetString(subject, "uri");
                string? cid = GetString(subject, "cid");
                Console.WriteLine($"  Liked {uri ?? "(subject unavailable)"}{(cid is null ? "" : $" (CID: {cid})")}");
                PrintRecordTime(value, evt, "Liked");
            }
        }
    }

    private static void WarnInvalidRecord(JetstreamCommitEvent evt, Exception exception)
    {
        AtUri recordUri = new($"at://{evt.Did}/{evt.Commit.Collection}/{evt.Commit.RKey}");
        Console.Error.WriteLine($"Could not fully decode {recordUri} at Jetstream sequence {evt.Sequence}: {exception.Message}");
    }

    private static string? GetString(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out JsonElement property) &&
        property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static void PrintRecordTime(JsonElement value, JetstreamCommitEvent evt, string action)
    {
        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("createdAt", out JsonElement createdAt) &&
            createdAt.ValueKind == JsonValueKind.String &&
            createdAt.TryGetDateTimeOffset(out DateTimeOffset timestamp))
        {
            Console.WriteLine($"  {action} at: {timestamp.ToLocalTime():G}");
        }
        else
        {
            Console.WriteLine($"  Observed at: {(evt.WitnessedAt ?? evt.DateTimeOffset).ToLocalTime():G} (record date unavailable)");
        }
    }

    private static void SaveCheckpoint(SnapshotCheckpoint checkpoint)
    {
        string pendingPath = CheckpointPath + ".tmp";
        File.WriteAllBytes(pendingPath, JsonSerializer.SerializeToUtf8Bytes(
            checkpoint, ReplayJsonContext.Default.SnapshotCheckpoint));
        File.Move(pendingPath, CheckpointPath, overwrite: true);
    }

    private sealed record RecentPost(string? Text, Cid? Cid);

    private sealed class RecentPosts(int capacity)
    {
        private readonly LinkedList<KeyValuePair<string, RecentPost>> _ordered = new();
        private readonly Dictionary<string, LinkedListNode<KeyValuePair<string, RecentPost>>> _byUri =
            new(StringComparer.Ordinal);

        public void Remember(string uri, RecentPost post)
        {
            TryRemove(uri, out _);
            LinkedListNode<KeyValuePair<string, RecentPost>> node = _ordered.AddLast(new KeyValuePair<string, RecentPost>(uri, post));
            _byUri.Add(uri, node);
            if (_byUri.Count > capacity)
            {
                LinkedListNode<KeyValuePair<string, RecentPost>> oldest = _ordered.First!;
                _byUri.Remove(oldest.Value.Key);
                _ordered.RemoveFirst();
            }
        }

        public bool TryRemove(string uri, out RecentPost? post)
        {
            if (_byUri.Remove(uri, out LinkedListNode<KeyValuePair<string, RecentPost>>? node))
            {
                _ordered.Remove(node);
                post = node.Value.Value;
                return true;
            }

            post = null;
            return false;
        }
    }
}

/// <summary>
/// Provides source-generated JSON metadata for saving and restoring replay checkpoints without reflection.
/// </summary>
/// <remarks><para>This supports trimming and native AOT; the context does not decode events or serialize the API key.</para></remarks>
[JsonSerializable(typeof(SnapshotCheckpoint))]
internal sealed partial class ReplayJsonContext : JsonSerializerContext;
