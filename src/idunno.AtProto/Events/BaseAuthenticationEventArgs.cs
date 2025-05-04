// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Events
{
    /// <summary>
    /// Base class for all authentication events.
    /// </summary>
    /// <param name="did">The <see cref="AtProto.Did"/> the credentials belong to.</param>
    /// <param name="service">The <see cref="Uri"/> of the service that credentials are for.</param>
    public abstract class BaseAuthenticationEventArgs(Did did, Uri service) : EventArgs
    {

        /// <summary>
        /// Gets the <see cref="AtProto.Did"/> the credentials were issued for.
        /// </summary>
        public Did Did { get; } = did;

        /// <summary>
        /// Gets the <see cref="Uri"/> of the service the session was created on.
        /// </summary>
        public Uri Service { get; } = service;
    }
}
