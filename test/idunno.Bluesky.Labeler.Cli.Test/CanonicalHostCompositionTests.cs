// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Host;
using idunno.Bluesky.Labeler.Host.MySQL;
using idunno.Bluesky.Labeler.Host.PostgreSQL;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Protects explicit provider selection and provider-neutral canonical registration and mapping.</summary>
public class CanonicalHostCompositionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CanonicalProviderRegistersOnlyItsLabelStoreAndOptionalDurableAuthentication(bool mysql, bool ui)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["Labeler:ConnectionString"] = mysql
            ? "Server=127.0.0.1;Database=fixture;User ID=fixture"
            : "Host=127.0.0.1;Database=fixture;Username=fixture";
        builder.Configuration["Labeler:PublicEndpoint"] = "https://labeler.example";
        builder.Configuration["Labeler:DatabaseProvider"] = mysql ? "MySQL" : "PostgreSQL";
        builder.Configuration["LabelerUI:Enabled"] = ui.ToString();
        if (mysql)
        {
            builder.AddMySqlCanonicalLabelerStorage();
        }
        else
        {
            builder.AddPostgreSqlCanonicalLabelerStorage();
        }
        Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(ILabelStore));
        Assert.Equal(ui ? 1 : 0, builder.Services.Count(descriptor => descriptor.ServiceType == typeof(ILabelerHostAuthenticationPersistence)));
        using ServiceProvider services = builder.Services.BuildServiceProvider();
        ILabelStore labels = services.GetRequiredService<ILabelStore>();
        Assert.Equal(mysql ? "idunno.Bluesky.Labeler.MySQL" : "idunno.Bluesky.Labeler.PostgreSQL", labels.GetType().Assembly.GetName().Name);
    }

    [Theory]
    [InlineData(false, "mysql")]
    [InlineData(false, "sqlite")]
    [InlineData(true, "postgresql")]
    [InlineData(true, "sqlite")]
    public void CanonicalVariantRejectsAConflictingProvider(bool mysql, string provider)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration["Labeler:DatabaseProvider"] = provider;
        Assert.Throws<InvalidOperationException>(() =>
        {
            if (mysql)
            {
                builder.AddMySqlCanonicalLabelerStorage();
            }
            else
            {
                builder.AddPostgreSqlCanonicalLabelerStorage();
            }
        });
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ServiceType == typeof(ILabelStore));
    }

    [Fact]
    public void SharedHostHasNoDatabaseProviderAssemblyReferences()
    {
        string[] dependencies = [.. typeof(LabelerApplicationExtensions).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name!)];
        Assert.DoesNotContain(dependencies, name => name.EndsWith(".PostgreSQL", StringComparison.Ordinal));
        Assert.DoesNotContain(dependencies, name => name.EndsWith(".MySQL", StringComparison.Ordinal));
        Assert.DoesNotContain(dependencies, name => name.EndsWith(".SQLite", StringComparison.Ordinal));
        Assert.DoesNotContain("Npgsql", dependencies);
        Assert.DoesNotContain("MySqlConnector", dependencies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedExtensionsInitializeAndMapWithoutProviderSelection(bool changedConfiguration)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string database = Path.Combine(Path.GetTempPath(), "canonical_" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
            using HttpClient identity = new(new NoNetworkHandler());
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Configuration["Labeler:IssuerDid"] = "did:plc:canonical";
            builder.Configuration["Labeler:PublicEndpoint"] = "https://labeler.example";
            builder.Configuration["Labeler:AdministrationAudience"] = "did:plc:canonical#labeler_admin";
            builder.Configuration["Labeler:DeclaredLabels"] = "review";
            builder.Configuration["Labeler:LocalAdministrationEndpoint"] = "canonical-" + Guid.NewGuid().ToString("N");
            builder.Configuration["Labeler:SigningKeys:0:Id"] = "fixture";
            builder.Configuration["Labeler:SigningKeys:0:PrivateKeyBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(key.ExportPkcs8PrivateKeyPem()));
            builder.Configuration["Labeler:InitialSigningKeyId"] = "fixture";
            builder.Services.AddSqliteLabelStorage($"Data Source={database};Pooling=False");
            using EnvironmentLabelSigningKeyRing ring = new(builder.Configuration);
            builder.AddCanonicalBlueskyLabeler(identity, ring);
            Assert.Throws<InvalidOperationException>(() => builder.AddCanonicalBlueskyLabeler(identity, ring));
            if (changedConfiguration)
            {
                builder.Configuration["Labeler:IssuerDid"] = "did:plc:changed";
                builder.Configuration["Labeler:PublicEndpoint"] = "https://changed.example";
                builder.Configuration["LabelerUI:Enabled"] = "true";
            }
            await using WebApplication app = builder.Build();
            await Assert.ThrowsAsync<InvalidOperationException>(() => app.InitializeCanonicalBlueskyLabelerAsync(null, token));
            await app.InitializeCanonicalBlueskyLabelerAsync(ring, token);
            app.MapCanonicalBlueskyLabeler();
            await app.StartAsync(token);
            Uri address = new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using HttpClient client = new() { BaseAddress = address };
            using HttpResponseMessage robots = await client.GetAsync("/robots.txt", token);
            Assert.Equal(HttpStatusCode.OK, robots.StatusCode);
            using HttpResponseMessage metadata = await client.GetAsync("/native-client-metadata.json", token);
            Assert.Equal(HttpStatusCode.OK, metadata.StatusCode);
            Assert.Contains("https://labeler.example/native-client-metadata.json", await metadata.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
            LabelStoreState state = await app.Services.GetRequiredService<ILabelStore>().GetStateAsync(token);
            Assert.Equal("did:plc:canonical", state.Issuer);
            Assert.Equal("fixture", ring.ActiveKeyId);
            await app.StopAsync(token);
        }
        finally
        {
            foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                File.Delete(database + suffix);
            }
        }
    }

    /// <summary>Rejects unexpected external traffic while exercising shared host composition.</summary>
    private sealed class NoNetworkHandler : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Canonical composition must not perform an unsolicited identity lookup.");
    }
}
