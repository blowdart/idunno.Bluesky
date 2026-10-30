// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections;
using System.Data.Common;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Testing;

namespace idunno.Bluesky.Labeler.Test;

public class ProviderConformanceTests
{
    private static readonly DateTimeOffset s_instant = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static SignedLabelSnapshot Sign(LabelSigningKey key, string subject = "did:plc:subject", bool negation = false) =>
        new(negation ? key.CreateNegation(StoreFixture.s_issuer, subject, "spam", s_instant)
            : key.CreateLabel(StoreFixture.s_issuer, subject, "spam", s_instant));

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
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
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
                await fixture.PublishThroughAsync();
                ILabelStore restarted = fixture.NewStore();
                await restarted.InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken);
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
    public async Task ReplacesRetainedLabelSignaturesWithoutChangingFieldsOrSequences(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey originalKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using LabelSigningKey replacementKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot original = Sign(originalKey);
        LabelAppendReceipt receipt = await fixture.Store.AppendAsync(
            "key-rotation",
            [original],
            cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        LabelStoreState before = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        StoredLabel stored = Assert.Single((await fixture.Store.ReplayAsync(
            before.StreamId,
            0,
            receipt.LastSequence,
            cancellationToken: TestContext.Current.CancellationToken)).Labels);
        await BeginResigningAsync(fixture, "key-rotation", replacementKey);

        SignedLabelSnapshot replacement = new(replacementKey.Sign(stored.SignedLabel.Label));
        SignedLabelSnapshot changed = new(replacementKey.CreateLabel(
            StoreFixture.s_issuer,
            "did:plc:subject",
            "changed",
            s_instant));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.ReplaceSignaturesAsync(
            [new StoredLabel(stored.Sequence, changed)],
            TestContext.Current.CancellationToken));
        await fixture.Store.ReplaceSignaturesAsync(
            [new StoredLabel(stored.Sequence, replacement)],
            TestContext.Current.CancellationToken);

        StoredLabel updated = Assert.Single((await fixture.Store.ReplayAsync(
            before.StreamId,
            0,
            receipt.LastSequence,
            cancellationToken: TestContext.Current.CancellationToken)).Labels);
        Assert.Equal(stored.Sequence, updated.Sequence);
        Assert.Equal(stored.SignedLabel.GetCanonicalUnsignedBytes(), updated.SignedLabel.GetCanonicalUnsignedBytes());
        Assert.Equal(
            stored.SignedLabel.Label.CreationTimestamp,
            updated.SignedLabel.Label.CreationTimestamp);
        updated.SignedLabel.Label.Verify(Document(replacementKey));

        LabelStoreState after = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        Assert.True(after.Revision > before.Revision);
        Assert.Equal(before.IssuedThrough, after.IssuedThrough);
        Assert.Equal(before.PublishedThrough, after.PublishedThrough);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task KeylessHostsMustDrainButOnlySignersMustActivate(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelMaintenanceStore maintenance = fixture.Services.GetRequiredService<ILabelMaintenanceStore>();
        Guid signer = Guid.NewGuid();
        Guid replica = Guid.NewGuid();
        await maintenance.RegisterHostAsync(signer, token);
        await maintenance.RegisterHostWithSigningRoleAsync(replica, requiresSigningKey: false, cancellationToken: token);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            maintenance.RegisterHostWithSigningRoleAsync(replica, requiresSigningKey: true, cancellationToken: token));
        await fixture.Store.InitializeAsync(StoreFixture.s_issuer, token);
        const string operation = "keyless-conformance";
        await maintenance.BeginRotationAsync(
            operationId: operation,
            previousKeyId: "active",
            replacementKeyId: "replacement",
            replacementPublicKey: "zreplacement",
            cancellationToken: token);
        await maintenance.AcknowledgeHostDrainedAsync(operation, signer, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            maintenance.CaptureRotationBoundaryAsync(operation, cancellationToken: token));
        await maintenance.AcknowledgeHostDrainedAsync(operation, replica, token);
        await maintenance.CaptureRotationBoundaryAsync(operation, cancellationToken: token);
        LabelMaintenanceState ready = await maintenance.VerifyRotationKeyPublicationAsync(
            operation, "zreplacement", cancellationToken: token);
        Assert.False(Assert.Single(ready.Hosts, host => host.InstanceId == replica).RequiresSigningKey);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            maintenance.AcknowledgeHostKeyActivatedAsync(operation, replica, "replacement", token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            maintenance.CompleteRotationAsync(operation, "replacement", cancellationToken: token));
        await maintenance.AcknowledgeHostKeyActivatedAsync(operation, signer, "replacement", token);
        LabelMaintenanceState completed = await maintenance.CompleteRotationAsync(
            operation, "replacement", cancellationToken: token);
        Assert.Equal(LabelMaintenancePhase.Completed, completed.Phase);
        Assert.False(Assert.Single(completed.Hosts, host => host.InstanceId == replica).KeyActivated);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task UpgradesLegacyHostRolesConservativelyAndRepeatably(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelMaintenanceStore maintenance = fixture.Services.GetRequiredService<ILabelMaintenanceStore>();
        Guid host = Guid.NewGuid();
        await maintenance.RegisterHostAsync(host, token);
        await maintenance.BeginRotationAsync(
            operationId: "role-upgrade",
            previousKeyId: "active",
            replacementKeyId: "replacement",
            replacementPublicKey: "zreplacement",
            cancellationToken: token);
        await fixture.ExecuteAsync("DROP TABLE label_host_roles; UPDATE label_schema SET version=6");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.GetStateAsync(token));
        await fixture.Store.InitializeAsync(StoreFixture.s_issuer, token);
        await fixture.Store.InitializeAsync(StoreFixture.s_issuer, token);
        LabelMaintenanceState upgraded = await maintenance.GetMaintenanceStateAsync(token);
        Assert.True(Assert.Single(upgraded.Hosts).RequiresSigningKey);
        Assert.False(upgraded.AllHostsDrained);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task CoordinatesResumableKeyRotationAcrossHostsAndRestart(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey originalKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using LabelSigningKey replacementKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await fixture.Store.AppendAsync(
            "rotation-entry",
            [Sign(originalKey)],
            cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);

        ILabelMaintenanceStore maintenance = fixture.Services.GetRequiredService<ILabelMaintenanceStore>();
        Guid[] hosts = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        await maintenance.RegisterHostAsync(hosts[0], TestContext.Current.CancellationToken);
        await maintenance.RegisterHostAsync(hosts[1], TestContext.Current.CancellationToken);
        LabelMaintenanceState state = await maintenance.BeginRotationAsync(
            operationId: "rotation-conformance",
            previousKeyId: "active",
            replacementKeyId: "replacement",
            replacementPublicKey: replacementKey.ExportPublicKeyMultibase(),
            cancellationToken: TestContext.Current.CancellationToken);

        LabelTarget target = LabelTarget.Account(new Did("did:plc:postrotation"));
        LabelOperationRequest request = LabelOperationRequest.Issue(
            operationId: "postrotation-issue",
            target: target,
            value: "spam",
            context: new(StoreFixture.s_issuer, "Check the signing-key fence."));
        LabelServiceResult<LabelOperationReceipt> blocked = await fixture.Store.CommitDecisionAsync(
            new(
                request: request,
                resolvedTarget: target,
                expectedRevision: (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).Revision,
                signedLabel: Sign(originalKey, target.Subject),
                audit: new(s_instant, "authorized", "policy", originalKey.ExportPublicKeyMultibase())),
            TestContext.Current.CancellationToken);
        Assert.False(blocked.Succeeded);
        Assert.Equal(LabelServiceErrorCode.IssuanceClosed, blocked.Error.Code);

        state = await maintenance.RegisterHostAsync(hosts[2], TestContext.Current.CancellationToken);
        Assert.Equal(3, state.Hosts.Count);
        await maintenance.AcknowledgeHostDrainedAsync("rotation-conformance", hosts[0], TestContext.Current.CancellationToken);
        await maintenance.AcknowledgeHostDrainedAsync("rotation-conformance", hosts[1], TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.CaptureRotationBoundaryAsync(
            "rotation-conformance",
            cancellationToken: TestContext.Current.CancellationToken));
        await maintenance.AcknowledgeHostDrainedAsync("rotation-conformance", hosts[2], TestContext.Current.CancellationToken);

        state = await maintenance.CaptureRotationBoundaryAsync(
            "rotation-conformance",
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(LabelMaintenancePhase.AwaitingKeyPublication, state.Phase);
        Assert.Equal(1, state.ThroughSequence);
        await maintenance.VerifyRotationKeyPublicationAsync(
            "rotation-conformance",
            replacementKey.ExportPublicKeyMultibase(),
            cancellationToken: TestContext.Current.CancellationToken);

        LabelStoreState beforeWrongKey = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        StoredLabel[] oldSignatures = [new(1, Sign(originalKey))];
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.ReplaceSignaturesAsync(
            oldSignatures, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => maintenance.ReplaceRotationBatchAsync(
            operationId: "rotation-conformance",
            expectedAfterSequence: 0,
            checkpoint: 1,
            replacementLabels: oldSignatures,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(beforeWrongKey, await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, (await maintenance.GetMaintenanceStateAsync(TestContext.Current.CancellationToken)).LastSequence);

        LabelSigningKeyRotationProgress progress = await LabelSigningKeyRotation.ResignNextBatchAsync(
            store: fixture.Store,
            maintenanceStore: maintenance,
            operationId: "rotation-conformance",
            replacementKey: replacementKey,
            batchSize: 1,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(progress.IsComplete);

        ILabelStore restartedStore = fixture.NewStore();
        await restartedStore.InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken);
        ILabelMaintenanceStore resumedMaintenance = (ILabelMaintenanceStore)restartedStore;
        state = await resumedMaintenance.GetMaintenanceStateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LabelMaintenancePhase.ReadyToReopen, state.Phase);
        Assert.Equal(1, state.LastSequence);
        foreach (Guid host in hosts.Take(2))
        {
            await resumedMaintenance.AcknowledgeHostKeyActivatedAsync(
                "rotation-conformance",
                host,
                "replacement",
                TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => resumedMaintenance.CompleteRotationAsync(
            "rotation-conformance",
            "replacement",
            cancellationToken: TestContext.Current.CancellationToken));
        await resumedMaintenance.AcknowledgeHostKeyActivatedAsync(
            "rotation-conformance",
            hosts[2],
            "replacement",
            TestContext.Current.CancellationToken);
        state = await resumedMaintenance.CompleteRotationAsync(
            "rotation-conformance",
            "replacement",
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(LabelMaintenancePhase.Completed, state.Phase);
        Assert.False(state.IsActive);

        LabelStoreState reopened = await restartedStore.GetStateAsync(TestContext.Current.CancellationToken);
        LabelServiceResult<LabelOperationReceipt> staleKey = await restartedStore.CommitDecisionAsync(
            new(
                request: request,
                resolvedTarget: target,
                expectedRevision: reopened.Revision,
                signedLabel: Sign(originalKey, target.Subject),
                audit: new(s_instant, "authorized", "policy", originalKey.ExportPublicKeyMultibase())),
            TestContext.Current.CancellationToken);
        Assert.False(staleKey.Succeeded);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, staleKey.Error.Code);
        Assert.Equal(reopened, await restartedStore.GetStateAsync(TestContext.Current.CancellationToken));

        StoredLabel resigned = Assert.Single((await restartedStore.ReplayAsync(
            state.StreamId,
            after: 0,
            through: 1,
            cancellationToken: TestContext.Current.CancellationToken)).Labels);
        resigned.SignedLabel.Label.Verify(Document(replacementKey));
        Assert.Equal("spam", resigned.SignedLabel.Label.Value);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RefusesRotationWithoutARegisteredHost(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey replacementKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        ILabelMaintenanceStore maintenance = fixture.Services.GetRequiredService<ILabelMaintenanceStore>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.BeginRotationAsync(
            operationId: "rotation-without-host",
            previousKeyId: "active",
            replacementKeyId: "replacement",
            replacementPublicKey: replacementKey.ExportPublicKeyMultibase(),
            cancellationToken: TestContext.Current.CancellationToken));
        LabelMaintenanceState state = await maintenance.GetMaintenanceStateAsync(TestContext.Current.CancellationToken);
        Assert.False(state.IsActive);
        Assert.Equal(LabelMaintenancePhase.None, state.Phase);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SignatureReplacementUsesTheValidatedSnapshotWhenCallerChangesTheBatch(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey originalKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using LabelSigningKey replacementKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot published = Sign(originalKey);
        SignedLabelSnapshot unpublished = Sign(originalKey, "did:plc:pending");
        await fixture.Store.AppendAsync("published", [published], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.AppendAsync("pending", [unpublished], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        await BeginResigningAsync(fixture, "validated-snapshot", replacementKey);
        StoredLabel expected = new(1, new SignedLabelSnapshot(replacementKey.Sign(published.Label)));
        StoredLabel forbidden = new(2, new SignedLabelSnapshot(replacementKey.Sign(unpublished.Label)));
        MutatingReplacementBatch input = new(expected, forbidden);

        await fixture.Store.ReplaceSignaturesAsync(input, TestContext.Current.CancellationToken);

        Assert.Equal(forbidden, input.Current);
        LabelStoreState state = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        StoredLabel updated = Assert.Single((await fixture.Store.ReplayAsync(
            state.StreamId,
            0,
            1,
            cancellationToken: TestContext.Current.CancellationToken)).Labels);
        Assert.Equal(expected.SignedLabel.GetJsonBytes(), updated.SignedLabel.GetJsonBytes());
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        StoredLabel pending = Assert.Single((await fixture.Store.ReplayAsync(
            state.StreamId,
            1,
            2,
            cancellationToken: TestContext.Current.CancellationToken)).Labels);
        Assert.Equal(unpublished.GetJsonBytes(), pending.SignedLabel.GetJsonBytes());
    }

    /// <summary>Changes the caller-owned batch immediately after its first read, without relying on task timing.</summary>
    private sealed class MutatingReplacementBatch(StoredLabel initial, StoredLabel replacement) : IReadOnlyList<StoredLabel>
    {
        internal StoredLabel Current { get; private set; } = initial;

        public int Count => 1;

        public StoredLabel this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfNotEqual(index, 0);
                StoredLabel current = Current;
                Current = replacement;
                return current;
            }
        }

        public IEnumerator<StoredLabel> GetEnumerator()
        {
            yield return this[0];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static async Task<ILabelMaintenanceStore> BeginResigningAsync(
        StoreFixture fixture,
        string operationId,
        LabelSigningKey replacementKey)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ILabelMaintenanceStore maintenance = fixture.Services.GetRequiredService<ILabelMaintenanceStore>();
        Guid host = Guid.NewGuid();
        await maintenance.RegisterHostAsync(host, cancellationToken);
        await maintenance.BeginRotationAsync(
            operationId: operationId,
            previousKeyId: "active",
            replacementKeyId: "replacement",
            replacementPublicKey: replacementKey.ExportPublicKeyMultibase(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await maintenance.AcknowledgeHostDrainedAsync(
            operationId: operationId,
            instanceId: host,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await maintenance.CaptureRotationBoundaryAsync(
            operationId: operationId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await maintenance.VerifyRotationKeyPublicationAsync(
            operationId: operationId,
            replacementPublicKey: replacementKey.ExportPublicKeyMultibase(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return maintenance;
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ResignsPublishedHistoryInResumableBatches(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey originalKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using LabelSigningKey replacementKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelAppendReceipt receipt = await fixture.Store.AppendAsync(
            "rotation-batch",
            [Sign(originalKey), Sign(originalKey, "did:plc:second")],
            cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        ILabelMaintenanceStore maintenance = fixture.Services.GetRequiredService<ILabelMaintenanceStore>();
        Guid host = Guid.NewGuid();
        await maintenance.RegisterHostAsync(host, TestContext.Current.CancellationToken);
        await maintenance.BeginRotationAsync(
            operationId: "rotation-batch",
            previousKeyId: "active",
            replacementKeyId: "replacement",
            replacementPublicKey: replacementKey.ExportPublicKeyMultibase(),
            cancellationToken: TestContext.Current.CancellationToken);
        await maintenance.AcknowledgeHostDrainedAsync(
            "rotation-batch",
            host,
            TestContext.Current.CancellationToken);
        LabelMaintenanceState boundary = await maintenance.CaptureRotationBoundaryAsync(
            "rotation-batch",
            cancellationToken: TestContext.Current.CancellationToken);
        await maintenance.VerifyRotationKeyPublicationAsync(
            "rotation-batch",
            replacementKey.ExportPublicKeyMultibase(),
            cancellationToken: TestContext.Current.CancellationToken);

        LabelSigningKeyRotationProgress first = await LabelSigningKeyRotation.ResignNextBatchAsync(
            store: fixture.Store,
            maintenanceStore: maintenance,
            operationId: "rotation-batch",
            replacementKey: replacementKey,
            batchSize: 1,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, first.LastSequence);
        Assert.False(first.IsComplete);

        LabelSigningKeyRotationProgress second = await LabelSigningKeyRotation.ResignNextBatchAsync(
            store: fixture.Store,
            maintenanceStore: maintenance,
            operationId: "rotation-batch",
            replacementKey: replacementKey,
            batchSize: 1,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(receipt.LastSequence, second.LastSequence);
        Assert.True(second.IsComplete);

        LabelPage page = await fixture.Store.ReplayAsync(
            boundary.StreamId,
            0,
            receipt.LastSequence,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, page.Labels.Count);
        foreach (StoredLabel label in page.Labels)
        {
            label.SignedLabel.Label.Verify(Document(replacementKey));
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
    [InlineData("sqlite", false)]
    [InlineData("postgresql", false)]
    [InlineData("mysql", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", true)]
    public async Task RecoversCommitBoundaryFailuresWithoutDuplicatingWrites(string engine, bool loseAcknowledgement)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot[] batch = [Sign(key), Sign(key, "did:plc:two")];
        LabelStoreState before = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        using MetricCollector<long> operations = new(fixture.MeterFactory, LabelPersistenceMetrics.MeterName, "idunno.bluesky.labeler.operations");
        int attempts = 0;
        int commits = 0;
        LabelAppendReceipt? observed = null;
        fixture.SetCommitHooks(async (connection, transaction, token) =>
        {
            attempts++;
            using DbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT issued, s.revision, (SELECT COUNT(*) FROM label_entries),
                    (SELECT COUNT(*) FROM label_operations), first_seq, last_seq, o.revision
                FROM label_state s CROSS JOIN label_operations o WHERE s.id=1
                """;
            await using (DbDataReader reader = await command.ExecuteReaderAsync(token))
            {
                Assert.True(await reader.ReadAsync(token));
                Assert.Equal(2, reader.GetInt64(0));
                Assert.Equal(before.Revision + 1, reader.GetInt64(1));
                Assert.Equal(2, reader.GetInt64(2));
                Assert.Equal(1, reader.GetInt64(3));
                LabelAppendReceipt current = new("commit-boundary", reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6));
                Assert.Equal(1, current.FirstSequence);
                Assert.Equal(2, current.LastSequence);
                if (observed is not null)
                {
                    Assert.Equal(observed, current);
                }

                observed = current;
                Assert.False(await reader.ReadAsync(token));
            }

            if (!loseAcknowledgement && attempts == 1)
            {
                await InjectTransientFailureAsync(engine, connection, transaction, token);
            }
        }, async (connection, token) =>
        {
            commits++;
            if (loseAcknowledgement)
            {
                await InjectTransientFailureAsync(engine, connection, null, token);
            }
        });

        if (loseAcknowledgement)
        {
            await Assert.ThrowsAnyAsync<DbException>(() => fixture.Store.AppendAsync("commit-boundary", batch,
                before.Revision, cancellationToken: TestContext.Current.CancellationToken));
        }
        else
        {
            LabelAppendReceipt receipt = await fixture.Store.AppendAsync("commit-boundary", batch,
                before.Revision, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(observed, receipt);
        }

        Assert.Equal(loseAcknowledgement ? 1 : 2, attempts);
        Assert.Equal(1, commits);
        Assert.Equal(loseAcknowledgement ? 0 : 1, operations.GetMeasurementSnapshot()
            .Count(measurement => measurement.Tags["outcome"]?.ToString() == "retry"));
        Assert.DoesNotContain(operations.GetMeasurementSnapshot(),
            measurement => measurement.Tags["outcome"]?.ToString() == "idempotent");
        fixture.SetCommitHooks();
        ILabelStore restarted = fixture.NewStore();
        LabelStoreState recovered = await restarted.GetStateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(before with { Revision = before.Revision + 1, IssuedThrough = 2 }, recovered);
        Assert.NotNull(observed);
        Assert.Equal(observed, await restarted.GetReceiptAsync("commit-boundary", TestContext.Current.CancellationToken));
        Assert.Equal(observed, await restarted.AppendAsync("commit-boundary", batch, before.Revision,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(recovered, await restarted.GetStateAsync(TestContext.Current.CancellationToken));
        recovered = await restarted.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        LabelPage replay = await restarted.ReplayAsync(recovered.StreamId, 0, recovered.PublishedThrough,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal<long>([1, 2], replay.Labels.Select(label => label.Sequence));
        for (int i = 0; i < batch.Length; i++)
        {
            Assert.Equal(batch[i].GetJsonBytes(), replay.Labels[i].SignedLabel.GetJsonBytes());
        }
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite", 1)]
    [InlineData("postgresql", 1)]
    [InlineData("mysql", 1)]
    [InlineData("sqlite", 100)]
    [InlineData("postgresql", 100)]
    [InlineData("mysql", 100)]
    public async Task DrainsUnfilteredQueryAndReplayInRequestedPageSizes(string engine, int limit)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot signed = Sign(key);
        int count = limit * 2 + 1;
        await fixture.Store.AppendAsync("pages", [.. Enumerable.Repeat(signed, count)],
            cancellationToken: TestContext.Current.CancellationToken);
        LabelStoreState state = await fixture.PublishThroughAsync();
        string? cursor = null;
        long after = 0;
        do
        {
            LabelPage query = await fixture.Store.QueryAsync([], cursor: cursor, limit: limit,
                cancellationToken: TestContext.Current.CancellationToken);
            LabelPage replay = await fixture.Store.ReplayAsync(state.StreamId, after, state.PublishedThrough, limit,
                TestContext.Current.CancellationToken);
            long[] expected =
            [
                .. Enumerable.Range((int)after + 1, Math.Min(limit, count - (int)after))
                    .Select(sequence => (long)sequence)
            ];
            Assert.Equal(expected, query.Labels.Select(label => label.Sequence));
            Assert.Equal(expected, replay.Labels.Select(label => label.Sequence));
            Assert.Equal(state.PublishedThrough, query.Through);
            Assert.Equal(state.PublishedThrough, replay.Through);
            Assert.Null(replay.Cursor);
            after = expected[^1];
            cursor = query.Cursor;
            Assert.Equal(after < count, cursor is not null);
        }
        while (cursor is not null);

        Assert.Equal(count, after);
        Assert.Empty((await fixture.Store.ReplayAsync(state.StreamId, after, state.PublishedThrough, limit,
            TestContext.Current.CancellationToken)).Labels);
    }

    private static async Task InjectTransientFailureAsync(string engine, DbConnection connection,
        DbTransaction? transaction, CancellationToken token)
    {
        if (engine == "sqlite")
        {
            throw new SqliteException("Injected commit-boundary failure.", 5);
        }

        using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = engine == "postgresql"
            ? "DO $$ BEGIN RAISE EXCEPTION 'Injected commit-boundary failure' USING ERRCODE = '40001'; END $$"
            : "SIGNAL SQLSTATE '40001' SET MYSQL_ERRNO = 1213, MESSAGE_TEXT = 'Injected commit-boundary failure'";
        await command.ExecuteNonQueryAsync(token);
        throw new InvalidOperationException("The database did not inject the expected transient failure.");
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SerializesConcurrentWritersAndDuplicateReceipts(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot signed = Sign(key);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ILabelStore[] stores = [.. Enumerable.Range(0, 12).Select(_ => fixture.NewStore())];
        Task<LabelAppendReceipt>[] tasks =
        [
            .. Enumerable.Range(0, 12).Select(i => Task.Run(async () =>
            {
                await start.Task;
                return await stores[i].AppendAsync("op-" + (i % 6), [signed]);
            }))
        ];
        start.SetResult();
        LabelAppendReceipt[] receipts = await Task.WhenAll(tasks);
        Assert.Equal(6, receipts.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 6).Select(i => (long)i), receipts.Select(receipt => receipt.FirstSequence).Distinct().Order());
        Assert.Empty((await fixture.Store.QueryAsync([], cancellationToken: TestContext.Current.CancellationToken)).Labels);
        LabelStoreState published = await fixture.Store.PublishAsync(3, TestContext.Current.CancellationToken);
        Assert.Equal(3, published.PublishedThrough);
        Assert.Equal(6, published.IssuedThrough);
        Assert.Equal(3, (await fixture.Store.GetSnapshotAsync(TestContext.Current.CancellationToken)).PendingLabels);
        fixture.DatabaseClock!.Advance(TimeSpan.FromSeconds(1));
        LabelStoreState[] promotions = await Task.WhenAll(stores.Take(3).Select(store =>
            Task.Run(() => store.PublishAsync(1, TestContext.Current.CancellationToken))));
        Assert.Equal<long>([4, 5, 6], promotions.Select(state => state.PublishedThrough).Order());
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
        Assert.Equal<long>([2, 3], second.Labels.Select(label => label.Sequence));
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
        state = await fixture.Store.BeginRetirementAsync(state.Revision, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, state.RetirementUpperBound);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.AppendAsync("closed", [Sign(key)], cancellationToken: TestContext.Current.CancellationToken));
        SignedLabelSnapshot firstNegation = Sign(key, negation: true);
        LabelAppendReceipt batch = await fixture.Store.AppendAsync("retire-1", [firstNegation], state.Revision, 1, cancellationToken: TestContext.Current.CancellationToken);
        ILabelStore restarted = fixture.NewStore();
        Assert.Equal(batch, await restarted.AppendAsync("retire-1", [firstNegation], retirementThrough: 1, cancellationToken: TestContext.Current.CancellationToken));
        // Reload the checkpoint after provider recreation; a new batch must not repeat progress.
        state = await restarted.GetStateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, state.RetirementThrough);
        await restarted.AppendAsync("retire-2", [Sign(key, "did:plc:two", true)], state.Revision, 2, cancellationToken: TestContext.Current.CancellationToken);
        state = await restarted.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(LabelerLifecyclePhase.Distributing, state.Phase);
        state = await restarted.BeginGracePeriodAsync(state.Revision, TimeSpan.FromDays(1), cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.CompleteRetirementAsync(state.Revision, cancellationToken: TestContext.Current.CancellationToken));
        await fixture.ExecuteAsync(engine switch
        {
            "sqlite" => "UPDATE label_state SET grace_deadline=CAST(unixepoch('subsec')*1000 AS INTEGER)-1",
            "postgresql" => "UPDATE label_state SET grace_deadline=CAST(EXTRACT(EPOCH FROM clock_timestamp())*1000 AS BIGINT)-1",
            _ => "UPDATE label_state SET grace_deadline=CAST(UNIX_TIMESTAMP(UTC_TIMESTAMP(3))*1000 AS SIGNED)-1"
        });
        state = await restarted.CompleteRetirementAsync(state.Revision, cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentException>(() => restarted.PurgeAsync(state.Revision, false, cancellationToken: TestContext.Current.CancellationToken));
        state = await restarted.PurgeAsync(state.Revision, true, 1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, state.ReplayFloor);
        await Assert.ThrowsAsync<InvalidDataException>(() => restarted.ReplayAsync(state.StreamId, 0, state.PublishedThrough, cancellationToken: TestContext.Current.CancellationToken));
        while (state.Phase != LabelerLifecyclePhase.Purged)
        {
            state = await fixture.NewStore().PurgeAsync(state.Revision, true, 1, cancellationToken: TestContext.Current.CancellationToken);
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
    public async Task PurgesRemainingCheckpointReceiptsAfterAllLabelsAreRemoved(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await fixture.Store.AppendAsync("initial", [Sign(key)], cancellationToken: TestContext.Current.CancellationToken);
        LabelStoreState state = await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        state = await fixture.Store.BeginRetirementAsync(state.Revision, cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.AppendAsync("checkpoint", [], state.Revision, 1, cancellationToken: TestContext.Current.CancellationToken);
        state = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        state = await fixture.Store.BeginGracePeriodAsync(state.Revision, TimeSpan.FromDays(1), cancellationToken: TestContext.Current.CancellationToken);
        await fixture.ExecuteAsync("UPDATE label_state SET grace_deadline=0");
        state = await fixture.Store.CompleteRetirementAsync(state.Revision, cancellationToken: TestContext.Current.CancellationToken);

        state = await fixture.Store.PurgeAsync(state.Revision, true, 1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(state.IssuedThrough, state.ReplayFloor);
        Assert.Equal(LabelerLifecyclePhase.Purging, state.Phase);
        Assert.Null(await fixture.Store.GetReceiptAsync("initial", TestContext.Current.CancellationToken));
        Assert.NotNull(await fixture.Store.GetReceiptAsync("checkpoint", TestContext.Current.CancellationToken));

        state = await fixture.NewStore().PurgeAsync(state.Revision, true, 1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(LabelerLifecyclePhase.Purged, state.Phase);
        Assert.Null(await fixture.Store.GetReceiptAsync("checkpoint", TestContext.Current.CancellationToken));
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
        await fixture.Store.InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken);
        await fixture.Store.InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot label = Sign(key);
        await fixture.Store.AppendAsync("preserve", [label], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.ExecuteAsync("DELETE FROM label_schema");
        await fixture.NewStore().InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken);
        Assert.Equal(1, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.InitializeAsync(new Did("did:plc:wrong"), TestContext.Current.CancellationToken));
        await fixture.ExecuteAsync("UPDATE label_schema SET version=999");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken));
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
        ILabelStore[] stores = [.. Enumerable.Range(0, 4).Select(_ => fixture.NewStore())];
        await Task.WhenAll(stores.Select(store => Task.Run(() => store.InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken))));
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
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.AppendAsync("decision\0retry", [signed], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.GetReceiptAsync("decision\0retry", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Store.AppendAsync("oversized", [.. Enumerable.Repeat(signed, 1001)], cancellationToken: TestContext.Current.CancellationToken));
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
        await fixture.Store.InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken);
        LabelStoreState state = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        state = await fixture.Store.BeginRetirementAsync(state.Revision, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(LabelerLifecyclePhase.Distributing, state.Phase);
        state = await fixture.Store.BeginGracePeriodAsync(state.Revision, TimeSpan.FromDays(1), cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(state.GraceDeadline);
        Assert.Equal(state, await fixture.NewStore().GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ScansSparseFiltersAcrossChunksWithoutExposingPendingLabels(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot unmatched = Sign(key);
        await fixture.Store.AppendAsync("chunk", [.. Enumerable.Repeat(unmatched, 1000)], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.AppendAsync("match", [Sign(key, "did:plc:target")], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.PublishThroughAsync(1000);
        Assert.Empty((await fixture.Store.QueryAsync(["did:plc:target"], cancellationToken: TestContext.Current.CancellationToken)).Labels);
        await fixture.PublishThroughAsync();
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
        Assert.Throws<InvalidDataException>(() => new SignedLabelSnapshot(new Label(1, StoreFixture.s_issuer, "did:plc:subject", null, "spam", false, s_instant, null)));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("label_schema")]
    [InlineData("label_state")]
    [InlineData("label_entries")]
    [InlineData("label_operations")]
    [InlineData("label_decisions")]
    [InlineData("label_distribution")]
    [InlineData("label_team")]
    [InlineData("label_admin_audit")]
    [InlineData("label_admin_receipts")]
    [InlineData("label_admin_tokens")]
    [InlineData("label_report_tokens")]
    [InlineData("label_policy_gate")]
    [InlineData("label_policy_versions")]
    [InlineData("label_policy_events")]
    [InlineData("label_policy_history")]
    [InlineData("label_subject_index")]
    [InlineData("label_pending_reports")]
    public async Task RejectsMySqlTablesChangedToNontransactionalStorage(string table)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync("mysql");
        await fixture.ExecuteAsync($"ALTER TABLE {table} ENGINE=MyISAM");
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.NewStore().GetStateAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken));
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
