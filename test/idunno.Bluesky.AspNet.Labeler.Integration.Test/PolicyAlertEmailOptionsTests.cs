// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Labeler;
using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public class PolicyAlertEmailOptionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationMessagesDescribeChangesWithoutAuditReasonsOrCredentials(bool change)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Did administratorDid = new("did:plc:email-admin");
        Assert.True((await team.BootstrapAsync(administratorDid, "uid:0", "bootstrap", token)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        Assert.True((await team.SetTeamNotificationEmailAsync(
            caller: administrator,
            change: new("email", administratorDid, "admin@example.invalid", "Register alerts."),
            cancellationToken: token)).Succeeded);
        ILabelPolicyAlertStore alerts = (ILabelPolicyAlertStore)fixture.Store;
        if (change)
        {
            Assert.True((await team.ChangeTeamAsync(
                caller: administrator,
                change: new(
                    operationId: "enroll",
                    action: LabelTeamAction.Enroll,
                    target: new Did("did:plc:second-email-admin"),
                    reason: "Private audit reason.",
                    role: LabelTeamRole.Administrator),
                cancellationToken: token)).Succeeded);
        }
        else
        {
            await alerts.RecordVerificationFailureAsync("Unavailable", token);
        }
        LabelPolicyAlertDelivery delivery = Assert.IsType<LabelPolicyAlertDelivery>(await alerts.ClaimPendingAlertAsync(
            leaseId: "render-message",
            leaseDuration: TimeSpan.FromMinutes(2),
            cancellationToken: token));
        LabelerPolicyEmailService service = new(
            issuer: TestServerBuilder.s_issuer,
            alerts: alerts,
            settings: new() { FromAddress = "labeler@example.invalid" },
            clock: fixture.Clock,
            logger: NullLogger<LabelerPolicyEmailService>.Instance);
        using System.Net.Mail.MailMessage message = service.CreateMessage(delivery, delivery.Recipients[0]);
        Assert.Equal(change ? "Labeler administration changed" : "Labeler policy verification failed", message.Subject);
        Assert.Contains(TestServerBuilder.s_issuer.Value, message.Body, StringComparison.Ordinal);
        Assert.Contains(change ? "was added" : "Unavailable", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Private audit reason", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("uid:0", message.Body, StringComparison.Ordinal);
        Assert.False(message.IsBodyHtml);
        Assert.Equal("admin@example.invalid", Assert.Single(message.To).Address);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationDeliveryDoesNotRequireAVerifiedPolicy(bool enabled)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        NotificationProbe probe = new();
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        ILabelPolicyStore policies = (ILabelPolicyStore)fixture.Store;
        Assert.Null(await policies.ReadPolicyStateAsync(token));
        LabelerPolicyEmailService notifications = new(
            issuer: TestServerBuilder.s_issuer,
            alerts: enabled ? probe : null,
            settings: new() { Enabled = enabled },
            clock: fixture.Clock,
            logger: NullLogger<LabelerPolicyEmailService>.Instance);
        if (enabled)
        {
            using PolicyVerificationWorker worker = new(
                store: policies,
                reports: (ILabelReportStore)fixture.Store,
                policies: new LabelPolicyService(TestServerBuilder.s_issuer, policies, identity),
                notifications: notifications,
                clock: fixture.Clock,
                logger: NullLogger<PolicyVerificationWorker>.Instance);
            await worker.StartAsync(token);
            try
            {
                await probe.Claimed.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
            }
            finally
            {
                await worker.StopAsync(token);
            }
        }
        else
        {
            await notifications.DeliverPendingAsync(token);
            Assert.False(probe.Claimed.Task.IsCompleted);
        }
    }

    /// <summary>Signals a delivery check without returning a message or connecting to an SMTP server.</summary>
    private sealed class NotificationProbe : ILabelPolicyAlertStore
    {
        internal TaskCompletionSource Claimed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RecordVerificationFailureAsync(string category, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No policy verification should run without an active policy.");

        public Task RecordVerificationSuccessAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No policy verification should run without an active policy.");

        public Task<LabelPolicyAlertDelivery?> ClaimPendingAlertAsync(
            string leaseId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            Claimed.TrySetResult();
            return Task.FromResult<LabelPolicyAlertDelivery?>(null);
        }

        public Task CompleteAlertDeliveryAsync(
            string alertId,
            string leaseId,
            bool delivered,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No message was claimed.");
    }

    [Fact]
    public void DisabledEmailAllowsDefaultConfiguration()
    {
        new LabelerModerationOptions().Validate();
    }

    [Fact]
    public void EnabledEmailAcceptsAuthenticatedStartTlsConfiguration()
    {
        new LabelerModerationOptions
        {
            PolicyEmail = new()
            {
                Enabled = true,
                Server = "smtp.example.invalid",
                Port = 587,
                FromAddress = "labeler@example.invalid",
                UserName = "labeler",
                Password = "secret"
            }
        }.Validate();
    }

    [Theory]
    [InlineData(null, "labeler@example.invalid", null, null)]
    [InlineData("smtp.example.invalid", null, null, null)]
    [InlineData("smtp.example.invalid", "Not an email", null, null)]
    [InlineData("smtp.example.invalid", "labeler@example.invalid", "labeler", null)]
    public void EnabledEmailRejectsIncompleteConfiguration(
        string? server,
        string? fromAddress,
        string? userName,
        string? password)
    {
        LabelerModerationOptions options = new()
        {
            PolicyEmail = new()
            {
                Enabled = true,
                Server = server,
                FromAddress = fromAddress,
                UserName = userName,
                Password = password
            }
        };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void RejectsPortsOutsideTcpRange(int port)
    {
        LabelerModerationOptions options = new()
        {
            PolicyEmail = new() { Port = port }
        };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }
}
