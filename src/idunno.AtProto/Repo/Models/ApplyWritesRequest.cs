// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Represents request of a batch transaction of repository creates, updates, and deletes.
    /// </summary>
    public sealed record ApplyWritesRequest
    {
        /// <summary>
        /// Creates a new instance of <see cref="ApplyWritesRequest"/>.
        /// </summary>
        /// <param name="repo"><para>The DID of the repo to write against.</para></param>
        /// <param name="validate">
        ///   <para>Flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="writes"><para>The collection of transactions to batch.</para></param>
        /// <param name="swapCommit"><para>If provided, the entire operation will fail if the current repo commit CID does not match this value. Used to prevent conflicting repo mutations.</para></param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="repo"/> or <paramref name="writes"/> is null.</exception>
        public ApplyWritesRequest(Did repo, bool? validate, ICollection<ApplyWritesRequestValueBase> writes, Cid? swapCommit)
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(writes);

            if (writes.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(writes), "cannot be empty.");
            }

            Repo = repo;
            Validate = validate;
            Writes = writes;
            SwapCommit = swapCommit;
        }

        /// <summary>
        /// Gets the DID of the repo to write against
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Repo { get; init; }

        /// <summary>
        /// Gets a flag indicating what validation will be performed, if any
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool? Validate { get; init; }

        /// <summary>
        /// Gets the collection of transactions to batch.
        /// </summary>
        [JsonInclude]
        public ICollection<ApplyWritesRequestValueBase> Writes { get; init; }

        /// <summary>
        /// Gets a value to be compared against the current repo commit.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Cid? SwapCommit { get; init; }
    }
}
