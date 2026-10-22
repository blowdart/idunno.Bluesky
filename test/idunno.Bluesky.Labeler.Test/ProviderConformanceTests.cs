// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;

namespace idunno.Bluesky.Labeler.Test;

public class ProviderConformanceTests
{
    private static readonly DateTimeOffset s_instant = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static SignedLabelSnapshot Sign(LabelSigningKey key, string subject = "did:plc:subject", bool negation = false) =>
        new(negation ? key.CreateNegation(StoreFixture.Issuer, subject, "spam", s_instant)
            : key.CreateLabel(StoreFixture.Issuer, subject, "spam", s_instant));

    private static DidDocument Document(LabelSigningKey key) => JsonSerializer.Deserialize<DidDocument>(
        $$"""
        {"id":"did:plc:label-issuer","verificationMethod":[{"id":"did:plc:label-issuer#atproto_label",
        "type":"Multikey","controller":"did:plc:label-issuer","publicKeyMultibase":"{{key.ExportPublicKeyMultibase()}}"}]}
        """, AtProtoJsonSerializerOptions.Default)!;

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task PreservesSignedRepresentationAcrossPublicationAndRestart(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        foreach (LabelSigningCurve curve in new[] { LabelSigningCurve.P256, LabelSigningCurve.Secp256k1 })
        {
            using LabelSigningKey key = LabelSigningKey.Generate(curve);
            foreach (string negation in new[] { string.Empty, ",\"neg\":false", ",\"neg\":true" })
            {
                string json = """
                    {"ver":1,"src":"did:plc:label-issuer","uri":"at://did:plc:subject/app.bsky.feed.post/test","cid":"BAFYREIGH5FJR6AN3QUJVFBM6XMSO6JYHIDO2DGUIXAEQP3JTCQFT2GOFBA","val":"spam","cts":"2026-01-02T03:04:05.123456789+01:00","exp":"2027-01-02T03:04:05.123456789Z"
                    """ + negation + "}";
                Label unsigned = JsonSerializer.Deserialize<Label>(json, AtProtoJsonSerializerOptions.Default)!;
                SignedLabelSnapshot signed = new(key.Sign(unsigned));
                signed = new(Encoding.UTF8.GetBytes(" \n" + Encoding.UTF8.GetString(signed.GetJsonBytes()) + "\r\n"));
                byte[] savedJson = signed.GetJsonBytes();
                byte[] savedPayload = signed.GetCanonicalUnsignedBytes();
                signed.GetJsonBytes()[0] ^= 1;
                string id = Guid.NewGuid().ToString("N");
                LabelAppendReceipt receipt = await fixture.Store.AppendAsync(id, [signed], cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(receipt, await fixture.Store.AppendAsync(id, [signed], cancellationToken: TestContext.Current.CancellationToken));
                await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
                ILabelStore restarted = fixture.NewStore();
                await restarted.InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken);
                LabelStoreState state = await restarted.GetStateAsync(TestContext.Current.CancellationToken);
                StoredLabel restored = Assert.Single((await restarted.ReplayAsync(state.StreamId, receipt.FirstSequence - 1, receipt.LastSequence, cancellationToken: TestContext.Current.CancellationToken)).Labels);
                Assert.Equal(savedJson, restored.SignedLabel.GetJsonBytes());
                Assert.Equal(savedPayload, restored.SignedLabel.GetCanonicalUnsignedBytes());
                restored.SignedLabel.Label.Verify(Document(key));
                Assert.Equal(receipt, await restarted.GetReceiptAsync(id, TestContext.Current.CancellationToken));
            }
        }
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RollsBackWholeBatchAndRejectsConflictingIdentity(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot valid = Sign(key);
        SignedLabelSnapshot otherIssuer = new(key.CreateLabel(new Did("did:plc:other"), "did:plc:subject", "spam", s_instant));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.AppendAsync("rollback", [valid, otherIssuer], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await fixture.Store.GetReceiptAsync("rollback", TestContext.Current.CancellationToken));
        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
        LabelAppendReceipt receipt = await fixture.Store.AppendAsync("fixed", [valid], cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.AppendAsync("fixed", [Sign(key, "did:plc:changed")], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(receipt, await fixture.Store.AppendAsync("fixed", [valid], expectedRevision: -1, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.AppendAsync("stale", [valid], expectedRevision: 0, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SerializesConcurrentWritersAndDuplicateReceipts(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot signed = Sign(key);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ILabelStore[] stores = Enumerable.Range(0, 12).Select(_ => fixture.NewStore()).ToArray();
        Task<LabelAppendReceipt>[] tasks = Enumerable.Range(0, 12).Select(i => Task.Run(async () =>
        {
            await start.Task;
            return await stores[i].AppendAsync("op-" + (i % 6), [signed]);
        })).ToArray();
        start.SetResult();
        LabelAppendReceipt[] receipts = await Task.WhenAll(tasks);
        Assert.Equal(6, receipts.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 6).Select(i => (long)i), receipts.Select(receipt => receipt.FirstSequence).Distinct().Order());
        Assert.Empty((await fixture.Store.QueryAsync([], cancellationToken: TestContext.Current.CancellationToken)).Labels);
        LabelStoreState published = await fixture.Store.PublishAsync(3, TestContext.Current.CancellationToken);
        Assert.Equal(3, published.PublishedThrough);
        Assert.Equal(6, published.IssuedThrough);
        Assert.Equal(3, (await fixture.Store.GetSnapshotAsync(TestContext.Current.CancellationToken)).PendingLabels);
        LabelStoreState[] promotions = await Task.WhenAll(stores.Take(3).Select(store =>
            Task.Run(() => store.PublishAsync(1, TestContext.Current.CancellationToken))));
        Assert.Equal(new long[] { 4, 5, 6 }, promotions.Select(state => state.PublishedThrough).Order());
        published = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Enumerable.Range(1, 6).Select(i => (long)i),
            (await fixture.Store.ReplayAsync(published.StreamId, 0, 6, cancellationToken: TestContext.Current.CancellationToken)).Labels.Select(label => label.Sequence));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task KeepsStablePagesAndReplayTipsAcrossConcurrentPublication(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await fixture.Store.AppendAsync("initial", [Sign(key, "did:plc:one"), Sign(key, "did:plc:two"), Sign(key, "did:plc:three")], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        LabelPage first = await fixture.Store.QueryAsync(["did:plc:*"], limit: 1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(first.Cursor);
        await fixture.Store.AppendAsync("later", [Sign(key, "did:plc:four")], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        LabelPage second = await fixture.NewStore().QueryAsync(["did:plc:*"], cursor: first.Cursor, limit: 100, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, second.Through);
        Assert.Equal(new long[] { 2, 3 }, second.Labels.Select(label => label.Sequence));
        Assert.Null(second.Cursor);
        Assert.Single((await fixture.Store.QueryAsync(["did:plc:two"], cancellationToken: TestContext.Current.CancellationToken)).Labels);
        Assert.Empty((await fixture.Store.QueryAsync(["did:plc:TWO"], cancellationToken: TestContext.Current.CancellationToken)).Labels);
        Assert.Empty((await fixture.Store.QueryAsync([], source: new Did("did:plc:other"), cancellationToken: TestContext.Current.CancellationToken)).Labels);
        Assert.Equal(3, (await fixture.Store.ReplayAsync(first.StreamId, 0, first.Through, cancellationToken: TestContext.Current.CancellationToken)).Labels.Count);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.QueryAsync([], cursor: first.Cursor, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.QueryAsync([], cursor: "invalid", cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.ReplayAsync(Guid.NewGuid(), 0, 3, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.ReplayAsync(first.StreamId, 0, 5, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RecoversRetirementAndBoundedPurgeWithoutReusingSequences(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await fixture.Store.AppendAsync("initial", [Sign(key), Sign(key, "did:plc:two")], cancellationToken: TestContext.Current.CancellationToken);
        LabelStoreState state = await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        LabelPage oldPage = await fixture.Store.QueryAsync([], limit: 1, cancellationToken: TestContext.Current.CancellationToken);
        state = await fixture.Store.BeginRetirementAsync(state.Revision, TestContext.Current.CancellationToken);
        Assert.Equal(2, state.RetirementUpperBound);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.AppendAsync("closed", [Sign(key)], cancellationToken: TestContext.Current.CancellationToken));
        SignedLabelSnapshot firstNegation = Sign(key, negation: true);
        LabelAppendReceipt batch = await fixture.Store.AppendAsync("retire-1", [firstNegation], state.Revision, 1, TestContext.Current.CancellationToken);
        ILabelStore restarted = fixture.NewStore();
        Assert.Equal(batch, await restarted.AppendAsync("retire-1", [firstNegation], retirementThrough: 1, cancellationToken: TestContext.Current.CancellationToken));
        // Reload the checkpoint after provider recreation; a new batch must not repeat progress.
        state = await restarted.GetStateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, state.RetirementThrough);
        await restarted.AppendAsync("retire-2", [Sign(key, "did:plc:two", true)], state.Revision, 2, TestContext.Current.CancellationToken);
        state = await restarted.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(LabelerLifecyclePhase.Distributing, state.Phase);
        state = await restarted.BeginGracePeriodAsync(state.Revision, TimeSpan.FromDays(1), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.CompleteRetirementAsync(state.Revision, TestContext.Current.CancellationToken));
        await fixture.ExecuteAsync(engine switch
        {
            "sqlite" => "UPDATE label_state SET grace_deadline=CAST(unixepoch('subsec')*1000 AS INTEGER)-1",
            "postgresql" => "UPDATE label_state SET grace_deadline=CAST(EXTRACT(EPOCH FROM clock_timestamp())*1000 AS BIGINT)-1",
            _ => "UPDATE label_state SET grace_deadline=CAST(UNIX_TIMESTAMP(UTC_TIMESTAMP(3))*1000 AS SIGNED)-1"
        });
        state = await restarted.CompleteRetirementAsync(state.Revision, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentException>(() => restarted.PurgeAsync(state.Revision, false, cancellationToken: TestContext.Current.CancellationToken));
        state = await restarted.PurgeAsync(state.Revision, true, 1, TestContext.Current.CancellationToken);
        Assert.Equal(1, state.ReplayFloor);
        await Assert.ThrowsAsync<InvalidDataException>(() => restarted.ReplayAsync(state.StreamId, 0, state.PublishedThrough, cancellationToken: TestContext.Current.CancellationToken));
        while (state.Phase != LabelerLifecyclePhase.Purged)
        {
            state = await fixture.NewStore().PurgeAsync(state.Revision, true, 1, TestContext.Current.CancellationToken);
        }

        Assert.Equal(4, state.IssuedThrough);
        Assert.Equal(4, state.ReplayFloor);
        Assert.Equal(oldPage.StreamId, state.StreamId);
        Assert.Null(await restarted.GetReceiptAsync("initial", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.AppendAsync("never-reopen", [Sign(key)], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task InitializesFromScriptAndRecoversPartialSchemaButRejectsFutureVersions(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine, initialize: false);
        string package = engine switch { "sqlite" => "SQLite", "mysql" => "MySQL", _ => "PostgreSQL" };
        string root = FindRoot();
        string schema = await File.ReadAllTextAsync(Path.Combine(root, "src", "idunno.Bluesky.Labeler." + package, "schema.sql"), TestContext.Current.CancellationToken);
        await fixture.ExecuteAsync(schema);
        await fixture.Store.InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken);
        await fixture.Store.InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot label = Sign(key);
        await fixture.Store.AppendAsync("preserve", [label], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.ExecuteAsync("DELETE FROM label_schema");
        await fixture.NewStore().InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken);
        Assert.Equal(1, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.InitializeAsync(new Did("did:plc:wrong"), TestContext.Current.CancellationToken));
        await fixture.ExecuteAsync("UPDATE label_schema SET version=999");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RecordsOfficialMetricsAndSafeGeneratedLogs(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using MetricCollector<long> labels = new(fixture.MeterFactory, "idunno.Bluesky.Labeler.Persistence", "idunno.bluesky.labeler.labels");
        using MetricCollector<long> operations = new(fixture.MeterFactory, "idunno.Bluesky.Labeler.Persistence", "idunno.bluesky.labeler.operations");
        using MetricCollector<long> active = new(fixture.MeterFactory, "idunno.Bluesky.Labeler.Persistence", "idunno.bluesky.labeler.operations.active");
        using MetricCollector<double> durations = new(fixture.MeterFactory, "idunno.Bluesky.Labeler.Persistence", "idunno.bluesky.labeler.operation.duration");
        using MetricCollector<long> backlog = new(fixture.MeterFactory, "idunno.Bluesky.Labeler.Persistence", "idunno.bluesky.labeler.publication.pending");
        using MetricCollector<long> bytes = new(fixture.MeterFactory, "idunno.Bluesky.Labeler.Persistence", "idunno.bluesky.labeler.bytes");
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot signed = Sign(key);
        await fixture.Store.AppendAsync("secret-operation", [signed], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.AppendAsync("secret-operation", [signed], cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.AppendAsync("conflict", [signed], -1, cancellationToken: TestContext.Current.CancellationToken));
        await fixture.Store.GetSnapshotAsync(TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Store.GetStateAsync(cancellation.Token));
        Assert.Equal(1, labels.GetMeasurementSnapshot().Where(measurement => measurement.Tags["operation"]?.ToString() == "append").Sum(measurement => measurement.Value));
        Assert.Equal(0, active.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.Contains(operations.GetMeasurementSnapshot(), measurement => measurement.Tags["outcome"]?.ToString() == "idempotent");
        Assert.Contains(operations.GetMeasurementSnapshot(), measurement => measurement.Tags["outcome"]?.ToString() == "conflict");
        Assert.Contains(operations.GetMeasurementSnapshot(), measurement => measurement.Tags["outcome"]?.ToString() == "cancelled");
        Assert.Equal(signed.GetJsonBytes().LongLength, bytes.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.All(durations.GetMeasurementSnapshot(), measurement => Assert.True(measurement.Value >= 0));
        Assert.Equal(1, Assert.Single(backlog.GetMeasurementSnapshot()).Value);
        FakeLogCollector logs = fixture.Services.GetFakeLogCollector();
        Assert.Contains(logs.GetSnapshot(), log => log.Id == 4);
        Assert.All(logs.GetSnapshot(), log =>
        {
            Assert.DoesNotContain("secret-operation", log.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("did:plc:", log.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.ConnectionString, log.Message, StringComparison.Ordinal);
        });
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SerializesConcurrentInitializationAndRejectsUnboundUse(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine, initialize: false);
        string package = engine switch { "sqlite" => "SQLite", "mysql" => "MySQL", _ => "PostgreSQL" };
        await fixture.ExecuteAsync(await File.ReadAllTextAsync(
            Path.Combine(FindRoot(), "src", "idunno.Bluesky.Labeler." + package, "schema.sql"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.GetStateAsync(TestContext.Current.CancellationToken));
        ILabelStore[] stores = Enumerable.Range(0, 4).Select(_ => fixture.NewStore()).ToArray();
        await Task.WhenAll(stores.Select(store => Task.Run(() => store.InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken))));
        LabelStoreState[] states = await Task.WhenAll(stores.Select(store => store.GetStateAsync(TestContext.Current.CancellationToken)));
        Assert.Single(states.Distinct());
        Assert.NotEqual(Guid.Empty, states[0].StreamId);
        Assert.Equal(0, states[0].Revision);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RejectsInvalidInputsAndCancellationWithoutMutation(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot signed = Sign(key);
        LabelStoreState before = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.AppendAsync("empty", [], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Store.AppendAsync(new string('x', 257), [signed], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => fixture.Store.AppendAsync("\ud800", [signed], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Store.AppendAsync("oversized", Enumerable.Repeat(signed, 1001).ToArray(), cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.QueryAsync(["a*b"], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Store.PublishAsync(0, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.ReplayAsync(before.StreamId, -1, 0, cancellationToken: TestContext.Current.CancellationToken));
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Store.AppendAsync("cancelled", [signed], cancellationToken: cancellation.Token));
        Assert.Null(await fixture.Store.GetReceiptAsync("cancelled", TestContext.Current.CancellationToken));
        Assert.Equal(before, await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RejectsSequenceOverflowAndCorruptStoredRepresentations(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot signed = Sign(key);
        await fixture.ExecuteAsync("UPDATE label_state SET issued=9223372036854775807");
        await Assert.ThrowsAsync<OverflowException>(() => fixture.Store.AppendAsync("overflow", [signed], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await fixture.Store.GetReceiptAsync("overflow", TestContext.Current.CancellationToken));
        await fixture.ExecuteAsync("UPDATE label_state SET issued=0");
        await fixture.Store.AppendAsync("valid", [signed], cancellationToken: TestContext.Current.CancellationToken);
        LabelStoreState state = await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<DbException>(() => fixture.ExecuteAsync("UPDATE label_entries SET format_version=999"));
        Assert.Single((await fixture.Store.ReplayAsync(state.StreamId, 0, 1, cancellationToken: TestContext.Current.CancellationToken)).Labels);
        await fixture.ExecuteAsync("UPDATE label_entries SET payload=" + (engine == "postgresql" ? "decode('00','hex')" : "X'00'"));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.QueryAsync([], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ResumesInterruptedSchemaCreationAndEmptyRetirement(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine, initialize: false);
        string package = engine switch { "sqlite" => "SQLite", "mysql" => "MySQL", _ => "PostgreSQL" };
        string schema = await File.ReadAllTextAsync(
            Path.Combine(FindRoot(), "src", "idunno.Bluesky.Labeler." + package, "schema.sql"), TestContext.Current.CancellationToken);
        await fixture.ExecuteAsync(schema[..(schema.IndexOf(';', StringComparison.Ordinal) + 1)]);
        await fixture.Store.InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken);
        LabelStoreState state = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        state = await fixture.Store.BeginRetirementAsync(state.Revision, TestContext.Current.CancellationToken);
        Assert.Equal(LabelerLifecyclePhase.Distributing, state.Phase);
        state = await fixture.Store.BeginGracePeriodAsync(state.Revision, TimeSpan.FromDays(1), TestContext.Current.CancellationToken);
        Assert.NotNull(state.GraceDeadline);
        Assert.Equal(state, await fixture.NewStore().GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ScansSparseFiltersAcrossChunksWithoutExposingPendingLabels(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot unmatched = Sign(key);
        await fixture.Store.AppendAsync("chunk", Enumerable.Repeat(unmatched, 1000).ToArray(), cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.AppendAsync("match", [Sign(key, "did:plc:target")], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(1000, TestContext.Current.CancellationToken);
        Assert.Empty((await fixture.Store.QueryAsync(["did:plc:target"], cancellationToken: TestContext.Current.CancellationToken)).Labels);
        await fixture.Store.PublishAsync(1, TestContext.Current.CancellationToken);
        LabelPage page = await fixture.NewStore().QueryAsync(["did:plc:target"], limit: 1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1001, Assert.Single(page.Labels).Sequence);
        Assert.Null(page.Cursor);
        LabelPage replay = await fixture.Store.ReplayAsync(page.StreamId, 999, 1001, limit: 1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1000, Assert.Single(replay.Labels).Sequence);
    }

    [Theory]
    [InlineData(LabelSigningCurve.P256)]
    [InlineData(LabelSigningCurve.Secp256k1)]
    public void KeepsCallerBuffersAndUnsignedInputOutsideImmutableRepresentation(LabelSigningCurve curve)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(curve);
        SignedLabelSnapshot signed = Sign(key);
        byte[] supplied = signed.GetJsonBytes();
        SignedLabelSnapshot snapshot = new(supplied);
        supplied[0] ^= 1;
        byte[] canonical = snapshot.GetCanonicalUnsignedBytes();
        canonical[0] ^= 1;
        Assert.Equal(signed.GetJsonBytes(), snapshot.GetJsonBytes());
        Assert.Equal(signed.GetCanonicalUnsignedBytes(), snapshot.GetCanonicalUnsignedBytes());
        Assert.Throws<InvalidDataException>(() => new SignedLabelSnapshot(new Label(1, StoreFixture.Issuer, "did:plc:subject", null, "spam", false, s_instant, null)));
    }

    [Fact(Timeout = 120_000)]
    public async Task RejectsMySqlTablesChangedToNontransactionalStorage()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync("mysql");
        await fixture.ExecuteAsync("ALTER TABLE label_entries ENGINE=MyISAM");
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.NewStore().GetStateAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=file:labels.db")]
    [InlineData("Data Source=labels.db;Mode=Memory")]
    public void RejectsVolatileOrUriSqliteRegistration(string connectionString)
    {
        ServiceCollection services = new();
        Assert.Throws<ArgumentException>(() => services.AddSqliteLabelStorage(connectionString));
        Assert.Empty(services);
    }

    [Fact]
    public void RegistersLazilyAndRejectsMultipleProviders()
    {
        ServiceCollection services = new();
        Assert.Same(services, services.AddSqliteLabelStorage("Data Source=not-opened.db"));
        Assert.Throws<InvalidOperationException>(() => services.AddPostgreSqlLabelStorage("Host=127.0.0.1"));
        Assert.Throws<InvalidOperationException>(() => services.AddMySqlLabelStorage("Server=127.0.0.1"));
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "idunno.Bluesky.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
