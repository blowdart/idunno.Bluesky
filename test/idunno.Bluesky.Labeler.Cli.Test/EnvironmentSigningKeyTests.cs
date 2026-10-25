// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using idunno.Bluesky.Labeler.Host;

using HostProgram = idunno.Bluesky.Labeler.Host.Program;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Exercises volume-free key configuration and restart recovery with real SQLite persistence.</summary>
public class EnvironmentSigningKeyTests
{
    [Theory]
    [InlineData("malformed")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("public")]
    [InlineData("identity")]
    public void RejectsInvalidSecretsWithoutIncludingTheirValues(string kind)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        Dictionary<string, string?> values = KeyConfiguration(key);
        string secret = kind == "public" ? Convert.ToBase64String(Encoding.UTF8.GetBytes("PRIVATE-SECRET-SENTINEL")) : "PRIVATE-SECRET-SENTINEL";
        values["Labeler:SigningKeys:0:PrivateKeyBase64"] = secret;
        if (kind == "duplicate")
        {
            values = KeyConfiguration(key);
            values["Labeler:SigningKeys:1:Id"] = "original";
            values["Labeler:SigningKeys:1:PrivateKeyBase64"] = secret;
        }
        else if (kind == "missing")
        {
            values.Remove("Labeler:SigningKeys:0:PrivateKeyBase64");
        }
        else if (kind == "identity")
        {
            values["Labeler:SigningKeys:0:Id"] = "../invalid";
        }

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new EnvironmentLabelSigningKeyRing(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        Assert.DoesNotContain(secret, error.ToString());
        Assert.DoesNotContain("PRIVATE-SECRET-SENTINEL", error.ToString());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("pem")]
    [InlineData("environment")]
    [InlineData(null)]
    public void RejectsUnknownOrAmbiguousProviderConfiguration(string? provider)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        Dictionary<string, string?> values = KeyConfiguration(key);
        values["Labeler:SigningKeyProvider"] = provider;
        values["Labeler:ActiveKeyPath"] = "do-not-write";
        Assert.Throws<InvalidOperationException>(() =>
            HostProgram.CreateSigningKeyRing(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultsToEnvironmentKeysWithOptionalPreviousKey(bool includePrevious)
    {
        using LabelSigningKey current = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using LabelSigningKey previous = LabelSigningKey.Generate(LabelSigningCurve.P256);
        Dictionary<string, string?> values = KeyConfiguration(current, includePrevious ? previous : null);
        values.Remove("Labeler:SigningKeyProvider");
        using IDisposable lifetime = HostProgram.CreateSigningKeyRing(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        EnvironmentLabelSigningKeyRing ring = Assert.IsType<EnvironmentLabelSigningKeyRing>(lifetime);
        Assert.Equal(current.ExportPublicKeyMultibase(), ring.GetPublicKeyMultibase("original"));
        if (includePrevious)
        {
            Assert.Equal(previous.ExportPublicKeyMultibase(), ring.GetPublicKeyMultibase("replacement"));
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => ring.GetSigningKey("replacement"));
        }

        Assert.Throws<InvalidOperationException>(() => ring.ActiveKeyId);
    }

    [Fact]
    public void DefaultProviderRequiresEnvironmentKeysInsteadOfFallingBackToFiles()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            HostProgram.CreateSigningKeyRing(new ConfigurationBuilder().Build()));
        Assert.Equal("Configure at least one protected signing key.", error.Message);
    }

    [Fact]
    public async Task RecoversSelectionAcrossRotationPhasesAndRemovesOldKeyWithoutFiles()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string database = Path.Combine(Path.GetTempPath(), "labeler-env-" + Guid.NewGuid().ToString("N") + ".db");
        ServiceCollection services = new();
        services.AddSqliteLabelStorage($"Data Source={database}");
        try
        {
            await using ServiceProvider provider = services.BuildServiceProvider();
            ILabelStore store = provider.GetRequiredService<ILabelStore>();
            ILabelMaintenanceStore maintenance = provider.GetRequiredService<ILabelMaintenanceStore>();
            ILabelSigningKeySelectionStore selections = provider.GetRequiredService<ILabelSigningKeySelectionStore>();
            await store.InitializeAsync(new Did("did:plc:environment-issuer"), token);
            using LabelSigningKey original = LabelSigningKey.Generate(LabelSigningCurve.P256);
            using LabelSigningKey replacement = LabelSigningKey.Generate(LabelSigningCurve.P256);
            await store.AppendAsync(
                "initial-label",
                [new SignedLabelSnapshot(original.CreateLabel(
                    new Did("did:plc:environment-issuer"),
                    "did:plc:subject",
                    "spam",
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)))],
                cancellationToken: token);
            await store.PublishAsync(cancellationToken: token);
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(KeyConfiguration(original, replacement)).Build();
            using EnvironmentLabelSigningKeyRing ring = new(config);
            Assert.Throws<InvalidOperationException>(() => ring.ActiveKeyId);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ring.InitializeAsync(
                selectionStore: selections,
                maintenance: maintenance,
                initialKeyId: null,
                cancellationToken: token));
            Assert.Null(await selections.GetSigningKeySelectionAsync(token));
            await ring.InitializeAsync(
                selectionStore: selections, maintenance: maintenance, initialKeyId: "original", cancellationToken: token);
            Assert.Equal("original", ring.ActiveKeyId);
            Guid host = Guid.NewGuid();
            await maintenance.RegisterHostAsync(host, token);
            await maintenance.BeginRotationAsync(
                operationId: "env-rotation",
                previousKeyId: "original",
                replacementKeyId: "replacement",
                replacementPublicKey: replacement.ExportPublicKeyMultibase(),
                cancellationToken: token);
            foreach (LabelMaintenancePhase phase in new[]
                {
                    LabelMaintenancePhase.Draining,
                    LabelMaintenancePhase.AwaitingKeyPublication,
                    LabelMaintenancePhase.Resigning,
                    LabelMaintenancePhase.ReadyToReopen
                })
            {
                Assert.Equal(phase, (await maintenance.GetMaintenanceStateAsync(token)).Phase);
                using EnvironmentLabelSigningKeyRing restarted = new(config);
                await restarted.InitializeAsync(
                    selectionStore: selections, maintenance: maintenance, initialKeyId: "stale-seed-not-installed", cancellationToken: token);
                Assert.Equal("original", restarted.ActiveKeyId);
                if (phase != LabelMaintenancePhase.ReadyToReopen)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.ActivateAsync("replacement", token));
                }

                switch (phase)
                {
                    case LabelMaintenancePhase.Draining:
                        await maintenance.AcknowledgeHostDrainedAsync("env-rotation", host, token);
                        await maintenance.CaptureRotationBoundaryAsync("env-rotation", cancellationToken: token);
                        break;
                    case LabelMaintenancePhase.AwaitingKeyPublication:
                        await maintenance.VerifyRotationKeyPublicationAsync(
                            "env-rotation", replacement.ExportPublicKeyMultibase(), cancellationToken: token);
                        break;
                    case LabelMaintenancePhase.Resigning:
                        await LabelSigningKeyRotation.ResignNextBatchAsync(
                            store: store, maintenanceStore: maintenance, operationId: "env-rotation", replacementKey: replacement, cancellationToken: token);
                        break;
                    case LabelMaintenancePhase.ReadyToReopen:
                        await ring.ActivateAsync("replacement", token);
                        using (EnvironmentLabelSigningKeyRing activated = new(config))
                        {
                            await activated.InitializeAsync(
                                selectionStore: selections, maintenance: maintenance, initialKeyId: "original", cancellationToken: token);
                            Assert.Equal("replacement", activated.ActiveKeyId);
                            await activated.ActivateAsync("replacement", token);
                        }

                        await maintenance.AcknowledgeHostKeyActivatedAsync("env-rotation", host, "replacement", token);
                        await maintenance.CompleteRotationAsync("env-rotation", "replacement", cancellationToken: token);
                        break;
                }
            }

            Dictionary<string, string?> replacementOnly = KeyConfiguration(replacement);
            replacementOnly["Labeler:SigningKeys:0:Id"] = "replacement";
            using EnvironmentLabelSigningKeyRing final = new(new ConfigurationBuilder().AddInMemoryCollection(replacementOnly).Build());
            await final.InitializeAsync(
                selectionStore: selections, maintenance: maintenance, initialKeyId: "original", cancellationToken: token);
            Assert.Equal("replacement", final.ActiveKeyId);
            Assert.Equal(replacement.ExportPublicKeyMultibase(), final.GetPublicKeyMultibase(final.ActiveKeyId));
            using EnvironmentLabelSigningKeyRing missing = new(new ConfigurationBuilder().AddInMemoryCollection(KeyConfiguration(original)).Build());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => missing.InitializeAsync(
                selectionStore: selections, maintenance: maintenance, initialKeyId: "original", cancellationToken: token));
            replacementOnly["Labeler:SigningKeys:0:PrivateKeyBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(original.ExportPkcs8PrivateKeyPem()));
            using EnvironmentLabelSigningKeyRing mismatch = new(new ConfigurationBuilder().AddInMemoryCollection(replacementOnly).Build());
            await Assert.ThrowsAsync<InvalidOperationException>(() => mismatch.InitializeAsync(
                selectionStore: selections, maintenance: maintenance, initialKeyId: "replacement", cancellationToken: token));
        }
        finally
        {
            File.Delete(database);
        }
    }

    [Fact]
    public void PemProviderStillLoadsItsPersistedSelection()
    {
        string directory = Path.Combine(Path.GetTempPath(), "labeler-pem-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string keyPath = Path.Combine(directory, "original.pem");
        string selectionPath = Path.Combine(directory, "active-key");
        try
        {
            using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
            File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem());
            File.WriteAllText(selectionPath, "original");
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Labeler:SigningKeyProvider"] = "pem",
                ["Labeler:SigningKeyDirectory"] = directory,
                ["Labeler:ActiveKeyPath"] = selectionPath
            }).Build();
            using IDisposable lifetime = HostProgram.CreateSigningKeyRing(config);
            ILabelSigningKeyRing ring = Assert.IsAssignableFrom<ILabelSigningKeyRing>(lifetime);
            Assert.Equal("original", ring.ActiveKeyId);
            Assert.Equal(key.ExportPublicKeyMultibase(), ring.GetPublicKeyMultibase(ring.ActiveKeyId));
        }
        finally
        {
            File.Delete(keyPath);
            File.Delete(selectionPath);
            Directory.Delete(directory);
        }
    }

    /// <summary>Builds separate configuration entries without creating private key files.</summary>
    private static Dictionary<string, string?> KeyConfiguration(LabelSigningKey original, LabelSigningKey? replacement = null)
    {
        Dictionary<string, string?> values = new()
        {
            ["Labeler:SigningKeyProvider"] = "environment",
            ["Labeler:SigningKeys:0:Id"] = "original",
            ["Labeler:SigningKeys:0:PrivateKeyBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(original.ExportPkcs8PrivateKeyPem()))
        };
        if (replacement is not null)
        {
            values["Labeler:SigningKeys:1:Id"] = "replacement";
            values["Labeler:SigningKeys:1:PrivateKeyBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(replacement.ExportPkcs8PrivateKeyPem()));
        }

        return values;
    }
}
