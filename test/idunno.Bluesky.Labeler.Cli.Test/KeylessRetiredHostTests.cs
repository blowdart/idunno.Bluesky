// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using HostProgram = idunno.Bluesky.Labeler.Host.Program;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Protects the canonical host's explicit, durable-state-validated keyless startup path.</summary>
public class KeylessRetiredHostTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("environment", false)]
    [InlineData("pem", false)]
    [InlineData("none", true)]
    public void KeylessRoleRequiresExplicitSelection(string? provider, bool keyless)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Labeler:SigningKeyProvider"] = provider }).Build();
        Assert.Equal(keyless, HostProgram.IsKeylessConfiguration(configuration));
    }

    [Theory]
    [InlineData("Labeler:SigningKeys:0:PrivateKeyBase64")]
    [InlineData("Labeler:SigningKeyDirectory")]
    [InlineData("Labeler:ActiveKeyPath")]
    [InlineData("Labeler:InitialSigningKeyId")]
    public void KeylessRoleRejectsConfiguredSecretsAndSelection(string conflictingKey)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Labeler:SigningKeyProvider"] = "none",
                [conflictingKey] = "not-a-secret"
            }).Build();
        Assert.Throws<InvalidOperationException>(() => HostProgram.IsKeylessConfiguration(configuration));
    }

    [Theory]
    [InlineData(LabelerLifecyclePhase.Active, false, false)]
    [InlineData(LabelerLifecyclePhase.Retiring, false, false)]
    [InlineData(LabelerLifecyclePhase.Distributing, false, false)]
    [InlineData(LabelerLifecyclePhase.Grace, false, false)]
    [InlineData(LabelerLifecyclePhase.Retired, false, true)]
    [InlineData(LabelerLifecyclePhase.Purging, false, true)]
    [InlineData(LabelerLifecyclePhase.Purged, false, true)]
    [InlineData(LabelerLifecyclePhase.Retired, true, false)]
    public async Task KeylessStartupRequiresTerminalDurableStateAndNoUnpublishedSigning(
        LabelerLifecyclePhase phase,
        bool pendingPublication,
        bool allowed)
    {
        await using HostStorage fixture = await HostStorage.CreateAsync();
        CancellationToken token = TestContext.Current.CancellationToken;
        await using (SqliteConnection connection = new(fixture.ConnectionString))
        {
            await connection.OpenAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE label_state SET phase=@phase,issued=@issued";
            command.Parameters.AddWithValue("@phase", (int)phase);
            command.Parameters.AddWithValue("@issued", pendingPublication ? 1 : 0);
            await command.ExecuteNonQueryAsync(token);
        }

        Task ValidateAsync() => HostProgram.ValidateKeylessHostingAsync(
            issuer: HostStorage.Issuer,
            store: fixture.Store,
            maintenance: fixture.Maintenance,
            policies: null,
            cancellationToken: token);
        if (allowed)
        {
            await ValidateAsync();
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(ValidateAsync);
        }
    }

    [Fact]
    public async Task KeylessStartupRejectsPendingRecoveryRotation()
    {
        await using HostStorage fixture = await HostStorage.CreateAsync();
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelStoreState state = await fixture.Store.GetStateAsync(token);
        state = await fixture.Store.BeginRetirementAsync(state.Revision, cancellationToken: token);
        state = await fixture.Store.BeginGracePeriodAsync(state.Revision, TimeSpan.FromDays(1), cancellationToken: token);
        await using (SqliteConnection connection = new(fixture.ConnectionString))
        {
            await connection.OpenAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE label_state SET grace_deadline=0";
            await command.ExecuteNonQueryAsync(token);
        }
        await fixture.Store.CompleteRetirementAsync(state.Revision, cancellationToken: token);
        await fixture.Maintenance.RegisterHostWithSigningRoleAsync(Guid.NewGuid(), requiresSigningKey: false, token);
        using LabelSigningKey replacement = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await fixture.Maintenance.BeginRotationAsync(
            operationId: "pending-recovery",
            previousKeyId: "original",
            replacementKeyId: "replacement",
            replacementPublicKey: replacement.ExportPublicKeyMultibase(),
            cancellationToken: token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => HostProgram.ValidateKeylessHostingAsync(
            issuer: HostStorage.Issuer,
            store: fixture.Store,
            maintenance: fixture.Maintenance,
            policies: null,
            cancellationToken: token));
    }

    [Fact]
    public async Task KeylessStartupDoesNotInitializeAnEmptyStore()
    {
        await using HostStorage fixture = await HostStorage.CreateAsync(initialize: false);
        CancellationToken token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<SqliteException>(() => HostProgram.ValidateKeylessHostingAsync(
            issuer: HostStorage.Issuer,
            store: fixture.Store,
            maintenance: fixture.Maintenance,
            policies: null,
            cancellationToken: token));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Store.GetStateAsync(token));
    }

    /// <summary>Keeps test databases within the checkout and disposes provider connections before removing them.</summary>
    private sealed class HostStorage(ServiceProvider services, string path, string connectionString) : IAsyncDisposable
    {
        internal static readonly Did Issuer = new("did:plc:keyless-host");
        internal string ConnectionString => connectionString;
        internal ILabelStore Store => services.GetRequiredService<ILabelStore>();
        internal ILabelMaintenanceStore Maintenance => services.GetRequiredService<ILabelMaintenanceStore>();

        /// <summary>Creates a real, initialized durable store without installing any signing keys.</summary>
        internal static async Task<HostStorage> CreateAsync(bool initialize = true)
        {
            Directory.CreateDirectory(".artifacts");
            string path = Path.GetFullPath(Path.Combine(".artifacts", "keyless-" + Guid.NewGuid().ToString("N") + ".db"));
            string connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString;
            ServiceCollection services = new();
            services.AddSqliteLabelStorage(connectionString);
            HostStorage fixture = new(services.BuildServiceProvider(), path, connectionString);
            if (initialize)
            {
                await fixture.Store.InitializeAsync(Issuer, TestContext.Current.CancellationToken);
            }
            return fixture;
        }

        /// <summary>Disposes provider-owned resources and removes only this fixture's database files.</summary>
        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                File.Delete(path + suffix);
            }
        }
    }
}
