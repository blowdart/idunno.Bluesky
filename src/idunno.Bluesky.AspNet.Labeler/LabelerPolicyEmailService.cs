// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Mail;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Delivers persisted policy-verification failure alerts through mandatory STARTTLS.</summary>
internal sealed partial class LabelerPolicyEmailService(
    Did issuer,
    ILabelPolicyAlertStore? alerts,
    LabelerPolicyEmailOptions settings,
    TimeProvider clock,
    ILogger<LabelerPolicyEmailService> logger)
{
    private static readonly TimeSpan s_leaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan s_sendDeadline = TimeSpan.FromSeconds(30);

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

    /// <summary>Claims and sends one durable message without exposing credentials or report contents.</summary>
    private async Task DeliverPendingAsync(ILabelPolicyAlertStore alerts, CancellationToken cancellationToken)
    {
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
                using MailMessage message = new()
                {
                    From = new MailAddress(settings.FromAddress!),
                    Subject = "Labeler policy verification failed",
                    Body = $"Independent policy verification for {issuer.Value} failed at {delivery.OccurredAt:O} " +
                        $"({delivery.Category}). Authenticated reports remain queued and non-actionable until an " +
                        "administrator restores verified policy state. Review the Xenon moderation health status and server logs.",
                    IsBodyHtml = false
                };
                message.To.Add(new MailAddress(recipient));
                await client.SendMailAsync(message, linked.Token).ConfigureAwait(false);
            }

            delivered = true;
        }
        catch (SmtpException)
        {
            DeliveryFailed(logger, "smtp");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DeliveryFailed(logger, "deadline");
        }

        await alerts.CompleteAlertDeliveryAsync(
            alertId: delivery.AlertId,
            leaseId: delivery.LeaseId,
            delivered: delivered,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Logs only a stable operational category, never an address, message, credential or SMTP exception.</summary>
    [LoggerMessage(EventId = 7306, Level = LogLevel.Warning, Message = "Policy verification alert email delivery failed ({Category}).")]
    private static partial void DeliveryFailed(ILogger logger, string category);
}
