// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Exercises transactional signing selection with each real storage engine.</summary>
public class SigningKeySelectionTests
{
    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SeedsOnceAcrossConcurrentHostsAndMigratesVersionSeven(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        await fixture.ExecuteAsync("DROP TABLE label_signing_selection; UPDATE label_schema SET version=7 WHERE id=1");
        ILabelStore restarted = fixture.NewStore();
        await restarted.InitializeAsync(StoreFixture.s_issuer, token);
        ILabelSigningKeySelectionStore selections = (ILabelSigningKeySelectionStore)restarted;
        Assert.Null(await selections.GetSigningKeySelectionAsync(token));
        Assert.Same(fixture.Store, fixture.Services.GetRequiredService<ILabelSigningKeySelectionStore>());
        using LabelSigningKey first = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using LabelSigningKey second = LabelSigningKey.Generate(LabelSigningCurve.Secp256k1);
        LabelSigningKeySelection[] results = await Task.WhenAll(
            selections.InitializeSigningKeySelectionAsync("first", first.ExportPublicKeyMultibase(), token),
            ((ILabelSigningKeySelectionStore)fixture.Store).InitializeSigningKeySelectionAsync("second", second.ExportPublicKeyMultibase(), token));
        Assert.Equal(results[0].KeyId, results[1].KeyId);
        Assert.Equal(results[0].PublicKey, results[1].PublicKey);
        Assert.Equal(StoreFixture.s_issuer.ToString(), results[0].Issuer);
        Assert.Equal((await fixture.Store.GetStateAsync(token)).StreamId, results[0].StreamId);
        LabelSigningKeySelection seeded = await selections.InitializeSigningKeySelectionAsync("stale", second.ExportPublicKeyMultibase(), token);
        Assert.Equal(results[0].KeyId, seeded.KeyId);
        Assert.Equal(results[0].PublicKey, seeded.PublicKey);
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            selections.InitializeSigningKeySelectionAsync("invalid", first.ExportPkcs8PrivateKeyPem(), token));
        await fixture.ExecuteAsync("UPDATE label_signing_selection SET issuer='did:plc:wrong' WHERE id=1");
        await Assert.ThrowsAsync<InvalidDataException>(() => selections.GetSigningKeySelectionAsync(token));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ActivationRequiresVerifiedDrainedRotationAndSurvivesRestartBeforeAcknowledgement(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelSigningKeySelectionStore selections = fixture.Services.GetRequiredService<ILabelSigningKeySelectionStore>();
        ILabelMaintenanceStore maintenance = fixture.Services.GetRequiredService<ILabelMaintenanceStore>();
        using LabelSigningKey original = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using LabelSigningKey replacement = LabelSigningKey.Generate(LabelSigningCurve.P256);
        string publicKey = replacement.ExportPublicKeyMultibase();
        await selections.InitializeSigningKeySelectionAsync("original", original.ExportPublicKeyMultibase(), token);
        await fixture.Store.AppendAsync(
            "retained",
            [new SignedLabelSnapshot(original.CreateLabel(
                StoreFixture.s_issuer,
                "did:plc:subject",
                "spam",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)))],
            cancellationToken: token);
        await fixture.Store.PublishAsync(cancellationToken: token);
        Guid host = Guid.NewGuid();
        await maintenance.RegisterHostAsync(host, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.BeginRotationAsync(
            operationId: "wrong-key",
            previousKeyId: "stale",
            replacementKeyId: "replacement",
            replacementPublicKey: publicKey,
            cancellationToken: token));
        await maintenance.BeginRotationAsync(
            operationId: "rotation",
            previousKeyId: "original",
            replacementKeyId: "replacement",
            replacementPublicKey: publicKey,
            cancellationToken: token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => selections.ActivateSigningKeySelectionAsync(
            operationId: "rotation", previousKeyId: "original", keyId: "replacement", publicKey: publicKey, cancellationToken: token));
        await maintenance.AcknowledgeHostDrainedAsync("rotation", host, token);
        await maintenance.CaptureRotationBoundaryAsync("rotation", cancellationToken: token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => selections.ActivateSigningKeySelectionAsync(
            operationId: "rotation", previousKeyId: "original", keyId: "replacement", publicKey: publicKey, cancellationToken: token));
        await maintenance.VerifyRotationKeyPublicationAsync("rotation", publicKey, cancellationToken: token);
        await LabelSigningKeyRotation.ResignNextBatchAsync(
            store: fixture.Store, maintenanceStore: maintenance, operationId: "rotation", replacementKey: replacement, cancellationToken: token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => selections.ActivateSigningKeySelectionAsync(
            operationId: "stale", previousKeyId: "original", keyId: "replacement", publicKey: publicKey, cancellationToken: token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => selections.ActivateSigningKeySelectionAsync(
            operationId: "rotation", previousKeyId: "wrong", keyId: "replacement", publicKey: publicKey, cancellationToken: token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => selections.ActivateSigningKeySelectionAsync(
            operationId: "rotation", previousKeyId: "original", keyId: "replacement", publicKey: original.ExportPublicKeyMultibase(), cancellationToken: token));
        Task<LabelSigningKeySelection> activation = selections.ActivateSigningKeySelectionAsync(
            operationId: "rotation", previousKeyId: "original", keyId: "replacement", publicKey: publicKey, cancellationToken: token);
        Task<LabelSigningKeySelection> concurrent = selections.ActivateSigningKeySelectionAsync(
            operationId: "rotation", previousKeyId: "original", keyId: "replacement", publicKey: publicKey, cancellationToken: token);
        foreach (LabelSigningKeySelection result in await Task.WhenAll(activation, concurrent))
        {
            Assert.Equal("replacement", result.KeyId);
        }

        ILabelSigningKeySelectionStore restarted = (ILabelSigningKeySelectionStore)fixture.NewStore();
        Assert.Equal("replacement", (await restarted.GetSigningKeySelectionAsync(token))!.KeyId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.CompleteRotationAsync("rotation", "replacement", cancellationToken: token));
        await maintenance.AcknowledgeHostKeyActivatedAsync("rotation", host, "replacement", token);
        await maintenance.CompleteRotationAsync("rotation", "replacement", cancellationToken: token);
        Assert.Equal("replacement", (await restarted.ActivateSigningKeySelectionAsync(
            operationId: "rotation", previousKeyId: "original", keyId: "replacement", publicKey: publicKey, cancellationToken: token)).KeyId);
        await maintenance.BeginRotationAsync(
            operationId: "next",
            previousKeyId: "replacement",
            replacementKeyId: "original",
            replacementPublicKey: original.ExportPublicKeyMultibase(),
            cancellationToken: token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.ActivateSigningKeySelectionAsync(
            operationId: "rotation", previousKeyId: "original", keyId: "replacement", publicKey: publicKey, cancellationToken: token));
    }
}
