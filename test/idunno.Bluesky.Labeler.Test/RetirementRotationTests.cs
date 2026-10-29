// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Text;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Protects recovery rotation's lifecycle, drain and destructive-cleanup boundaries.</summary>
public class RetirementRotationTests
{
    [Theory]
    [InlineData("Requested")]
    [InlineData("Attested")]
    public async Task KeyRemovalPreventsRotationAndNewSigningHostsButAllowsKeylessInspectionHosts(string keyRemoval)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync("sqlite");
        CancellationToken token = TestContext.Current.CancellationToken;
        await fixture.ExecuteAsync("UPDATE label_state SET phase=4");
        await using (DbConnection connection = fixture.Connection())
        {
            await connection.OpenAsync(token);
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO label_retirement(id,payload) VALUES(1,@payload)";
            DbParameter payload = command.CreateParameter();
            payload.ParameterName = "@payload";
            payload.Value = Encoding.UTF8.GetBytes($$"""
                {"operationId":"retirement","generation":1,"running":false,"stage":"Completed",
                 "scannedThrough":0,"createdAt":"2026-01-01T00:00:00Z","actor":"did:plc:administrator",
                 "authorityGeneration":1,"credential":"uid:0","localIdentity":"uid:0",
                 "expiresAt":"2026-01-02T00:00:00Z","reason":"Retire.","keyRemoval":"{{keyRemoval}}"}
                """);
            command.Parameters.Add(payload);
            await command.ExecuteNonQueryAsync(token);
        }

        ILabelMaintenanceStore maintenance = (ILabelMaintenanceStore)fixture.NewStore();
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.RegisterHostWithSigningRoleAsync(
            Guid.NewGuid(), requiresSigningKey: true, token));
        Guid keylessHost = Guid.NewGuid();
        LabelMaintenanceState registered = await maintenance.RegisterHostWithSigningRoleAsync(
            keylessHost, requiresSigningKey: false, token);
        Assert.False(registered.IsActive);
        using LabelSigningKey replacement = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.BeginRotationAsync(
            operationId: "removal-closed",
            previousKeyId: "original",
            replacementKeyId: "replacement",
            replacementPublicKey: replacement.ExportPublicKeyMultibase(),
            cancellationToken: token));
    }

    [Theory]
    [InlineData(LabelerLifecyclePhase.Retiring)]
    [InlineData(LabelerLifecyclePhase.Distributing)]
    [InlineData(LabelerLifecyclePhase.Grace)]
    [InlineData(LabelerLifecyclePhase.Retired)]
    public async Task RecoveryRotationPreservesTheRetirementPhaseAndOrdinaryIssuanceFence(LabelerLifecyclePhase phase)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync("sqlite");
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey original = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using LabelSigningKey replacement = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot label = new(original.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: fixture.DatabaseClock!.GetUtcNow()));
        await fixture.Store.AppendAsync("initial", [label], cancellationToken: token);
        LabelStoreState state = await fixture.Store.PublishAsync(cancellationToken: token);
        state = await fixture.Store.BeginRetirementAsync(state.Revision, cancellationToken: token);
        if (phase != LabelerLifecyclePhase.Retiring)
        {
            await fixture.Store.AppendAsync(
                operationId: "enumerated",
                labels: [],
                expectedRevision: state.Revision,
                retirementThrough: state.RetirementUpperBound,
                cancellationToken: token);
            state = await fixture.Store.GetStateAsync(token);
        }
        if (phase is LabelerLifecyclePhase.Grace or LabelerLifecyclePhase.Retired)
        {
            state = await fixture.Store.BeginGracePeriodAsync(state.Revision, TimeSpan.FromDays(1), cancellationToken: token);
        }
        if (phase == LabelerLifecyclePhase.Retired)
        {
            await fixture.ExecuteAsync("UPDATE label_state SET grace_deadline=0");
            state = await fixture.Store.CompleteRetirementAsync(state.Revision, cancellationToken: token);
        }

        Assert.Equal(phase, state.Phase);
        ILabelMaintenanceStore maintenance = (ILabelMaintenanceStore)fixture.NewStore();
        Guid host = Guid.NewGuid();
        await maintenance.RegisterHostWithSigningRoleAsync(host, requiresSigningKey: false, token);
        await maintenance.BeginRotationAsync(
            operationId: "recovery",
            previousKeyId: "original",
            replacementKeyId: "replacement",
            replacementPublicKey: replacement.ExportPublicKeyMultibase(),
            cancellationToken: token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.CaptureRotationBoundaryAsync(
            "recovery", cancellationToken: token));
        await maintenance.AcknowledgeHostDrainedAsync("recovery", host, token);
        LabelMaintenanceState boundary = await maintenance.CaptureRotationBoundaryAsync("recovery", cancellationToken: token);
        await maintenance.VerifyRotationKeyPublicationAsync(
            "recovery", replacement.ExportPublicKeyMultibase(), cancellationToken: token);
        LabelSigningKeyRotationProgress progress = await LabelSigningKeyRotation.ResignNextBatchAsync(
            store: fixture.Store,
            maintenanceStore: maintenance,
            operationId: "recovery",
            replacementKey: replacement,
            batchSize: 1,
            cancellationToken: token);
        Assert.True(progress.IsComplete);
        await maintenance.CompleteRotationAsync("recovery", "replacement", cancellationToken: token);

        LabelStoreState after = await fixture.Store.GetStateAsync(token);
        Assert.Equal(phase, after.Phase);
        Assert.Equal(state.RetirementThrough, after.RetirementThrough);
        Assert.Equal(state.GraceDeadline, after.GraceDeadline);
        Assert.Equal(state.StreamId, boundary.StreamId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.AppendAsync(
            "ordinary-closed", [label], cancellationToken: token));
    }

    [Theory]
    [InlineData(LabelerLifecyclePhase.Purging)]
    [InlineData(LabelerLifecyclePhase.Purged)]
    public async Task CleanupRejectsRecoveryRotation(LabelerLifecyclePhase phase)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync("sqlite");
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelStoreState state = await fixture.Store.GetStateAsync(token);
        state = await fixture.Store.BeginRetirementAsync(state.Revision, cancellationToken: token);
        state = await fixture.Store.BeginGracePeriodAsync(state.Revision, TimeSpan.FromDays(1), cancellationToken: token);
        await fixture.ExecuteAsync("UPDATE label_state SET grace_deadline=0");
        state = await fixture.Store.CompleteRetirementAsync(state.Revision, cancellationToken: token);
        await fixture.Store.PurgeAsync(state.Revision, true, cancellationToken: token);
        if (phase == LabelerLifecyclePhase.Purging)
        {
            await fixture.ExecuteAsync("UPDATE label_state SET phase=5");
        }

        ILabelMaintenanceStore maintenance = (ILabelMaintenanceStore)fixture.Store;
        await maintenance.RegisterHostWithSigningRoleAsync(Guid.NewGuid(), requiresSigningKey: false, token);
        using LabelSigningKey replacement = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.BeginRotationAsync(
            operationId: "cleanup-closed",
            previousKeyId: "original",
            replacementKeyId: "replacement",
            replacementPublicKey: replacement.ExportPublicKeyMultibase(),
            cancellationToken: token));
        Assert.Equal(phase, (await fixture.Store.GetStateAsync(token)).Phase);
    }
}
