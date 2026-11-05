// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Mail;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Delivers persisted policy alerts and administration change notifications through mandatory STARTTLS.</summary>
internal sealed partial class LabelerPolicyEmailService(
    Did issuer,
    ILabelPolicyAlertStore? alerts,
    LabelerPolicyEmailOptions settings,
    TimeProvider clock,
    ILogger<LabelerPolicyEmailService> logger,
    Uri? xenonEndpoint = null)
{
    private static readonly TimeSpan s_leaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan s_sendDeadline = TimeSpan.FromSeconds(30);
    private readonly object _testLock = new();
    private DateTimeOffset? _lastTest;

    /// <summary>Gets the test-message transport, allowing diagnostic delivery to be exercised without external mail.</summary>
    internal Func<SmtpClient, MailMessage, CancellationToken, Task> TestMessageSender { get; init; } =
        static (client, message, cancellationToken) => client.SendMailAsync(message, cancellationToken);

    /// <summary>Sends a bounded, rate-limited diagnostic message to a server-resolved administrator address.</summary>
    internal async Task<bool> SendTestAsync(string recipient, CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return false;
        }
        lock (_testLock)
        {
            DateTimeOffset now = clock.GetUtcNow();
            if (_lastTest is { } previous && now < previous.AddSeconds(30))
            {
                return false;
            }
            _lastTest = now;
        }
        using SmtpClient client = new(settings.Server!, settings.Port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = settings.UserName is null ? null : new NetworkCredential(settings.UserName, settings.Password),
            Timeout = (int)s_sendDeadline.TotalMilliseconds
        };
        using MailMessage message = new(settings.FromAddress!, recipient)
        {
            Subject = "Xenon notification email test",
            Body = WithHostLink($"This is a test of administrator notification email for {issuer.Value}. No policy or moderation state was changed."),
            IsBodyHtml = false
        };
        using CancellationTokenSource deadline = new(s_sendDeadline, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            await TestMessageSender(client, message, linked.Token).ConfigureAwait(false);
            return true;
        }
        catch (SmtpException exception)
        {
            LogSmtpFailure(exception, "smtp-test");
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DeliveryFailed(logger: logger, category: "smtp-test", failure: "deadline-exceeded", smtpStatusCode: null);
            return false;
        }
    }

    /// <summary>Persists one failure notification and attempts pending delivery when configured.</summary>
    internal async Task VerificationFailedAsync(string category, CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return;
        }

        ILabelPolicyAlertStore alertStore = alerts ??
            throw new InvalidOperationException("Enabled SMTP alerts require durable policy-alert storage.");
        await alertStore.RecordVerificationFailureAsync(category, cancellationToken).ConfigureAwait(false);
        await DeliverPendingAsync(alertStore, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Closes an outage incident and retries any notification retained during SMTP downtime.</summary>
    internal async Task VerificationSucceededAsync(CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return;
        }

        ILabelPolicyAlertStore alertStore = alerts ??
            throw new InvalidOperationException("Enabled SMTP alerts require durable policy-alert storage.");
        await alertStore.RecordVerificationSuccessAsync(cancellationToken).ConfigureAwait(false);
        await DeliverPendingAsync(alertStore, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Retries durable notifications independently of the selected policy's verification phase.</summary>
    internal async Task DeliverPendingAsync(CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return;
        }
        ILabelPolicyAlertStore alertStore = alerts ??
            throw new InvalidOperationException("Enabled SMTP alerts require durable policy-alert storage.");
        await DeliverPendingAsync(alertStore, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Claims and sends one durable message without exposing credentials or report contents.</summary>
    private async Task DeliverPendingAsync(ILabelPolicyAlertStore alerts, CancellationToken cancellationToken)
    {
        await alerts.InitializeNotificationsAsync(cancellationToken).ConfigureAwait(false);
        string leaseId = Guid.NewGuid().ToString("N");
        LabelPolicyAlertDelivery? delivery = await alerts.ClaimPendingAlertAsync(
            leaseId: leaseId,
            leaseDuration: s_leaseDuration,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (delivery is null)
        {
            return;
        }

        bool delivered = false;
        try
        {
            using SmtpClient client = new(settings.Server!, settings.Port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = settings.UserName is null
                    ? null
                    : new NetworkCredential(settings.UserName, settings.Password),
                Timeout = (int)s_sendDeadline.TotalMilliseconds
            };
            using CancellationTokenSource deadline = new(s_sendDeadline, clock);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                deadline.Token, cancellationToken);
            foreach (string recipient in delivery.Recipients)
            {
                using MailMessage message = CreateMessage(delivery, recipient);
                await client.SendMailAsync(message, linked.Token).ConfigureAwait(false);
            }

            delivered = true;
        }
        catch (SmtpException exception)
        {
            LogSmtpFailure(exception, "smtp");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DeliveryFailed(logger: logger, category: "smtp", failure: "deadline-exceeded", smtpStatusCode: null);
        }

        await alerts.CompleteAlertDeliveryAsync(
            alertId: delivery.AlertId,
            leaseId: delivery.LeaseId,
            delivered: delivered,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Formats a plain-text notification without report contents, user-supplied audit reasons or access credentials.</summary>
    internal MailMessage CreateMessage(LabelPolicyAlertDelivery delivery, string recipient)
    {
        MailMessage message = new()
        {
            From = new MailAddress(settings.FromAddress!),
            Subject = delivery.ChangeDescription is null ? "Labeler policy verification failed" : "Labeler administration changed",
            Body = delivery.ChangeDescription is { } description
                ? $"An administration change for {issuer.Value} was recorded at {delivery.OccurredAt:O}.\n\n" +
                    $"{description}\n\nReview the Policy or Team page and administration history in Xenon. " +
                    "This notification is sent to active administrators with a registered notification email address."
                : $"Independent policy verification for {issuer.Value} failed at {delivery.OccurredAt:O} " +
                    $"({delivery.Category}). Authenticated reports remain queued and non-actionable until an " +
                    "administrator restores verified policy state. Review the Xenon moderation health status and server logs.",
            IsBodyHtml = false
        };
        message.Body = WithHostLink(message.Body);
        message.To.Add(new MailAddress(recipient));
        return message;
    }

    /// <summary>Appends only the configured UI location, never a request-supplied host.</summary>
    private string WithHostLink(string body) =>
        xenonEndpoint is null ? body : body + $"\n\nSent via {xenonEndpoint.AbsoluteUri}";

    /// <summary>Classifies numeric SMTP outcomes without inspecting or logging untrusted server text.</summary>
    private void LogSmtpFailure(SmtpException exception, string category)
    {
        int status = (int)exception.StatusCode;
        string failure = status switch
        {
            534 or 535 => "authentication-rejected",
            530 => "authentication-or-starttls-required",
            454 => "authentication-or-tls-temporarily-unavailable",
            550 => "mailbox-unavailable",
            553 => "mailbox-name-rejected",
            >= 400 and < 500 => "temporary-smtp-rejection",
            >= 500 and < 600 => "permanent-smtp-rejection",
            _ => "transport-or-protocol-failure"
        };
        DeliveryFailed(
            logger: logger,
            category: category,
            failure: failure,
            smtpStatusCode: status is >= 400 and < 600 ? status : null);
    }

    /// <summary>Logs sanitized delivery failures without addresses, credentials, exception objects or raw server replies.</summary>
    [LoggerMessage(EventId = 7306, Level = LogLevel.Error, Message = "Administrator notification email delivery failed ({Category}): {Failure}; SMTP status code: {SmtpStatusCode}.")]
    private static partial void DeliveryFailed(ILogger logger, string category, string failure, int? smtpStatusCode);
}

/// <summary>Identifies the optional registered web UI's configured public location for email attribution.</summary>
internal sealed record LabelerEmailUiLink(Uri? Endpoint);
