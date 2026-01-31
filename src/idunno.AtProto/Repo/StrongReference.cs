// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Moderation;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Represents a strong reference to a record, consisting of an AT URI and a content-hash fingerprint.
    /// </summary>
    public sealed record StrongReference : SubjectType
    {
        /// <summary>
        /// Creates a new <see cref="StrongReference"/> with the specified AT URI and CID.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> for the new subject.</param>
        /// <param name="cid">The <see cref="AtProto.Cid"/> for the new subject</param>
        public StrongReference(AtUri uri, Cid cid)
        {
            Uri = uri;
            Cid = cid;
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> for the subject.
        /// </summary>
        [JsonInclude]
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets the <see cref="AtProto.Cid"/> for the subject.
        /// </summary>
        [JsonInclude]
        public Cid Cid { get; init; }

        /// <summary>
        /// Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string representation of this instance.</returns>
        public override string ToString() => $"{Uri}#{Cid}";
    }
}
