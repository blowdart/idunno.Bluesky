// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Encapsulates data returned by the DeleteRecord API call.
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
    public record DeleteRecordResponse
    {
        /// <summary>
        /// Creates a new instance of <see cref="DeleteRecordResponse"/>
        /// </summary>
        /// <param name="commit">The commit information of the deleteRecord operation.</param>
        [JsonConstructor]
        internal DeleteRecordResponse(Commit commit)
        {
            Commit = commit;
        }

        /// <summary>
        /// Gets the commit information of the deleteRecord operation.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Commit Commit { get; init; }
    }
}
