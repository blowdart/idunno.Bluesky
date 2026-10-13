// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.BotSdk.DataProviders;
using idunno.Bluesky.Feed;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

public sealed partial class RelationalProviderTests
{
    /// <summary>Records a dry-run outcome without making a remote call, including reads.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task DeveloperModeNeverPosts(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(11, [work], null, CancellationToken.None);

        ActionStatistics statistics = new(TestRecords.Start);
        ActionDispatcher dispatcher = new(
            store.Database,
            store.Options,
            NullLogger<ActionDispatcher>.Instance,
            statistics,
            TestRecords.Handlers());
        await dispatcher.SendAsync(work, null, CancellationToken.None);
        Assert.Equal(1, await store.CountAsync("reply_work WHERE status = 'simulated'"));
        Assert.Null(await store.Database.NextWorkAsync(CancellationToken.None));
        Assert.StartsWith("1 action completed since ", statistics.Summary);
        await dispatcher.SendAsync(work, null, CancellationToken.None);
        Assert.StartsWith("1 action completed since ", statistics.Summary);
    }

    /// <summary>Reconciles an accepted reply after losing its response without creating a duplicate.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task ReconcilesUnknownWriteOutcome(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        store.Options.DeveloperMode = false;
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(11, [work], null, CancellationToken.None);

        FakeRemote remote = new(store.Options) { LoseFirstResponse = true };
        ActionStatistics statistics = new(TestRecords.Start);
        ActionDispatcher dispatcher = new(
            store.Database,
            store.Options,
            NullLogger<ActionDispatcher>.Instance,
            statistics,
            TestRecords.Handlers());
        await Assert.ThrowsAsync<HttpRequestException>(() => dispatcher.SendAsync(work, remote, CancellationToken.None));
        Assert.StartsWith("0 actions completed since ", statistics.Summary);

        BotAction uncertain = (await store.Database.NextWorkAsync(CancellationToken.None))!;
        Assert.Equal("uncertain", uncertain.Status);
        await dispatcher.SendAsync(uncertain, remote, CancellationToken.None);
        Assert.Equal(1, remote.Writes);
        Assert.Equal(work.RecordKey, remote.Key);
        Assert.True(ReplyRecords.SameRecord(TestRecords.Post(work), remote.Saved!));
        Assert.Equal(1, await store.CountAsync("reply_work WHERE status = 'sent'"));
        Assert.StartsWith("1 action completed since ", statistics.Summary);
    }

    /// <summary>Retries a confirmed missing uncertain reply with its original key, timestamp, and content.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task RetriesMissingUncertainRecordWithSameKey(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        store.Options.DeveloperMode = false;
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(11, [work], null, CancellationToken.None);
        await store.Database.MarkAttemptAsync(work, CancellationToken.None);

        FakeRemote remote = new(store.Options);
        ActionStatistics statistics = new(TestRecords.Start);
        ActionDispatcher dispatcher = new(
            store.Database,
            store.Options,
            NullLogger<ActionDispatcher>.Instance,
            statistics,
            TestRecords.Handlers());
        await dispatcher.SendAsync(work with { Status = "uncertain" }, remote, CancellationToken.None);
        Assert.Equal(1, remote.Writes);
        Assert.Equal(work.RecordKey, remote.Key);
        Assert.True(ReplyRecords.SameRecord(TestRecords.Post(work), remote.Saved!));
        Assert.StartsWith("1 action completed since ", statistics.Summary);
    }

    /// <summary>Reconciles uncertain sends before honoring a later source deletion.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task ReconcilesBeforeApplyingDeletion(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        store.Options.DeveloperMode = false;
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(11, [work], null, CancellationToken.None);
        await store.Database.MarkAttemptAsync(work, CancellationToken.None);
        await store.Database.HandleAsync(12, [], null, CancellationToken.None, work.SourceUri);

        BotAction uncertain = (await store.Database.NextWorkAsync(CancellationToken.None))!;
        Assert.True(uncertain.TargetDeleted);

        FakeRemote remote = new(store.Options) { Saved = TestRecords.Post(work), Key = work.RecordKey };
        ActionStatistics statistics = new(TestRecords.Start);
        ActionDispatcher dispatcher = new(
            store.Database,
            store.Options,
            NullLogger<ActionDispatcher>.Instance,
            statistics,
            TestRecords.Handlers());
        await dispatcher.SendAsync(uncertain, remote, CancellationToken.None);
        Assert.Equal(0, remote.Writes);
        Assert.Equal(1, await store.CountAsync("reply_work WHERE status = 'sent'"));
    }

    /// <summary>Prevents writes to a thread after its root post has been deleted.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task DoesNotReplyToDeletedRoot(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        store.Options.DeveloperMode = false;
        StrongReference root = new(
            new AtUri($"at://{TestRecords.AuthorDid}/app.bsky.feed.post/root"),
            new Cid(TestRecords.CidValue));
        BotAction initial = TestRecords.Work();
        StrongReference parent = new(initial.SourceUri, new Cid(TestRecords.CidValue));
        BotAction work = TestRecords.Work(new ReplyReferences(parent, root));
        await store.Database.HandleAsync(11, [work], null, CancellationToken.None);
        await store.Database.HandleAsync(12, [], null, CancellationToken.None, root.Uri.ToString());

        BotAction deleted = (await store.Database.NextWorkAsync(CancellationToken.None))!;
        Assert.True(deleted.TargetDeleted);
        Assert.Equal(0, await store.Database.MarkAttemptAsync(deleted, CancellationToken.None));

        FakeRemote remote = new(store.Options);
        ActionStatistics statistics = new(TestRecords.Start);
        ActionDispatcher dispatcher = new(
            store.Database,
            store.Options,
            NullLogger<ActionDispatcher>.Instance,
            statistics,
            TestRecords.Handlers());
        await dispatcher.SendAsync(deleted, remote, CancellationToken.None);
        Assert.Equal(0, remote.Writes);
        Assert.Equal(1, await store.CountAsync("reply_work WHERE status = 'not-sent'"));
    }

    /// <summary>Records confirmed reply restrictions as terminal outcomes without attempting a write.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task DoesNotBypassReplyRestrictions(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        store.Options.DeveloperMode = false;
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(11, [work], null, CancellationToken.None);

        FakeRemote remote = new(store.Options) { Restricted = true };
        ActionStatistics statistics = new(TestRecords.Start);
        ActionDispatcher dispatcher = new(
            store.Database,
            store.Options,
            NullLogger<ActionDispatcher>.Instance,
            statistics,
            TestRecords.Handlers());
        await dispatcher.SendAsync(work, remote, CancellationToken.None);
        Assert.Equal(0, remote.Writes);
        Assert.Equal(1, await store.CountAsync("reply_work WHERE status = 'not-sent'"));
    }

    /// <summary>Leaves an uncertain action untouched when its fixed key contains a different record.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task DoesNotOverwriteMismatchedReply(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        store.Options.DeveloperMode = false;
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(11, [work], null, CancellationToken.None);
        await store.Database.MarkAttemptAsync(work, CancellationToken.None);

        FakeRemote remote = new(store.Options)
        {
            Saved = new Post("different content", TestRecords.Start),
            Key = work.RecordKey
        };
        ActionStatistics statistics = new(TestRecords.Start);
        ActionDispatcher dispatcher = new(
            store.Database,
            store.Options,
            NullLogger<ActionDispatcher>.Instance,
            statistics,
            TestRecords.Handlers());
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            dispatcher.SendAsync(work with { Status = "uncertain" }, remote, CancellationToken.None));
        Assert.Equal(0, remote.Writes);
        Assert.NotNull(await store.Database.NextWorkAsync(CancellationToken.None));
    }

    private sealed class FakeRemote(BotOptions options) : IBotActionRemote
    {
        public BlueskyAgent Agent => throw new NotSupportedException("This test does not use a real SDK client.");

        public string BotDid => options.Did;
        public Post? Saved { get; set; }
        public string? Key { get; set; }
        public int Writes { get; private set; }
        public bool LoseFirstResponse { get; set; }
        public bool Restricted { get; set; }

        public Task<string?> FindRecordAsync(BotAction action, CancellationToken cancellationToken) =>
            Task.FromResult(Saved is null ? null : JsonSerializer.Serialize(Saved, BotJson.Post));

        public Task<PostView> TargetAsync(AtUri uri, CancellationToken cancellationToken) =>
            Task.FromResult(TestRecords.View(uri, Restricted));

        public Task<BotActionResult> CreateRecordAsync(BotAction action, CancellationToken cancellationToken)
        {
            Writes++;
            Saved = TestRecords.Post(action);
            Key = action.RecordKey;

            if (LoseFirstResponse)
            {
                LoseFirstResponse = false;
                throw new HttpRequestException("Server accepted the write; response was lost.");
            }

            return Task.FromResult(new BotActionResult(true, ActionRecords.Uri(action, this).ToString(), "created"));
        }

        public Task<bool> HasBookmarkAsync(AtUri uri, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task CreateBookmarkAsync(StrongReference subject, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> FindListMemberAsync(AtUri listUri, Did author, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
