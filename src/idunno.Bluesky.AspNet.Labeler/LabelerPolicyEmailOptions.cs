// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Mail;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Configures optional authenticated SMTP delivery for policy-verification failure alerts.</summary>
public sealed class LabelerPolicyEmailOptions
{
    /// <summary>Gets whether email alerts are enabled.</summary>
    /// <value><see langword="true" /> to send alert messages; otherwise, <see langword="false" />.</value>
    public bool Enabled { get; init; }

    /// <summary>Gets the SMTP server host name or IP address.</summary>
    /// <value>The SMTP server, required when alerts are enabled.</value>
    public string? Server { get; init; }

    /// <summary>Gets the sender address used for policy alerts.</summary>
    /// <value>A single email address, required when alerts are enabled.</value>
    public string? FromAddress { get; init; }

    /// <summary>Gets the SMTP server port.</summary>
    /// <value>The TCP port, initially 587.</value>
    public int Port { get; init; } = 587;

    /// <summary>Gets the optional SMTP authentication user name.</summary>
    /// <value>The user name, paired with <see cref="Password"/> when authentication is configured.</value>
    public string? UserName { get; init; }

    /// <summary>Gets the optional SMTP authentication password.</summary>
    /// <value>The secret password, paired with <see cref="UserName"/> when authentication is configured.</value>
    public string? Password { get; init; }

    /// <summary>Validates SMTP configuration and rejects incomplete or insecure setup.</summary>
    /// <exception cref="ArgumentException">The enabled server, sender address or credentials are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured SMTP port is invalid.</exception>
    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Port, 65535);
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Server) || Server.Length > 253 ||
            Server != Server.Trim() || Uri.CheckHostName(Server) == UriHostNameType.Unknown)
        {
            throw new ArgumentException("A valid SMTP server host is required.");
        }

        if (string.IsNullOrWhiteSpace(FromAddress) || FromAddress.Length > 254 ||
            !MailAddress.TryCreate(FromAddress, out MailAddress? sender) ||
            sender is null ||
            !string.Equals(sender.Address, FromAddress, StringComparison.Ordinal))
        {
            throw new ArgumentException("A single sender email address is required.");
        }

        if ((UserName is null) != (Password is null) ||
            UserName is { Length: > 256 } ||
            Password is { Length: > 4096 } ||
            UserName?.Any(character => character is '\r' or '\n') == true)
        {
            throw new ArgumentException("SMTP username and password must be supplied together and remain within their bounds.");
        }

        if (UserName is { Length: 0 } || Password is { Length: 0 })
        {
            throw new ArgumentException("SMTP credentials must not be empty.");
        }
    }
}
