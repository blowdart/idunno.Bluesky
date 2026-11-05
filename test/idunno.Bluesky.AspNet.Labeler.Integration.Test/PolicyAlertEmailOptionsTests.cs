// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.AspNet.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public class PolicyAlertEmailOptionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmailsIncludeConfiguredHostOnlyWhenTheUiIsAvailable(bool uiEnabled)
    {
        Uri? endpoint = uiEnabled ? new Uri("https://labeler.example/Xenon/") : null;
        LabelerPolicyEmailService service = new(
            issuer: TestServerBuilder.s_issuer,
            alerts: null,
            settings: new()
            {
                Enabled = true,
                Server = "smtp.example.invalid",
                FromAddress = "labeler@example.invalid"
            },
            clock: new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            logger: NullLogger<LabelerPolicyEmailService>.Instance,
            xenonEndpoint: endpoint)
        {
            TestMessageSender = (_, message, _) =>
            {
                CheckBody(message.Body);
                return Task.CompletedTask;
            }
        };
        Assert.True(await service.SendTestAsync("admin@example.invalid", TestContext.Current.CancellationToken));
        void CheckBody(string body)
        {
            if (uiEnabled)
            {
                Assert.EndsWith("\n\nSent via https://labeler.example/Xenon/", body, StringComparison.Ordinal);
            }
            else
            {
                Assert.DoesNotContain("Sent via", body, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData(535, "authentication-rejected")]
    [InlineData(534, "authentication-rejected")]
    [InlineData(530, "authentication-or-starttls-required")]
    [InlineData(454, "authentication-or-tls-temporarily-unavailable")]
    [InlineData(550, "mailbox-unavailable")]
    [InlineData(553, "mailbox-name-rejected")]
    [InlineData(421, "temporary-smtp-rejection")]
    [InlineData(554, "permanent-smtp-rejection")]
    [InlineData(-1, "transport-or-protocol-failure")]
    public async Task EmailFailureLogsSanitizedClassificationAndStatusWithoutSensitiveData(int status, string failure)
    {
        FakeLogger<LabelerPolicyEmailService> logger = new();
        LabelerPolicyEmailService service = new(
            issuer: TestServerBuilder.s_issuer,
            alerts: null,
            settings: new()
            {
                Enabled = true,
                Server = "smtp.example.invalid",
                FromAddress = "sender@example.invalid",
                UserName = "private-user",
                Password = "private-key"
            },
            clock: new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            logger: logger)
        {
            TestMessageSender = (_, _, _) => Task.FromException(new System.Net.Mail.SmtpException(
                (System.Net.Mail.SmtpStatusCode)status,
                "Untrusted reply containing private-key private-user sender@example.invalid recipient@example.invalid"))
        };
        Assert.False(await service.SendTestAsync("recipient@example.invalid", TestContext.Current.CancellationToken));
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal(7306, record.Id.Id);
        Assert.Null(record.Exception);
        Assert.Contains(failure, record.Message, StringComparison.Ordinal);
        Assert.Contains(record.StructuredState!, item => item.Key == "Category" && item.Value == "smtp-test");
        Assert.Contains(record.StructuredState!, item => item.Key == "SmtpStatusCode" &&
            (status == -1 ? item.Value is null or "(null)" : item.Value == status.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.DoesNotContain("private-", record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("@example.invalid", record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Untrusted reply", record.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestEmailUsesStartTlsAndRateLimitsSuccessfulAndFailedAttempts(bool accepted)
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        int attempts = 0;
        LabelerPolicyEmailService service = new(
            issuer: TestServerBuilder.s_issuer,
            alerts: null,
            settings: new()
            {
                Enabled = true,
                Server = "smtp.example.invalid",
                Port = 587,
                FromAddress = "labeler@example.invalid",
                UserName = "resend",
                Password = "test-only"
            },
            clock: clock,
            logger: NullLogger<LabelerPolicyEmailService>.Instance)
        {
            TestMessageSender = (client, message, cancellationToken) =>
            {
                attempts++;
                Assert.True(client.EnableSsl);
                Assert.False(client.UseDefaultCredentials);
                Assert.Equal("smtp.example.invalid", client.Host);
                Assert.Equal(587, client.Port);
                System.Net.NetworkCredential credentials = Assert.IsType<System.Net.NetworkCredential>(client.Credentials);
                Assert.Equal("resend", credentials.UserName);
                Assert.Equal("test-only", credentials.Password);
                Assert.Equal("admin@example.invalid", Assert.Single(message.To).Address);
                Assert.Equal("labeler@example.invalid", message.From!.Address);
                Assert.Equal("Xenon notification email test", message.Subject);
                Assert.Contains(TestServerBuilder.s_issuer.Value, message.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("test-only", message.Body, StringComparison.Ordinal);
                Assert.False(message.IsBodyHtml);
                Assert.True(cancellationToken.CanBeCanceled);
                return accepted ? Task.CompletedTask : Task.FromException(new System.Net.Mail.SmtpException());
            }
        };
        CancellationToken token = TestContext.Current.CancellationToken;
        Assert.Equal(accepted, await service.SendTestAsync("admin@example.invalid", token));
        Assert.False(await service.SendTestAsync("admin@example.invalid", token));
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(await service.SendTestAsync("admin@example.invalid", token));
        Assert.Equal(1, attempts);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(accepted, await service.SendTestAsync("admin@example.invalid", token));
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TestEmailHonorsDisabledConfigurationAndCallerCancellation(bool enabled)
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        LabelerPolicyEmailOptions settings = new()
        {
            Enabled = enabled,
            Server = "smtp.example.invalid",
            FromAddress = "labeler@example.invalid"
        };
        int attempts = 0;
        LabelerPolicyEmailService service = new(
            issuer: TestServerBuilder.s_issuer,
            alerts: null,
            settings: settings,
            clock: clock,
            logger: NullLogger<LabelerPolicyEmailService>.Instance)
        {
            TestMessageSender = (_, _, token) =>
            {
                attempts++;
                return Task.FromCanceled(token);
            }
        };
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        if (enabled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.SendTestAsync("admin@example.invalid", cancellation.Token));
        }
        else
        {
            Assert.False(await service.SendTestAsync("admin@example.invalid", cancellation.Token));
        }
        Assert.Equal(enabled ? 1 : 0, attempts);
    }

    [Fact]
    public async Task TestEmailDeadlineCancelsAnUnresponsiveTransport()
    {
        FakeLogger<LabelerPolicyEmailService> logger = new();
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        TaskCompletionSource sending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        LabelerPolicyEmailService service = new(
            issuer: TestServerBuilder.s_issuer,
            alerts: null,
            settings: new()
            {
                Enabled = true,
                Server = "smtp.example.invalid",
                FromAddress = "labeler@example.invalid"
            },
            clock: clock,
            logger: logger)
        {
            TestMessageSender = async (_, _, token) =>
            {
                sending.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        };
        Task<bool> result = service.SendTestAsync("admin@example.invalid", TestContext.Current.CancellationToken);
        await sending.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.False(await result.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Contains("deadline-exceeded", record.Message, StringComparison.Ordinal);
        Assert.Null(record.Exception);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NotificationMessagesDescribeChangesWithoutAuditReasonsOrCredentials(bool change, bool uiEnabled)
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
            logger: NullLogger<LabelerPolicyEmailService>.Instance,
            xenonEndpoint: uiEnabled ? new Uri("https://labeler.example/Xenon/") : null);
        using System.Net.Mail.MailMessage message = service.CreateMessage(delivery, delivery.Recipients[0]);
        Assert.Equal(change ? "Labeler administration changed" : "Labeler policy verification failed", message.Subject);
        Assert.Contains(TestServerBuilder.s_issuer.Value, message.Body, StringComparison.Ordinal);
        Assert.Contains(change ? "was added" : "Unavailable", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Private audit reason", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("uid:0", message.Body, StringComparison.Ordinal);
        Assert.False(message.IsBodyHtml);
        Assert.Equal("admin@example.invalid", Assert.Single(message.To).Address);
        if (uiEnabled)
        {
            Assert.EndsWith("\n\nSent via https://labeler.example/Xenon/", message.Body, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("Sent via", message.Body, StringComparison.Ordinal);
        }
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

        public Task InitializeNotificationsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

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
