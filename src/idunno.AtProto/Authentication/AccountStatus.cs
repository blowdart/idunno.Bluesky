// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// The hosting status of a user account.
    /// </summary>
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
        Deactivated
    }
}
