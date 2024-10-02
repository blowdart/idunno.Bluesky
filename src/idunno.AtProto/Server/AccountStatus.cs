// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Server
{
    /// <summary>
    /// The hosting status of a user account.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AccountStatus>))]
    public enum AccountStatus
    {
        /// <summary>
        /// The account has been taken down.
        /// </summary>
        Takendown = 1,

        /// <summary>
        /// The account his been suspended.
        /// </summary>
        Suspended = 2,

        /// <summary>
        /// The account deactivated.
        /// </summary>
        Deactivated = 3
    }
}
