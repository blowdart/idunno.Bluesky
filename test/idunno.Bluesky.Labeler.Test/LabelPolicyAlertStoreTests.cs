// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Test;

public class LabelPolicyAlertStoreTests
{
    private static readonly Did s_administrator = new("did:plc:alert-admin");
    private static readonly Did s_moderator = new("did:plc:alert-moderator");

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
        Assert.Null(await restarted.ClaimPendingAlertAsync(
            leaseId: "alert-lease-3",
            leaseDuration: TimeSpan.FromMinutes(2),
            cancellationToken: token));

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
