// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    public sealed record RepoDescription
    {
        [JsonConstructor]
        public RepoDescription(Handle handle, Did did, DidDocument didDoc, IReadOnlyCollection<string> collections, bool handleIsCorrect)
        {
            Handle = handle;
            Did = did;
            DidDoc = didDoc;
            Collections = collections;
            HandleIsCorrect = handleIsCorrect;
        }

        /// <summary>
        /// Gets the <see cref="Handle"> the repo belongs to.
        /// </summary>
        /// <value>The handle the repo belongs to.</value>
        [JsonInclude]
        [JsonRequired]
        public Handle Handle { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Did"> the repo belongs to.
        /// </summary>
        /// <value>The Did the repo belongs to.</value>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; internal set; }

        /// <summary>
        /// Gets the <see cref="DidDocument"/> for the repo.
        /// </summary>
        /// <value>The <see cref="DidDocument"/> for the repo.</value>
        [JsonInclude]
        [JsonRequired]
        public DidDocument DidDoc { get; internal set; }

        /// <summary>
        /// Gets a collection of NSIDs for the collections present in the repo.
        /// </summary>
        /// <value>A collection of NSIDs for the collections present in the repo.</value>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyCollection<string> Collections { get; internal set; }

        /// <summary>
        /// Gets a flag indicating if the repo handle is currently valid (resolves bi-directionally).
        /// </summary>
        /// <value>A flag indicating if the repo handle is currently valid (resolves bi-directionally).</value>
        [JsonInclude]
        [JsonRequired]
        public bool HandleIsCorrect { get; internal set; }
    }
}
