// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// An abstract class for an <see cref="AtProtoObject"/> that has an <see cref="AtUri"/> and <see cref="Cid"/>.
    /// </summary>
    public abstract record AtProtoReferencedObject : AtProtoObject
    {
        /// <summary>
        /// Sets the strong reference properties for the <see cref="AtProtoReferencedObject"/>
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the record.</param>
        /// <param name="cid">The <see cref="AtProto.Cid"/> of the record.</param>
        /// <exception cref="ArgumentNullException">Throws if <paramref name="uri"/> or <paramref name="cid"/> is null.</exception>
        protected AtProtoReferencedObject(AtUri uri, Cid cid)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(cid);

            Uri = uri;
            Cid = cid;

            StrongReference = new StrongReference(Uri, cid);
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public AtUri Uri {get; init;}

        /// <summary>
        /// Gets the <see cref="AtProto.Cid">Content Identifier</see> of the record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Cid Cid { get; init; }

        /// <summary>
        /// Gets a <see cref="StrongReference"/> for the record.
        /// </summary>
        [JsonIgnore]
        public StrongReference StrongReference { get; }
    }
}
