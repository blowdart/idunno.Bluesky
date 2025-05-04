// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Events
{
    /// <summary>
    /// A class holding information about a session that has just been ended.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> the session was created for.</param>
    /// <param name="service">The <see cref="Uri"/> of the service the session was created on.</param>
    public sealed class UnauthenticatedEventArgs(
        Did did,
        Uri service) : BaseAuthenticationEventArgs(did, service)
    {
    }
}
