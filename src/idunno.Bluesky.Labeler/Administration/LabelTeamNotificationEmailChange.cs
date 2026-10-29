// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Mail;

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Describes an administrator's audited notification-email change.</summary>
public sealed class LabelTeamNotificationEmailChange
{
    /// <summary>Initializes a notification-email change.</summary>
    /// <param name="operationId">The stable retry identity.</param>
    /// <param name="target">The active administrator's DID.</param>
    /// <param name="notificationEmail">The address to register, or <see langword="null" /> to clear it.</param>
    /// <param name="reason">The audit reason, which must not contain the email address.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A text argument or email address is invalid.</exception>
    public LabelTeamNotificationEmailChange(
        string operationId,
        Did target,
        string? notificationEmail,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(target);
        OperationId = LabelInput.ValidateText(operationId, nameof(operationId), 256);
        Reason = LabelInput.ValidateText(reason, nameof(reason), 4096);
        if (Reason.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("The audit reason must not contain email addresses.", nameof(reason));
        }

        if (notificationEmail is not null)
        {
            LabelInput.ValidateText(notificationEmail, nameof(notificationEmail), 254);
            if (!MailAddress.TryCreate(notificationEmail, out MailAddress? address) ||
                !string.Equals(address.Address, notificationEmail, StringComparison.Ordinal))
            {
                throw new ArgumentException("Enter a single email address without a display name.", nameof(notificationEmail));
            }
        }

        Target = target;
        NotificationEmail = notificationEmail;
    }

    /// <summary>Gets the stable retry identity.</summary>
    public string OperationId { get; }

    /// <summary>Gets the target administrator DID.</summary>
    public Did Target { get; }

    /// <summary>Gets the registered email address, or <see langword="null" /> to clear it.</summary>
    public string? NotificationEmail { get; }

    /// <summary>Gets the audit reason without private notification details.</summary>
    public string Reason { get; }
}
