// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// A class containing information an AT Proto server has returned about a repo.
    /// </summary>
    public sealed record RepoDescription
    {
        /// <summary>
        /// Creates a new instance of <see cref="RepoDescription"/>.
        /// </summary>
        /// <param name="handle">The <see cref="Handle"/> the is owned by.</param>
        /// <param name="did">The <see cref="Did"/> for the repo.</param>
        /// <param name="didDoc">The <see cref="DidDocument"/> for the repo.</param>
        /// <param name="collections">Any collection NSIDs the repo may contain.</param>
        /// <param name="handleIsCorrect">A flag indicating indicating if handle is currently valid (resolves bi-directionally)</param>
        [JsonConstructor]
        internal RepoDescription(Handle handle, Did did, DidDocument didDoc, IReadOnlyCollection<Nsid> collections, bool handleIsCorrect)
        {
            Handle = handle;
            Did = did;
            DidDoc = didDoc;
            Collections = collections;
            HandleIsCorrect = handleIsCorrect;
        }

        /// <summary>
        /// Gets the <see cref="Handle" /> the repo owned by.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Handle Handle { get; init; }

        /// <summary>
        /// Gets the <see cref="Did"/> for the repo.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; init; }

        /// <summary>
        /// Gets the <see cref="DidDocument"/> for the repo.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DidDocument DidDoc { get; init; }

        /// <summary>
        /// Gets a collection of NSIDs for the collections present in the repo.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyCollection<Nsid> Collections { get; init; }

        /// <summary>
        /// Gets a flag indicating if the repo handle is currently valid (resolves bi-directionally).
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public bool HandleIsCorrect { get; init; }
    }
}
