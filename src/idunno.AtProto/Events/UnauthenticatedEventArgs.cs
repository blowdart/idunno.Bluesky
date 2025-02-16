// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Events
{
    /// <summary>
    /// A class holding information about a session that has just been ended.
    /// </summary>
    public sealed class UnauthenticatedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of <see cref="UnauthenticatedEventArgs"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> the session was created for.</param>
        /// <param name="service">The <see cref="Uri"/> of the service the session was created on.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> or <paramref name="service"/> are null.</exception>
        public UnauthenticatedEventArgs(Did did, Uri service)
        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentNullException.ThrowIfNull(service);

            Did = did;
            Service = service;
        }

        /// <summary>
        /// Gets the <see cref="AtProto.Did"/> the session was created for.
        /// </summary>
        public Did Did { get; }

        /// <summary>
        /// Gets the <see cref="Uri"/> of the service the session was created on.
        /// </summary>
        public Uri Service { get; }
    }
}
