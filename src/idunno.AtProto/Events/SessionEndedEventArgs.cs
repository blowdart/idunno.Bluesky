// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Events
{
    /// <summary>
    /// A class holding information about a session that has just been ended.
    /// </summary>
    public sealed class SessionEndedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of <see cref="SessionEndedEventArgs"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> the session was created for.</param>
        /// <param name="service">The <see cref="Uri"/> of the service the session was created on.</param>
        public SessionEndedEventArgs(Did did, Uri service)
        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentNullException.ThrowIfNull(service);

            Did = did;
            Service = service;
        }

        /// <summary>
        /// Gets the <see cref="Did"/> the session was created for.
        /// </summary>
        /// <value>The <see cref="Did"/> the session was created for.</value>
        public Did Did { get; }

        /// <summary>
        /// Gets the <see cref="Uri"/> of the service the session was created on.
        /// </summary>
        /// <value>The <see cref="Uri"/> of the service the session was created on.</value>
        public Uri Service { get; }
    }
}
