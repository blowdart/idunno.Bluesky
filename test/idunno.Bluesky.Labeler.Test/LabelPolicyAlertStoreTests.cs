// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.Labeler.Test;

public class LabelPolicyAlertStoreTests
{
    private static readonly Did s_administrator = new("did:plc:alert-admin");
    private static readonly Did s_moderator = new("did:plc:alert-moderator");
    private static readonly Did s_secondAdministrator = new("did:plc:second-alert-admin");

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task FirstEmailEnablementSkipsHistoricalChangesAndRetainsLaterRetriesAcrossRestarts(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "bootstrap", token)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        Assert.True((await team.SetRemoteAdministrationEnabledAsync(
            caller: administrator,
            operationId: "before-email",
            enabled: true,
            reason: "Historical change.",
            cancellationToken: token)).Succeeded);
        ILabelPolicyAlertStore alerts = (ILabelPolicyAlertStore)fixture.Store;
        await alerts.InitializeNotificationsAsync(token);
        Assert.Null(await ClaimAsync(alerts, token));
        Assert.True((await team.SetRemoteAdministrationEnabledAsync(
            caller: administrator,
            operationId: "after-email",
            enabled: false,
            reason: "New change while no addresses are registered.",
            cancellationToken: token)).Succeeded);
        ILabelPolicyAlertStore restarted = (ILabelPolicyAlertStore)fixture.NewStore();
        await restarted.InitializeNotificationsAsync(token);
        Assert.Null(await ClaimAsync(restarted, token));
        Assert.True((await team.SetTeamNotificationEmailAsync(
            caller: administrator,
            change: new("email", s_administrator, "admin@example.invalid", "Register alerts."),
            cancellationToken: token)).Succeeded);
        LabelPolicyAlertDelivery current = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(restarted, token));
        Assert.Contains("disabled", current.ChangeDescription, StringComparison.Ordinal);
        await restarted.CompleteAlertDeliveryAsync(
            alertId: current.AlertId,
            leaseId: current.LeaseId,
            delivered: false,
            cancellationToken: token);
        await alerts.InitializeNotificationsAsync(token);
        LabelPolicyAlertDelivery retry = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
        Assert.Equal(current.AlertId, retry.AlertId);
        await CompleteAsync(alerts, retry, token);
        Assert.Null(await ClaimAsync(restarted, token));
        IReadOnlyList<LabelAdministrationReceipt> audit = (await team.GetAdministrationAuditAsync(
            caller: administrator,
            cancellationToken: token)).Result!;
        Assert.Contains(audit, item => item.OperationId == "before-email" && item.Succeeded);
        Assert.Contains(audit, item => item.OperationId == "after-email" && item.Succeeded);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task InitializingAnExistingDeliveryPreservesItsPendingRetry(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "bootstrap", token)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        Assert.True((await team.SetTeamNotificationEmailAsync(
            caller: administrator,
            change: new("email", s_administrator, "admin@example.invalid", "Register alerts."),
            cancellationToken: token)).Succeeded);
        Assert.True((await team.SetRemoteAdministrationEnabledAsync(
            caller: administrator,
            operationId: "existing-delivery",
            enabled: true,
            reason: "An existing installation already started delivery.",
            cancellationToken: token)).Succeeded);
        ILabelPolicyAlertStore alerts = (ILabelPolicyAlertStore)fixture.Store;
        LabelPolicyAlertDelivery pending = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
        await alerts.CompleteAlertDeliveryAsync(
            alertId: pending.AlertId,
            leaseId: pending.LeaseId,
            delivered: false,
            cancellationToken: token);
        ILabelPolicyAlertStore restarted = (ILabelPolicyAlertStore)fixture.NewStore();
        await restarted.InitializeNotificationsAsync(token);
        LabelPolicyAlertDelivery retry = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(restarted, token));
        Assert.Equal(pending.AlertId, retry.AlertId);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ChangesAreDurablePrivateIdempotentAndDeliveredToASingleAdministrator(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine, databaseClock: clock);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "bootstrap", token)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        Assert.True((await team.SetTeamNotificationEmailAsync(
            caller: administrator,
            change: new("email", s_administrator, "admin@example.invalid", "Register alerts."),
            cancellationToken: token)).Succeeded);
        ILabelPolicyAlertStore alerts = (ILabelPolicyAlertStore)fixture.Store;
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "moderator",
                action: LabelTeamAction.Enroll,
                target: s_moderator,
                reason: "Private audit reason.",
                role: LabelTeamRole.Moderator),
            cancellationToken: token)).Succeeded);
        LabelPolicyAlertDelivery moderatorAdded = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
        Assert.Contains(s_moderator.Value, moderatorAdded.ChangeDescription, StringComparison.Ordinal);
        Assert.Equal("admin@example.invalid", Assert.Single(moderatorAdded.Recipients));
        await CompleteAsync(alerts, moderatorAdded, token);
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "second-admin",
                action: LabelTeamAction.Enroll,
                target: s_secondAdministrator,
                reason: "Private audit reason.",
                role: LabelTeamRole.Administrator),
            cancellationToken: token)).Succeeded);

        ILabelPolicyAlertStore restarted = (ILabelPolicyAlertStore)fixture.NewStore();
        LabelPolicyAlertDelivery added = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(restarted, token));
        Assert.Contains("was added", added.ChangeDescription, StringComparison.Ordinal);
        Assert.DoesNotContain("Private audit reason", added.ChangeDescription, StringComparison.Ordinal);
        Assert.DoesNotContain("uid:0", added.ChangeDescription, StringComparison.Ordinal);
        Assert.Equal("admin@example.invalid", Assert.Single(added.Recipients));
        Assert.Null(await ClaimAsync(alerts, token));
        clock.Advance(TimeSpan.FromMinutes(2));
        LabelPolicyAlertDelivery recovered = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
        Assert.Equal(added.AlertId, recovered.AlertId);
        await alerts.CompleteAlertDeliveryAsync(
            alertId: recovered.AlertId,
            leaseId: "wrong-lease",
            delivered: true,
            cancellationToken: token);
        Assert.Null(await ClaimAsync(restarted, token));
        await alerts.CompleteAlertDeliveryAsync(
            alertId: recovered.AlertId,
            leaseId: recovered.LeaseId,
            delivered: false,
            cancellationToken: token);
        recovered = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(restarted, token));
        Assert.Equal(added.AlertId, recovered.AlertId);
        await CompleteAsync(restarted, recovered, token);

        LabelTeamChange roleChange = new(
            operationId: "role",
            action: LabelTeamAction.SetRole,
            target: s_moderator,
            reason: "Private audit reason.",
            role: LabelTeamRole.Viewer);
        Assert.True((await team.ChangeTeamAsync(administrator, roleChange, token)).Succeeded);
        Assert.True((await team.ChangeTeamAsync(administrator, roleChange, token)).Succeeded);
        LabelPolicyAlertDelivery changed = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
        Assert.Contains("Viewer role", changed.ChangeDescription, StringComparison.Ordinal);
        await CompleteAsync(alerts, changed, token);
        Assert.Null(await ClaimAsync(alerts, token));
        foreach (LabelTeamAction action in new[] { LabelTeamAction.Suspend, LabelTeamAction.Reinstate })
        {
            Assert.True((await team.ChangeTeamAsync(
                caller: administrator,
                change: new(
                    operationId: action.ToString(),
                    action: action,
                    target: s_moderator,
                    reason: "Change membership availability."),
                cancellationToken: token)).Succeeded);
            LabelPolicyAlertDelivery availability = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
            Assert.Contains(action == LabelTeamAction.Suspend ? "suspended" : "reinstated", availability.ChangeDescription, StringComparison.Ordinal);
            await CompleteAsync(alerts, availability, token);
        }

        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "remove-moderator",
                action: LabelTeamAction.Remove,
                target: s_moderator,
                reason: "Remove member."),
            cancellationToken: token)).Succeeded);
        LabelPolicyAlertDelivery removed = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
        Assert.Contains("was removed", removed.ChangeDescription, StringComparison.Ordinal);
        await CompleteAsync(alerts, removed, token);
        Assert.True((await team.SetRemoteAdministrationEnabledAsync(
            caller: administrator,
            operationId: "remote",
            enabled: true,
            reason: "Enable access.",
            cancellationToken: token)).Succeeded);
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "remove-admin",
                action: LabelTeamAction.Remove,
                target: s_secondAdministrator,
                reason: "Remove administrator."),
            cancellationToken: token)).Succeeded);
        LabelPolicyAlertDelivery remote = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
        Assert.Contains("Remote CLI", remote.ChangeDescription, StringComparison.Ordinal);
        await CompleteAsync(alerts, remote, token);
        LabelPolicyAlertDelivery administratorRemoved = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(restarted, token));
        Assert.Contains(s_secondAdministrator.Value, administratorRemoved.ChangeDescription, StringComparison.Ordinal);
        Assert.Equal("admin@example.invalid", Assert.Single(administratorRemoved.Recipients));
        await CompleteAsync(restarted, administratorRemoved, token);
        Assert.Null(await ClaimAsync(alerts, token));
        await alerts.RecordVerificationFailureAsync("Unavailable", token);
        LabelPolicyAlertDelivery failure = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
        Assert.Null(failure.ChangeDescription);
        Assert.Equal("admin@example.invalid", Assert.Single(failure.Recipients));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task CliChangesNotifyWithoutCredentialsAndDoNotNotifyForDeniedOrUnchangedRequests(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "bootstrap", token)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "second-admin",
                action: LabelTeamAction.Enroll,
                target: s_secondAdministrator,
                reason: "Enroll admin.",
                role: LabelTeamRole.Administrator),
            cancellationToken: token)).Succeeded);
        Assert.True((await team.SetTeamNotificationEmailAsync(
            caller: administrator,
            change: new("email", s_administrator, "admin@example.invalid", "Register alerts."),
            cancellationToken: token)).Succeeded);
        ILabelPolicyAlertStore alerts = (ILabelPolicyAlertStore)fixture.Store;
        await CompleteAsync(alerts, Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token)), token);
        foreach (LabelTeamAction action in new[] { LabelTeamAction.BindLocal, LabelTeamAction.BindLocal, LabelTeamAction.RevokeLocal })
        {
            Assert.True((await team.ChangeTeamAsync(
                caller: administrator,
                change: new(
                    operationId: Guid.NewGuid().ToString("N"),
                    action: action,
                    target: s_secondAdministrator,
                    reason: "Secret reason.",
                    credential: "uid:1234"),
                cancellationToken: token)).Succeeded);
            LabelPolicyAlertDelivery change = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
            Assert.Contains("Local CLI access", change.ChangeDescription, StringComparison.Ordinal);
            Assert.DoesNotContain("uid:1234", change.ChangeDescription, StringComparison.Ordinal);
            Assert.DoesNotContain("Secret reason", change.ChangeDescription, StringComparison.Ordinal);
            await CompleteAsync(alerts, change, token);
        }
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "invalidate-access",
                action: LabelTeamAction.SetRole,
                target: s_secondAdministrator,
                reason: "Invalidate existing credentials.",
                role: LabelTeamRole.Administrator),
            cancellationToken: token)).Succeeded);
        LabelPolicyAlertDelivery invalidated = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
        Assert.Contains("Access credentials", invalidated.ChangeDescription, StringComparison.Ordinal);
        await CompleteAsync(alerts, invalidated, token);
        foreach (bool enabled in new[] { true, false })
        {
            Assert.True((await team.SetRemoteAdministrationEnabledAsync(
                caller: administrator,
                operationId: Guid.NewGuid().ToString("N"),
                enabled: enabled,
                reason: "CLI change.",
                cancellationToken: token)).Succeeded);
            LabelPolicyAlertDelivery change = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
            Assert.Contains(enabled ? "enabled" : "disabled", change.ChangeDescription, StringComparison.Ordinal);
            await CompleteAsync(alerts, change, token);
        }
        Assert.True((await team.SetRemoteAdministrationEnabledAsync(
            caller: administrator,
            operationId: "unchanged",
            enabled: false,
            reason: "No change.",
            cancellationToken: token)).Succeeded);
        LabelAdministrationCaller other = (await team.AuthenticateBrowserAsync(s_secondAdministrator, token)).Result!;
        Assert.False((await team.SetRemoteAdministrationEnabledAsync(
            caller: other,
            operationId: "denied",
            enabled: true,
            reason: "Not owner.",
            cancellationToken: token)).Succeeded);
        Assert.Null(await ClaimAsync(alerts, token));
    }

    [Fact]
    public async Task AuditPagingAndRecipientUpdatesSurviveStoreRecreation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync("sqlite");
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "bootstrap", token)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        for (int index = 0; index < 110; index++)
        {
            Assert.True((await team.RecordAdministrationDenialAsync("Audit paging fixture.", administrator, token)).Succeeded);
        }
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "second-admin",
                action: LabelTeamAction.Enroll,
                target: s_secondAdministrator,
                reason: "Enroll admin.",
                role: LabelTeamRole.Administrator),
            cancellationToken: token)).Succeeded);
        foreach (Did target in new[] { s_administrator, s_secondAdministrator })
        {
            Assert.True((await team.SetTeamNotificationEmailAsync(
                caller: administrator,
                change: new(Guid.NewGuid().ToString("N"), target, "admin@example.invalid", "Register alerts."),
                cancellationToken: token)).Succeeded);
        }
        ILabelPolicyAlertStore alerts = (ILabelPolicyAlertStore)fixture.Store;
        Assert.Null(await ClaimAsync(alerts, token));
        ILabelPolicyAlertStore restarted = (ILabelPolicyAlertStore)fixture.NewStore();
        LabelPolicyAlertDelivery added = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(restarted, token));
        Assert.Equal("admin@example.invalid", Assert.Single(added.Recipients));
        await CompleteAsync(restarted, added, token);
        Assert.True((await team.SetTeamNotificationEmailAsync(
            caller: administrator,
            change: new("clear", s_administrator, null, "Clear alerts."),
            cancellationToken: token)).Succeeded);
        Assert.True((await team.SetRemoteAdministrationEnabledAsync(
            caller: administrator,
            operationId: "remote",
            enabled: true,
            reason: "Enable access.",
            cancellationToken: token)).Succeeded);
        LabelPolicyAlertDelivery remote = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(restarted, token));
        await restarted.CompleteAlertDeliveryAsync(
            alertId: remote.AlertId,
            leaseId: remote.LeaseId,
            delivered: false,
            cancellationToken: token);
        Assert.True((await team.SetTeamNotificationEmailAsync(
            caller: administrator,
            change: new("replace", s_secondAdministrator, "updated@example.invalid", "Update alerts."),
            cancellationToken: token)).Succeeded);
        LabelPolicyAlertDelivery retry = Assert.IsType<LabelPolicyAlertDelivery>(await ClaimAsync(alerts, token));
        Assert.Equal(remote.AlertId, retry.AlertId);
        Assert.Equal("updated@example.invalid", Assert.Single(retry.Recipients));
    }

    /// <summary>Claims a pending notification with a distinct lease for each attempt.</summary>
    private static Task<LabelPolicyAlertDelivery?> ClaimAsync(ILabelPolicyAlertStore alerts, CancellationToken token) =>
        alerts.ClaimPendingAlertAsync(
            leaseId: Guid.NewGuid().ToString("N"),
            leaseDuration: TimeSpan.FromMinutes(2),
            cancellationToken: token);

    /// <summary>Acknowledges the exact claim without changing unrelated pending notifications.</summary>
    private static Task CompleteAsync(ILabelPolicyAlertStore alerts, LabelPolicyAlertDelivery delivery, CancellationToken token) =>
        alerts.CompleteAlertDeliveryAsync(
            alertId: delivery.AlertId,
            leaseId: delivery.LeaseId,
            delivered: true,
            cancellationToken: token);

    [Fact]
    public async Task AlertIsDeduplicatedDurableAndDeliveredOnlyToActiveAdministratorAddresses()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync("sqlite");
        ILabelAdministrationStore administration = (ILabelAdministrationStore)fixture.Store;
        Assert.True((await administration.BootstrapAsync(s_administrator, "uid:0", "alert-bootstrap", token)).Succeeded);
        LabelAdministrationCaller administrator = (await administration.AuthenticateLocalAsync("uid:0", token)).Result!;
        Assert.True((await administration.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "alert-enroll-moderator",
                action: LabelTeamAction.Enroll,
                target: s_moderator,
                reason: "Enroll a moderator.",
                role: LabelTeamRole.Moderator),
            cancellationToken: token)).Succeeded);

        const string address = "admin@example.invalid";
        LabelServiceResult<LabelAdministrationReceipt> registered = await administration.SetTeamNotificationEmailAsync(
            caller: administrator,
            change: new(
                operationId: "alert-register-email",
                target: s_administrator,
                notificationEmail: address,
                reason: "Register a policy alert address."),
            cancellationToken: token);
        Assert.True(registered.Succeeded, registered.Error?.Message);
        Assert.DoesNotContain(address, registered.Result!.Reason, StringComparison.OrdinalIgnoreCase);

        LabelAdministrationCaller moderator = (await administration.AuthenticateBrowserAsync(s_moderator, token)).Result!;
        IReadOnlyList<LabelTeamMember> moderatorView = (await administration.GetTeamAsync(moderator, token)).Result!;
        Assert.All(moderatorView, member => Assert.Null(member.NotificationEmail));

        ILabelPolicyAlertStore alerts = (ILabelPolicyAlertStore)fixture.Store;
        await alerts.RecordVerificationFailureAsync("Unavailable", token);
        await alerts.RecordVerificationFailureAsync("Timeout", token);

        ILabelPolicyAlertStore restarted = (ILabelPolicyAlertStore)fixture.NewStore();
        LabelPolicyAlertDelivery first = (await restarted.ClaimPendingAlertAsync(
            leaseId: "alert-lease-1",
            leaseDuration: TimeSpan.FromMinutes(2),
            cancellationToken: token))!;
        Assert.Equal("Unavailable", first.Category);
        Assert.Equal(address, Assert.Single(first.Recipients));

        await restarted.CompleteAlertDeliveryAsync(
            alertId: first.AlertId,
            leaseId: first.LeaseId,
            delivered: false,
            cancellationToken: token);
        LabelPolicyAlertDelivery retry = (await restarted.ClaimPendingAlertAsync(
            leaseId: "alert-lease-2",
            leaseDuration: TimeSpan.FromMinutes(2),
            cancellationToken: token))!;
        Assert.Equal(first.AlertId, retry.AlertId);
        Assert.Equal(address, Assert.Single(retry.Recipients));

        await restarted.RecordVerificationSuccessAsync(token);
        await restarted.CompleteAlertDeliveryAsync(
            alertId: retry.AlertId,
            leaseId: retry.LeaseId,
            delivered: true,
            cancellationToken: token);
        LabelPolicyAlertDelivery enrollment = Assert.IsType<LabelPolicyAlertDelivery>(await restarted.ClaimPendingAlertAsync(
            leaseId: "alert-lease-3",
            leaseDuration: TimeSpan.FromMinutes(2),
            cancellationToken: token));
        Assert.Contains(s_moderator.Value, enrollment.ChangeDescription, StringComparison.Ordinal);
        await CompleteAsync(restarted, enrollment, token);
        Assert.Null(await ClaimAsync(restarted, token));

        LabelServiceResult<LabelAdministrationReceipt> cleared = await administration.SetTeamNotificationEmailAsync(
            caller: administrator,
            change: new(
                operationId: "alert-clear-email",
                target: s_administrator,
                notificationEmail: null,
                reason: "Clear a policy alert address."),
            cancellationToken: token);
        Assert.True(cleared.Succeeded, cleared.Error?.Message);
        await restarted.RecordVerificationFailureAsync("Unavailable", token);
        Assert.Null(await restarted.ClaimPendingAlertAsync(
            leaseId: "alert-lease-4",
            leaseDuration: TimeSpan.FromMinutes(2),
            cancellationToken: token));
    }

    [Fact]
    public void NotificationEmailAuditReasonCannotContainAnAddress()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new LabelTeamNotificationEmailChange(
            operationId: "alert-reason",
            target: s_administrator,
            notificationEmail: "admin@example.invalid",
            reason: "Register admin@example.invalid."));

        Assert.Equal("reason", exception.ParamName);
    }
}
