// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Checks issuer-wide rolling publication budgets on the actual database engines.</summary>
public class PublicationRateLimitTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SecondBudgetIsSharedByConcurrentPublishersAndSurvivesRestartAndClockRegression(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        await AppendAsync(fixture, token);
        ILabelStore other = fixture.NewStore();
        await Task.WhenAll(
            fixture.Store.PublishAsync(250, token),
            other.PublishAsync(250, token));
        Assert.Equal(5, (await other.GetStateAsync(token)).PublishedThrough);
        LabelStoreState published = await other.GetStateAsync(token);
        Assert.Equal(5, (await other.ReplayAsync(
            streamId: published.StreamId,
            after: 0,
            through: published.PublishedThrough,
            cancellationToken: token)).Labels.Count);
        await other.InitializeAsync(StoreFixture.s_issuer, token);
        Assert.Equal(5, (await fixture.NewStore().PublishAsync(250, token)).PublishedThrough);
        DateTimeOffset initial = fixture.DatabaseClock!.GetUtcNow();
        ILabelStore regressed = fixture.NewStore(new FakeTimeProvider(initial.AddMilliseconds(-500)));
        Assert.Equal(5, (await regressed.PublishAsync(250, token)).PublishedThrough);
        fixture.DatabaseClock.SetUtcNow(initial.AddMilliseconds(999));
        Assert.Equal(5, (await other.PublishAsync(250, token)).PublishedThrough);
        fixture.DatabaseClock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(10, (await other.PublishAsync(250, token)).PublishedThrough);
        Assert.Equal(10, (await fixture.Store.PublishAsync(250, token)).PublishedThrough);
    }

    [Theory]
    [InlineData("sqlite", 3_600_000, 10_000)]
    [InlineData("postgresql", 3_600_000, 10_000)]
    [InlineData("mysql", 3_600_000, 10_000)]
    [InlineData("sqlite", 86_400_000, 100_000)]
    [InlineData("postgresql", 86_400_000, 100_000)]
    [InlineData("mysql", 86_400_000, 100_000)]
    public async Task LongWindowBudgetsCountLabelsAndReleaseOnlyAtExactRollingBoundary(string engine, int windowMilliseconds, int capacity)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        await AppendAsync(fixture, token);
        long start = fixture.DatabaseClock!.GetUtcNow().ToUnixTimeMilliseconds();
        // A compact fixture summary represents prior publication, not an actual new over-limit batch.
        await SeedAsync(
            fixture: fixture,
            timestamp: start,
            count: capacity - 2,
            token: token);
        fixture.DatabaseClock.Advance(TimeSpan.FromMilliseconds(windowMilliseconds - 1));
        Assert.Equal(2, (await fixture.Store.PublishAsync(250, token)).PublishedThrough);
        Assert.Equal(2, (await fixture.NewStore().PublishAsync(250, token)).PublishedThrough);
        fixture.DatabaseClock.Advance(TimeSpan.FromMilliseconds(1));
        // The older allowance expires, but the two just-published labels still occupy the second window.
        Assert.Equal(5, (await fixture.NewStore().PublishAsync(250, token)).PublishedThrough);
        Assert.Equal(5, (await fixture.Store.PublishAsync(250, token)).PublishedThrough);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task PublicationAndBudgetCommitAtomicallyAndLostAcknowledgementDoesNotDoubleCharge(string engine, bool afterCommit)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        await AppendAsync(fixture, token);
        fixture.SetCommitHooks(
            beforeCommit: afterCommit ? null : (_, _, _) => throw new SqliteException("Rollback publication.", 10),
            afterCommit: afterCommit ? (_, _) => throw new SqliteException("Lost publication acknowledgement.", 10) : null);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Store.PublishAsync(250, token));
        fixture.SetCommitHooks();
        Assert.Equal(afterCommit ? 5 : 0, (await fixture.Store.GetStateAsync(token)).PublishedThrough);
        Assert.Equal(5, (await fixture.NewStore().PublishAsync(250, token)).PublishedThrough);
        Assert.Equal(5, (await fixture.Store.PublishAsync(250, token)).PublishedThrough);
        fixture.DatabaseClock!.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(10, (await fixture.Store.PublishAsync(250, token)).PublishedThrough);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task VersionTwelveUpgradeInstallsAccountingBeforeAllowingPublication(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        await AppendAsync(fixture, token);
        await fixture.ExecuteAsync("DROP TABLE label_publication_batches");
        await fixture.ExecuteAsync("DROP TABLE label_publication_clock");
        await fixture.ExecuteAsync("UPDATE label_schema SET version=12");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.PublishAsync(250, token));
        ILabelStore upgraded = fixture.NewStore();
        await upgraded.InitializeAsync(StoreFixture.s_issuer, token);
        await upgraded.InitializeAsync(StoreFixture.s_issuer, token);
        Assert.Equal(5, (await upgraded.PublishAsync(250, token)).PublishedThrough);
        Assert.Equal(5, (await fixture.Store.PublishAsync(250, token)).PublishedThrough);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ExpiredAccountingCleanupIsBoundedAndNeverDeletesRecentUsage(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        await AppendAsync(fixture, token);
        long start = fixture.DatabaseClock!.GetUtcNow().ToUnixTimeMilliseconds();
        await SeedAsync(
            fixture: fixture,
            timestamp: start,
            count: 1,
            token: token,
            entries: 300);
        fixture.DatabaseClock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(5, (await fixture.Store.PublishAsync(250, token)).PublishedThrough);
        await using DbConnection connection = fixture.Connection();
        await connection.OpenAsync(token);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM label_publication_batches";
        Assert.Equal(51, Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(5, (await fixture.NewStore().PublishAsync(250, token)).PublishedThrough);
        Assert.Equal(1, Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture));
        fixture.DatabaseClock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(10, (await fixture.NewStore().PublishAsync(250, token)).PublishedThrough);
    }

    /// <summary>Queues positive labels and negations so the same publication budget covers both.</summary>
    private static async Task AppendAsync(StoreFixture fixture, CancellationToken token)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot positive = new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: fixture.DatabaseClock!.GetUtcNow()));
        SignedLabelSnapshot negative = new(key.CreateNegation(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: fixture.DatabaseClock.GetUtcNow().AddTicks(1)));
        await fixture.Store.AppendAsync("queued", [.. Enumerable.Repeat(positive, 6), .. Enumerable.Repeat(negative, 6)], cancellationToken: token);
    }

    /// <summary>Seeds prior usage under database time without advancing thousands of unrelated signing operations.</summary>
    private static async Task SeedAsync(
        StoreFixture fixture,
        long timestamp,
        int count,
        CancellationToken token,
        int entries = 1)
    {
        await using DbConnection connection = fixture.Connection();
        await connection.OpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO label_publication_batches(through_seq,published_at,label_count) VALUES(@through,@time,@count)";
        foreach ((string name, object value) in new (string, object)[] { ("@through", 0), ("@time", timestamp), ("@count", count) })
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        for (int entry = 0; entry < entries; entry++)
        {
            command.Parameters["@through"].Value = -entry;
            await command.ExecuteNonQueryAsync(token);
        }

        await transaction.CommitAsync(token);
    }
}
