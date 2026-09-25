// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto;

/// <summary>
/// The hosting status of a user account.
/// </summary>
[JsonConverter(typeof(AccountStatusConverter))]
public enum AccountStatus
{
    /// <summary>
    /// The account has been taken down.
    /// </summary>
    Takendown,

    /// <summary>
    /// The account is suspended.
    /// </summary>
    Suspended,

    /// <summary>
    /// The account is deactivated.
    /// </summary>
    Deactivated,

    /// <summary>
    /// The account is deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// The account has been throttled
    /// </summary>
    Throttled,

    /// <summary>
    /// The account status is not one this library recognizes.
    /// </summary>
    /// <remarks>
    /// <para>The set of account statuses is decided by the service, not by this library, so a status added
    /// upstream is surfaced as <see cref="Unknown"/> rather than causing the response to fail to deserialize.</para>
    /// </remarks>
    Unknown
}