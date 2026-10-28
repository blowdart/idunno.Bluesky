// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Checks that dependency registration cannot silently install permissive or ambiguous boundaries.</summary>
public class LabelIssuanceRegistrationTests
{
    [Theory]
    [InlineData("authorizer", false)]
    [InlineData("authorizer", true)]
    [InlineData("policy", false)]
    [InlineData("policy", true)]
    [InlineData("store", false)]
    [InlineData("store", true)]
    public void RequiresExactlyOneSecurityAndStorageDependency(string dependency, bool duplicate)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient client = new(new TargetResolutionHandler());
        IServiceCollection services = new ServiceCollection();
        if (dependency != "store" || duplicate)
        {
            services.AddSqliteLabelStorage("Data Source=registration.db");
            if (dependency == "store")
            {
                services.Add(Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ILabelStore)));
            }
        }

        if (dependency != "authorizer" || duplicate)
        {
            services.AddSingleton<ILabelIssuanceAuthorizer>(new TestLabelIssuanceAuthorizer());
            if (dependency == "authorizer")
            {
                services.AddSingleton<ILabelIssuanceAuthorizer>(new TestLabelIssuanceAuthorizer());
            }
        }

        if (dependency != "policy" || duplicate)
        {
            services.AddSingleton<ILabelIssuancePolicyProvider>(
                new TestLabelIssuancePolicyProvider(
                    new(
                        version: "policy",
                        declaredValues: ["spam"],
                        allowAccounts: true,
                        allowRecords: true)));
            if (dependency == "policy")
            {
                services.AddSingleton<ILabelIssuancePolicyProvider>(
                    new TestLabelIssuancePolicyProvider(
                        new(
                            version: "other",
                            declaredValues: ["spam"],
                            allowAccounts: true,
                            allowRecords: true)));
            }
        }

        services.AddLabelIssuance(StoreFixture.s_issuer, key, client);
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<LabelIssuanceService>());
    }

    [Fact]
    public async Task UsesScopedServiceAndRegisteredClockWithoutOwningKeyOrClient()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync("sqlite");
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient client = new(new TargetResolutionHandler());
        FakeTimeProvider clock = new(
            new DateTimeOffset(
                year: 2026,
                month: 1,
                day: 1,
                hour: 0,
                minute: 0,
                second: 0,
                offset: TimeSpan.Zero));
        ServiceCollection services = new();
        services.AddSingleton(fixture.Store);
        services.AddSingleton<ILabelIssuanceAuthorizer>(new TestLabelIssuanceAuthorizer());
        services.AddSingleton<ILabelIssuancePolicyProvider>(
            new TestLabelIssuancePolicyProvider(
                new(
                    version: "policy",
                    declaredValues: ["spam"],
                    allowAccounts: true,
                    allowRecords: true)));
        services.AddSingleton<TimeProvider>(clock);
        services.AddLabelIssuance(StoreFixture.s_issuer, key, client);
        Assert.Throws<InvalidOperationException>(() => services.AddLabelIssuance(StoreFixture.s_issuer, key, client));
        await using ServiceProvider provider = services.BuildServiceProvider();
        using (IServiceScope scope = provider.CreateScope())
        {
            LabelIssuanceService service = scope.ServiceProvider.GetRequiredService<LabelIssuanceService>();
            Assert.Same(service, scope.ServiceProvider.GetRequiredService<LabelIssuanceService>());
            LabelServiceResult<LabelOperationReceipt> issued = await service.ExecuteAsync(
                LabelOperationRequest.Issue(
                    operationId: "registered",
                    target: LabelTarget.Account(TargetResolutionHandler.SubjectDid),
                    value: "spam",
                    context: new(StoreFixture.s_issuer, "Registration test")),
                TestContext.Current.CancellationToken);
            Assert.True(issued.Succeeded);
            Assert.Equal(clock.GetUtcNow(), issued.Result.Label!.SignedLabel.Label.CreationTimestamp);
        }

        // Scope disposal must not dispose either caller-owned dependency.
        Assert.NotEmpty(key.ExportPublicKeyMultibase());
        using HttpResponseMessage response = await client.GetAsync(
            "https://plc.directory/" + TargetResolutionHandler.SubjectDid,
            TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode);
    }
}
