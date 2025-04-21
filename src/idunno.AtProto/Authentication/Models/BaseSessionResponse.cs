// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Authentication.Models
{
    /// <summary>
    /// A base class for properties common to the response from every session API.
    /// </summary>
    public abstract record BaseSessionResponse
    {
        private protected BaseSessionResponse(
            Handle handle,
            Did did,
            DidDocument? didDoc,
            bool? active,
            string? accountStatus)
        {
            ArgumentNullException.ThrowIfNull(handle);
            ArgumentNullException.ThrowIfNull(did);

            Handle = handle;
            Did = did;
            DidDoc = didDoc;
            Active = active;
            Status = accountStatus;
        }

        /// <summary>
        /// The <see cref="AtProto.Handle">Handle</see> the newly created session belongs to.
        /// </summary>
        [JsonRequired]
        public Handle Handle { get; init; }

        /// <summary>
        /// The <see cref="AtProto.Did">Did</see> the newly created session belongs to.
        /// </summary>
        [JsonRequired]
        public Did Did { get; init; }

        /// <summary>
        /// The <see cref="DidDocument">DidDocument</see> for the <see cref="Did"/> that the newly created session belongs to.
        /// </summary>
        public DidDocument? DidDoc { get; init; }

        /// <summary>
        /// A flag indicating whether the account associated with newly created session is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool? Active { get; init; }

        /// <summary>
        /// If <see cref="Active"/> is <see langword="false"/>, a possible reason the account is inactive.
        /// </summary>
        /// <remarks>
        /// <para>If <see cref="Active"/> and no status is supplied, then the host makes no claim for why the repository is no longer being hosted.</para>
        /// </remarks>
        [JsonPropertyName("status")]
        public string? Status { get; init; }
    }
}
