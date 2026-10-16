// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.BotSdk.Test;

internal static class TestRecords
{
    public static ActionHandlerRegistry Handlers() => new(
        [new ReplyActionHandler(), new LikeActionHandler(), new RepostActionHandler(), new BookmarkActionHandler(), new ListItemActionHandler()]);

    public const string BotDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";
    public const string AuthorDid = "did:plc:7e6kocyzb77xkncplrkoojej";
    public const string CidValue = "bafyreigwnxqttkhzha2ig4io6wwht3qiugtor4ruglceyfdbnyq53a55fe";
    public static readonly DateTimeOffset Start = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    public static BotOptions Options() => new()
    {
        Did = BotDid,
        ArchiveApiKey = "not-a-real-key",
        CredentialEncryptionKey = Convert.ToBase64String(new byte[32])
    };

    public static JetstreamCommitEvent Event(string text = "Example post", long sequence = 11, ReplyReferences? reply = null) => new()
    {
        Did = new Did(AuthorDid),
        TimeStamp = Start.ToUnixTimeMilliseconds() * 1000,
        WitnessedAt = Start,
        Sequence = sequence,
        Kind = JetStreamEventKind.Commit,
        Commit = new JetstreamCommit
        {
            Operation = JetstreamCommitOperation.Create,
            Collection = new Nsid("app.bsky.feed.post"),
            Rev = "3mjfhkjsshs2q",
            RKey = new RecordKey("3mjfhkjsshs2q"),
            Cid = new Cid(CidValue),
            Record = JsonSerializer.SerializeToElement(new Post(text, Start.AddDays(-30), reply: reply),
                BlueskyJsonSerializerOptions.Options)
        }
    };

    public static BotAction Work(ReplyReferences? reply = null)
    {
        StrongReference source = new(
            new AtUri($"at://{AuthorDid}/app.bsky.feed.post/3mjfhkjsshs2q"),
            new Cid(CidValue));

        return BotActions.Reply(
            source,
            11,
            new Post("Saved test action", Start, reply: reply ?? new ReplyReferences(source, source)));
    }

    public static Post Post(BotAction work) =>
        JsonSerializer.Deserialize<Post>(work.RecordJson, BlueskyJsonSerializerOptions.Options)!;

    public static PostView View(AtUri uri, bool restricted = false)
    {
        string json = JsonSerializer.Serialize(new
        {
            uri = uri.ToString(),
            cid = CidValue,
            author = new { did = AuthorDid, handle = "author.test" },
            record = JsonSerializer.SerializeToElement(new Post("Example post", Start), BlueskyJsonSerializerOptions.Options),
            indexedAt = Start,
            viewer = new { replyDisabled = restricted }
        });

        return JsonSerializer.Deserialize<PostView>(json, BlueskyJsonSerializerOptions.Options)!;
    }
}
